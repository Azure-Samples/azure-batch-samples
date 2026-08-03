from __future__ import print_function
import datetime
import os
import sys
import time

import config

from azure.core.exceptions import HttpResponseError, ResourceExistsError
from azure.identity import DefaultAzureCredential
from azure.storage.blob import (
    BlobServiceClient,
    BlobSasPermissions,
    ContainerSasPermissions,
    generate_blob_sas,
    generate_container_sas,
)
from azure.batch import BatchClient, models

sys.path.append('.')
sys.path.append('..')

# Update the Batch and Storage account values in config.py with values
# unique to your accounts. Authentication uses Microsoft Entra ID through
# DefaultAzureCredential, so account keys are no longer required. Sign in
# with `az login` and make sure your identity has the required roles on
# both accounts (see the README).


def query_yes_no(question, default="yes"):
    """
    Prompts the user for yes/no input, displaying the specified question text.

    :param str question: The text of the prompt for input.
    :param str default: The default if the user hits <ENTER>. Acceptable
    values are 'yes', 'no', and None.
    :rtype: str
    :return: 'yes' or 'no'
    """
    valid = {'y': 'yes', 'n': 'no'}
    if default is None:
        prompt = ' [y/n] '
    elif default == 'yes':
        prompt = ' [Y/n] '
    elif default == 'no':
        prompt = ' [y/N] '
    else:
        raise ValueError("Invalid default answer: '{}'".format(default))

    while 1:
        choice = input(question + prompt).lower()
        if default and not choice:
            return default
        try:
            return valid[choice[0]]
        except (KeyError, IndexError):
            print("Please respond with 'yes' or 'no' (or 'y' or 'n').\n")


def print_batch_exception(batch_exception):
    """
    Prints the contents of the specified Batch exception.

    :param batch_exception:
    """
    print('-------------------------------------------')
    print('Exception encountered:')
    error = getattr(batch_exception, 'error', None)
    if error is not None and getattr(error, 'message', None):
        print(error.message)
        for detail in (getattr(error, 'values', None) or []):
            print('{}:\t{}'.format(detail.key, detail.value))
    else:
        print(batch_exception)
    print('-------------------------------------------')


def upload_file_to_container(blob_service_client, user_delegation_key,
                             sas_expiry, container_name, file_path):
    """
    Uploads a local file to an Azure Blob storage container.

    :param blob_service_client: A blob service client.
    :type blob_service_client: `azure.storage.blob.BlobServiceClient`
    :param user_delegation_key: Key used to sign the SAS token.
    :param sas_expiry: The expiry time for the generated SAS token.
    :param str container_name: The name of the Azure Blob storage container.
    :param str file_path: The local path to the file.
    :rtype: `azure.batch.models.ResourceFile`
    :return: A ResourceFile initialized with a SAS URL appropriate for Batch
    tasks.
    """
    blob_name = os.path.basename(file_path)
    blob_client = blob_service_client.get_blob_client(
        container_name, blob_name)

    print('Uploading file {} to container [{}]...'.format(
        file_path, container_name))

    with open(file_path, "rb") as data:
        blob_client.upload_blob(data, overwrite=True)

    # Generate a read-only SAS token for the blob, signed with the user
    # delegation key (no storage account key required).
    sas_token = generate_blob_sas(
        account_name=config._STORAGE_ACCOUNT_NAME,
        container_name=container_name,
        blob_name=blob_name,
        user_delegation_key=user_delegation_key,
        permission=BlobSasPermissions(read=True),
        expiry=sas_expiry)

    sas_url = "{}?{}".format(blob_client.url, sas_token)

    return models.ResourceFile(http_url=sas_url, file_path=blob_name)


def get_container_sas_url(blob_service_client, user_delegation_key,
                          sas_expiry, container_name):
    """
    Obtains a shared access signature URL that provides write access to the
    output container to which the tasks will upload their output.

    :param blob_service_client: A blob service client.
    :type blob_service_client: `azure.storage.blob.BlobServiceClient`
    :param user_delegation_key: Key used to sign the SAS token.
    :param sas_expiry: The expiry time for the generated SAS token.
    :param str container_name: The name of the Azure Blob storage container.
    :rtype: str
    :return: A SAS URL granting write access to the specified container.
    """
    # Generate a write SAS token for the container, signed with the user
    # delegation key.
    sas_token = generate_container_sas(
        account_name=config._STORAGE_ACCOUNT_NAME,
        container_name=container_name,
        user_delegation_key=user_delegation_key,
        permission=ContainerSasPermissions(
            write=True, create=True, list=True),
        expiry=sas_expiry)

    # Construct the SAS URL for the container.
    container_sas_url = "https://{}.blob.core.windows.net/{}?{}".format(
        config._STORAGE_ACCOUNT_NAME, container_name, sas_token)

    return container_sas_url


def create_pool(batch_service_client, pool_id):
    """
    Creates a pool of compute nodes with the specified OS settings.

    :param batch_service_client: A Batch service client.
    :type batch_service_client: `azure.batch.BatchClient`
    :param str pool_id: An ID for the new pool.
    """
    print('Creating pool [{}]...'.format(pool_id))

    # Create a new pool of Linux compute nodes using an Azure Virtual
    # Machines Marketplace image. For more information about creating pools
    # of Linux nodes, see:
    # https://learn.microsoft.com/azure/batch/batch-linux-nodes

    # The start task installs ffmpeg on each node from an available
    # repository, using an administrator user identity.

    new_pool = models.BatchPoolCreateOptions(
        id=pool_id,
        virtual_machine_configuration=models.VirtualMachineConfiguration(
            image_reference=models.BatchVmImageReference(
                publisher="canonical",
                offer="ubuntu-24_04-lts",
                sku="server-gen1",
                version="latest"
            ),
            node_agent_sku_id="batch.node.ubuntu 24.04"),
        vm_size=config._POOL_VM_SIZE,
        target_dedicated_nodes=config._DEDICATED_POOL_NODE_COUNT,
        target_low_priority_nodes=config._LOW_PRIORITY_POOL_NODE_COUNT,
        start_task=models.BatchStartTask(
            command_line=(
                "/bin/bash -c \"apt-get update && "
                "apt-get install -y ffmpeg\""),
            wait_for_success=True,
            user_identity=models.UserIdentity(
                auto_user=models.AutoUserSpecification(
                    scope=models.AutoUserScope.POOL,
                    elevation_level=models.ElevationLevel.ADMIN)),
        )
    )

    batch_service_client.create_pool(pool=new_pool)


def create_job(batch_service_client, job_id, pool_id):
    """
    Creates a job with the specified ID, associated with the specified pool.

    :param batch_service_client: A Batch service client.
    :type batch_service_client: `azure.batch.BatchClient`
    :param str job_id: The ID for the job.
    :param str pool_id: The ID for the pool.
    """
    print('Creating job [{}]...'.format(job_id))

    job = models.BatchJobCreateOptions(
        id=job_id,
        pool_info=models.BatchPoolInfo(pool_id=pool_id))

    batch_service_client.create_job(job=job)


def add_tasks(batch_service_client, job_id, input_files,
              output_container_sas_url):
    """
    Adds a task for each input file in the collection to the specified job.

    :param batch_service_client: A Batch service client.
    :type batch_service_client: `azure.batch.BatchClient`
    :param str job_id: The ID of the job to which to add the tasks.
    :param list input_files: A collection of input files. One task will be
     created for each input file.
    :param str output_container_sas_url: A SAS URL granting write access to
    the specified Azure Blob storage container.
    """

    print('Adding {} tasks to job [{}]...'.format(len(input_files), job_id))

    tasks = list()

    for idx, input_file in enumerate(input_files):
        input_file_path = input_file.file_path
        output_file_path = "".join(
            input_file_path.split('.')[:-1]) + '.mp3'
        command = "/bin/bash -c \"ffmpeg -i {} {} \"".format(
            input_file_path, output_file_path)
        output_file = models.OutputFile(
            file_pattern=output_file_path,
            destination=models.OutputFileDestination(
                container=models.OutputFileBlobContainerDestination(
                    container_url=output_container_sas_url)),
            upload_options=models.OutputFileUploadConfiguration(
                upload_condition=(
                    models.OutputFileUploadCondition.TASK_SUCCESS)))
        tasks.append(models.BatchTaskCreateOptions(
            id='Task{}'.format(idx),
            command_line=command,
            resource_files=[input_file],
            output_files=[output_file]))

    batch_service_client.create_tasks(job_id=job_id, task_collection=tasks)


def wait_for_tasks_to_complete(batch_service_client, job_id, timeout):
    """
    Returns when all tasks in the specified job reach the Completed state.

    :param batch_service_client: A Batch service client.
    :type batch_service_client: `azure.batch.BatchClient`
    :param str job_id: The id of the job whose tasks should be monitored.
    :param timedelta timeout: The duration to wait for task completion. If
    all tasks in the specified job do not reach Completed state within this
    time period, an exception will be raised.
    """
    timeout_expiration = datetime.datetime.now() + timeout

    print("Monitoring all tasks for 'Completed' state, timeout in {}..."
          .format(timeout), end='')

    while datetime.datetime.now() < timeout_expiration:
        print('.', end='')
        sys.stdout.flush()
        tasks = batch_service_client.list_tasks(job_id=job_id)

        incomplete_tasks = [
            task for task in tasks
            if task.state != models.BatchTaskState.COMPLETED]
        if not incomplete_tasks:
            print()
            return True
        else:
            time.sleep(5)

    print()
    raise RuntimeError("ERROR: Tasks did not reach 'Completed' state within "
                       "timeout period of " + str(timeout))


if __name__ == '__main__':

    start_time = datetime.datetime.now().replace(microsecond=0)
    print('Sample start: {}'.format(start_time))
    print()

    # Authenticate with Microsoft Entra ID. DefaultAzureCredential tries
    # multiple credential sources in order (environment variables, managed
    # identity, Azure CLI sign-in, and so on), so the same code works
    # locally and in production without storing account keys.
    credential = DefaultAzureCredential()

    # Create the blob service client, for use in obtaining references to
    # blob storage containers and uploading files to containers.
    blob_service_client = BlobServiceClient(
        account_url="https://{}.blob.core.windows.net/".format(
            config._STORAGE_ACCOUNT_NAME),
        credential=credential)

    # Use the blob service client to create the containers in Azure Storage
    # if they don't yet exist.
    input_container_name = 'input'
    output_container_name = 'output'
    for container_name in (input_container_name, output_container_name):
        try:
            blob_service_client.create_container(container_name)
        except ResourceExistsError:
            pass
        print('Container [{}] created.'.format(container_name))

    # Request a user delegation key from the Blob service. The key is signed
    # with the app's Microsoft Entra credentials and is used to sign the SAS
    # tokens for the input and output containers (no storage account key
    # required). A user delegation key is valid for a maximum of seven days.
    start = datetime.datetime.now(datetime.timezone.utc)
    expiry = start + datetime.timedelta(hours=4)
    user_delegation_key = blob_service_client.get_user_delegation_key(
        key_start_time=start, key_expiry_time=expiry)

    # Sign the SAS tokens with the same expiry as the user delegation key.
    sas_expiry = expiry

    # Create a list of all MP4 files in the InputFiles directory.
    input_file_paths = []

    input_files_dir = os.path.join(sys.path[0], 'InputFiles')
    for folder, subs, files in os.walk(input_files_dir):
        for filename in files:
            if filename.endswith(".mp4"):
                input_file_paths.append(os.path.abspath(
                    os.path.join(folder, filename)))

    # Upload the input files. This is the collection of files that are to be
    # processed by the tasks.
    input_files = [
        upload_file_to_container(blob_service_client, user_delegation_key,
                                 sas_expiry, input_container_name, file_path)
        for file_path in input_file_paths]

    # Obtain a shared access signature URL that provides write access to the
    # output container to which the tasks will upload their output.
    output_container_sas_url = get_container_sas_url(
        blob_service_client,
        user_delegation_key,
        sas_expiry,
        output_container_name)

    # Create a Batch client. We'll now be interacting with the Batch service
    # in addition to Storage. The Batch client uses the same
    # DefaultAzureCredential to authenticate through Microsoft Entra ID.
    batch_client = BatchClient(
        endpoint=config._BATCH_ACCOUNT_URL,
        credential=credential)

    try:
        # Create the pool that will contain the compute nodes that will
        # execute the tasks.
        create_pool(batch_client, config._POOL_ID)

        # Create the job that will run the tasks.
        create_job(batch_client, config._JOB_ID, config._POOL_ID)

        # Add the tasks to the job. Pass the input files and a SAS URL
        # to the storage container for output files.
        add_tasks(batch_client, config._JOB_ID,
                  input_files, output_container_sas_url)

        # Pause execution until tasks reach Completed state.
        wait_for_tasks_to_complete(batch_client,
                                   config._JOB_ID,
                                   datetime.timedelta(minutes=30))

        print("  Success! All tasks reached the 'Completed' state within "
              "the specified timeout period.")

    except HttpResponseError as err:
        print_batch_exception(err)
        raise

    # Delete input container in storage
    print('Deleting container [{}]...'.format(input_container_name))
    blob_service_client.delete_container(input_container_name)

    # Print out some timing info
    end_time = datetime.datetime.now().replace(microsecond=0)
    print()
    print('Sample end: {}'.format(end_time))
    print('Elapsed time: {}'.format(end_time - start_time))
    print()

    # Clean up Batch resources (if the user so chooses).
    if query_yes_no('Delete job?') == 'yes':
        batch_client.begin_delete_job(job_id=config._JOB_ID)

    if query_yes_no('Delete pool?') == 'yes':
        batch_client.begin_delete_pool(pool_id=config._POOL_ID)

    print()
    input('Press ENTER to exit...')
