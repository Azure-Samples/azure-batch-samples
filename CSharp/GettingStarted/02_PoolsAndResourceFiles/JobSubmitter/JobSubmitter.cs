//Copyright (c) Microsoft Corporation

namespace Microsoft.Azure.Batch.Samples.PoolsAndResourceFiles
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using Common;
    using Microsoft.Azure.Batch;
    using Microsoft.Azure.Batch.Auth;
    using Microsoft.Azure.Batch.FileStaging;
    using Microsoft.Extensions.Configuration;
    using WindowsAzure.Storage;
    using WindowsAzure.Storage.Auth;

    /// <summary>
    /// Manages submission and lifetime of the Azure Batch job and pool.
    /// </summary>
    public class JobSubmitter
    {
        private readonly Settings poolsAndResourceFileSettings;
        private readonly AccountSettings accountSettings;

        // The SimpleTask project is included via project-depedency, so the 
        // executable produced by that project will be in the same working 
        // directory as JobSubmitter at runtime.
        private const string SimpleTaskExe = "SimpleTask.exe";

        public JobSubmitter()
        {
            this.accountSettings = SampleHelpers.LoadAccountSettings();
            this.poolsAndResourceFileSettings = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("settings.json")
                .Build()
                .Get<Settings>();
        }

        public JobSubmitter(AccountSettings accountSettings, Settings settings)
        {
            this.poolsAndResourceFileSettings = settings;
            this.accountSettings = accountSettings;
        }

        /// <summary>
        /// Populates Azure Storage with the required files, and 
        /// submits the job to the Azure Batch service.
        /// </summary>
        public async Task RunAsync()
        {
            Console.WriteLine("Running with the following settings: ");
            Console.WriteLine("-------------------------------------");
            Console.WriteLine(this.poolsAndResourceFileSettings.ToString());
            Console.WriteLine(this.accountSettings.ToString());

            // Set up the Batch Service credentials used to authenticate with the Batch Service.
            BatchSharedKeyCredentials credentials = new BatchSharedKeyCredentials(
                this.accountSettings.BatchServiceUrl,
                this.accountSettings.BatchAccountName,
                this.accountSettings.BatchAccountKey);

            // Delete the blob containers which contain the task input files since we no longer need them
            CloudStorageAccount cloudStorageAccount = new CloudStorageAccount(
                new StorageCredentials(this.accountSettings.StorageAccountName,
                    this.accountSettings.StorageAccountKey),
                    this.accountSettings.StorageServiceUrl,
                    useHttps: true);

            // Get an instance of the BatchClient for a given Azure Batch account.
            using (BatchClient batchClient = BatchClient.Open(credentials))
            {
                string jobId = null;

                // Track the containers which are created as part of job submission so that we can clean them up later.
                HashSet<string> blobContainerNames = new HashSet<string>();

                try
                {
                    // Allocate a pool
                    await this.CreatePoolIfNotExistAsync(batchClient, cloudStorageAccount);

                    // Submit the job
                    jobId = GettingStartedCommon.CreateJobId("SimpleJob");
                    blobContainerNames = await this.SubmitJobAsync(batchClient, cloudStorageAccount, jobId);

                    // Print out the status of the pools/jobs under this account
                    await GettingStartedCommon.PrintJobsAsync(batchClient);
                    await GettingStartedCommon.PrintPoolsAsync(batchClient);

                    // Wait for the job to complete
                    List<CloudTask> tasks = await batchClient.JobOperations.ListTasks(jobId).ToListAsync();
                    await GettingStartedCommon.WaitForTasksAndPrintOutputAsync(batchClient, tasks, TimeSpan.FromMinutes(10));
                }
                finally
                {
                    // Delete the pool (if configured) and job

                    // Delete Azure Storage container data
                    await SampleHelpers.DeleteContainersAsync(cloudStorageAccount, blobContainerNames);

                    // Delete Azure Batch resources
                    List<string> jobIdsToDelete = new List<string>();
                    List<string> poolIdsToDelete = new List<string>();

                    if (this.poolsAndResourceFileSettings.ShouldDeleteJob)
                    {
                        jobIdsToDelete.Add(jobId);
                    }

                    if (this.poolsAndResourceFileSettings.ShouldDeletePool)
                    {
                        poolIdsToDelete.Add(this.poolsAndResourceFileSettings.PoolId);
                    }

                    await SampleHelpers.DeleteBatchResourcesAsync(batchClient, jobIdsToDelete, poolIdsToDelete);
                }
            }
        }

        /// <summary>
        /// Creates a pool if it doesn't already exist.  If the pool already exists, this method resizes it to meet the expected
        /// targets specified in settings.
        /// </summary>
        /// <param name="batchClient">The BatchClient to use when interacting with the Batch service.</param>
        /// <param name="cloudStorageAccount">The CloudStorageAccount to upload start task required files to.</param>
        /// <returns>An asynchronous <see cref="Task"/> representing the operation.</returns>
        private async Task CreatePoolIfNotExistAsync(BatchClient batchClient, CloudStorageAccount cloudStorageAccount)
        {
            // You can learn more about os families and versions at:
            // https://azure.microsoft.com/en-us/documentation/articles/cloud-services-guestos-update-matrix/
            CloudPool pool = batchClient.PoolOperations.CreatePool(
                poolId: this.poolsAndResourceFileSettings.PoolId,
                targetDedicatedComputeNodes: this.poolsAndResourceFileSettings.PoolTargetNodeCount,
                virtualMachineSize: this.poolsAndResourceFileSettings.PoolNodeVirtualMachineSize,
                cloudServiceConfiguration: new CloudServiceConfiguration(this.poolsAndResourceFileSettings.PoolOsFamily));

            // Create a new start task to facilitate pool-wide file management or installation.
            // In this case, we just add a single dummy data file to the StartTask.
            string localSampleFilePath = GettingStartedCommon.GenerateTemporaryFile("StartTask.txt", "hello from Batch PoolsAndResourceFiles sample!");
            List<string> files = new List<string> { localSampleFilePath };

            List<ResourceFile> resourceFiles = await SampleHelpers.UploadResourcesAndCreateResourceFileReferencesAsync(
                cloudStorageAccount,
                this.poolsAndResourceFileSettings.BlobContainer,
                files);

            pool.StartTask = new StartTask()
            {
                CommandLine = "cmd /c dir",
                ResourceFiles = resourceFiles
            };

            await GettingStartedCommon.CreatePoolIfNotExistAsync(batchClient, pool);
        }

        /// <summary>
        /// Creates a job and adds a task to it. The task is a 
        /// custom executable which has a resource file associated with it.
        /// </summary>
        /// <param name="batchClient">The BatchClient to use when interacting with the Batch service.</param>
        /// <param name="cloudStorageAccount">The storage account to upload the files to.</param>
        /// <param name="jobId">The ID of the job.</param>
        /// <returns>The set of container names containing the jobs input files.</returns>
        private async Task<HashSet<string>> SubmitJobAsync(BatchClient batchClient, CloudStorageAccount cloudStorageAccount, string jobId)
        {
            // create an empty unbound Job
            CloudJob unboundJob = batchClient.JobOperations.CreateJob();
            unboundJob.Id = jobId;
            unboundJob.PoolInformation = new PoolInformation() { PoolId = this.poolsAndResourceFileSettings.PoolId };

            // Commit Job to create it in the service
            await unboundJob.CommitAsync();

            List<CloudTask> tasksToRun = new List<CloudTask>();

            // Create a task which requires some resource files
            CloudTask taskWithFiles = new CloudTask("task_with_file1", SimpleTaskExe);

            // Set up a collection of files to be staged -- these files will be uploaded to Azure Storage
            // when the tasks are submitted to the Azure Batch service.
            taskWithFiles.FilesToStage = new List<IFileStagingProvider>();
            
            // generate a local file in temp directory
            string localSampleFile = Path.Combine(Environment.GetEnvironmentVariable("TEMP"), "HelloWorld.txt");
            File.WriteAllText(localSampleFile, "hello from Batch PoolsAndResourceFiles sample!");

            StagingStorageAccount fileStagingStorageAccount = new StagingStorageAccount(
                storageAccount: this.accountSettings.StorageAccountName,
                storageAccountKey: this.accountSettings.StorageAccountKey,
                blobEndpoint: cloudStorageAccount.BlobEndpoint.ToString());

            // add the files as a task dependency so they will be uploaded to storage before the task 
            // is submitted and downloaded to the node before the task starts execution.
            FileToStage helloWorldFile = new FileToStage(localSampleFile, fileStagingStorageAccount);
            FileToStage simpleTaskFile = new FileToStage(SimpleTaskExe, fileStagingStorageAccount);

            // When this task is added via JobOperations.AddTaskAsync below, the FilesToStage are uploaded to storage once.
            // The Batch service does not automatically delete content from your storage account, so files added in this 
            // way must be manually removed when they are no longer used.
            taskWithFiles.FilesToStage.Add(helloWorldFile);
            taskWithFiles.FilesToStage.Add(simpleTaskFile);

            tasksToRun.Add(taskWithFiles);

            var fileStagingArtifacts = new ConcurrentBag<ConcurrentDictionary<Type, IFileStagingArtifact>>();
            
            // Use the AddTask method which takes an enumerable of tasks for best performance, as it submits up to 100
            // tasks at once in a single request.  If the list of tasks is N where N > 100, this will correctly parallelize 
            // the requests and return when all N tasks have been added.
            await batchClient.JobOperations.AddTaskAsync(jobId, tasksToRun, fileStagingArtifacts: fileStagingArtifacts);

            // Extract the names of the blob containers from the file staging artifacts
            HashSet<string> blobContainerNames = GettingStartedCommon.ExtractBlobContainerNames(fileStagingArtifacts);
            return blobContainerNames;
        }
    }
}
