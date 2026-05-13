## Getting Started

### [01_HelloWorld](./01_HelloWorld)
The HelloWorld sample is an introduction to the framework required to communicate with the Batch service. It submits a job using an auto-pool and then submits a task which performs a simple echo command.  The task has no required files.  The focus of this sample is on the API calls required to add a job to the Batch service and monitor the status of that job from a client.

### [02_PoolsAndResourceFiles](./02_PoolsAndResourceFiles)
This sample expands on the HelloWorld sample. It creates a fixed pool and then submits a task with a set of required files. Required files are uploaded to Azure Blob Storage and then referenced by the task as `ResourceFile`s, which the Batch service downloads to the compute node before the task runs. This sample also showcases the use of a `BatchStartTask` as a method to get files onto every node in the pool.

### [03_JobManager](./03_JobManager)
This sample extends on the previous sample.  It uses a fixed pool and submits a job with a JobManager task.  The JobManager task adds other tasks to the job and also ends the job when all tasks have completed.
