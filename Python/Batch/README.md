## Azure Batch Python Samples

### Configuring the samples
In order to run these Python samples, they must be configured with Azure Batch
and Azure Storage credentials. The credentials for each sample are gathered
from the common configuration located [here](./configuration.cfg). Once you
have configured your account credentials, you can run any of the samples and
they will make use of the credentials provided in the common configuration
file.

Each sample also has a configuration file specific to the individual sample
(for example [sample1\_helloworld.cfg](./sample1_helloworld.cfg))

### Setting up the Python environment
In order to run the samples, you will need a Python interpreter compatible
with version 2.7 or 3.3+. You will also need to install the
[Azure Batch](https://pypi.python.org/pypi/azure-batch) and
[Azure Storage](https://pypi.python.org/pypi/azure-storage) python packages.
Installation can be performed using the [requirements.txt](./requirements.txt)
file via the command `pip install -r requirements.txt`

### List of Samples

#### [sample1\_helloworld.py](./sample1_helloworld.py)
The HelloWorld sample is an introduction to the framework required to
communicate with the Batch service. It submits a job using an auto-pool and
then submits a task which performs a simple echo command.  The task has no
required files.  The focus of this sample is on the API calls required to add
a job to the Batch service and monitor the status of that job from a client.

#### [sample2\_pools\_and\_resourcefiles.py](./sample2_pools_and_resourcefiles.py)
This sample expands on the HelloWorld sample. It creates a fixed pool and then
submits a simple python script as the only task of the job. This sample also
showcases the use of a StartTask as a method to get files onto every node in
the pool. This sample is geared towards Linux with calls to list node agent
sku ids, selecting a publisher, offer and sku for the Linux VM gallery image.

#### [sample3\_encrypted\_resourcefiles.py](./sample3_encrypted_resourcefiles.py)
This sample shows how to generate on-demand encryption keys in conjunction with
[blobxfer](https://github.com/Azure/blobxfer) to encrypt local files into Azure
Storage which will then be decrypted for the task when it executes which
ensures encrypted files not only in transit but encrypted in storage
immediately. This sample showcases a variety of Azure Batch interaction
including: adding certificates to an account, creating a pool with a
certificate reference, and accessing certificates on a Linux Batch compute
node. This sample is geared towards Linux with the assumption of a locally
available OpenSSL and blobxfer installation that is accessible from the sample
path invocation. This sample can be run on Windows with an appropriate openssl
binary and modified openssl invocations (i.e., `openssl.exe` instead of
`openssl`).

#### [sample4\_job\_scheduler.py](./sample4_job_scheduler.py)
This sample demonstrates how to use a Job Schedule to run recurring work. The 
sample creates a Job Schedule with a Job specification that has an AutoPool 
with a StartTask and a JobManager Task. The Job Schedule will create a Job, 
at which point the AutoPool for that Job is created. The AutoPool's StartTask 
will run on every Compute Node, downloading and installing Python. Once completed, 
the Job's JobManager Task will execute, running a simple Python program. The Job 
will complete once all tasks under it (here, only the Job Manager) have 
completed, at which point the Job Schedule is able to create the next Job 
recurrence based on its schedule. This Job Schedule is configured to run every 
10 minutes, for 30 minutes in total. The Jobs created underneath the Job 
Schedule will each create their own CloudServices AutoPool. The AutoPool's 
lifetime is scoped to the Job.


## Azure Batch on Linux Best Practices

Although some of the Python samples are not specific to Linux, the Azure Batch
team would like to provide guidance on best practices for hosting your Linux
workloads on Azure Batch.

#### Wrap your command(s) in a shell or provide a shell script

Unless you have a single program you wish to execute that is resolvable in the
default `PATH` specified by the distribution (i.e., `/bin`, `/usr/bin`) or
the `$AZ_BATCH_TASK_WORKING_DIR`, then it is advisable to wrap your command
in a shell. For instance,

    /bin/bash -c "command1 && command2"

would execute `command1` followed by `command2`, if `command1` was successful
inside a bash shell.

Alternatively, upload a shell script as part of your resource files for
your task that encompasses your program execution workflow.

#### Check for exit codes for each command in a shell script

You should check for exit codes within your shell script for each command
invocation in a series if you depend on successful program execution for
each. For example,

    #!/usr/bin/env bash
    command1
    command2
    command3

will always return exit code of the last command or `command3` in this
example. If you need to track individual exit codes, remember to store and
check the return code for each command, or alternatively use the built-ins
available in your shell to handle this for you. For example, the above can be
modified to:

    #!/usr/bin/env bash
    set -e
    command1
    command2
    command3

If `command2` fails, then the entire script will exit with the proper
return code of the failing command.

#### Wait for your background commands

If you require executing multiple programs at the same time and cannot split
the invocation across multiple tasks, ensure you wrap your execution flow in
a shell and provide the appropriate wait command for all child processes. For
instance,

    #!/usr/bin/env bash
    set -e
    command1 &
    command2 &
    command3 &
    wait

This would ensure that all child processes exit before the parent exits.
Without the `wait` command, the Azure Batch service will not be able to
properly track when the compute node has completed execution of the
backgrounded tasks.

#### Set preferred locale and encoding

Linux shell scripts or program invocations via Azure Batch tasks will execute
under the `POSIX` locale. If your programs require a specific locale and
encoding, e.g., to encode Unicode characters, then you will need to set the
locale via an environment variable. For example,

    # set environment variables on job: applies to all tasks in job
    job = azure.batch.models.CloudJob(
        common_environment_settings=[
            azure.batch.models.EnvironmentSetting('LC_ALL', 'en_US.UTF-8')
        ],
        # ... more args
    )

would set the `LC_ALL` environment variable to English US locale and UTF-8
encoding for all tasks added to the job. Alternatively you can set environment
variables for each individual task:

    # set environment variables on single task
    task = azure.batch.models.CloudTask(
        environment_settings=[
            azure.batch.models.EnvironmentSetting('LC_ALL', 'en_US.UTF-8')
        ],
        # ... more args
    )

There are similar environment settings arguments for start task, job
preparation task, and job release task. Although we recommend using the
built-in environment variable control provided by the Azure Batch API as
these environment variables will be set on execution of the task, you
can, as always, directly set shell environment variables in the shell
invocation for your task command(s):

    /bin/bash -c "export LC_ALL=en_US.UTF-8; command1"

A final note: not all locales may be present and installed on the compute node
and may require a start task or job preparation task for installation of the
desired locale.

#### stdout.txt and stderr.txt encoding

On Linux compute nodes, task `stdout.txt` and `stderr.txt` files are encoded
with UTF-8. If your program generates Unicode characters, ensure that the file
is interpreted with UTF-8 encoding. Please see above related note regarding
locale and encoding.

#### Do not perform release upgrades on compute nodes

Many distributions offer the ability to perform a release upgrade. By
"release upgrade," we refer to major version upgrades such as from Ubuntu
14.04 to 15.10 (and not 14.04.3 LTS to 14.04.4 LTS). We advise against
performing such release upgrades because underlying system dependencies
or init processes may change (e.g., minimum supported GLIBC version or a
wholesale replacement of the init process from upstart to systemd).

If you wish to perform such an upgrade, we recommend creating a new pool
with the desired platform target and migrating your jobs and tasks to the
new pool.

Note that we are evaluating automating os and security updates as a possible
future enhancement.

#### Consider asyncio for blocking Azure Batch calls

With Python [3.4](https://docs.python.org/3.4/library/asyncio.html),
[3.5+ (async/await)](https://docs.python.org/3.5/library/asyncio.html), or
with the [Trollius](https://pypi.python.org/pypi/trollius) backport package,
one can wrap blocking I/O calls such as calls to the Azure Batch service to
the asyncio event loop to provide asynchronous, non-blocking behavior in your
Python scripts and programs.

Note that we are evaluating bringing native async/await capability (3.5+) to
the Azure Batch Python SDK.
