## DotNetTutorial

This sample backs the code snippets found in [Get started with the Azure Batch library for .NET](https://azure.microsoft.com/documentation/articles/batch-dotnet-get-started/).

The solution includes two projects - *DotNetTutorial* and *TaskApplication* - that together demonstrate a common Batch application workflow. While the code in the solution does not demonstrate every feature of the Batch service, it is intended to act as a primer for basic Batch concepts and features such as pools, nodes, jobs, and tasks, as well as demonstrate interaction between Batch and the Azure Storage service.

The *DotNetTutorial* code sample is a Visual Studio 2015 solution consisting of two projects: **DotNetTutorial** and **TaskApplication**.

- **DotNetTutorial** is the client application that interacts with the Batch and Storage services to execute a parallel workload on compute nodes (virtual machines). DotNetTutorial runs on your local workstation.

- **TaskApplication** is the executable that runs on compute nodes in Azure to perform the actual work. In the sample, `TaskApplication.exe` parses the text in a file downloaded from Azure Storage (the input file), and produces a text file (the output file) that contains a list of the top three words appearing in the input file. After creating the output file, TaskApplication then uploads the file to Azure Storage, making it available to the client application for download. TaskApplication runs in parallel on multiple compute nodes in the Batch service.

The following diagram illustrates the primary operations performed by the client application, *DotNetTutorial*, and the application that is executed by the tasks, *TaskApplication*. This basic workflow is typical of many compute solutions created with Batch, and while it does not demonstrate every feature available in the Batch service, nearly every Batch scenario will include similar processes.

![Batch example workflow][1]<br/>

**1.** Create blob **containers** in Azure Storage<br/>
**2.** Upload task application and input files to containers<br/>
**3.** Create Batch **pool**<br/>
  &nbsp;&nbsp;&nbsp;&nbsp;**3a.** Pool **StartTask** downloads task binary (TaskApplication) to nodes as they join the pool<br/>
**4.** Create Batch **job**<br/>
**5.** Add **tasks** to job<br/>
  &nbsp;&nbsp;&nbsp;&nbsp;**5a.** The tasks are scheduled to execute on nodes<br/>
	&nbsp;&nbsp;&nbsp;&nbsp;**5b.** Each task downloads its input data from Azure Storage, then begins execution<br/>
**6.** Monitor tasks<br/>
  &nbsp;&nbsp;&nbsp;&nbsp;**6a.** As tasks complete, they upload their output data to Azure Storage<br/>
**7.** Download task output from Storage

[dotnet_getstarted]: http://azure.microsoft.com/documentation/articles/batch-dotnet-get-started/
[1]: batch_workflow_sm.png "Batch solution workflow (full diagram)"
