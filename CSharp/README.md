## General Info

### Visual Studio Requirements
You will need Visual Studio 2017 or later to compile the projects. If you don't have it, you can download the community edition [here](https://www.visualstudio.com/products/visual-studio-community-vs.aspx) for free.

### A note on .NET Core
The samples currently all target .NET Framework. Most samples can easily be changed to target .NET Core by changing the .csproj to produce a netcoreapp binary instead of a net462 binary. In the case of samples with
worker task exectuables or JobManager executables a standalone .NET Core binary distribution must be produced, zipped, uploaded to Azure Storage, and unzipped on the node prior to execution of the task process.
In order to keep the samples simple, this is left as an exercize to the reader.

### Building the Samples
Download the samples and open the solution file for each one using Visual Studio. Right click on the solution and select "Rebuild". Visual Studio will analyze the dependencies and download the dependent binaries from [nuget.org](http://www.nuget.org/packages/Azure.Batch/).

### Preallocating Pools and Compute Nodes
Samples outside of [ArticleProjects](./ArticleProjects) require a pre-created pool with deployed compute nodes. If your tasks are not being scheduled, it is likely that there are no nodes to host the tasks; some of the samples detect this and will add 3 small nodes to the specified pool. You can use the [Azure Batch Explorer](https://azure.github.io/BatchExplorer/) to monitor this situation, as well as create the pool and resize it to have some number of nodes. You can then specify this pool's id in some of the samples so you don't have to wait for the nodes to become available.

### Configuring Credentials
In order the run the samples, they must be configured with Azure Batch and Azure Storage credentials. The credentials for each sample are gathered from the AccountSettings configuration located [here](./Common/AccountSettings.settings). The settings can be set via the Visual Studio settings manager. Once you have configured your account credentials, you can run any of the samples and they will make use of the credentials provided in the AccountSettings configuration.

## Sample Descriptions

### [AccountManagement](./AccountManagement)
This sample demonstrates the use of the [Microsoft.Azure.Management.Batch](https://msdn.microsoft.com/library/azure/mt463120.aspx) library to perform basic operations related to Batch accounts. View the [companion article](https://azure.microsoft.com/documentation/articles/batch-management-dotnet/) for more information.

### [ArticleProjects](./ArticleProjects)
These projects contain the sample code backing various articles in [Batch documentation](http://azure.microsoft.com/documentation/services/batch/). Most of the samples in [ArticleProjects](./ArticleProjects) are intended not as end-to-end API usage samples, but rather to demonstrate a specific feature of the Batch service.

### [BatchMetrics](./BatchMetrics)
This sample demonstrates efficient list queries and provides a utility library for job progress monitoring.

### [GettingStarted](./GettingStarted)
This set of samples is intended to be the starting point for learning the concepts behind Azure Batch and its API. It covers basic features of the service, including jobs, pools, tasks, and more.

### [TextSearch](./TextSearch)
This map-reduce style sample uses Azure Batch to perform parallel text processing on an input file by splitting it up into multiple sub-files and performing regular expression matching on each sub-file.
This sample makes use of a variety of Azure Batch features, such as auto-pool functionality, task `ResourceFiles` for moving data into VMs, task `OutputFiles` for moving data out of VMs, and task dependencies for specifying tasks relationships to one another. See the readme in the source directory for more information.

### [TopNWords](./TopNWords)
This sample demonstrates how to process a set of input blobs in parallel on multiple compute nodes. In this case, there is only one blob but the code can be expanded to load more blobs and bind them to individual tasks. The task writes a list of length N to stdout that contains the words in the blob with the highest occurrence count. A run-once job is created followed by the creation of multiple tasks with each task processing its blob. The job code then waits for each of the tasks to complete and prints out the list generated for each input blob.
