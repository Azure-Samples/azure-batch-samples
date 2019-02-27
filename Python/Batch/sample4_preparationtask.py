# sample5_preparationtask.py Code Sample

from __future__ import print_function
try:
    import configparser
except ImportError:
    import ConfigParser as configparser
import datetime
import os

import azure.batch.batch_service_client as batch
import azure.batch.batch_auth as batchauth
import azure.batch.models as batchmodels

import common.helpers

preptaskcommand = 'cmd /c set'


def submit_job_and_add_task(batch_client, job_id, vm_size, vm_count):
    """Submits a job to the Azure Batch service
    and adds a simple task with preparation task
    :param batch_client: The batch client to use.
    :type batch_client: `batchserviceclient.BatchServiceClient`
    :param str job_id: The id of the job to create.
    """

    pool_info = batchmodels.PoolInformation(
        auto_pool_specification=batchmodels.AutoPoolSpecification(
            auto_pool_id_prefix="Helloworld_jobprep",
            pool=batchmodels.PoolSpecification(
                vm_size=vm_size,
                target_dedicated_nodes=vm_count,
                cloud_service_configuration={'os_family': "4"}),
            keep_alive=False,
            pool_lifetime_option=batchmodels.PoolLifetimeOption.job))

    job = batchmodels.JobAddParameter(id=job_id, pool_info=pool_info, job_preparation_task=batch.models.JobPreparationTask(
            command_line= preptaskcommand,
            wait_for_success=True)
    )

    batch_client.job.add(job)

    task = batchmodels.TaskAddParameter(
        id="HelloWorld_Task",
        command_line=common.helpers.wrap_commands_in_shell(
            'windows', ['echo Hello world from the Batch Hello world sample!'])
    )

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

    credentials = batchauth.SharedKeyCredentials(
        batch_account_name,
        batch_account_key)

    batch_client = batch.BatchServiceClient(
        credentials,
        base_url=batch_service_url)

    # Retry 5 times -- default is 3
    batch_client.config.retry_policy.retries = 5
    job_id = common.helpers.generate_unique_resource_name("samplePrepJob")

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
    global_config = configparser.ConfigParser()
    global_config.read(common.helpers._SAMPLES_CONFIG_FILE_NAME)

    sample_config = configparser.ConfigParser()
    sample_config.read(os.path.splitext(os.path.basename(__file__))[0] + '.cfg')

    execute_sample(global_config, sample_config)
