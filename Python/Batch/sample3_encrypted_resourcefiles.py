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

import base64
from configparser import ConfigParser
import datetime
import os
import subprocess
from typing import Tuple

from azure.core.exceptions import ResourceExistsError, ResourceNotFoundError

from azure.storage.blob import (
    BlobServiceClient,
    BlobSasPermissions
)

from azure.batch import BatchServiceClient
from azure.batch.batch_auth import SharedKeyCredentials
import azure.batch.models as batchmodels

import common.helpers

_CONTAINER_NAME = 'encryptedresourcefiles'
_RESOURCE_NAME = 'secret.txt'
_RESOURCE_TO_ENCRYPT = os.path.join('resources', _RESOURCE_NAME)
_PFX_PASSPHRASE = '123abc'


def generate_secrets(privatekey_pemfile: str, pfxfile: str) -> str:
    """Generate a pem file for use with blobxfer and a derived pfx file for
    use with Azure Batch service

    :param privatekey_pemfile: name of the privatekey pem file to generate
    :param pfxfile: name of the pfx file to export
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
    storage_account_name: str,
    storage_account_key: str,
    container: str,
    localresource: str,
    rm_rsakey_pemfile: bool = True
) -> Tuple[str, str]:
    """Encrypts localfile and places it in blob storage via blobxfer

    :param storage_account_name: storage account name
    :param storage_account_key: storage account key
    :param container: blob storage container
    :param localresource: local resource file to encrypt
    :param rm_rsakey_pemfile: remove RSA key pem file
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
    batch_client: BatchServiceClient,
    pfxfile: str,
    pfx_password: str,
    sha1_cert_tp: str,
    rm_pfxfile: bool = True
):
    """Adds a certificate to a Batch account.

    :param batch_client: The batch client to use.
    :param pfxfile: pfx file to upload
    :param pfx_passphrase: pfx password
    :param sha1_cert_tp: sha1 thumbprint of pfx
    :param rm_pfxfile: remove PFX file from local disk
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
    batch_client: BatchServiceClient,
    pool_id: str,
    vm_size: str,
    vm_count: int,
    sha1_cert_tp: str
):
    """Creates an Azure Batch pool with the specified id.

    :param batch_client: The batch client to use.
    :param pool_id: The id of the pool to create.
    :param vm_size: vm size (sku)
    :param vm_count: number of vms to allocate
    :param sha1_cert_tp: sha1 cert thumbprint for cert ref
    """
    # create start task commands
    # 1. update repository
    # 2. install blobxfer pre-requisites
    # 3. pip install blobxfer python script
    start_task_commands = [
        'apt-get update',
        'apt-get install -y build-essential libssl-dev libffi-dev ' +
        'libpython3-dev python3-dev python3-pip',
        'pip3 install --upgrade pip',
        'pip3 install --upgrade blobxfer'
    ]

    user = batchmodels.AutoUserSpecification(
        scope=batchmodels.AutoUserScope.pool,
        elevation_level=batchmodels.ElevationLevel.admin)
    # create pool with start task and cert ref with visibility of task
    pool = batchmodels.PoolAddParameter(
        id=pool_id,
        virtual_machine_configuration=batchmodels.VirtualMachineConfiguration(
            image_reference=batchmodels.ImageReference(
                publisher="canonical",
                offer="0001-com-ubuntu-server-focal",
                sku="20_04-lts"
            ),
            node_agent_sku_id="batch.node.ubuntu 20.04"),
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
        batch_client,
        pool_id,
        set(
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
    batch_client: BatchServiceClient,
    blob_service_client: BlobServiceClient,
    storage_account_name: str,
    container: str,
    resourcefile: str,
    job_id: str,
    pool_id: str,
    sha1_cert_tp: str
):
    """Submits a job to the Azure Batch service and adds
    a task that decrypts the file stored in Azure Storage.

    :param batch_client: The batch client to use.
    :param blob_service_client: The blob service client to use.
    :param storage_account_name: storage account name
    :param container: blob storage container
    :param resourcefile: resource file to add to task
    :param job_id: The id of the job to create.
    :param pool_id: The id of the pool to use.
    :param sha1_cert_tp: sha1 cert thumbprint for cert ref
    """
    job = batchmodels.JobAddParameter(
        id=job_id,
        pool_info=batchmodels.PoolInformation(pool_id=pool_id))

    batch_client.job.add(job)

    # generate short-lived sas key for blobxfer
    sastoken = common.helpers.create_sas_token(
        blob_service_client,
        container,
        _RESOURCE_NAME,
        BlobSasPermissions.from_string("r")
    )

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


def execute_sample(global_config: ConfigParser, sample_config: ConfigParser):
    """Executes the sample with the specified configurations.

    :param global_config: The global configuration to use.
    :param sample_config: The sample specific configuration to use.
    """
    # Set up the configuration
    batch_account_key = global_config.get('Batch', 'batchaccountkey')
    batch_account_name = global_config.get('Batch', 'batchaccountname')
    batch_service_url = global_config.get('Batch', 'batchserviceurl')

    storage_account_key = global_config.get('Storage', 'storageaccountkey')
    storage_account_url = global_config.get('Storage', 'storageaccounturl')

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

    credentials = SharedKeyCredentials(
        batch_account_name,
        batch_account_key)

    batch_client = BatchServiceClient(
        credentials,
        batch_url=batch_service_url)

    # Retry 5 times -- default is 3
    batch_client.config.retry_policy.retries = 5

    blob_service_client = BlobServiceClient(
        account_url=storage_account_url,
        credential=storage_account_key)

    storage_account_name = blob_service_client.account_name
    if not storage_account_name:
        raise ValueError(
            "BlobServiceClient must be initialized with a valid account name")

    job_id = common.helpers.generate_unique_resource_name(
        'EncryptedResourceFiles')
    pool_id = common.helpers.generate_unique_resource_name(
        'EncryptedResourceFiles')
    sha1_cert_tp = None
    try:
        # Create blob container if it doesn't exist
        try:
            blob_service_client.create_container(_CONTAINER_NAME)
        except ResourceExistsError:
            pass

        # encrypt local file and upload to blob storage via blobxfer
        rsapfxfile, sha1_cert_tp = encrypt_localfile_to_blob_storage(
            storage_account_name,
            storage_account_key,
            _CONTAINER_NAME,
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
            blob_service_client,
            storage_account_name,
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
            try:
                blob_service_client.delete_container(_CONTAINER_NAME)
            except ResourceNotFoundError:
                pass
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
    global_config = ConfigParser()
    global_config.read(common.helpers.SAMPLES_CONFIG_FILE_NAME)

    sample_config = ConfigParser()
    sample_config.read(
        os.path.splitext(os.path.basename(__file__))[0] + '.cfg')

    execute_sample(global_config, sample_config)
