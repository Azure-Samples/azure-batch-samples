##Getting Started

###[01_HelloWorld](./01_HelloWorld)
The HelloWorld sample is an introduction to the framework required to communicate with the Batch service. It submits a job with an auto-pool, and then submits a task which performs a simple echo command.  The task has no required files.  The focus of this sample is on the API calls required to add a job to the Batch service and monitor the status of that job from a client.

###[02_PoolsAndResourceFiles](./02_PoolsAndResourceFiles)
This sample expands on the HelloWorld sample.  It creates a fixed pool, and then uses the FileStaging feature to submit a task which has a set of required files. The FileStaging is used to move the files into Azure Storage and then onto the Batch compute node.  This sample also showcases the use of a StartTask as a method to get files onto every node in the pool.

