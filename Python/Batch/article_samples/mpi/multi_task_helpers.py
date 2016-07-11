# multi_task_helpers.py - Helper functions for Batch Python SDK sample to
# run multiinstance task
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
import sys
import time

try:
    input = raw_input
except NameError:
    pass

import azure.batch.batch_service_client as batch
import azure.batch.models as batchmodels

sys.path.append('.')
import common.helpers  # noqa


def create_pool_and_wait_for_vms(batch_service_client, pool_id, distro,
                                 version, vm_size, target_dedicated,
                                 command_line, resource_files, run_elevated):
    """
    Creates a pool of compute nodes with the specified OS settings.

    :param batch_service_client: A Batch service client.
    :type batch_service_client: `azure.batch.BatchServiceClient`
    :param str pool_id: An ID for the new pool.
    :param str distro: The Linux distribution that should be installed on the
    compute nodes, e.g. 'Ubuntu' or 'CentOS'.
    :param str version: The version of the operating system for the compute
    nodes, e.g. '15' or '14.04'.
    :param str vm_size: The size of VM, eg 'Standard_A1' or 'Standard_D1'
     as per
    https://azure.microsoft.com/en-us/documentation/articles/
    virtual-machines-windows-sizes/
    :param int target_dedicated: Number of target VMs for the pool
    :param str command_line: command line for the pool's start task.
    :param list resource_files: A collection of resource files for the pool's
    start task.
    :param bool run_elevated: flag determining if start task should run as
     elevated
    """
    print('Creating pool [{}]...'.format(pool_id))

    # Create a new pool of Linux compute nodes using an Azure Virtual Machines
    # Marketplace image. For more information about creating pools of Linux
    # nodes, see:
    # https://azure.microsoft.com/documentation/articles/batch-linux-nodes/

    # Get the virtual machine configuration for the desired distro and version.
    # For more information about the virtual machine configuration, see:
    # https://azure.microsoft.com/documentation/articles/batch-linux-nodes/
    vm_config = common.helpers.\
        get_vm_config_for_distro(batch_service_client, distro, version)
    new_pool = batch.models.PoolAddParameter(
        id=pool_id,
        virtual_machine_configuration=vm_config,
        vm_size=vm_size,
        target_dedicated=target_dedicated,
        resize_timeout=datetime.timedelta(minutes=15),
        enable_inter_node_communication=True,
        max_tasks_per_node=1,
        start_task=batch.models.StartTask(
            command_line=command_line,
            run_elevated=run_elevated,
            wait_for_success=True,
            resource_files=resource_files),
    )

    common.helpers.create_pool_if_not_exist(batch_service_client, new_pool)

    # because we want all nodes to be available before any tasks are assigned
    # to the pool, here we will wait for all compute nodes to reach idle
    nodes = common.helpers.wait_for_all_nodes_state(
        batch_service_client, new_pool,
        frozenset(
            (batchmodels.ComputeNodeState.starttaskfailed,
             batchmodels.ComputeNodeState.unusable,
             batchmodels.ComputeNodeState.idle)
        )
    )
    # ensure all node are idle
    if any(node.state != batchmodels.ComputeNodeState.idle for node in nodes):
        raise RuntimeError('node(s) of pool {} not in idle state'.format(
            pool_id))


def add_task(batch_service_client, job_id, task_id, application_cmdline,
             input_files, run_elevated, num_instances,
             coordination_cmdline, common_files):
    """
    Adds a task for each input file in the collection to the specified job.

    :param batch_service_client: A Batch service client.
    :type batch_service_client: `azure.batch.BatchServiceClient`
    :param str job_id: The ID of the job to which to add the task.
    :param str task_id: The ID of the task to be added.
    :param str application_cmdline: The application commandline for the task.
    :param list input_files: A collection of input files.
    :param bool run_elevated: flag determining if task should run as elevated
    :param int num_instances: Number of instances for the task
    :param str coordination_cmdline: The application commandline for the task.
    :param list common_files: A collection of common input files.
    """

    print('Adding {} task to job [{}]...'.format(task_id, job_id))

    multi_instance_settings = None
    if coordination_cmdline or (num_instances and num_instances > 1):
        multi_instance_settings = batchmodels.MultiInstanceSettings(
            number_of_instances=num_instances,
            coordination_command_line=coordination_cmdline,
            common_resource_files=common_files)
    task = batchmodels.TaskAddParameter(
        id=task_id,
        command_line=application_cmdline,
        run_elevated=run_elevated,
        resource_files=input_files,
        multi_instance_settings=multi_instance_settings
    )
    batch_service_client.task.add(job_id, task)


def wait_for_subtasks_to_complete(batch_service_client, job_id, task_id,
                                  timeout):
    """
    Returns when all subtasks in the specified task reach the Completed state.

    :param batch_service_client: A Batch service client.
    :type batch_service_client: `azure.batch.BatchServiceClient`
    :param str job_id: The id of the job whose tasks should be to monitored.
    :param str task_id: The id of the task whose subtasks should be monitored.
    :param timedelta timeout: The duration to wait for task completion. If all
    tasks in the specified job do not reach Completed state within this time
    period, an exception will be raised.
    """
    timeout_expiration = datetime.datetime.now() + timeout

    print("Monitoring all tasks for 'Completed' state, timeout in {}..."
          .format(timeout), end='')

    while datetime.datetime.now() < timeout_expiration:
        print('.', end='')
        sys.stdout.flush()

        subtasks = batch_service_client.task.list_subtasks(job_id, task_id)
        incomplete_subtasks = [subtask for subtask in subtasks.value if
                               subtask.state !=
                               batchmodels.TaskState.completed]

        if not incomplete_subtasks:
            print()
            return True
        else:
            time.sleep(10)

    print()
    raise RuntimeError(
        "ERROR: Subtasks did not reach 'Completed' state within "
        "timeout period of " + str(timeout))


def wait_for_tasks_to_complete(batch_service_client, job_id, timeout):
    """
    Returns when all tasks in the specified job reach the Completed state.

    :param batch_service_client: A Batch service client.
    :type batch_service_client: `azure.batch.BatchServiceClient`
    :param str job_id: The id of the job whose tasks should be to monitored.
    :param timedelta timeout: The duration to wait for task completion. If all
    tasks in the specified job do not reach Completed state within this time
    period, an exception will be raised.
    """
    timeout_expiration = datetime.datetime.now() + timeout

    print("Monitoring all tasks for 'Completed' state, timeout in {}..."
          .format(timeout), end='')

    while datetime.datetime.now() < timeout_expiration:
        print('.', end='')
        sys.stdout.flush()
        tasks = batch_service_client.task.list(job_id)

        for task in tasks:
            if task.state == batchmodels.TaskState.completed:
                # Pause execution until subtasks reach Completed state.
                wait_for_subtasks_to_complete(batch_service_client, job_id,
                                              task.id,
                                              datetime.timedelta(minutes=10))

        incomplete_tasks = [task for task in tasks if
                            task.state != batchmodels.TaskState.completed]
        if not incomplete_tasks:
            print()
            return True
        else:
            time.sleep(10)

    print()
    raise RuntimeError("ERROR: Tasks did not reach 'Completed' state within "
                       "timeout period of " + str(timeout))
