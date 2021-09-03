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
from configparser import ConfigParser
import datetime
import io
import os
import time
from typing import Any, List, Set, Tuple, Union

from azure.core.exceptions import ResourceExistsError

from azure.storage.blob import (
    BlobServiceClient,
    ContainerSasPermissions,
    BlobSasPermissions,
    generate_container_sas,
    generate_blob_sas
)
from azure.batch import BatchServiceClient
import azure.batch.models as batchmodels


STANDARD_OUT_FILE_NAME = 'stdout.txt'
STANDARD_ERROR_FILE_NAME = 'stderr.txt'
SAMPLES_CONFIG_FILE_NAME = 'configuration.cfg'


class TimeoutError(Exception):
    """An error which can occur if a timeout has expired.
    """

    def __init__(self, message):
        self.message = message


def decode_string(string: Union[str, bytes], encoding: str = 'utf-8') -> str:
    """Decode a string with specified encoding

    :param string: string to decode
    :param encoding: encoding of string to decode
    :return: decoded string
    """
    if isinstance(string, str):
        return string
    if isinstance(string, bytes):
        return string.decode(encoding)
    raise ValueError('invalid string type: {}'.format(type(string)))


def select_latest_verified_vm_image_with_node_agent_sku(
    batch_client: BatchServiceClient,
    publisher: str,
    offer: str,
    sku_starts_with: str
) -> Tuple[str, batchmodels.ImageReference]:
    """Select the latest verified image that Azure Batch supports given
    a publisher, offer and sku (starts with filter).

    :param batch_client: The batch client to use.
    :param publisher: vm image publisher
    :param offer: vm image offer
    :param sku_starts_with: vm sku starts with filter
    :return: (node agent sku id to use, vm image ref to use)
    """
    # get verified vm image list and node agent sku ids from service
    options = batchmodels.AccountListSupportedImagesOptions(
        filter="verificationType eq 'verified'")
    images = batch_client.account.list_supported_images(
        account_list_supported_images_options=options)

    # pick the latest supported sku
    skus_to_use = [
        (image.node_agent_sku_id, image.image_reference) for image in images
        if image.image_reference.publisher.lower() == publisher.lower() and
        image.image_reference.offer.lower() == offer.lower() and
        image.image_reference.sku.startswith(sku_starts_with)
    ]

    # pick first
    agent_sku_id, image_ref_to_use = skus_to_use[0]
    return (agent_sku_id, image_ref_to_use)


def wait_for_tasks_to_complete(
    batch_client: BatchServiceClient,
    job_id: str,
    timeout: datetime.timedelta
):
    """Waits for all the tasks in a particular job to complete.

    :param batch_client: The batch client to use.
    :param job_id: The id of the job to monitor.
    :param timeout: The maximum amount of time to wait.
    """
    time_to_timeout_at = datetime.datetime.now() + timeout

    while datetime.datetime.now() < time_to_timeout_at:
        print("Checking if all tasks are complete...")
        tasks = batch_client.task.list(job_id)

        incomplete_tasks = [task for task in tasks if
                            task.state != batchmodels.TaskState.completed]
        if not incomplete_tasks:
            return
        time.sleep(5)

    raise TimeoutError("Timed out waiting for tasks to complete")


def print_task_output(
    batch_client: BatchServiceClient,
    job_id: str,
    task_ids: List[str],
    encoding: str = None
):
    """Prints the stdout and stderr for each task specified.

    :param batch_client: The batch client to use.
    :param job_id: The id of the job to monitor.
    :param task_ids: The collection of tasks to print the output for.
    :param encoding: The encoding to use when downloading the file.
    """
    for task_id in task_ids:
        file_text = read_task_file_as_string(
            batch_client,
            job_id,
            task_id,
            STANDARD_OUT_FILE_NAME,
            encoding)
        print("{} content for task {}: ".format(
            STANDARD_OUT_FILE_NAME,
            task_id))
        print(file_text)

        file_text = read_task_file_as_string(
            batch_client,
            job_id,
            task_id,
            STANDARD_ERROR_FILE_NAME,
            encoding)
        print("{} content for task {}: ".format(
            STANDARD_ERROR_FILE_NAME,
            task_id))
        print(file_text)


def print_configuration(config: ConfigParser):
    """Prints the configuration being used as a dictionary

    :param config: The configuration.
    """
    configuration_dict = {s: dict(config.items(s)) for s in
                          config.sections() + ['DEFAULT']}

    print("Configuration is:")
    print(configuration_dict)


def _read_stream_as_string(stream, encoding: str = None) -> str:
    """Read stream as string

    :param stream: input stream generator
    :param encoding: The encoding of the file. The default is utf-8.
    :return: The file content.
    """
    if encoding is None:
        encoding = 'utf-8'

    output = io.BytesIO()
    try:
        for data in stream:
            output.write(data)
        return output.getvalue().decode(encoding)
    except Exception:
        raise RuntimeError('Could not write data to stream or decode bytes')
    finally:
        output.close()


def read_task_file_as_string(
    batch_client: BatchServiceClient,
    job_id: str,
    task_id: str,
    file_name: str,
    encoding: str = None
) -> str:
    """Reads the specified file as a string.

    :param batch_client: The batch client to use.
    :param job_id: The id of the job.
    :param task_id: The id of the task.
    :param file_name: The name of the file to read.
    :param encoding: The encoding of the file. The default is utf-8.
    :return: The file content.
    """
    stream = batch_client.file.get_from_task(job_id, task_id, file_name)
    return _read_stream_as_string(stream, encoding)


def read_compute_node_file_as_string(
    batch_client: BatchServiceClient,
    pool_id: str,
    node_id: str,
    file_name: str,
    encoding: str = None
) -> str:
    """Reads the specified file as a string.

    :param batch_client: The batch client to use.
    :param pool_id: The id of the pool.
    :param node_id: The id of the node.
    :param file_name: The name of the file to read.
    :param encoding: The encoding of the file.  The default is utf-8
    :return: The file content.
    """
    stream = batch_client.file.get_from_compute_node(
        pool_id, node_id, file_name)
    return _read_stream_as_string(stream, encoding)


def create_pool_if_not_exist(
    batch_client: BatchServiceClient,
    pool: batchmodels.PoolAddParameter
):
    """Creates the specified pool if it doesn't already exist

    :param batch_client: The batch client to use.
    :param pool: The pool to create.
    """
    try:
        print("Attempting to create pool:", pool.id)
        batch_client.pool.add(pool)
        print("Created pool:", pool.id)
    except batchmodels.BatchErrorException as e:
        if e.error.code != "PoolExists":
            raise
        else:
            print("Pool {!r} already exists".format(pool.id))


def create_job(
    batch_service_client: BatchServiceClient,
    job_id: str,
    pool_id: str
):
    """
    Creates a job with the specified ID, associated with the specified pool.

    :param batch_service_client: A Batch service client.
    :param job_id: The ID for the job.
    :param pool_id: The ID for the pool.
    """
    print('Creating job [{}]...'.format(job_id))

    job = batchmodels.JobAddParameter(
        id=job_id,
        pool_info=batchmodels.PoolInformation(pool_id=pool_id))

    try:
        batch_service_client.job.add(job)
    except batchmodels.BatchErrorException as err:
        print_batch_exception(err)
        if err.error.code != "JobExists":
            raise
        else:
            print("Job {!r} already exists".format(job_id))


def wait_for_all_nodes_state(
    batch_client: BatchServiceClient,
    pool_id: str,
    node_state: Set[batchmodels.ComputeNodeState]
) -> List[batchmodels.ComputeNode]:
    """Waits for all nodes in pool to reach any specified state in set

    :param batch_client: The batch client to use.
    :param pool: The pool containing the node.
    :param node_state: node states to wait for
    :return: list of compute nodes
    """
    print('waiting for all nodes in pool {} to reach one of: {!r}'.format(
        pool_id, node_state))
    i = 0
    while True:
        # refresh pool to ensure that there is no resize error
        pool = batch_client.pool.get(pool_id)  # type: Any

        if pool.resize_errors is not None:
            resize_errors = "\n".join([repr(e) for e in pool.resize_errors])
            raise RuntimeError(
                'resize error encountered for pool {}:\n{}'.format(
                    pool.id, resize_errors))
        nodes = list(batch_client.compute_node.list(pool.id))
        if (len(nodes) >= pool.target_dedicated_nodes and
                all(node.state in node_state for node in nodes)):
            return nodes
        i += 1
        if i % 3 == 0:
            print('waiting for {} nodes to reach desired state...'.format(
                pool.target_dedicated_nodes))
        time.sleep(10)


def create_container_and_create_sas(
    blob_service_client: BlobServiceClient,
    container_name: str,
    permission: ContainerSasPermissions,
    expiry: datetime.datetime = None,
    timeout: int = None
) -> str:
    """Create a blob sas token

    :param blob_service_client: The storage block blob client to use.
    :param container_name: The name of the container to upload the blob to.
    :param permission The permissions of the SAS token
    :param expiry: The SAS expiry time.
    :param timeout: timeout in minutes from now for expiry,
        will only be used if expiry is not specified
    :return: A SAS token
    """
    if expiry is None:
        if timeout is None:
            timeout = 30
        expiry = datetime.datetime.utcnow() + datetime.timedelta(
            minutes=timeout)

    try:
        blob_service_client.create_container(container_name)
    except ResourceExistsError:
        pass

    storage_account_name = blob_service_client.account_name
    if not storage_account_name:
        raise ValueError("Blob service client must have a valid account name")

    return generate_container_sas(
        account_name=storage_account_name,
        container_name=container_name, permission=permission, expiry=expiry)


def create_sas_token(
    blob_service_client: BlobServiceClient,
    container_name: str,
    blob_name: str,
    permission: BlobSasPermissions,
    expiry: datetime.datetime = None,
    timeout: int = None
) -> str:
    """Create a blob sas token

    :param blob_service_client: The storage block blob client to use.
    :param container_name: The name of the container to upload the blob to.
    :param blob_name: The name of the blob to upload the local file to.
    :param permission The permissions of the SAS token
    :param expiry: The SAS expiry time.
    :param timeout: timeout in minutes from now for expiry,
        will only be used if expiry is not specified
    :return: A SAS token
    """
    if expiry is None:
        if timeout is None:
            timeout = 30
        expiry = datetime.datetime.utcnow() + datetime.timedelta(
            minutes=timeout)

    if not blob_service_client.account_name:
        raise ValueError("Blob service client must have a valid account name")
    if not blob_service_client.credential:
        raise ValueError("Blob service client must have a valid credential")

    return generate_blob_sas(
        account_name=blob_service_client.account_name,
        account_key=blob_service_client.credential.account_key,
        container_name=container_name,
        blob_name=blob_name,
        permission=permission,
        expiry=expiry)


def build_sas_url(
    blob_service_client: BlobServiceClient,
    container_name: str,
    blob_name: str,
    sas_token: str
) -> str:
    """Builds a signed URL for a blob

    :param blob_service_client: The blob service client
    :param container_name: The name of the blob container
    :param blob_name: The name of the blob
    :param sas_token: An SAS token
    """
    base_url = str(blob_service_client.url)
    if not base_url.endswith("/"):
        base_url += "/"

    return "%s%s/%s?%s" % (
        base_url,
        container_name,
        blob_name,
        sas_token
    )


def upload_blob_and_create_sas(
    blob_service_client: BlobServiceClient,
    container_name: str,
    blob_name: str,
    file_name: str,
    expiry: datetime.datetime = None,
    timeout: int = None
) -> str:
    """Uploads a file from local disk to Azure Storage and creates
    a SAS for it.

    :param blob_service_client: The storage block blob client to use.
    :param container_name: The name of the container to upload the blob to.
    :param blob_name: The name of the blob to upload the local file to.
    :param file_name: The name of the local file to upload.
    :param expiry: The SAS expiry time.
    :param int timeout: timeout in minutes from now for expiry,
        will only be used if expiry is not specified
    :return: A SAS URL to the blob with the specified expiry time.
    """
    try:
        blob_service_client.create_container(container_name)
    except ResourceExistsError:
        pass

    blob_client = blob_service_client.get_blob_client(
        container=container_name, blob=blob_name)

    with open(file_name, "rb") as data:
        blob_client.upload_blob(data, overwrite=True)

    sas_token = create_sas_token(
        blob_service_client,
        container_name,
        blob_name,
        permission=BlobSasPermissions.from_string("r"),
        expiry=expiry,
        timeout=timeout)

    return build_sas_url(
        blob_service_client,
        container_name,
        blob_name,
        sas_token)


def upload_file_to_container(
    blob_service_client: BlobServiceClient,
    container_name: str,
    file_path: str,
    timeout: int
) -> batchmodels.ResourceFile:
    """
    Uploads a local file to an Azure Blob storage container.

    :param blob_service_client: A blob service client.
    :param container_name: The name of the Azure Blob storage container.
    :param file_path: The local path to the file.
    :param timeout: timeout in minutes from now for expiry,
        will only be used if expiry is not specified
    :return: A ResourceFile initialized with a SAS URL appropriate for Batch
        tasks.
    """
    blob_name = os.path.basename(file_path)
    print('Uploading file {} to container [{}]...'.format(
        file_path, container_name))
    sas_url = upload_blob_and_create_sas(
        blob_service_client, container_name, blob_name, file_path, expiry=None,
        timeout=timeout)
    return batchmodels.ResourceFile(
        file_path=blob_name, http_url=sas_url)


def download_blob_from_container(
    blob_service_client: BlobServiceClient,
    container_name: str,
    blob_name: str,
    directory_path: str
):
    """
    Downloads specified blob from the specified Azure Blob storage container.

    :param blob_service_client: A blob service client.
    :param container_name: The Azure Blob storage container from which to
        download file.
    :param blob_name: The name of blob to be downloaded
    :param directory_path: The local directory to which to download the file.
    """
    print('Downloading result file from container [{}]...'.format(
        container_name))

    destination_file_path = os.path.join(directory_path, blob_name)

    blob_client = blob_service_client.get_blob_client(
        container=container_name, blob=blob_name)

    blob = blob_client.download_blob()

    with open(destination_file_path, "wb") as destination_file:
        blob.download_to_stream(destination_file)

    print('  Downloaded blob [{}] from container [{}] to {}'.format(
        blob_name, container_name, destination_file_path))

    print('  Download complete!')


def generate_unique_resource_name(resource_prefix: str) -> str:
    """Generates a unique resource name by appending a time
    string after the specified prefix.

    :param resource_prefix: The resource prefix to use.
    :return: A string with the format "resource_prefix-<time>".
    """
    return resource_prefix + "-" + \
        datetime.datetime.utcnow().strftime("%Y%m%d-%H%M%S")


def query_yes_no(question: str, default: str = "yes") -> str:
    """
    Prompts the user for yes/no input, displaying the specified question text.

    :param question: The text of the prompt for input.
    :param default: The default if the user hits <ENTER>. Acceptable values
    are 'yes', 'no', and None.
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

    choice = default

    while 1:
        user_input = input(question + prompt).lower()
        if not user_input:
            break
        try:
            choice = valid[user_input[0]]
        except (KeyError, IndexError):
            print("Please respond with 'yes' or 'no' (or 'y' or 'n').\n")

    return choice


def print_batch_exception(batch_exception: batchmodels.BatchErrorException):
    """
    Prints the contents of the specified Batch exception.

    :param batch_exception:
    """
    print('-------------------------------------------')
    print('Exception encountered:')
    if (batch_exception.error and batch_exception.error.message and
            batch_exception.error.message.value):
        print(batch_exception.error.message.value)
        if batch_exception.error.values:
            print()
            for mesg in batch_exception.error.values:
                print('{}:\t{}'.format(mesg.key, mesg.value))
    print('-------------------------------------------')


def wrap_commands_in_shell(ostype: str, commands: List[str]) -> str:
    """Wrap commands in a shell

    :param ostype: OS type, linux or windows
    :param commands: list of commands to wrap
    :return: a shell wrapping commands
    """
    if ostype.lower() == 'linux':
        return '/bin/bash -c \'set -e; set -o pipefail; {}; wait\''.format(
            ';'.join(commands))
    elif ostype.lower() == 'windows':
        return 'cmd.exe /c "{}"'.format('&'.join(commands))
    else:
        raise ValueError('unknown ostype: {}'.format(ostype))


def wait_for_job_under_job_schedule(
    batch_client: BatchServiceClient,
    job_schedule_id: str,
    timeout: datetime.timedelta
):
    """Waits for a job to be created and returns a job id.

       :param batch_client: The batch client to use.
       :param job_schedule_id: The id of the job schedule to monitor.
       :param timeout: The maximum amount of time to wait.
    """
    time_to_timeout_at = datetime.datetime.now() + timeout

    while datetime.datetime.now() < time_to_timeout_at:
        cloud_job_schedule = batch_client.job_schedule.get(
            job_schedule_id=job_schedule_id)  # type: Any

        print("Checking if job exists...")
        job = cloud_job_schedule.execution_info.recent_job
        if job and job.id is not None:
            return job.id
        time.sleep(1)

    raise TimeoutError("Timed out waiting for tasks to complete")


def wait_for_job_schedule_to_complete(
    batch_client: BatchServiceClient,
    job_schedule_id: str,
    timeout_at: datetime.datetime
):
    """Waits for a job schedule to complete.

       :param batch_client: The batch client to use.
       :param job_schedule_id: The id of the job schedule to monitor.
       :param timeout: The maximum amount of time to wait.
    """
    while datetime.datetime.now() < timeout_at:
        cloud_job_schedule = batch_client.job_schedule.get(
            job_schedule_id=job_schedule_id)  # type: Any

        print("Checking if job schedule is complete...")
        state = cloud_job_schedule.state
        if state == batchmodels.JobScheduleState.completed:
            return
        time.sleep(10)
