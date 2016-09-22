# linux_mpi_task_demo.py - Batch Python tutorial sample for multi-instance
# tasks in linux (OpenFoam application)
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

try:
    input = raw_input
except NameError:
    pass

import azure.storage.blob as azureblob
import azure.batch.batch_service_client as batch
import azure.batch.batch_auth as batchauth
import multi_task_helpers

sys.path.append('.')
import common.helpers  # noqa

# Update the Batch and Storage account credential strings below with the values
# unique to your accounts.  These are used when constructing connection strings
# for the Batch and Storage client objects.
_BATCH_ACCOUNT_KEY = ''
_BATCH_ACCOUNT_NAME = ''
_BATCH_ACCOUNT_URL = ''

_STORAGE_ACCOUNT_NAME = ''
_STORAGE_ACCOUNT_KEY = ''

_OS_NAME = 'linux'
_APP_NAME = 'openfoam'
_POOL_ID = common.helpers.generate_unique_resource_name(
    'pool_{}_{}'.format(_OS_NAME, _APP_NAME))
_POOL_NODE_COUNT = 3
_POOL_VM_SIZE = 'STANDARD_A9'
_NODE_OS_PUBLISHER = 'OpenLogic'
_NODE_OS_OFFER = 'CentOS-HPC'
_NODE_OS_SKU = '7.1'
_JOB_ID = 'job-{}'.format(_POOL_ID)
_TASK_ID = common.helpers.generate_unique_resource_name(
    'task_{}_{}'.format(_OS_NAME, _APP_NAME))
_TASK_OUTPUT_FILE_PATH_ON_VM = '$AZ_BATCH_TASK_SHARED_DIR/VTK.tgz'
_TASK_OUTPUT_BLOB_NAME = 'Result-{}-VTK.tgz'.format(_TASK_ID)
_NUM_INSTANCES = _POOL_NODE_COUNT
_NUM_PROCESSES_PER_VM = 16
_NUM_ITERATIONS_OPENFOAM = 500


if __name__ == '__main__':

    start_time = datetime.datetime.now().replace(microsecond=0)
    print('Sample start: {}'.format(start_time))
    print()

    # Create the blob client, for use in obtaining references to
    # blob storage containers and uploading files to containers.
    blob_client = azureblob.BlockBlobService(
        account_name=_STORAGE_ACCOUNT_NAME,
        account_key=_STORAGE_ACCOUNT_KEY)

    # Use the blob client to create the containers in Azure Storage if they
    # don't yet exist.
    app_container_name = 'application'
    input_container_name = 'input'
    output_container_name = 'output'
    blob_client.create_container(app_container_name, fail_on_exist=False)
    blob_client.create_container(input_container_name, fail_on_exist=False)
    blob_client.create_container(output_container_name, fail_on_exist=False)

    # Obtain a shared access signature that provides write access to the output
    # container to which the tasks will upload their output.
    output_container_sas_token = common.helpers. \
        create_container_and_create_sas(blob_client,
                                        output_container_name,
                                        azureblob.BlobPermissions.WRITE,
                                        expiry=None,
                                        timeout=120)

    # Paths to the pool starttask script.  This script will be executed on all
    # compute nodes in pool
    pool_starttask_file_paths = [os.path.realpath(
        './article_samples/mpi/data/{}/{}/nodeprep-cmd'.format(_OS_NAME,
                                                               _APP_NAME))]
    # Upload the application scripts/files to Azure Storage.
    pool_starttask_files = [
        common.helpers.upload_file_to_container(
            blob_client, app_container_name, file_path, timeout=120)
        for file_path in pool_starttask_file_paths]

    # Specify the commands for the pool's start task.  The start task is run
    # on each node as it joins the pool, and when it's rebooted or re-imaged.
    # We use the start task to prep the node for running our task script.
    start_task_commands = ['nodeprep-cmd']
    # start_task_commands = ['hostname']
    start_task_cmdline = \
        common.helpers.wrap_commands_in_shell(_OS_NAME, start_task_commands)

    # The collection of scripts/data files that are to be used/processed by
    # the task (used/processed by primary in a multiinstance task).
    input_file_paths = [os.path.realpath(
        './article_samples/mpi/data/{}/{}/application-cmd'.format(_OS_NAME,
                                                                  _APP_NAME)),
                        os.path.realpath(
        './article_samples/mpi/azure_storage_helper.py')]
    # Upload the script/data files to Azure Storage
    input_files = [
        common.helpers.upload_file_to_container(
            blob_client, input_container_name, file_path, timeout=120)
        for file_path in input_file_paths]

    # Main application command to execute multiinstance task on a group of
    # nodes, eg. MPI.
    true_application_cmdline = 'sudo -u _azbatch ' \
                               '$AZ_BATCH_TASK_WORKING_DIR/application-cmd ' \
                               '{} {} {} $AZ_BATCH_TASK_SHARED_DIR ' \
                               '$AZ_BATCH_HOST_LIST'. \
        format(_NUM_INSTANCES, _NUM_PROCESSES_PER_VM, _NUM_ITERATIONS_OPENFOAM)
    # On each node participating in a multiinstance task, first coordination
    # command runs on all nodes and then primary executes application command.
    application_cmdline = [
        true_application_cmdline,
        # Command to upload results to Azure storage
        'python $AZ_BATCH_TASK_WORKING_DIR/azure_storage_helper.py '
        '--filepath {} --blobname {} --storageaccount {} '
        '--storagecontainer {} --sastoken "{}"'.format(
            _TASK_OUTPUT_FILE_PATH_ON_VM,
            _TASK_OUTPUT_BLOB_NAME,
            _STORAGE_ACCOUNT_NAME,
            output_container_name,
            output_container_sas_token)]

    # The collection of common scripts/data files that are to be
    # used/processed by all subtasks (including primary) in a
    # multi-instance task.
    common_file_paths = [os.path.realpath(
        './article_samples/mpi/data/{}/{}/coordination-cmd'.format(_OS_NAME,
                                                                   _APP_NAME)),
                         os.path.realpath(
        './article_samples/mpi/data/{}/{}/genimages.py'.format(_OS_NAME,
                                                               _APP_NAME))]
    # Upload the common script/data files to Azure Storage
    common_files = [
        common.helpers.upload_file_to_container(
            blob_client, input_container_name, file_path, timeout=120)
        for file_path in common_file_paths]

    # Command to run on all subtasks including primary before starting
    # application command on primary.
    coordination_cmdline = [
        # This creates/sets up the NFS share and downloads the input data
        # files, etc
        '$AZ_BATCH_TASK_SHARED_DIR/coordination-cmd']

    # Create a Batch service client.  We'll now be interacting with the Batch
    # service in addition to Storage
    credentials = batchauth.SharedKeyCredentials(_BATCH_ACCOUNT_NAME,
                                                 _BATCH_ACCOUNT_KEY)
    batch_client = batch.BatchServiceClient(credentials,
                                            base_url=_BATCH_ACCOUNT_URL)

    # Create the pool that will contain the compute nodes that will execute the
    # tasks. The resource files we pass in are used for configuring the pool's
    # start task, which is executed each time a node first joins the pool (or
    # is rebooted or re-imaged).
    multi_task_helpers.create_pool_and_wait_for_vms(
        batch_client, _POOL_ID, _NODE_OS_PUBLISHER, _NODE_OS_OFFER,
        _NODE_OS_SKU, _POOL_VM_SIZE, _POOL_NODE_COUNT, start_task_cmdline,
        pool_starttask_files, run_elevated=True)

    # Create the job that will run the tasks.
    common.helpers.create_job(batch_client, _JOB_ID, _POOL_ID)

    # Add the tasks to the job.  We need to supply a container shared access
    # signature (SAS) token for the tasks so that they can upload their output
    # to Azure Storage.
    multi_task_helpers.\
        add_task(batch_client, _JOB_ID, _TASK_ID,
                 common.helpers.wrap_commands_in_shell(_OS_NAME,
                                                       application_cmdline),
                 input_files, True, _NUM_INSTANCES,
                 common.helpers.wrap_commands_in_shell(_OS_NAME,
                                                       coordination_cmdline),
                 common_files)

    # Pause execution until task (and all subtasks for a multiinstance task)
    # reach Completed state.
    multi_task_helpers.\
        wait_for_tasks_to_complete(batch_client, _JOB_ID,
                                   datetime.timedelta(minutes=120))

    print("Success! Task reached the 'Completed' state within the "
          "specified timeout period.")

    # Print out some timing info
    end_time = datetime.datetime.now().replace(microsecond=0)
    print()
    print('Sample end: {}'.format(end_time))
    print('Elapsed time: {}'.format(end_time - start_time))
    print()

    # Download the task output files from the output Storage container to a
    # local directory
    if common.helpers.query_yes_no(
            'Download results ?') == 'yes':
        common.helpers.download_blob_from_container(
            blob_client,
            output_container_name,
            _TASK_OUTPUT_BLOB_NAME,
            os.path.expanduser('~'))

    # Clean up storage resources
    if common.helpers.query_yes_no(
            'Delete containers?') == 'yes':
        print('Deleting containers...')
        blob_client.delete_container(app_container_name)
        blob_client.delete_container(input_container_name)
        blob_client.delete_container(output_container_name)

    # Clean up Batch resources (if the user so chooses).
    if common.helpers.query_yes_no(
            'Delete job?') == 'yes':
        batch_client.job.delete(_JOB_ID)

    if common.helpers.query_yes_no(
            'Delete pool?') == 'yes':
        batch_client.pool.delete(_POOL_ID)

    print()
    input('Press ENTER to exit...')
