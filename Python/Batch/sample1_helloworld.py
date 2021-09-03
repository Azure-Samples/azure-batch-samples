# sample1_helloworld.py Code Sample
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

import datetime
import os
from configparser import ConfigParser

from azure.batch import BatchServiceClient
from azure.batch.batch_auth import SharedKeyCredentials
import azure.batch.models as batchmodels

import common.helpers


def submit_job_and_add_task(
    batch_client: BatchServiceClient,
    job_id: str,
    vm_size: str,
    node_count: int
):
    """Submits a job to the Azure Batch service and adds a simple task.

    :param batch_client: The batch client to use.
    :param job_id: The id of the job to create.
    :param vm_size: The VM size to use.
    :param node_count: The number of dedicated nodes to start.
    """

    vm_config = batchmodels.VirtualMachineConfiguration(
        image_reference=batchmodels.ImageReference(
            publisher="canonical",
            offer="ubuntuserver",
            sku="18.04-lts"
        ),
        node_agent_sku_id="batch.node.ubuntu 18.04"
    )
    pool_info = batchmodels.PoolInformation(
        auto_pool_specification=batchmodels.AutoPoolSpecification(
            auto_pool_id_prefix="HelloWorld",
            pool=batchmodels.PoolSpecification(
                vm_size=vm_size,
                target_dedicated_nodes=node_count,
                virtual_machine_configuration=vm_config),
            keep_alive=False,
            pool_lifetime_option=batchmodels.PoolLifetimeOption.job))

    job = batchmodels.JobAddParameter(id=job_id, pool_info=pool_info)

    batch_client.job.add(job)

    task = batchmodels.TaskAddParameter(
        id="HelloWorld",
        command_line=common.helpers.wrap_commands_in_shell(
            'linux', ['echo Hello world from the Batch Hello world sample!'])
    )

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

    should_delete_job = sample_config.getboolean(
        'DEFAULT',
        'shoulddeletejob')
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
    job_id = common.helpers.generate_unique_resource_name("HelloWorld")

    try:
        submit_job_and_add_task(
            batch_client,
            job_id,
            pool_vm_size,
            pool_vm_count)

        common.helpers.wait_for_tasks_to_complete(
            batch_client,
            job_id,
            datetime.timedelta(minutes=25))

        tasks = batch_client.task.list(job_id)
        task_ids = [task.id for task in tasks]

        common.helpers.print_task_output(batch_client, job_id, task_ids)
    finally:
        if should_delete_job:
            print("Deleting job: ", job_id)
            batch_client.job.delete(job_id)


if __name__ == '__main__':
    global_config = ConfigParser()
    global_config.read(common.helpers.SAMPLES_CONFIG_FILE_NAME)

    sample_config = ConfigParser()
    sample_config.read(
        os.path.splitext(os.path.basename(__file__))[0] + '.cfg')

    execute_sample(global_config, sample_config)
