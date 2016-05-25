# sample5_docker_batch_task.py Code Sample
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
import urllib.parse

import azure.storage.blob as azureblob
import azure.batch.batch_service_client as batch
import azure.batch.batch_auth as batchauth
import azure.batch.models as batchmodels

import common.helpers

_CONTAINER_NAME = 'docker'
_OUTPUT_CONTAINER_NAME = 'output'

_STARTTASK_RESOURCE_FILE = 'docker_starttask.sh'
_STARTTASK_SHELL_SCRIPT_PATH = os.path.join(
    'resources', _STARTTASK_RESOURCE_FILE)

_FFMPEG_IMAGE = 'yidingz/ffmpeg:v3'
_TASK_RESOURCE_FILE = 'docker_batch_task.sh'
_TASK_RESOURCE_FILE_PATH = os.path.join(
    'resources', _TASK_RESOURCE_FILE)
_TASK_CLI = \
    '/bin/sh -c "cat {0} '\
    '| docker run -a STDIN -a STDOUT -a STDERR -d=false -i {1} '\
    'bash /dev/stdin {2}"'

_JOB_STARTTASK_CLI = 'docker pull ' + _FFMPEG_IMAGE

_INPUT_FILE_URLS = [
    'http://video.ch9.ms/ch9/d02f/'
    '5ae17426-06d5-466d-b8d7-6b5db23fd02f/AzureContainerServicewithDocker.mp4',
    'http://video.ch9.ms/ch9/3626/'
    '1bc8418b-95e5-4a54-88aa-8d53541f3626/AzureWebAppsLocalCache.mp4'
]


def create_pool(batch_client, block_blob_client, pool_id, vm_size, vm_count):
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
            run_elevated=True,
            wait_for_success=True,
            resource_files=[
                batchmodels.ResourceFile(
                    file_path=_STARTTASK_RESOURCE_FILE, blob_source=sas_url)
            ]),
    )
    common.helpers.create_pool_if_not_exist(batch_client, pool)

    # Block wait until all nodes are online before submitting job
    nodes = common.helpers.wait_for_all_nodes_state(
        batch_client, pool,
        frozenset(
            (batchmodels.ComputeNodeState.starttaskfailed,
             batchmodels.ComputeNodeState.unusable,
             batchmodels.ComputeNodeState.idle)
        )
    )

    # ensure all node are idle
    if any(node.state != batchmodels.ComputeNodeState.idle for node in nodes):
        raise RuntimeError('node(s) of pool {} not in idle state'.format(
            pool.id))


def add_docker_batch_task(batch_client, block_blob_client, job_id, pool_id):
    """Submits a docker task via Batch scheduler

    :param batch_client: The batch client to use.
    :type batch_client: `batchserviceclient.BatchServiceClient`
    :param block_blob_client: The storage block blob client to use.
    :type block_blob_client: `azure.storage.blob.BlockBlobService`
    :param str job_id: The id of the job to use.
    :param str pool_id: The id of the pool to use.
    :rtype: list
    :return: a list of task_id of the task added.
    """

    task_resource_sas_url = common.helpers.upload_blob_and_create_sas(
        block_blob_client,
        _CONTAINER_NAME,
        _TASK_RESOURCE_FILE,
        _TASK_RESOURCE_FILE_PATH,
        datetime.datetime.utcnow() + datetime.timedelta(hours=1))

    output_container_sas_key = common.helpers.create_container_and_create_sas(
        block_blob_client=block_blob_client,
        container_name=_OUTPUT_CONTAINER_NAME,
        permission=azureblob.ContainerPermissions.WRITE |
        azureblob.ContainerPermissions.LIST,
        expiry=datetime.datetime.utcnow() + datetime.timedelta(hours=1))

    # The start task pulls docker image yidingz/ffmpeg:v3
    job = batchmodels.JobAddParameter(
        id=job_id,
        pool_info=batchmodels.PoolInformation(pool_id=pool_id),
        job_preparation_task=batchmodels.JobPreparationTask(
            command_line=_JOB_STARTTASK_CLI,
            run_elevated=True
        )
    )
    batch_client.job.add(job)

    task_id_list = []
    index = 0
    for url in _INPUT_FILE_URLS:
        filename = urllib.parse.urlsplit(url).path.split('/')[-1]
        parameters = "'{0}' '{1}' '{2}' '{3}'".format(
            url,
            filename,
            output_container_sas_key,
            block_blob_client.account_name)
        # Each task will download a video from chanel9,
        # transcode, and upload to specified output container
        task = batchmodels.TaskAddParameter(
            id=str(index).zfill(4) + '_' + filename.split('.')[0],
            command_line=_TASK_CLI.format(_TASK_RESOURCE_FILE,
                                          _FFMPEG_IMAGE,
                                          parameters),
            run_elevated=True,
            resource_files=[
                batchmodels.ResourceFile(
                    file_path=_TASK_RESOURCE_FILE,
                    blob_source=task_resource_sas_url)
            ]
        )
        task_id_list.append(task.id)
        batch_client.task.add(job_id=job_id, task=task)
        index += 1
    return task_id_list


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

    should_delete_job = sample_config.getboolean(
        'DEFAULT',
        'shoulddeletejob')
    should_delete_pool = sample_config.getboolean(
        'DEFAULT',
        'shoulddeletepool')

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
    client_configuration = batch.BatchServiceClientConfiguration(
        credentials,
        base_url=batch_service_url)

    # Retry 5 times -- default is 3
    client_configuration.retry_policy.retries = 5
    batch_client = batch.BatchServiceClient(client_configuration)

    block_blob_client = azureblob.BlockBlobService(
        account_name=storage_account_name,
        account_key=storage_account_key,
        endpoint_suffix=storage_account_suffix)

    job_id = common.helpers.generate_unique_resource_name('DockerBatchTask')
    pool_id = common.helpers.generate_unique_resource_name('DockerBatchTask')

    try:
        # create pool
        create_pool(
            batch_client,
            block_blob_client,
            pool_id,
            pool_vm_size,
            pool_vm_count)

        # submit job and add a task
        print('submitting docker run tasks via Azure Batch...')
        add_docker_batch_task(
            batch_client,
            block_blob_client,
            job_id,
            pool_id)

        # wait for tasks to complete
        common.helpers.wait_for_tasks_to_complete(
            batch_client,
            job_id,
            datetime.timedelta(minutes=25))

    finally:
        # perform clean up
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
