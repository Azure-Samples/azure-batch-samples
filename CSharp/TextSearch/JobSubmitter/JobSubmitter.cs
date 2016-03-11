//Copyright (c) Microsoft Corporation

namespace Microsoft.Azure.Batch.Samples.TextSearch
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Common;
    using Microsoft.Azure.Batch.Auth;
    using Microsoft.Azure.Batch.Common;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Auth;

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
            this.textSearchSettings = Settings.Default;
            this.accountSettings = AccountSettings.Default;
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

            //Upload resources if required.
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
                    this.textSearchSettings.BlobContainer,
                    files);
            }
            
            //Generate a SAS for the container.
            string containerSasUrl = SampleHelpers.ConstructContainerSas(
                cloudStorageAccount,
                this.textSearchSettings.BlobContainer);

            //Set up the Batch Service credentials used to authenticate with the Batch Service.
            BatchSharedKeyCredentials credentials = new BatchSharedKeyCredentials(
                this.accountSettings.BatchServiceUrl,
                this.accountSettings.BatchAccountName,
                this.accountSettings.BatchAccountKey);

            using (BatchClient batchClient = await BatchClient.OpenAsync(credentials))
            {
                //
                // Construct the job properties in local memory before commiting them to the Batch Service.
                //

                //Allow enough compute nodes in the pool to run each mapper task, and 1 extra to run the job manager.
                int numberOfPoolComputeNodes = 1 + this.textSearchSettings.NumberOfMapperTasks;

                //Define the pool specification for the pool which the job will run on.
                PoolSpecification poolSpecification = new PoolSpecification()
                    {
                        TargetDedicated = numberOfPoolComputeNodes,
                        VirtualMachineSize = "small",
                        //You can learn more about os families and versions at: 
                        //http://azure.microsoft.com/documentation/articles/cloud-services-guestos-update-matrix
                        OSFamily = "4",
                        TargetOSVersion = "*"
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
                
                //Define the job manager for this job.  This job manager will run first and will submit the tasks for 
                //the job.  The job manager is the executable which manages the lifetime of the job
                //and all tasks which should run for the job.  In this case, the job manager submits the mapper and reducer tasks.
                List<ResourceFile> jobManagerResourceFiles = SampleHelpers.GetResourceFiles(containerSasUrl, Constants.RequiredExecutableFiles);
                const string jobManagerTaskId = "JobManager";

                JobManagerTask jobManagerTask = new JobManagerTask()
                    {
                        ResourceFiles = jobManagerResourceFiles,
                        CommandLine = Constants.JobManagerExecutable,

                        //Determines if the job should terminate when the job manager process exits.
                        KillJobOnCompletion = true,
                        Id = jobManagerTaskId
                    };

                //Create the unbound job in local memory.  An object which exists only in local memory (and not on the Batch Service) is "unbound".
                string jobId = Environment.GetEnvironmentVariable("USERNAME") + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");

                CloudJob unboundJob = batchClient.JobOperations.CreateJob(jobId, poolInformation);
                unboundJob.JobManagerTask = jobManagerTask; //Assign the job manager task to this job

                try
                {
                    //Commit the unbound job to the Batch Service.
                    Console.WriteLine("Adding job: {0} to the Batch Service.", unboundJob.Id);
                    await unboundJob.CommitAsync(); //Issues a request to the Batch Service to add the job which was defined above.

                    //
                    // Wait for the job manager task to complete.
                    //
                    
                    //An object which is backed by a corresponding Batch Service object is "bound."
                    CloudJob boundJob = await batchClient.JobOperations.GetJobAsync(jobId);

                    CloudTask boundJobManagerTask = await boundJob.GetTaskAsync(jobManagerTaskId);

                    TimeSpan maxJobCompletionTimeout = TimeSpan.FromMinutes(30);
                    
                    // Monitor the current tasks to see when they are done.
                    // Occasionally a task may get killed and requeued during an upgrade or hardware failure, including the job manager
                    // task.  The job manager will be re-run in this case.  Robustness against this was not added into the sample for 
                    // simplicity, but should be added into any production code.
                    Console.WriteLine("Waiting for job's tasks to complete");

                    TaskStateMonitor taskStateMonitor = batchClient.Utilities.CreateTaskStateMonitor();
                    bool timedOut = await taskStateMonitor.WhenAllAsync(new List<CloudTask> { boundJobManagerTask }, TaskState.Completed, maxJobCompletionTimeout);

                    Console.WriteLine("Done waiting for job manager task.");

                    await boundJobManagerTask.RefreshAsync();

                    //Check to ensure the job manager task exited successfully.
                    await Helpers.CheckForTaskSuccessAsync(boundJobManagerTask, dumpStandardOutOnTaskSuccess: false);

                    if (timedOut)
                    {
                        throw new TimeoutException(string.Format("Timed out waiting for job manager task to complete."));
                    }

                    //
                    // Download and write out the reducer tasks output
                    //

                    string reducerText = await SampleHelpers.DownloadBlobTextAsync(cloudStorageAccount, this.textSearchSettings.BlobContainer, Constants.ReducerTaskResultBlobName);
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
                        Console.WriteLine("Deleting job {0}", jobId);
                        batchClient.JobOperations.DeleteJob(jobId);
                    }

                    //Note that there were files uploaded to a container specified in the 
                    //configuration file.  This container will not be deleted or cleaned up by this sample.
                }
            }
        }

    }
}
