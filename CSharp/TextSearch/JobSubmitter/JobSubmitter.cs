//Copyright (c) Microsoft Corporation

namespace Microsoft.Azure.Batch.Samples.TextSearch
{
    using System;
    using System.IO;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Common;
    using Microsoft.Azure.Batch.Auth;
    using Microsoft.Azure.Batch.Common;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Auth;
    using Microsoft.Extensions.Configuration;

    /// <summary>
    /// Submits the job to the Batch Service and waits for it to complete.
    /// Once it has completed, it downloads the reducer task output
    /// and prints it to the console.
    /// </summary>
    public class JobSubmitter
    {
        private readonly Settings textSearchSettings;
        private readonly AccountSettings accountSettings;

        /// <summary>
        /// Constructs a JobSubmitter with default values.
        /// </summary>
        public JobSubmitter()
        {
            //Load the configuration settings.
            this.textSearchSettings = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("settings.json")
                .Build()
                .Get<Settings>();
            this.accountSettings = SampleHelpers.LoadAccountSettings();
        }

        /// <summary>
        /// Populates Azure Storage with the required files, and 
        /// submits the job to the Azure Batch service.
        /// </summary>
        public async Task RunAsync()
        {
            Console.WriteLine("Running with the following settings: ");
            Console.WriteLine("----------------------------------------");
            Console.WriteLine(this.textSearchSettings.ToString());
            Console.WriteLine(this.accountSettings.ToString());
            
            CloudStorageAccount cloudStorageAccount = new CloudStorageAccount(
                new StorageCredentials(
                    this.accountSettings.StorageAccountName,
                    this.accountSettings.StorageAccountKey), 
                this.accountSettings.StorageServiceUrl,
                useHttps: true);

            //Upload resources if required
            Console.WriteLine($"Creating container {this.textSearchSettings.OutputBlobContainer} if it doesn't exist...");
            var blobClient = cloudStorageAccount.CreateCloudBlobClient();
            var outputContainer = blobClient.GetContainerReference(this.textSearchSettings.OutputBlobContainer);
            await outputContainer.CreateIfNotExistsAsync();

            if (this.textSearchSettings.ShouldUploadResources)
            {
                Console.WriteLine("Splitting file: {0} into {1} subfiles", 
                    Constants.TextFilePath, 
                    this.textSearchSettings.NumberOfMapperTasks);

                //Split the text file into the correct number of files for consumption by the mapper tasks.
                FileSplitter splitter = new FileSplitter();
                List<string> mapperTaskFiles = await splitter.SplitAsync(
                    Constants.TextFilePath, 
                    this.textSearchSettings.NumberOfMapperTasks);

                List<string> files = Constants.RequiredExecutableFiles.Union(mapperTaskFiles).ToList();

                await SampleHelpers.UploadResourcesAsync(
                    cloudStorageAccount,
                    this.textSearchSettings.InputBlobContainer,
                    files);
            }
            
            //Generate a SAS for the container.
            string inputContainerSasUrl = SampleHelpers.ConstructContainerSas(
                cloudStorageAccount,
                this.textSearchSettings.InputBlobContainer,
                permissions: WindowsAzure.Storage.Blob.SharedAccessBlobPermissions.Read);

            string outputContainerSasUrl = SampleHelpers.ConstructContainerSas(
                cloudStorageAccount,
                this.textSearchSettings.OutputBlobContainer,
                permissions: WindowsAzure.Storage.Blob.SharedAccessBlobPermissions.Read |
                    WindowsAzure.Storage.Blob.SharedAccessBlobPermissions.Write);

            //Set up the Batch Service credentials used to authenticate with the Batch Service.
            BatchSharedKeyCredentials credentials = new BatchSharedKeyCredentials(
                this.accountSettings.BatchServiceUrl,
                this.accountSettings.BatchAccountName,
                this.accountSettings.BatchAccountKey);

            using (BatchClient batchClient = BatchClient.Open(credentials))
            {
                //
                // Construct the job properties in local memory before commiting them to the Batch Service.
                //

                //Allow enough compute nodes in the pool to run each mapper task
                int numberOfPoolComputeNodes = this.textSearchSettings.NumberOfMapperTasks;

                //Define the pool specification for the pool which the job will run on.
                PoolSpecification poolSpecification = new PoolSpecification()
                {
                    TargetDedicatedComputeNodes = numberOfPoolComputeNodes,
                    VirtualMachineSize = "standard_d1_v2",
                    //You can learn more about os families and versions at: 
                    //http://azure.microsoft.com/documentation/articles/cloud-services-guestos-update-matrix
                    CloudServiceConfiguration = new CloudServiceConfiguration(osFamily: "5")
                };

                //Use the auto pool feature of the Batch Service to create a pool when the job is created.
                //This creates a new pool for each job which is added.
                AutoPoolSpecification autoPoolSpecification = new AutoPoolSpecification()
                {
                    AutoPoolIdPrefix= "TextSearchPool",
                    KeepAlive = false,
                    PoolLifetimeOption = PoolLifetimeOption.Job,
                    PoolSpecification = poolSpecification
                };

                //Define the pool information for this job -- it will run on the pool defined by the auto pool specification above.
                PoolInformation poolInformation = new PoolInformation()
                {
                    AutoPoolSpecification = autoPoolSpecification
                };

                //Create the unbound job in local memory.  An object which exists only in local memory (and not on the Batch Service) is "unbound".
                string jobId = Environment.GetEnvironmentVariable("USERNAME") + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");

                CloudJob unboundJob = batchClient.JobOperations.CreateJob(jobId, poolInformation);
                unboundJob.UsesTaskDependencies = true;

                try
                {
                    //Commit the unbound job to the Batch Service.
                    Console.WriteLine($"Adding job: {unboundJob.Id} to the Batch Service.");
                    await unboundJob.CommitAsync(); //Issues a request to the Batch Service to add the job which was defined above.

                    // Add tasks to the job
                    var mapperTasks = CreateMapperTasks(inputContainerSasUrl, outputContainerSasUrl);
                    var reducerTask = CreateReducerTask(inputContainerSasUrl, outputContainerSasUrl, mapperTasks);

                    var tasksToAdd = Enumerable.Concat(mapperTasks, new[] { reducerTask });

                    //Submit the unbound task collection to the Batch Service.
                    //Use the AddTask method which takes a collection of CloudTasks for the best performance.
                    Console.WriteLine("Submitting {0} mapper tasks", this.textSearchSettings.NumberOfMapperTasks);
                    Console.WriteLine("Submitting 1 reducer task");
                    await batchClient.JobOperations.AddTaskAsync(jobId, tasksToAdd);

                    //An object which is backed by a corresponding Batch Service object is "bound."
                    CloudJob boundJob = await batchClient.JobOperations.GetJobAsync(jobId);

                    // Update the job now that we've added tasks so that when all of the tasks which we have added
                    // are complete, the job will automatically move to the completed state.
                    boundJob.OnAllTasksComplete = OnAllTasksComplete.TerminateJob;
                    boundJob.Commit();
                    boundJob.Refresh();

                    //
                    // Wait for the tasks to complete.
                    //
                    List<CloudTask> tasks = await batchClient.JobOperations.ListTasks(jobId).ToListAsync();
                    TimeSpan maxJobCompletionTimeout = TimeSpan.FromMinutes(30);

                    // Monitor the current tasks to see when they are done.
                    // Occasionally a task may get killed and requeued during an upgrade or hardware failure, 
                    // Robustness against this was not added into the sample for 
                    // simplicity, but should be added into any production code.
                    Console.WriteLine("Waiting for job's tasks to complete");

                    TaskStateMonitor taskStateMonitor = batchClient.Utilities.CreateTaskStateMonitor();
                    try
                    {
                        await taskStateMonitor.WhenAll(tasks, TaskState.Completed, maxJobCompletionTimeout);
                    }
                    finally
                    {
                        Console.WriteLine("Done waiting for all tasks to complete");

                        // Refresh the task list
                        tasks = await batchClient.JobOperations.ListTasks(jobId).ToListAsync();

                        //Check to ensure the job manager task exited successfully.
                        foreach (var task in tasks)
                        {
                            await Helpers.CheckForTaskSuccessAsync(task, dumpStandardOutOnTaskSuccess: false);
                        }
                    }

                    //
                    // Download and write out the reducer tasks output
                    //
                    string reducerText = await SampleHelpers.DownloadBlobTextAsync(cloudStorageAccount, this.textSearchSettings.OutputBlobContainer, Constants.ReducerTaskResultBlobName);
                    Console.WriteLine("Reducer reuslts:");
                    Console.WriteLine(reducerText);
                }
                finally
                {
                    //Delete the job.
                    //This will delete the auto pool associated with the job as long as the pool
                    //keep alive property is set to false.
                    if (this.textSearchSettings.ShouldDeleteJob)
                    {
                        Console.WriteLine($"Deleting job {jobId}");
                        await batchClient.JobOperations.DeleteJobAsync(jobId);
                    }

                    if (this.textSearchSettings.ShouldDeleteContainers)
                    {
                        Console.WriteLine("Deleting containers");
                        var inputContainer = blobClient.GetContainerReference(this.textSearchSettings.InputBlobContainer);
                        await inputContainer.DeleteIfExistsAsync();

                        await outputContainer.DeleteIfExistsAsync();
                    }

                }
            }
        }

        private IEnumerable<CloudTask> CreateMapperTasks(string inputContainerSas, string outputContainerSas)
        {
            //The collection of tasks to add to the Batch Service.
            List<CloudTask> tasksToAdd = new List<CloudTask>();

            for (int i = 0; i < this.textSearchSettings.NumberOfMapperTasks; i++)
            {
                string taskId = Helpers.GetMapperTaskId(i);
                string fileBlobName = Helpers.GetSplitFileName(i);
                string mapperFileBlobSas = SampleHelpers.ConstructBlobSource(inputContainerSas, fileBlobName);

                string commandLine = string.Format("{0} {1}", Constants.MapperTaskExecutable, fileBlobName);
                CloudTask unboundMapperTask = new CloudTask(taskId, commandLine);

                //The set of files (exes, dlls and configuration files) required to run the mapper task. They have already been uploaded
                //so just get their sas's
                IReadOnlyList<string> mapperTaskRequiredFiles = Constants.RequiredExecutableFiles;
                List<ResourceFile> mapperTaskResourceFiles = SampleHelpers.GetResourceFiles(inputContainerSas, mapperTaskRequiredFiles);
                mapperTaskResourceFiles.Add(ResourceFile.FromUrl(mapperFileBlobSas, fileBlobName));

                unboundMapperTask.OutputFiles = new List<OutputFile>
                {
                    new OutputFile(
                        filePattern: "..\\stdout.txt",
                        destination: new OutputFileDestination(
                            container: new OutputFileBlobContainerDestination(outputContainerSas, path: taskId)),
                        uploadOptions: new OutputFileUploadOptions(uploadCondition: OutputFileUploadCondition.TaskSuccess))
                };
                unboundMapperTask.ResourceFiles = mapperTaskResourceFiles;

                yield return unboundMapperTask;
            }
        }

        private CloudTask CreateReducerTask(string inputContainerSas, string outputContainerSas, IEnumerable<CloudTask> mapperTasks)
        {
            CloudTask unboundReducerTask = new CloudTask(Constants.ReducerTaskId, Constants.ReducerTaskExecutable);

            //The set of files (exes, dlls and configuration files) required to run the reducer task.
            List<ResourceFile> reducerTaskResourceFiles = SampleHelpers.GetResourceFiles(inputContainerSas, Constants.RequiredExecutableFiles);

            //The mapper outputs to reduce
            var mapperOutputs = Enumerable.Range(0, this.textSearchSettings.NumberOfMapperTasks).Select(Helpers.GetMapperTaskId);
            reducerTaskResourceFiles.AddRange(SampleHelpers.GetResourceFiles(outputContainerSas, mapperOutputs));
            unboundReducerTask.ResourceFiles = reducerTaskResourceFiles;

            // Upload the reducer task stdout as the result file for the entire job
            unboundReducerTask.OutputFiles = new List<OutputFile>
            {
                new OutputFile(
                    filePattern: "..\\stdout.txt",
                    destination: new OutputFileDestination(
                        container: new OutputFileBlobContainerDestination(outputContainerSas, path: Constants.ReducerTaskResultBlobName)),
                    uploadOptions: new OutputFileUploadOptions(uploadCondition: OutputFileUploadCondition.TaskSuccess))
            };

            // Depend on the mapper tasks so that they are all complete before the reducer runs
            unboundReducerTask.DependsOn = TaskDependencies.OnTasks(mapperTasks);

            return unboundReducerTask;
        }

    }
}
