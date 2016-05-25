##Azure Batch Python Samples

###Configuring the samples
In order the run the samples, they must be configured with Azure Batch and
Azure Storage credentials. The credentials for each sample are gathered from
the common configuration located [here](./configuration.cfg). Once you
have configured your account credentials, you can run any of the samples and
they will make use of the credentials provided in the common configuration
file.

Each sample also has a configuration file specific to the individual sample
(for example [sample1\_helloworld.cfg](./sample1_helloworld.cfg))

###Setting up the Python environment
In order to run the samples, you will need a Python interpreter compatible
with version 2.7, 3.3, 3.4 or 3.5. You will also need to install the Azure
Batch and Azure Storage python packages.  This can be done using the
[requirements.txt](./requirements.txt) file using
`pip install -r requirements.txt`

You can also optionally use the
[Visual Studio project](./BatchSamples.pyproj) and the
[Python Tools for Visual Studio plugin](https://github.com/Microsoft/PTVS/wiki/PTVS-Installation).

###List of Samples

####[sample1\_helloworld.py](./sample1_helloworld.py)
The HelloWorld sample is an introduction to the framework required to
communicate with the Batch service. It submits a job using an auto-pool and
then submits a task which performs a simple echo command.  The task has no
required files.  The focus of this sample is on the API calls required to add
a job to the Batch service and monitor the status of that job from a client.

####[sample2\_pools\_and\_resourcefiles.py](./sample2_pools_and_resourcefiles.py)
This sample expands on the HelloWorld sample. It creates a fixed pool and then
submits a simple python script as the only task of the job. This sample also
showcases the use of a StartTask as a method to get files onto every node in
the pool. This sample is geared towards Linux with calls to list node agent
sku ids, selecting a publisher, offer and sku for the Linux VM gallery image.

####[sample3\_encrypted\_resourcefiles.py](./sample3_encrypted_resourcefiles.py)
This sample shows how to generate on-demand encryption keys in conjunction with
[blobxfer](../Storage) to encrypt local files into Azure Storage which will
then be decrypted for the task when it executes. This sample showcases a
variety of Azure Batch interaction including: adding certificates to an
account, creating a pool with a certificate reference, and accessing
certificates on a Linux Batch compute node. This sample is geared towards
Linux with the assumption of a locally available OpenSSL and blobxfer
installation that is accessible from the sample path invocation. This sample
can be run on Windows with an appropriate openssl binary and modified
openssl invocations (i.e., `openssl.exe` instead of `openssl`).

####[sample4\_docker\_swarm.py](./sample4\_docker\_swarm.py)
This sample shows how to create a pool of compute nodes that are also
clustered into a docker swarm. Additionally, this sample shows how to connect
to the docker swarm locally through an ssh tunnel. This sample showcases a
variety of Azure Batch interaction including: creating a inter-node
communication enabled pool, adding a compute node user with an ssh key,
retrieving remote login settings for a compute node, and adding an affinitized
task for a compute node. This sample is geared towards Linux with the
assumption of ssh and docker tools available from the machine executing the
sample. There is an additional configuration option to generate an ssh
tunnel script to interact with the batch pool after the sample is run. When
using this option, you will also need to disable the delete pool option as
well.

####[sample5\_docker\_batch\_task.py](./sample4\_docker\_batch\_task.py)
This sample shows how to schedule tasks that run a docker container. 
Specifically, this sample uses pool start up task to install docker
on Batch VMs which is Ubuntu based. It then uses job preparation tasks to
pull the application image from docker hub and finally submit a set of simple
tasks. Each task will launch a docker container to perform ffmpeg transcoding
on a downloaded mp4 file and upload the result to blob storage. The docker
image is CentOS based with ffmpeg and blobxfer preinstalled. The docker image
(yidingz/ffmpeg:v3) is available on docker hub. 

###Azure Batch on Linux Best Practices

Although some of the Python samples are not specific to Linux, the Azure Batch
team would like to provide guidance on best practices for hosting your Linux
workloads on Azure Batch.

* _Wrap your command(s) in a shell or provide a shell script_

Unless you have a single program you wish to execute that is resolvable in the
default `PATH` specified by the distribution (i.e., `/bin`, `/usr/bin`), then
it is advisable to wrap your command in a shell. For instance,

    /bin/bash -c "command1 && command2"

would execute `command1` followed by `command2`, if command1 was successful
inside a bash shell.

Alternatively, upload a shell script as part of your resource files for
your task that encompasses your program execution workflow.

* _Check for exit codes for each command in a shell script_

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

* _Wait for your background commands_

If you require executing multiple programs at the same time and cannot split
the invocation across multiple tasks, ensure you wrap your execution flow in
a shell and provide the appropriate wait command for all child processes. For
instance,

    /bin/bash -c "command1 &; command2 &; command3 &; wait"

This would ensure that all child processes exit before the parent exits.
Without the `wait` command, the Azure Batch service will not be able to
properly track when the compute node has completed execution of the
backgrounded tasks.

* _Set preferred locale and encoding_

Linux shell scripts or program invocations via Azure Batch tasks will execute
under the `POSIX` locale. If your programs require a specific locale and
encoding, e.g., to encode Unicode characters, then you will need to set the
locale via an environment variable. For example,

    # set environment variables on job: applies to all tasks in job
    job = batchserviceclient.models.CloudJob(
        common_environment_settings=[
            batchserviceclient.models.EnvironmentSettings('LC_ALL', 'en_US.UTF-8')
        ],
        # ... more args
    )

would set the `LC_ALL` environment variable to English US locale and UTF-8
encoding for all tasks added to the job. Alternatively you can set environment
variables for each individual task:

    # set environment variables on single task
    task = batchserviceclient.models.CloudTask(
        environment_settings=[
            batchserviceclient.models.EnvironmentSettings('LC_ALL', 'en_US.UTF-8')
        ],
        # ... more args
    )

There are similar environment settings arguments for start task, job
preparation task, and job release task. Although we recommend using the
built-in environment variable control provided by the Azure Batch API, you
can, as always, directly set shell environment variables in the shell
invocation for your task command(s):

    /bin/bash -c "export LC_ALL=en_US.UTF-8; command1"

A final note: not all locales may be present and installed on the compute node
and may require a start task or job preparation task for installation of the
desired locale.

* _stdout.txt and stderr.txt encoding_

On Linux compute nodes, these files are encoded with UTF-8. If your program
generates Unicode characters, ensure that the file is interpreted with UTF-8
encoding. Please see above related note regarding locale and encoding.

* _Do not perform release upgrades on compute nodes_

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

* _Consider asyncio for blocking Azure Batch calls_

With Python [3.4](https://docs.python.org/3.4/library/asyncio.html),
[3.5+ (async/await)](https://docs.python.org/3.5/library/asyncio.html), or
with the [Trollius](https://pypi.python.org/pypi/trollius) backport package,
one can wrap blocking I/O calls such as calls to the Azure Batch service to
the asyncio event loop to provide asynchronous, non-blocking behavior in your
Python scripts and programs.

Note that we are evaluating bringing native async/await capability (3.5+) to
the Azure Batch Python SDK.
