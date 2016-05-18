# python_tutorial_client.py - Batch Python SDK tutorial sample
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
import datetime
import os
import sys
import time

try:
    input = raw_input
except NameError:
    pass

import azure.storage.blob as azureblob
import azure.batch.batch_service_client as batch
import azure.batch.batch_auth as batchauth
import azure.batch.models as batchmodels

# Update the Batch and Storage account credential strings below with the values
# unique to your accounts. These are used when constructing connection strings
# for the Batch and Storage client objects.
BATCH_ACCOUNT_NAME = ''
BATCH_ACCOUNT_KEY = ''
BATCH_ACCOUNT_URL = ''

STORAGE_ACCOUNT_NAME = ''
STORAGE_ACCOUNT_KEY = ''

POOL_ID = 'PythonTutorialPool'
POOL_NODE_COUNT = 1
POOL_NODE_SIZE = 'STANDARD_A1'
NODE_OS_DISTRO = 'Ubuntu'
NODE_OS_VERSION = '14'

JOB_ID = 'PythonTutorialJob'


def query_yes_no(question, default="yes"):
    """
    Prompts the user for yes/no input, displaying the specified question text.

    :param str question: The text of the prompt for input.
    :param str default: The default if the user hits <ENTER>. Acceptable values
    are 'yes', 'no', and None.
    :return: 'yes' or 'no'
    """
    valid = {'yes': 'yes',   'y': 'yes',  'ye': 'yes',
             'no': 'no',     'n': 'no'}
    if default is None:
        prompt = ' [y/n] '
    elif default == 'yes':
        prompt = ' [Y/n] '
    elif default == 'no':
        prompt = ' [y/N] '
    else:
        raise ValueError("Invalid default answer: '%s'" % default)

    while 1:
        choice = input(question + prompt).lower()
        if default is not None and choice == '':
            return default
        elif choice in valid.keys():
            return valid[choice]
        else:
            print("Please respond with 'yes' or 'no' (or 'y' or 'n').\n")


def wrap_commands_in_shell(os_type, commands):
    """
    Wraps commands in a shell appropriate for the specified OS.

    :param list commands: List of commands to wrap.
    :param str os_type: OS type, linux or windows
    :rtype: str
    :return: A command line wrapping the specified commands in a command shell.
    """
    if os_type.lower() == 'linux':
        return "/bin/bash -c 'set -e; set -o pipefail; {}; wait'".format(
            ';'.join(commands))
    elif os_type.lower() == 'windows':
        return 'cmd.exe /c "{}"'.format('&'.join(commands))
    else:
        raise ValueError('Unknown os_type: {}'.format(os_type))


def print_batch_exception(batch_exception):
    """
    Prints the contents of the specified Batch exception.

    :param batch_exception:
    :return:
    """
    print('-------------------------------------------')
    print('Exception encountered:')
    print(batch_exception.error.message.value)
    print()
    try:
        for mesg in batch_exception.error.values:
            print('{0}:\t{1}'.format(mesg.key, mesg.value))
    except:
        pass
    print('-------------------------------------------')


def upload_file_to_container(block_blob_client, container_name, file_path):
    """
    Uploads a local file to an Azure Blob storage container.

    :param BlockBlobService block_blob_client: A blob service client.
    :param str container_name: The name of the Azure Blob storage container.
    :param str file_path: The local path to the file.
    :return: ResourceFile An azure.batch.models.Resource file initialized with
    a SAS URL appropriate for Batch tasks.
    """
    blob_name = os.path.basename(file_path)

    print('Uploading file {0} to container [{1}]...'.format(file_path,
                                                            container_name))

    block_blob_client.create_blob_from_path(container_name,
                                            blob_name,
                                            file_path)

    sas_token = block_blob_client.generate_blob_shared_access_signature(
        container_name,
        blob_name,
        permission=azureblob.BlobPermissions.READ,
        expiry=datetime.datetime.utcnow() + datetime.timedelta(hours=2))

    sas_url = block_blob_client.make_blob_url(container_name,
                                              blob_name,
                                              sas_token=sas_token)

    return batchmodels.ResourceFile(file_path=blob_name,
                                    blob_source=sas_url)


def upload_files_to_container(block_blob_client, container_name, file_paths):
    """
    Uploads the files in the collection to the specified Azure Blob storage
    container.

    :param BlockBlobService block_blob_client: A blob service client.
    :param str container_name: The name of the Azure Blob storage container.
    :param list file_paths: A collection of local file paths.
    :return: list A collection of ResourceFiles that include SAS URLs.
    """
    resource_files = list()

    for file_path in file_paths:
        resource_files.append(
            upload_file_to_container(block_blob_client,
                                     container_name,
                                     file_path))

    return resource_files


def get_container_sas_token(
        block_blob_client,
        container_name,
        blob_permissions):
    """
    Obtains a shared access signature granting the specified permissions to the
    container.

    :param BlockBlobService block_blob_client: A blob service client.
    :param str container_name: The name of the Azure Blob storage container.
    :param BlobPermissions blob_permissions:
    :return: str A SAS token granting the specified permissions to the
    container.
    """
    # Obtain the SAS token for the container, setting the expiry time and
    # permissions. In this case, no start time is specified, so the shared\
    # access signature becomes valid immediately.
    container_sas_token = \
        block_blob_client.generate_container_shared_access_signature(
            container_name,
            permission=blob_permissions,
            expiry=datetime.datetime.utcnow() + datetime.timedelta(hours=2))

    return container_sas_token


def create_pool(batch_service_client, pool_id,
                resource_files, distro, version):
    """
    Creates a pool of compute nodes with the specified OS settings.

    :param BatchServiceClient batch_service_client: A Batch service client.
    :param str pool_id: The ID for the pool.
    :param list resource_files: A collection of resource files for the pool's
    start task.
    :param str distro: The Linux distribution to that should be installed on the
    compute nodes, e.g. 'Ubuntu' or 'CentOS'.
    :param str version: The version of the operating system for the compute nodes,
    e.g. '15' or '14.04'.
    :return:
    """
    print('Creating pool [{0}]...'.format(pool_id))

    # Create a new pool of Linux compute nodes using an Azure Virtual Machines
    # Marketplace image. For more information about creating pools of Linux
    # nodes, see:
    # https://azure.microsoft.com/documentation/articles/batch-linux-nodes/

    # Get the list of node agents from the Batch service
    node_agent_skus = batch_service_client.account.list_node_agent_skus()

    # Get the first node agent that is compatible with the specified distro
    node_agent = next(agent for agent in node_agent_skus
                      for image_ref in agent.verified_image_references
                      if distro.lower() in image_ref.offer.lower() and
                      version.lower() in image_ref.sku.lower())

    # Get the last image reference from the list of verified references
    # for the node agent we obtained. Typically, the verified image
    # references are returned in ascending release order.
    ir = [image_ref for image_ref in node_agent.verified_image_references
          if distro.lower() in image_ref.offer.lower() and
          version.lower() in image_ref.sku.lower()][-1]

    # Create the VirtualMachineConfiguration, specifying the VM image
    # reference and the Batch node agent to be installed on the node.
    # Note that these commands are valid for a pool of Ubuntu-based compute
    # nodes, and that you may need to adjust the commands for execution
    # on other distros.
    vmc = batchmodels.VirtualMachineConfiguration(
        image_reference=ir,
        node_agent_sku_id=node_agent.id)

    task_commands = [
        'cp -r $AZ_BATCH_TASK_WORKING_DIR/* $AZ_BATCH_NODE_SHARED_DIR',
        'chmod +x $AZ_BATCH_NODE_SHARED_DIR/python_tutorial_task.py',
        'apt-get -y install python-pip',
        'pip install azure-storage']

    new_pool = batch.models.PoolAddParameter(
        id=pool_id,
        virtual_machine_configuration=vmc,
        vm_size=POOL_NODE_SIZE,
        target_dedicated=POOL_NODE_COUNT,
        start_task=batch.models.StartTask(
            command_line=wrap_commands_in_shell('linux', task_commands),
            run_elevated=True,
            wait_for_success=True,
            resource_files=resource_files),
        )

    try:
        batch_service_client.pool.add(new_pool)
    except batchmodels.batch_error.BatchErrorException as err:
        print_batch_exception(err)
    except:
        print('Unexpected error:', sys.exc_info()[0])
        raise


def create_job(batch_service_client, job_id, pool_id):
    """
    Creates a job with the specified ID, associated with the specified pool.

    :param BatchServiceClient batch_service_client: A Batch service client.
    :param str job_id: The ID for the job.
    :param str pool_id: The ID for the pool.
    """
    print('Creating job [{0}]...'.format(job_id))

    job = batch.models.JobAddParameter(
        JOB_ID,
        batch.models.PoolInformation(pool_id=pool_id))

    try:
        batch_service_client.job.add(job)
    except batchmodels.batch_error.BatchErrorException as err:
        print_batch_exception(err)
    except:
        print('Unexpected error:', sys.exc_info()[0])
        raise


def add_tasks(batch_service_client, job_id, input_files,
              output_container_name, output_container_sas_token):
    """
    Adds a task for each input file in the collection to the specified job.

    :param BatchServiceClient batch_service_client: A Batch service client.
    :param str job_id: The ID of the job to which to add the tasks.
    :param list input_files: A collection of input files. One task will be
     created for each input file.
    :param output_container_name: The ID of an Azure Blob storage container to
    which the tasks will upload their results.
    :param output_container_sas_token: A SAS token granting write access to
    the specified Azure Blob storage container.
    """

    print('Adding {0} tasks to job [{1}]...'.format(len(input_files), job_id))

    tasks = list()

    for input_file in input_files:

        command = ['python $AZ_BATCH_NODE_SHARED_DIR/python_tutorial_task.py '
                   '--filepath {0} --numwords {1} --storageaccount {2} '
                   '--storagecontainer {3} --sastoken "{4}"'.format(
                    input_file.file_path,
                    '3',
                    STORAGE_ACCOUNT_NAME,
                    output_container_name,
                    output_container_sas_token)]

        tasks.append(batch.models.TaskAddParameter(
                'topNtask{}'.format(input_files.index(input_file)),
                wrap_commands_in_shell('linux', command),
                resource_files=[input_file]
                )
        )

    batch_service_client.task.add_collection(job_id, tasks)


def wait_for_tasks_to_complete(batch_service_client, job_id, timeout):
    """
    Returns when all tasks in the specified job reach the Completed state.

    :param BatchServiceClient batch_service_client: A Batch service client.
    :param str job_id: The id of the job whose tasks should be to monitored.
    :param timedelta timeout: The duration to wait for task completion. If all tasks in
    the specified job do not reach Completed state within this time period, an
    exception will be raised.
    """
    timeout_expiration = datetime.datetime.now() + timeout

    print("Monitoring all tasks for 'Completed' state, timeout in {0}..."
          .format(timeout), end='')

    while datetime.datetime.now() < timeout_expiration:
        print('.', end='')
        sys.stdout.flush()
        tasks = batch_service_client.task.list(job_id)

        incomplete_tasks = [task for task in tasks if
                            task.state != batchmodels.TaskState.completed]
        if not incomplete_tasks:
            print()
            return True
        else:
            time.sleep(1)

    print()
    raise RuntimeError("ERROR: Tasks did not reach 'Completed' state within "
                       "timeout period of " + str(timeout))


def download_blobs_from_container(block_blob_client, container_name,
                                  directory_path):
    """
    Downloads all blobs from the specified Azure Blob storage container.

    :param BlockBlobService block_blob_client: A blob service client.
    :param container_name: The Azure Blob storage container from which to
     download files.
    :param directory_path: The local directory to which to download the files.
    """
    print('Downloading all files from container [{0}]...'.format(
        container_name))

    container_blobs = block_blob_client.list_blobs(container_name)

    for blob in container_blobs.items:
        destination_file_path = os.path.join(directory_path, blob.name)

        block_blob_client.get_blob_to_path(container_name,
                                     blob.name,
                                     destination_file_path)

        print('  Downloaded blob [{0}] from container [{1}] to {2}'.format(
            blob.name,
            container_name,
            destination_file_path))

    print('  Download complete!')

if __name__ == '__main__':

    start_time = datetime.datetime.now().replace(microsecond=0)
    print('Sample start: {0}'.format(start_time))
    print()

    # Create the blob client, for use in obtaining references to
    # blob storage containers and uploading files to containers.
    blob_client = azureblob.BlockBlobService(
        account_name=STORAGE_ACCOUNT_NAME,
        account_key=STORAGE_ACCOUNT_KEY)

    # Use the blob client to create the containers in Azure Storage if they
    # don't yet exist.
    app_container_name = 'application'
    input_container_name = 'input'
    output_container_name = 'output'
    blob_client.create_container(app_container_name, fail_on_exist=False)
    blob_client.create_container(input_container_name, fail_on_exist=False)
    blob_client.create_container(output_container_name, fail_on_exist=False)

    # Paths to the task script. This script will be executed by the tasks that
    # run on the compute nodes.
    application_file_paths = [os.path.realpath('python_tutorial_task.py')]

    # The collection of data files that are to be processed by the tasks.
    input_file_paths = [os.path.realpath('./data/taskdata1.txt'),
                        os.path.realpath('./data/taskdata2.txt'),
                        os.path.realpath('./data/taskdata3.txt')]

    # Upload the application script to Azure Storage. This is the script that
    # will process the data files, and is executed by each of the tasks on the
    # compute nodes.
    application_files = upload_files_to_container(blob_client,
                                                  app_container_name,
                                                  application_file_paths)

    # Upload the data files. This is the data that will be processed by each of
    # the tasks executed on the compute nodes in the pool.
    input_files = upload_files_to_container(blob_client,
                                            input_container_name,
                                            input_file_paths)

    # Obtain a shared access signature that provides write access to the output
    # container to which the tasks will upload their output.
    output_container_sas_token = get_container_sas_token(
        blob_client,
        output_container_name,
        azureblob.BlobPermissions.WRITE)

    # Create a Batch service client. We'll now be interacting with the Batch
    # service in addition to Storage
    credentials = batchauth.SharedKeyCredentials(BATCH_ACCOUNT_NAME,
                                                 BATCH_ACCOUNT_KEY)

    batch_client = batch.BatchServiceClient(
        batch.BatchServiceClientConfiguration(
            credentials,
            base_url=BATCH_ACCOUNT_URL))

    # Create the pool that will contain the compute nodes that will execute the
    # tasks. The resource files we pass in are used for configuring the pool's
    # start task, which is executed each time a node first joins the pool (or
    # is rebooted or re-imaged).
    create_pool(batch_client,
                POOL_ID,
                application_files,
                NODE_OS_DISTRO,
                NODE_OS_VERSION)

    # Create the job that will run the tasks.
    create_job(batch_client, JOB_ID, POOL_ID)

    # Add the tasks to the job. We need to supply a container shared access
    # signature (SAS) token for the tasks so that they can upload their output
    # to Azure Storage.
    add_tasks(batch_client,
              JOB_ID,
              input_files,
              output_container_name,
              output_container_sas_token)

    # Pause execution  until tasks reach Completed state.
    wait_for_tasks_to_complete(batch_client,
                               JOB_ID,
                               datetime.timedelta(minutes=25))

    print("  Success! All tasks reached the 'Completed' state within the "
          "specified timeout period.")

    # Download the task output files from the output Storage container to a
    # local directory
    download_blobs_from_container(blob_client,
                                  output_container_name,
                                  os.path.expanduser('~'))

    # Clean up storage resources
    print('Deleting containers...')
    blob_client.delete_container(app_container_name)
    blob_client.delete_container(input_container_name)
    blob_client.delete_container(output_container_name)

    # Print out some timing info
    end_time = datetime.datetime.now().replace(microsecond=0)
    print()
    print('Sample end: {0}'.format(end_time))
    print('Elapsed time: {0}'.format(end_time - start_time))
    print()

    # Clean up Batch resources (if the user so chooses)
    if query_yes_no('Delete job?') == 'yes':
        batch_client.job.delete(JOB_ID)

    if query_yes_no('Delete pool?') == 'yes':
        batch_client.pool.delete(POOL_ID)

    print()
    input('Press ENTER to exit...')
    exit()
