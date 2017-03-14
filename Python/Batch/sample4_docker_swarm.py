# sample4_docker_swarm.py Code Sample
#
# Copyright (c) Microsoft Corporation
#
# All rights reserved.
#
# MIT License
#
# Permission is hereby granted, free of charge, to any person obtaining a
# copy of this software and associated documentation files (the "Software"),
# to deal in the Software without restriction, including without limitation
# the rights to use, copy, modify, merge, publish, distribute, sublicense,
# and/or sell copies of the Software, and to permit persons to whom the
# Software is furnished to do so, subject to the following conditions:
#
# The above copyright notice and this permission notice shall be included in
# all copies or substantial portions of the Software.
#
# THE SOFTWARE IS PROVIDED *AS IS*, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
# IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
# FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
# AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
# LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
# FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
# DEALINGS IN THE SOFTWARE.

from __future__ import print_function
try:
    import configparser
except ImportError:
    import ConfigParser as configparser
import datetime
import os
import subprocess
import time

import azure.storage.blob as azureblob
import azure.batch.batch_service_client as batch
import azure.batch.batch_auth as batchauth
import azure.batch.models as batchmodels

import common.helpers

_CONTAINER_NAME = 'docker'
_TASK_ID = 'MyDockerBatchTask'
_STARTTASK_RESOURCE_FILE = 'docker_starttask.sh'
_STARTTASK_SHELL_SCRIPT_PATH = os.path.join(
    'resources', _STARTTASK_RESOURCE_FILE)
_NODE_USERNAME = 'docker'
_SSH_TUNNEL_SCRIPT = 'ssh_tunnel_batch_docker_swarm.sh'
_POOL_ADMIN_USER = batchmodels.UserIdentity(
    auto_user=batchmodels.AutoUserSpecification(
        scope=batchmodels.AutoUserScope.pool,
        elevation_level=batchmodels.ElevationLevel.admin))


def connect_to_remote_docker_swarm_master(
        batch_client, pool_id, nodes, master_node_id, username,
        ssh_private_key, generate_ssh_tunnel_script):
    """Connects to the remote docker swarm master to run sample commands and
    optionally generates an ssh tunnel script

    :param batch_client: The batch client to use.
    :type batch_client: `batchserviceclient.BatchServiceClient`
    :param str pool_id: The pool id containing the node.
    :param nodes: list of `batchserviceclient.models.ComputeNode`
    :type node: list
    :param str master: master vnet ip address
    :param str username: compute node user name
    :param str ssh_private_key: path to private key file
    :param bool generate_ssh_tunnel_script: generate an ssh tunnel script
    """
    # get remote login info for master
    print('retrieving remote login settings for {}'.format(master_node_id))
    rls = batch_client.compute_node.get_remote_login_settings(
        pool_id, master_node_id)
    remote_ip = rls.remote_login_ip_address
    ssh_port = str(rls.remote_login_port)

    # set up ssh tunnel
    print('creating ssh tunnel to {}:{} docker swarm endpoint'.format(
        remote_ip, ssh_port))
    sshproc = None
    try:
        print('waiting for ssh tunnel to be established...')
        # Note: disabling strict host key checking is not recommended in
        # general, but is used below for this sample to suppress ssh prompts
        ssh_args = [
            'ssh', '-o', 'StrictHostKeyChecking=no', '-o',
            'UserKnownHostsFile=/dev/null', '-i', ssh_private_key,
            '-p', ssh_port, '-N', '-L', '2375:localhost:2375',
            '{}@{}'.format(username, remote_ip)
        ]
        sshproc = subprocess.Popen(ssh_args)
        time.sleep(4)
        if generate_ssh_tunnel_script:
            print('generating ssh tunnel script for use outside of sample')
            with open(_SSH_TUNNEL_SCRIPT, 'w') as fd:
                fd.write('#!/usr/bin/env bash\n')
                fd.write('set -e\n')
                fd.write(' '.join(ssh_args))
            os.chmod(_SSH_TUNNEL_SCRIPT, 0o755)

        # issue docker info and node ls against swarm endpoint
        print('>>local>> docker info:')
        docker_info = subprocess.check_output(
            ['docker', '-H', 'tcp://localhost:2375', 'info'])
        print(common.helpers.decode_string(docker_info))
        print('>>local>> docker node ls:')
        docker_info = subprocess.check_output(
            ['docker', '-H', 'tcp://localhost:2375', 'node', 'ls'])
        print(common.helpers.decode_string(docker_info))

        # issue a docker run command locally
        print('>>local>> docker ps -a:')
        docker_ps = subprocess.check_output(
            ['docker', '-H', 'tcp://localhost:2375', 'ps', '-a'])
        print(common.helpers.decode_string(docker_ps))
        print('>>local>> docker run hello-world:')
        docker_run = subprocess.check_output(
            ['docker', '-H', 'tcp://localhost:2375', 'run', '-i',
             'hello-world']
        )
        print(common.helpers.decode_string(docker_run))
        print('>>local>> docker ps -a:')
        docker_ps = subprocess.check_output(
            ['docker', '-H', 'tcp://localhost:2375', 'ps', '-a'])
        print(common.helpers.decode_string(docker_ps))
    finally:
        # kill ssh tunnel
        if sshproc is not None:
            print('tearing down ssh tunnel...')
            sshproc.terminate()
            sshproc.wait()
            print('ssh tunnel terminated.')


def designate_master_docker_swarm_node(batch_client, pool_id, nodes, job_id):
    """Designate a master docker swarm node by selecting a node in the
    pool to be the swarm manager. This is accomplished via IP selection in
    the pool of nodes and running the swarm init command via an
    affinitized task. This is for Docker 1.12+.

    :param batch_client: The batch client to use.
    :type batch_client: `batchserviceclient.BatchServiceClient`
    :param str pool_id: The id of the pool.
    :param list nodes: list of `batchserviceclient.models.ComputeNode`
    :param str job_id: The id of the job to create.
    :rtype: tuple
    :return: ((master ipaddress, master node id), swarm token)
    """
    # designate the lowest ip address node as the master
    nodes = sorted(nodes, key=lambda node: node.ip_address)
    master_node_ip_address = nodes[0].ip_address
    master_node_id = nodes[0].id
    master_node_affinity_id = nodes[0].affinity_id
    master_node = (master_node_ip_address, master_node_id)
    print('master node is: {}'.format(master_node))

    # create job
    job = batchmodels.JobAddParameter(
        id=job_id,
        pool_info=batchmodels.PoolInformation(pool_id=pool_id))

    batch_client.job.add(job)

    # add docker swarm manage as an affinitized task to run on the master node
    # NOTE: task affinity is weak. if the node has no available scheduling
    # slots, the task may be executed on a different node. for this example,
    # it is not an issue since this node should be available for scheduling.
    task_commands = [
        'docker swarm init --advertise-addr {}'.format(master_node_ip_address),
        'docker swarm join-token -q worker',
    ]
    print('initializing docker swarm cluster via Azure Batch task...')
    task = batchmodels.TaskAddParameter(
        id='swarm-manager',
        affinity_info=batchmodels.AffinityInformation(
            affinity_id=master_node_affinity_id),
        command_line=common.helpers.wrap_commands_in_shell(
            'linux', task_commands),
        user_identity=_POOL_ADMIN_USER,
    )
    batch_client.task.add(job_id=job.id, task=task)

    # wait for task to complete
    common.helpers.wait_for_tasks_to_complete(
        batch_client,
        job_id,
        datetime.timedelta(minutes=5))

    # retrieve the swarm token
    stdout = common.helpers.read_task_file_as_string(
        batch_client,
        job.id,
        task.id,
        common.helpers._STANDARD_OUT_FILE_NAME)
    token = stdout.splitlines()[-1].strip()
    print('swarm token: {}'.format(token))

    return master_node, token


def add_nodes_to_swarm(
        batch_client, pool_id, nodes, job_id, master_node, swarm_token):
    """Add compute nodes to swarm

    :param batch_client: The batch client to use.
    :type batch_client: `batchserviceclient.BatchServiceClient`
    :param str pool_id: The id of the pool to create.
    :param list nodes: list of `batchserviceclient.models.ComputeNode`
    :param str job_id: The id of the job.
    :param tuple master_node: master node info
    :param str swarm_token: swarm token
    """
    task_commands = [
        'docker swarm join --token {} {}:2377'.format(
            swarm_token, master_node[0]),
    ]
    print('joining docker swarm for each compute node via Azure Batch task...')
    i = 0
    for node in nodes:
        # manager node is already part of the swarm, so skip it
        if node.id == master_node[1]:
            continue
        task = batchmodels.TaskAddParameter(
            id='swarm-join-{0:03d}'.format(i),
            affinity_info=batchmodels.AffinityInformation(
                affinity_id=node.affinity_id),
            command_line=common.helpers.wrap_commands_in_shell(
                'linux', task_commands),
            user_identity=_POOL_ADMIN_USER,
        )
        batch_client.task.add(job_id=job_id, task=task)
        i += 1

    # wait for task to complete
    common.helpers.wait_for_tasks_to_complete(
        batch_client,
        job_id,
        datetime.timedelta(minutes=5))

    print('docker swarm cluster created.')


def generate_ssh_keypair(key_fileprefix):
    """Generate an ssh keypair for use with user logins

    :param str key_fileprefix: key file prefix
    :rtype: tuple
    :return: (private key filename, public key filename)
    """
    pubkey = key_fileprefix + '.pub'
    try:
        os.remove(key_fileprefix)
    except OSError:
        pass
    try:
        os.remove(pubkey)
    except OSError:
        pass
    print('generating ssh key pair')
    subprocess.check_call(
        ['ssh-keygen', '-f', key_fileprefix, '-t', 'rsa', '-N', ''''''])
    return (key_fileprefix, pubkey)


def add_admin_user_to_compute_node(
        batch_client, pool_id, node, username, ssh_public_key):
    """Adds an administrative user to the Batch Compute Node

    :param batch_client: The batch client to use.
    :type batch_client: `batchserviceclient.BatchServiceClient`
    :param str pool_id: The pool id containing the node.
    :param node: The compute node.
    :type node: `batchserviceclient.models.ComputeNode`
    :param str username: user name
    :param str ssh_public_key: ssh rsa public key
    """
    print('adding user {} to node {} in pool {}'.format(
        username, node.id, pool_id))
    batch_client.compute_node.add_user(
        pool_id,
        node.id,
        batchmodels.ComputeNodeUser(
            username,
            is_admin=True,
            password=None,
            ssh_public_key=common.helpers.decode_string(
                open(ssh_public_key, 'rb').read()))
    )
    print('user {} added to node {}.'.format(username, node.id))


def create_pool_and_wait_for_nodes(
        batch_client, block_blob_client, pool_id, vm_size, vm_count):
    """Creates an Azure Batch pool with the specified id.

    :param batch_client: The batch client to use.
    :type batch_client: `batchserviceclient.BatchServiceClient`
    :param block_blob_client: The storage block blob client to use.
    :type block_blob_client: `azure.storage.blob.BlockBlobService`
    :param str pool_id: The id of the pool to create.
    :param str vm_size: vm size (sku)
    :param int vm_count: number of vms to allocate
    :rtype: list
    :return: list of `batchserviceclient.models.ComputeNode`
    """
    # pick the latest supported 14.04 sku for UbuntuServer
    sku_to_use, image_ref_to_use = \
        common.helpers.select_latest_verified_vm_image_with_node_agent_sku(
            batch_client, 'Canonical', 'UbuntuServer', '14.04')

    # upload start task script
    block_blob_client.create_container(_CONTAINER_NAME, fail_on_exist=False)
    sas_url = common.helpers.upload_blob_and_create_sas(
        block_blob_client,
        _CONTAINER_NAME,
        _STARTTASK_RESOURCE_FILE,
        _STARTTASK_SHELL_SCRIPT_PATH,
        datetime.datetime.utcnow() + datetime.timedelta(hours=1))

    # create pool with start task
    pool = batchmodels.PoolAddParameter(
        id=pool_id,
        enable_inter_node_communication=True,
        virtual_machine_configuration=batchmodels.VirtualMachineConfiguration(
            image_reference=image_ref_to_use,
            node_agent_sku_id=sku_to_use),
        vm_size=vm_size,
        target_dedicated=vm_count,
        start_task=batchmodels.StartTask(
            command_line=_STARTTASK_RESOURCE_FILE,
            user_identity=_POOL_ADMIN_USER,
            wait_for_success=True,
            resource_files=[
                batchmodels.ResourceFile(
                    file_path=_STARTTASK_RESOURCE_FILE, blob_source=sas_url)
            ]),
    )
    common.helpers.create_pool_if_not_exist(batch_client, pool)

    # because we want all nodes to be available before any tasks are assigned
    # to the pool, here we will wait for all compute nodes to reach idle
    nodes = common.helpers.wait_for_all_nodes_state(
        batch_client, pool,
        frozenset(
            (batchmodels.ComputeNodeState.start_task_failed,
             batchmodels.ComputeNodeState.unusable,
             batchmodels.ComputeNodeState.idle)
        )
    )

    # ensure all node are idle
    if any(node.state != batchmodels.ComputeNodeState.idle for node in nodes):
        raise RuntimeError('node(s) of pool {} not in idle state'.format(
            pool.id))

    return nodes


def add_docker_batch_task(batch_client, block_blob_client, job_id, pool_id):
    """Submits a docker task via Batch scheduler

    :param batch_client: The batch client to use.
    :type batch_client: `batchserviceclient.BatchServiceClient`
    :param block_blob_client: The storage block blob client to use.
    :type block_blob_client: `azure.storage.blob.BlockBlobService`
    :param str job_id: The id of the job to use.
    :param str pool_id: The id of the pool to use.
    :rtype: str
    :return: task id of added task
    """
    task_commands = [
        'docker run hello-world',
        'docker ps -a',
    ]

    task = batchmodels.TaskAddParameter(
        id=_TASK_ID,
        command_line=common.helpers.wrap_commands_in_shell(
            'linux', task_commands),
        user_identity=_POOL_ADMIN_USER,
    )

    batch_client.task.add(job_id=job_id, task=task)

    return task.id


def execute_sample(global_config, sample_config):
    """Executes the sample with the specified configurations.

    :param global_config: The global configuration to use.
    :type global_config: `configparser.ConfigParser`
    :param sample_config: The sample specific configuration to use.
    :type sample_config: `configparser.ConfigParser`
    """
    # Set up the configuration
    batch_account_key = global_config.get('Batch', 'batchaccountkey')
    batch_account_name = global_config.get('Batch', 'batchaccountname')
    batch_service_url = global_config.get('Batch', 'batchserviceurl')

    storage_account_key = global_config.get('Storage', 'storageaccountkey')
    storage_account_name = global_config.get('Storage', 'storageaccountname')
    storage_account_suffix = global_config.get(
        'Storage',
        'storageaccountsuffix')

    should_delete_container = sample_config.getboolean(
        'DEFAULT',
        'shoulddeletecontainer')
    should_delete_job = sample_config.getboolean(
        'DEFAULT',
        'shoulddeletejob')
    should_delete_pool = sample_config.getboolean(
        'DEFAULT',
        'shoulddeletepool')
    generate_ssh_tunnel_script = sample_config.getboolean(
        'DEFAULT',
        'generatesshtunnelscript')
    pool_vm_size = sample_config.get(
        'DEFAULT',
        'poolvmsize')
    pool_vm_count = sample_config.getint(
        'DEFAULT',
        'poolvmcount')

    # Print the settings we are running with
    common.helpers.print_configuration(global_config)
    common.helpers.print_configuration(sample_config)

    credentials = batchauth.SharedKeyCredentials(
        batch_account_name,
        batch_account_key)
    batch_client = batch.BatchServiceClient(
        credentials,
        base_url=batch_service_url)

    # Retry 5 times -- default is 3
    batch_client.config.retry_policy.retries = 5

    block_blob_client = azureblob.BlockBlobService(
        account_name=storage_account_name,
        account_key=storage_account_key,
        endpoint_suffix=storage_account_suffix)

    job_id = common.helpers.generate_unique_resource_name('DockerSwarm')
    pool_id = common.helpers.generate_unique_resource_name('DockerSwarm')
    public_key = None
    private_key = None
    try:
        # create pool and wait for node idle
        nodes = create_pool_and_wait_for_nodes(
            batch_client,
            block_blob_client,
            pool_id,
            pool_vm_size,
            pool_vm_count)

        # generate ssh key pair
        private_key, public_key = generate_ssh_keypair('batch_id_rsa')

        # add compute node user to nodes with ssh key
        for node in nodes:
            add_admin_user_to_compute_node(
                batch_client, pool_id, node, _NODE_USERNAME, public_key)

        # designate a swarm master node
        master_node, swarm_token = designate_master_docker_swarm_node(
            batch_client, pool_id, nodes, job_id)

        # add nodes to swarm
        add_nodes_to_swarm(
            batch_client, pool_id, nodes, job_id, master_node, swarm_token)

        # connect to docker remotely
        connect_to_remote_docker_swarm_master(
            batch_client, pool_id, nodes, master_node[1], _NODE_USERNAME,
            private_key, generate_ssh_tunnel_script)

        # submit job and add a task
        print('submitting a docker run task via Azure Batch...')
        task_id = add_docker_batch_task(
            batch_client,
            block_blob_client,
            job_id,
            pool_id)

        # wait for tasks to complete
        common.helpers.wait_for_tasks_to_complete(
            batch_client,
            job_id,
            datetime.timedelta(minutes=5))

        common.helpers.print_task_output(batch_client, job_id, [task_id])
    finally:
        # perform clean up
        if public_key is not None:
            try:
                os.remove(public_key)
            except OSError:
                pass
        if private_key is not None:
            if generate_ssh_tunnel_script:
                print('not deleting ssh private key due to ssh tunnel script!')
            else:
                try:
                    os.remove(private_key)
                except OSError:
                    pass
        if should_delete_container:
            print('Deleting container: {}'.format(_CONTAINER_NAME))
            block_blob_client.delete_container(
                _CONTAINER_NAME,
                fail_not_exist=False)
        if should_delete_job:
            print('Deleting job: {}'.format(job_id))
            batch_client.job.delete(job_id)
        if should_delete_pool:
            print('Deleting pool: {}'.format(pool_id))
            batch_client.pool.delete(pool_id)


if __name__ == '__main__':
    global_config = configparser.ConfigParser()
    global_config.read(common.helpers._SAMPLES_CONFIG_FILE_NAME)

    sample_config = configparser.ConfigParser()
    sample_config.read(
        os.path.splitext(os.path.basename(__file__))[0] + '.cfg')

    execute_sample(global_config, sample_config)
