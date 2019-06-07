# Sample4_job_scheduler.py Code Sample

# Create a job schedule
#    With job specification that has an autopool
#    autopool has a start task
#    job specification has a job manager task


from __future__ import print_function

try:
    import configparser
except ImportError:
    import ConfigParser as configparser

import datetime
import os

import azure.batch.batch_service_client as batch
import azure.storage.blob as azureblob
import azure.batch.batch_auth as batchauth
import azure.batch.models as batchmodels

import common.helpers

_CONTAINER_NAME = 'jobscheduler'
_SIMPLE_TASK_NAME = 'simple_task.py'
_SIMPLE_TASK_PATH = os.path.join('resources', 'simple_task.py')
_PYTHON_DOWNLOAD = 'https://www.python.org/ftp/python/3.7.3/python-3.7.3-amd64.exe'
_PYTHON_INSTALL = '.\python373.exe /passive InstallAllUsers=1 PrependPath=1 Include_test=0'
_USER_ELEVATION_LEVEL = 'admin'


def create_job_schedule(batch_client, job_id, vm_size, vm_count, block_blob_client):
    cloud_service_config = batchmodels.CloudServiceConfiguration(os_family='6')

    user_id = batchmodels.UserIdentity(
        auto_user=batchmodels.AutoUserSpecification(elevation_level=_USER_ELEVATION_LEVEL))

    python_download = batchmodels.ResourceFile(http_url=_PYTHON_DOWNLOAD, file_path='python373.exe')

    pool_info = batchmodels.PoolInformation(
        auto_pool_specification=batchmodels.AutoPoolSpecification(
            auto_pool_id_prefix="JobScheduler",
            pool=batchmodels.PoolSpecification(
                vm_size=vm_size,
                target_dedicated_nodes=vm_count,
                cloud_service_configuration=cloud_service_config,
                start_task=batchmodels.StartTask(
                    command_line=common.helpers.wrap_commands_in_shell('windows', ['{}'.format(_PYTHON_INSTALL)]),
                    resource_files=[python_download],
                    wait_for_success=True,
                    user_identity=user_id)),
            keep_alive=False,
            pool_lifetime_option=batchmodels.PoolLifetimeOption.job_schedule))

    sas_url = common.helpers.upload_blob_and_create_sas(
        block_blob_client,
        _CONTAINER_NAME,
        _SIMPLE_TASK_NAME,
        _SIMPLE_TASK_PATH,
        datetime.datetime.utcnow() + datetime.timedelta(hours=1))

    job_spec = batchmodels.JobSpecification(
        pool_info=pool_info,
        on_all_tasks_complete=batchmodels.OnAllTasksComplete.terminate_job,
        job_manager_task=batchmodels.JobManagerTask(
            id="JobManagerTask",
            command_line=common.helpers.wrap_commands_in_shell('windows', [f'python {_SIMPLE_TASK_NAME}']),
            resource_files=[batchmodels.ResourceFile(
                file_path=_SIMPLE_TASK_NAME,
                http_url=sas_url)]))

    do_not_run_after = datetime.datetime.utcnow() + datetime.timedelta(hours=1)

    schedule = batchmodels.Schedule(
        do_not_run_after=do_not_run_after,
        recurrence_interval=datetime.timedelta(minutes=30))

    scheduled_job = batchmodels.JobScheduleAddParameter(id=job_id, schedule=schedule, job_specification=job_spec)

    batch_client.job_schedule.add(cloud_job_schedule=scheduled_job)


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

    block_blob_client = azureblob.BlockBlobService(
        account_name=storage_account_name,
        account_key=storage_account_key,
        endpoint_suffix=storage_account_suffix)

    batch_client.config.retry_policy.retries = 5
    job_id = common.helpers.generate_unique_resource_name("JobScheduler")

    try:
        create_job_schedule(
            batch_client,
            job_id,
            pool_vm_size,
            pool_vm_count,
            block_blob_client)

        common.helpers.wait_for_tasks_to_complete(
            batch_client,
            job_id,
            datetime.timedelta(minutes=25))

        tasks = batch_client.task.list(job_id)
        task_ids = [task.id for task in tasks]

        common.helpers.print_task_output(batch_client, job_id, task_ids)

    except batchmodels.BatchErrorException as e:
        for x in e.error.values:
            print(x)

    finally:
        if should_delete_job:
            print("Deleting job: ", job_id)
            batch_client.job.delete(job_id)
        if should_delete_container:
            block_blob_client.delete_container(
                _CONTAINER_NAME,
                fail_not_exist=False)


if __name__ == '__main__':
    global_config = configparser.ConfigParser()
    global_config.read(common.helpers._SAMPLES_CONFIG_FILE_NAME)

    sample_config = configparser.ConfigParser()
    sample_config.read(
        os.path.splitext(os.path.basename(__file__))[0] + '.cfg')

    execute_sample(global_config, sample_config)
