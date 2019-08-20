# sample3_encrypted_resourcefiles.py Code Sample
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
import base64
try:
    import configparser
except ImportError:
    import ConfigParser as configparser
import datetime
import os
import subprocess

import azure.storage.blob as azureblob
import azure.batch._batch_service_client as batch
import azure.batch.batch_auth as batchauth
import azure.batch.models as batchmodels

import common.helpers

_CONTAINER_NAME = 'encryptedresourcefiles'
_RESOURCE_NAME = 'secret.txt'
_RESOURCE_TO_ENCRYPT = os.path.join('resources', _RESOURCE_NAME)
_TASK_NAME = 'EncryptedResourceFiles'
_PFX_PASSPHRASE = '123abc'


def generate_secrets(privatekey_pemfile, pfxfile):
    """Generate a pem file for use with blobxfer and a derived pfx file for
    use with Azure Batch service

    :param str privatekey_pemfile: name of the privatekey pem file to generate
    :param str pfxfile: name of the pfx file to export
    :rtype: str
    :return: sha1 thumbprint of pfx
    """
    # generate pem file with private key and no password
    # NOTE: generated private key is insecure and is only shown below for
    # sample purposes. Always protect private keys with strong passphrases.
    subprocess.check_call(
        ['openssl', 'req', '-new', '-nodes', '-x509', '-newkey', 'rsa:2048',
         '-keyout', privatekey_pemfile, '-out', 'cert.pem', '-days', '14',
         '-subj', '/C=US/ST=None/L=None/O=None/CN=Test']
    )
    # convert pem to pfx for Azure Batch service
    # NOTE: generated pfx file has a weak passphrase and is only shown below
    # for sample purposes. Always protect private keys with strong passphrases.
    subprocess.check_call(
        ['openssl', 'pkcs12', '-export', '-out', pfxfile, '-inkey',
         privatekey_pemfile, '-in', 'cert.pem', '-certfile', 'cert.pem',
         '-passin', 'pass:', '-passout', 'pass:' + _PFX_PASSPHRASE]
    )
    # remove cert.pem
    os.remove('cert.pem')
    # compute sha1 thumbprint of pfx
    pfxdump = subprocess.check_output(
        ['openssl', 'pkcs12', '-in', pfxfile, '-nodes', '-passin',
         'pass:' + _PFX_PASSPHRASE]
    )
    proc = subprocess.Popen(
        ['openssl', 'x509', '-noout', '-fingerprint'], stdin=subprocess.PIPE,
        stdout=subprocess.PIPE
    )
    sha1_cert_tp = proc.communicate(input=pfxdump)[0]
    # return just the thumbprint from the above openssl command in lowercase
    # expected openssl output is in the form: SHA1 Fingerprint=<thumbprint>
    return ''.join(common.helpers.decode_string(
        sha1_cert_tp).strip().split('=')[1].split(':')).lower()


def encrypt_localfile_to_blob_storage(
        storage_account_name, storage_account_key, container, localresource,
        rm_rsakey_pemfile=True):
    """Encrypts localfile and places it in blob storage via blobxfer

    :param str storage_account_name: storage account name
    :param str storage_account_key: storage account key
    :param str container: blob storage container
    :param str localresource: local resource file to encrypt
    :param bool rm_rsakey_pemfile: remove RSA key pem file
    :rtype: tuple
    :return: (path to rsa private key as a PFX, sha1 thumbprint)
    """
    # create encryption secrets
    rsakeypem = 'rsakey.pem'
    rsakeypfx = 'rsakey.pfx'
    sha1_cert_tp = generate_secrets(rsakeypem, rsakeypfx)
    subprocess.check_call(
        ['blobxfer', 'upload', '--storage-account', storage_account_name,
         '--remote-path', container, '--local-path', localresource,
         '--storage-account-key', storage_account_key,
         '--rsa-private-key', rsakeypem,
         '--no-progress-bar']
    )
    # remove rsakey.pem
    if rm_rsakey_pemfile:
        os.remove(rsakeypem)
    return (rsakeypfx, sha1_cert_tp)


def add_certificate_to_account(
        batch_client, pfxfile, pfx_password, sha1_cert_tp, rm_pfxfile=True):
    """Adds a certificate to a Batch account.

    :param batch_client: The batch client to use.
    :type batch_client: `batchserviceclient.BatchServiceClient`
    :param str pfxfile: pfx file to upload
    :param str pfx_passphrase: pfx password
    :param str sha1_cert_tp: sha1 thumbprint of pfx
    :param bool rm_pfxfile: remove PFX file from local disk
    """
    print('adding pfx cert {} to account'.format(sha1_cert_tp))
    data = common.helpers.decode_string(
        base64.b64encode(open(pfxfile, 'rb').read()))
    batch_client.certificate.add(
        certificate=batchmodels.CertificateAddParameter(
            thumbprint=sha1_cert_tp,
            thumbprint_algorithm='sha1',
            data=data,
            certificate_format=batchmodels.CertificateFormat.pfx,
            password=pfx_password)
    )
    # remove pfxfile
    if rm_pfxfile:
        os.remove(pfxfile)
    print('certificate added.')


def create_pool_and_wait_for_node(
        batch_client, pool_id, vm_size, vm_count, sha1_cert_tp):
    """Creates an Azure Batch pool with the specified id.

    :param batch_client: The batch client to use.
    :type batch_client: `batchserviceclient.BatchServiceClient`
    :param str pool_id: The id of the pool to create.
    :param str vm_size: vm size (sku)
    :param int vm_count: number of vms to allocate
    :param str sha1_cert_tp: sha1 cert thumbprint for cert ref
    """
    # pick the latest supported 16.04 sku for UbuntuServer
    sku_to_use, image_ref_to_use = \
        common.helpers.select_latest_verified_vm_image_with_node_agent_sku(
            batch_client, 'Canonical', 'UbuntuServer', '16.04')

    # create start task commands
    # 1. update repository
    # 2. install blobxfer pre-requisites
    # 3. pip install blobxfer python script
    start_task_commands = [
        'apt-get update',
        'apt-get install -y build-essential libssl-dev libffi-dev ' +
        'libpython-dev python-dev python-pip',
        'pip install --upgrade blobxfer'
    ]

    user = batchmodels.AutoUserSpecification(
        scope=batchmodels.AutoUserScope.pool,
        elevation_level=batchmodels.ElevationLevel.admin)
    # create pool with start task and cert ref with visibility of task
    pool = batchmodels.PoolAddParameter(
        id=pool_id,
        virtual_machine_configuration=batchmodels.VirtualMachineConfiguration(
            image_reference=image_ref_to_use,
            node_agent_sku_id=sku_to_use),
        vm_size=vm_size,
        target_dedicated_nodes=vm_count,
        start_task=batchmodels.StartTask(
            command_line=common.helpers.wrap_commands_in_shell(
                'linux', start_task_commands),
            user_identity=batchmodels.UserIdentity(auto_user=user),
            wait_for_success=True),
        certificate_references=[
            batchmodels.CertificateReference(
                thumbprint=sha1_cert_tp,
                thumbprint_algorithm='sha1',
                visibility=[batchmodels.CertificateVisibility.task])
        ],
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
            pool_id))


def submit_job_and_add_task(
        batch_client, block_blob_client, storage_account_name,
        storage_account_key, container, resourcefile, job_id, pool_id,
        sha1_cert_tp):
    """Submits a job to the Azure Batch service and adds
    a task that decrypts the file stored in Azure Storage.

    :param batch_client: The batch client to use.
    :type batch_client: `batchserviceclient.BatchServiceClient`
    :param block_blob_client: The storage block blob client to use.
    :type block_blob_client: `azure.storage.blob.BlockBlobService`
    :param str storage_account_name: storage account name
    :param str storage_account_key: storage account key
    :param str container: blob storage container
    :param str resourcefile: resource file to add to task
    :param str job_id: The id of the job to create.
    :param str pool_id: The id of the pool to use.
    :param str sha1_cert_tp: sha1 cert thumbprint for cert ref
    """
    job = batchmodels.JobAddParameter(
        id=job_id,
        pool_info=batchmodels.PoolInformation(pool_id=pool_id))

    batch_client.job.add(job)

    # generate short-lived sas key for blobxfer
    sastoken = common.helpers.create_sas_token(
        block_blob_client, container, _RESOURCE_NAME,
        azureblob.BlobPermissions.READ)

    # issue the following commands for the task:
    # 1. convert pfx installed by the Azure Batch Service to pem
    # 2. transfer the encrypted blob from Azure Storage to local disk and
    #    decrypt contents using the private key
    # 3. output decrypted secret.txt file
    # Note: certs on Linux Batch Compute Nodes are placed in:
    # $AZ_BATCH_CERTIFICATES_DIR where the cert itself has a naming convention
    # of <thumbprint algorithm>-<lowercase thumbprint>.<certificate format>
    task_commands = [
        ('openssl pkcs12 -in $AZ_BATCH_CERTIFICATES_DIR/sha1-{tp}.pfx -out '
         '$AZ_BATCH_CERTIFICATES_DIR/privatekey.pem -nodes -password '
         'file:$AZ_BATCH_CERTIFICATES_DIR/sha1-{tp}.pfx.pw').format(
             tp=sha1_cert_tp),
        ('blobxfer download --storage-account {sa} '
         '--remote-path {cont}/{rf} --local-path . --sas "{sas}" '
         '--rsa-private-key $AZ_BATCH_CERTIFICATES_DIR/privatekey.pem').format(
             sa=storage_account_name, cont=container, sas=sastoken,
             rf=resourcefile),
        'echo secret.txt contents:',
        'cat {}'.format(resourcefile)
    ]

    task = batchmodels.TaskAddParameter(
        id="MyEncryptedResourceTask",
        command_line=common.helpers.wrap_commands_in_shell(
            'linux', task_commands))

    batch_client.task.add(job_id=job.id, task=task)


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
    should_delete_cert = sample_config.getboolean(
        'DEFAULT',
        'shoulddeletecert')
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
        batch_url=batch_service_url)

    # Retry 5 times -- default is 3
    batch_client.config.retry_policy.retries = 5

    block_blob_client = azureblob.BlockBlobService(
        account_name=storage_account_name,
        account_key=storage_account_key,
        endpoint_suffix=storage_account_suffix)

    job_id = common.helpers.generate_unique_resource_name(
        'EncryptedResourceFiles')
    pool_id = common.helpers.generate_unique_resource_name(
        'EncryptedResourceFiles')
    sha1_cert_tp = None
    try:
        # encrypt local file and upload to blob storage via blobxfer
        rsapfxfile, sha1_cert_tp = encrypt_localfile_to_blob_storage(
            storage_account_name, storage_account_key, _CONTAINER_NAME,
            _RESOURCE_TO_ENCRYPT)

        # add certificate to account
        add_certificate_to_account(
            batch_client, rsapfxfile, _PFX_PASSPHRASE, sha1_cert_tp)

        # create pool and wait for node idle
        create_pool_and_wait_for_node(
            batch_client,
            pool_id,
            pool_vm_size,
            pool_vm_count,
            sha1_cert_tp)

        # submit job and add a task
        submit_job_and_add_task(
            batch_client,
            block_blob_client,
            storage_account_name,
            storage_account_key,
            _CONTAINER_NAME,
            _RESOURCE_NAME,
            job_id,
            pool_id,
            sha1_cert_tp)

        # wait for tasks to complete
        common.helpers.wait_for_tasks_to_complete(
            batch_client,
            job_id,
            datetime.timedelta(minutes=20))

        tasks = batch_client.task.list(job_id)
        task_ids = [task.id for task in tasks]

        common.helpers.print_task_output(batch_client, job_id, task_ids)
    finally:
        # perform clean up
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
        if should_delete_cert and sha1_cert_tp is not None:
            # cert deletion requires no active references to cert, so
            # override any config settings for preserving job/pool
            if not should_delete_job:
                print('Deleting job: {}'.format(job_id))
                batch_client.job.delete(job_id)
            if not should_delete_pool:
                print('Deleting pool: {}'.format(pool_id))
                batch_client.pool.delete(pool_id)
            print('Deleting cert: {}'.format(sha1_cert_tp))
            batch_client.certificate.delete('sha1', sha1_cert_tp)


if __name__ == '__main__':
    global_config = configparser.ConfigParser()
    global_config.read(common.helpers._SAMPLES_CONFIG_FILE_NAME)

    sample_config = configparser.ConfigParser()
    sample_config.read(
        os.path.splitext(os.path.basename(__file__))[0] + '.cfg')

    execute_sample(global_config, sample_config)
