##General Info

###Visual Studio Requirements
You will need VS 2013 or later to compile the projects. If you use VS 2012 or 2010, make sure you have the latest Nuget plugin (2.8 or later) installed. Nuget can be found on  [Visual Studio Gallery](https://visualstudiogallery.msdn.microsoft.com/27077b70-9dad-4c64-adcf-c7cf6bc9970c).

###Building the Samples
Download the samples and open the solution file for each one using Visual Studio. Right click on the solution and select "Rebuild". Visual Studio will analyze the dependencies and download the dependent binaries from [nuget.org](http://www.nuget.org/packages/Azure.Batch/).

###Preallocating Pools and VMs
Some of the samples require a pre-created pool with deployed VMs. If your tasks are not being scheduled, it is likely that there are no VMs to host their tasks. You can use Batch Explorer to monitor this situation as well as create the pool and resize it to have some number of VMs. You can then supply this pool name to some of the samples so you don't have to wait for VMs to become available.

###Updating to Batch Client 2.0
We're in the process of updating the samples to use the just released 2.0 version of the Batch Client library. Stay tuned...

##Sample Descriptions

###[AccountManagement](./AccountManagement)
This sample demonstrates how to use the Microsoft.Azure.Management.Batch library to perform basic operations related to Batch accounts.

###[BatchExplorer](./BatchExplorer)
Updated to Batch Client 2.0

Azure Batch Explorer is a GUI application to view and manage Azure Batch Service. View this [blog post](http://blogs.technet.com/b/windowshpc/archive/2015/01/20/azure-batch-explorer-sample-walkthrough.aspx) for more detail.

###[HelloWorld](./HelloWorld)
Updated to Batch Client 2.0

The HelloWorld sample is an introduction to the framework required to communicate with the Batch service. It performs some basic list functions as well as creating a pool and a set of example jobs. It also demonstrates submitting collections of tasks and the task state monitor which, together, provides a simple way of submitting a set of tasks to the service and monitoring their completion. It also demonstrates the use of the FileStaging interface that moves files from your client machine to the compute nodes in the pool prior to running the task on that node.

###[ImgProc](./ImgProc)
ImgProc demonstrates how to use a single binary that acts both as a client submitting work to the service and as the executable that runs as a task. This particular sample utilizes ImageMagick to convert image files into their associated thumbprint images.

###[TextSearch](./TextSearch)
This map-reduce style sample uses Azure Batch to perform parallel text processing on an input file by splitting it up into multiple sub-files and performing regular expression matching on each sub-file. The results are then rolled-up into a final report. This sample also uses a Job Manager to orchestrate the mapper and reducer tasks. It combines all 4 functions into one binary: the submission of the workitem, the job manager, and the mapper and reducer code.

###[TopNWords](./TopNWords)
Updated to Batch Client 2.0

This sample demonstrates how to process a set of input blobs in parallel on multiple compute nodes. In this case, there is only one blob but the code can be expanded to load more blobs and bind them to individual tasks. The task writes a list of length N to stdout that contains the words in the blob with the highest occurrence count. A run-once job is created followed by the creation of multiple tasks with each task processing the its blob. The job code then waits for each of the tasks to complete and prints out the list generated for each input blob.
