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
import io
import time

import azure.storage.blob as azureblob
import batchserviceclient.models as batchmodels


_STANDARD_OUT_FILE_NAME = 'stdout.txt'
_STANDARD_ERROR_FILE_NAME = 'stderr.txt'
_SAMPLES_CONFIG_FILE_NAME = 'configuration.cfg'


class TimeoutError(Exception):
    """An error which can occur if a timeout has expired.
    """
    def __init__(self, message):
        self.message = message


def decode_string(string, encoding=None):
    """Decode a string with specified encoding

    :type string: str or bytes
    :param string: string to decode
    :param str encoding: encoding of string to decode
    :rtype: str
    :return: decoded string
    """
    if isinstance(string, str):
        return string
    if encoding is None:
        encoding = 'utf-8'
    if isinstance(string, bytes):
        return string.decode(encoding)
    raise ValueError('invalid string type: {}'.format(type(string)))


def select_latest_verified_vm_image_with_node_agent_sku(
        batch_client, publisher, offer, sku_starts_with):
    """Select the latest verified image that Azure Batch supports given
    a publisher, offer and sku (starts with filter).

    :param batch_client: The batch client to use.
    :type batch_client: `batchserviceclient.BatchServiceClient`
    :param str publisher: vm image publisher
    :param str offer: vm image offer
    :param str sku_starts_with: vm sku starts with filter
    :rtype: tuple
    :return: (node agent sku id to use, vm image ref to use)
    """
    # get verified vm image list and node agent sku ids from service
    node_agent_skus = batch_client.account.list_node_agent_skus()
    # pick the latest supported sku
    skus_to_use = [
        (sku, image_ref) for sku in node_agent_skus for image_ref in sorted(
            sku.verified_image_references, key=lambda item: item.sku)
        if image_ref.publisher.lower() == publisher.lower() and
        image_ref.offer.lower() == offer.lower() and
        image_ref.sku.startswith(sku_starts_with)
    ]
    sku_to_use, image_ref_to_use = skus_to_use[-1]
    return (sku_to_use.id, image_ref_to_use)


def wait_for_tasks_to_complete(batch_client, job_id, timeout):
    """Waits for all the tasks in a particular job to complete.

    :param batch_client: The batch client to use.
    :type batch_client: `batchserviceclient.BatchServiceClient`
    :param str job_id: The id of the job to monitor.
    :param timeout: The maximum amount of time to wait.
    :type timeout: `datetime.timedelta`
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


def print_task_output(batch_client, job_id, task_ids, encoding=None):
    """Prints the stdout and stderr for each task specified.

    :param batch_client: The batch client to use.
    :type batch_client: `batchserviceclient.BatchServiceClient`
    :param str job_id: The id of the job to monitor.
    :param task_ids: The collection of tasks to print the output for.
    :type task_ids: `list`
    :param str encoding: The encoding to use when downloading the file.
    """
    for task_id in task_ids:
        file_text = read_task_file_as_string(
            batch_client,
            job_id,
            task_id,
            _STANDARD_OUT_FILE_NAME,
            encoding)
        print("{} content for task {}: ".format(
            _STANDARD_OUT_FILE_NAME,
            task_id))
        print(file_text)

        file_text = read_task_file_as_string(
            batch_client,
            job_id,
            task_id,
            _STANDARD_ERROR_FILE_NAME,
            encoding)
        print("{} content for task {}: ".format(
            _STANDARD_ERROR_FILE_NAME,
            task_id))
        print(file_text)


def print_configuration(config):
    """Prints the configuration being used as a dictionary

    :param config: The configuration.
    :type config: `configparser.ConfigParser`
    """
    configuration_dict = {s: dict(config.items(s)) for s in
                          config.sections() + ['DEFAULT']}

    print("Configuration is:")
    print(configuration_dict)


def _read_stream_as_string(stream, encoding):
    """Read stream as string

    :param stream: input stream generator
    :param str encoding: The encoding of the file. The default is utf-8.
    :return: The file content.
    :rtype: str
    """
    output = io.BytesIO()
    try:
        for data in stream:
            output.write(data)
        if encoding is None:
            encoding = 'utf-8'
        return output.getvalue().decode(encoding)
    finally:
        output.close()
    raise RuntimeError('could not write data to stream or decode bytes')


def read_task_file_as_string(
        batch_client, job_id, task_id, file_name, encoding=None):
    """Reads the specified file as a string.

    :param batch_client: The batch client to use.
    :type batch_client: `batchserviceclient.BatchServiceClient`
    :param str job_id: The id of the job.
    :param str task_id: The id of the task.
    :param str file_name: The name of the file to read.
    :param str encoding: The encoding of the file. The default is utf-8.
    :return: The file content.
    :rtype: str
    """
    stream = batch_client.file.get_from_task(job_id, task_id, file_name)
    return _read_stream_as_string(stream, encoding)


def read_compute_node_file_as_string(
        batch_client, pool_id, node_id, file_name, encoding=None):
    """Reads the specified file as a string.

    :param batch_client: The batch client to use.
    :type batch_client: `batchserviceclient.BatchServiceClient`
    :param str pool_id: The id of the pool.
    :param str node_id: The id of the node.
    :param str file_name: The name of the file to read.
    :param str encoding: The encoding of the file.  The default is utf-8
    :return: The file content.
    :rtype: str
    """
    stream = batch_client.file.get_from_compute_node(
        pool_id, node_id, file_name)
    return _read_stream_as_string(stream, encoding)


def create_pool_if_not_exist(batch_client, pool):
    """Creates the specified pool if it doesn't already exist

    :param batch_client: The batch client to use.
    :type batch_client: `batchserviceclient.BatchServiceClient`
    :param pool: The pool to create.
    :type pool: `batchserviceclient.models.CloudPool`
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


def wait_for_all_nodes_state(batch_client, pool, node_state):
    """Waits for all nodes in pool to reach any specified state in set

    :param batch_client: The batch client to use.
    :type batch_client: `batchserviceclient.BatchServiceClient`
    :param pool: The pool containing the node.
    :type pool: `batchserviceclient.models.CloudPool`
    :param set node_state: node states to wait for
    :rtype: list
    :return: list of `batchserviceclient.models.ComputeNode`
    """
    print('waiting for all nodes in pool {} to reach one of: {!r}'.format(
        pool.id, node_state))
    i = 0
    while True:
        # refresh pool to ensure that there is no resize error
        pool = batch_client.pool.get(pool.id)
        if pool.resize_error is not None:
            raise RuntimeError(
                'resize error encountered for pool {}: {!r}'.format(
                    pool.id, pool.resize_error))
        nodes = list(batch_client.compute_node.list(pool.id))
        if (len(nodes) >= pool.target_dedicated and
                all(node.state in node_state for node in nodes)):
            return nodes
        i += 1
        if i % 3 == 0:
            print('waiting for {} nodes to reach desired state...'.format(
                pool.target_dedicated))
        time.sleep(10)


def create_sas_token(
        block_blob_client, container_name, blob_name, permission, expiry=None):
    """Create a blob sas token

    :param block_blob_client: The storage block blob client to use.
    :type block_blob_client: `azure.storage.blob.BlockBlobService`
    :param str container_name: The name of the container to upload the blob to.
    :param str blob_name: The name of the blob to upload the local file to.
    :param expiry: The SAS expiry time.
    :type expiry: `datetime.datetime`
    :return: A SAS token
    :rtype: str
    """
    if expiry is None:
        expiry = datetime.datetime.utcnow() + datetime.timedelta(minutes=30)
    return block_blob_client.generate_blob_shared_access_signature(
        container_name, blob_name, permission=permission, expiry=expiry)


def upload_blob_and_create_sas(
        block_blob_client, container_name, blob_name, file_name, expiry):
    """Uploads a file from local disk to Azure Storage and creates
    a SAS for it.

    :param block_blob_client: The storage block blob client to use.
    :type block_blob_client: `azure.storage.blob.BlockBlobService`
    :param str container_name: The name of the container to upload the blob to.
    :param str blob_name: The name of the blob to upload the local file to.
    :param str file_name: The name of the local file to upload.
    :param expiry: The SAS expiry time.
    :type expiry: `datetime.datetime`
    :return: A SAS URL to the blob with the specified expiry time.
    :rtype: str
    """
    block_blob_client.create_container(
        container_name,
        fail_on_exist=False)

    block_blob_client.create_blob_from_path(
        container_name,
        blob_name,
        file_name)

    sas_token = create_sas_token(
        block_blob_client,
        container_name,
        blob_name,
        permission=azureblob.BlobPermissions.READ,
        expiry=expiry)

    sas_url = block_blob_client.make_blob_url(
        container_name,
        blob_name,
        sas_token=sas_token)

    return sas_url


def generate_unique_resource_name(resource_prefix):
    """Generates a unique resource name by appending a time
    string after the specified prefix.

    :param str resource_prefix: The resource prefix to use.
    :return: A string with the format "resource_prefix-<time>".
    :rtype: str
    """
    return resource_prefix + "-" + \
        datetime.datetime.utcnow().strftime("%Y%m%d-%H%M%S")


def wrap_commands_in_shell(ostype, commands):
    """Wrap commands in a shell

    :param list commands: list of commands to wrap
    :param str ostype: OS type, linux or windows
    :rtype: str
    :return: a shell wrapping commands
    """
    if ostype.lower() == 'linux':
        return '/bin/bash -c \'set -e; set -o pipefail; {}; wait\''.format(
            ';'.join(commands))
    elif ostype.lower() == 'windows':
        return 'cmd.exe /c "{}"'.format('&'.join(commands))
    else:
        raise ValueError('unknown ostype: {}'.format(ostype))
