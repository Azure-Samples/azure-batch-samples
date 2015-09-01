namespace Microsoft.Azure.Batch.Samples.PoolsAndResourceFiles
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using Common;
    using GettingStarted.Common;
    using Microsoft.Azure.Batch;
    using Microsoft.Azure.Batch.Auth;
    using Microsoft.Azure.Batch.Common;
    using Microsoft.Azure.Batch.FileStaging;
    using WindowsAzure.Storage;
    using WindowsAzure.Storage.Auth;

    /// <summary>
    /// Manages submission and lifetime of the Azure Batch job and pool.
    /// </summary>
    public class JobSubmitter
    {
        private readonly Settings configurationSettings;

        // The SimpleTask project is included via project-depedency, so the 
        // executable produced by that project will be in the same working 
        // directory as JobSubmitter at runtime.
        private const string SimpleTaskExe = "SimpleTask.exe";

        public JobSubmitter()
        {
            this.configurationSettings = Settings.Default;
        }

        public JobSubmitter(Settings settings)
        {
            this.configurationSettings = settings;
        }

        /// <summary>
        /// Populates Azure Storage with the required files, and 
        /// submits the job to the Azure Batch service.
        /// </summary>
        public async Task RunAsync()
        {
            Console.WriteLine("Running with the following settings: ");
            Console.WriteLine("-------------------------------------");
            Console.WriteLine(this.configurationSettings.ToString());

            // Set up the Batch Service credentials used to authenticate with the Batch Service.
            BatchSharedKeyCredentials credentials = new BatchSharedKeyCredentials(
                this.configurationSettings.BatchServiceUrl,
                this.configurationSettings.BatchAccountName,
                this.configurationSettings.BatchAccountKey);

            // Get an instance of the BatchClient for a given Azure Batch account.
            using (BatchClient batchClient = await BatchClient.OpenAsync(credentials))
            {
                // add a retry policy. The built-in policies are No Retry (default), Linear Retry, and Exponential Retry
                batchClient.CustomBehaviors.Add(RetryPolicyProvider.LinearRetryProvider(TimeSpan.FromSeconds(10), 3));

                string jobId = null;

                // Track the containers which are created as part of job submission so that we can clean them up later.
                HashSet<string> blobContainerNames = new HashSet<string>();

                try
                {
                    // Allocate a pool
                    await this.CreatePoolIfNotExistAsync(batchClient);

                    // Submit the job
                    jobId = GettingStartedCommon.CreateJobId("SimpleJob");
                    blobContainerNames = await this.SubmitJobAsync(batchClient, jobId);

                    // Print out the status of the pools/jobs under this account
                    await GettingStartedCommon.PrintJobsAsync(batchClient);
                    await GettingStartedCommon.PrintPoolsAsync(batchClient);

                    // Wait for the job to complete
                    await GettingStartedCommon.WaitForJobAndPrintOutputAsync(batchClient, jobId);
                }
                finally
                {
                    // Delete the pool (if configured) and job
                    // TODO: In C# 6 we can await here instead of .Wait()
                    this.CleanupResourcesAsync(batchClient, jobId, blobContainerNames).Wait();
                }
            }
        }

        /// <summary>
        /// Creates a pool if it doesn't already exist.  If the pool already exists, this method resizes it to meet the expected
        /// targets specified in settings.
        /// </summary>
        /// <param name="batchClient">The BatchClient to use when interacting with the Batch service.</param>
        /// <returns>An asynchronous <see cref="Task"/> representing the operation.</returns>
        private async Task CreatePoolIfNotExistAsync(BatchClient batchClient)
        {
            bool successfullyCreatedPool = false;

            // Attempt to create the pool
            try
            {
                // Create an in-memory representation of the Batch pool which we would like to create.  We are free to modify/update 
                // this pool object in memory until we commit it to the service via the CommitAsync method.
                Console.WriteLine("Attempting to create pool: {0}", this.configurationSettings.PoolId);

                // You can learn more about os families and versions at:
                // https://azure.microsoft.com/en-us/documentation/articles/cloud-services-guestos-update-matrix/
                CloudPool pool = batchClient.PoolOperations.CreatePool(
                    poolId: this.configurationSettings.PoolId,
                    targetDedicated: this.configurationSettings.PoolTargetNodeCount,
                    virtualMachineSize: this.configurationSettings.PoolNodeVirtualMachineSize,
                    osFamily: this.configurationSettings.PoolOSFamily);

                // Create a new start task to facilitate pool-wide file management or installation.
                // In this case, we just add a single dummy data file to the StartTask.
                const string startTaskFileName = "StartTask.txt";
                string localSampleFile = Path.Combine(Environment.GetEnvironmentVariable("TEMP"), startTaskFileName);
                File.WriteAllText(localSampleFile, "hello from Batch PoolsAndResourceFiles sample!");
                List<string> files = new List<string> { localSampleFile };

                // Upload the file for the start task to Azure Storage
                CloudStorageAccount cloudStorageAccount = new CloudStorageAccount(
                    new StorageCredentials(this.configurationSettings.StorageAccountName,
                        this.configurationSettings.StorageAccountKey), 
                    new Uri(this.configurationSettings.StorageBlobEndpoint),
                    null,
                    null,
                    null);

                await SampleHelpers.UploadResourcesAsync(
                    cloudStorageAccount, 
                    this.configurationSettings.BlobContainer,
                    files);

                // Generate resource file references to the blob we just uploaded
                string containerSas = SampleHelpers.ConstructContainerSas(cloudStorageAccount, this.configurationSettings.BlobContainer);

                List<ResourceFile> startTaskResourceFiles = SampleHelpers.GetResourceFiles(containerSas, new List<string> { startTaskFileName });

                pool.StartTask = new StartTask()
                    {
                        CommandLine = "cmd /c dir",
                        ResourceFiles = startTaskResourceFiles
                    };

                // Create the pool on the Batch Service
                await pool.CommitAsync();

                successfullyCreatedPool = true;
                Console.WriteLine("Created pool {0} with {1} {2} nodes",
                    pool,
                    this.configurationSettings.PoolTargetNodeCount,
                    this.configurationSettings.PoolNodeVirtualMachineSize);
            }
            catch (BatchException e)
            {
                // Swallow the specific error code PoolExists since that is expected if the pool already exists
                if (e.RequestInformation != null &&
                    e.RequestInformation.AzureError != null &&
                    e.RequestInformation.AzureError.Code == BatchErrorCodeStrings.PoolExists)
                {
                    // The pool already existed when we tried to create it
                    successfullyCreatedPool = false;
                    Console.WriteLine("The pool already existed when we tried to create it");
                }
                else
                {
                    throw; // Any other exception is unexpected
                }
            }

            // If the pool already existed, make sure that its targets are correct
            if (!successfullyCreatedPool)
            {
                CloudPool existingPool = await batchClient.PoolOperations.GetPoolAsync(this.configurationSettings.PoolId);

                // If the pool doesn't have the right number of nodes and it isn't resizing then we need
                // to ask it to resize
                if (existingPool.CurrentDedicated != this.configurationSettings.PoolTargetNodeCount &&
                    existingPool.AllocationState != AllocationState.Resizing)
                {
                    // Resize the pool to the desired target.  Note that provisioning the nodes in the pool may take some time
                    await existingPool.ResizeAsync(this.configurationSettings.PoolTargetNodeCount);
                }
            }
        }

        /// <summary>
        /// Creates a job and adds a task to it. The task is a 
        /// custom executable which has a resource file associated with it.
        /// </summary>
        /// <param name="batchClient">The BatchClient to use when interacting with the Batch service.</param>
        /// <param name="jobId">The ID of the job.</param>
        /// <returns>The set of container names containing the jobs input files.</returns>
        private async Task<HashSet<string>> SubmitJobAsync(BatchClient batchClient, string jobId)
        {
            // create an empty unbound Job
            CloudJob unboundJob = batchClient.JobOperations.CreateJob();
            unboundJob.Id = jobId;
            unboundJob.PoolInformation = new PoolInformation() { PoolId = this.configurationSettings.PoolId };

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
                storageAccount: this.configurationSettings.StorageAccountName,
                storageAccountKey: this.configurationSettings.StorageAccountKey,
                blobEndpoint: this.configurationSettings.StorageBlobEndpoint);

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

        /// <summary>
        /// Deletes the job and pool
        /// </summary>
        /// <param name="batchClient">The BatchClient to use when interacting with the Batch service.</param>
        /// <param name="jobId">The ID of the job.</param>
        /// <param name="blobContainerNames">The name of the containers created for the jobs resource files.</param>
        /// <returns>An asynchronous <see cref="Task"/> representing the operation.</returns>
        private async Task CleanupResourcesAsync(BatchClient batchClient, string jobId, IEnumerable<string> blobContainerNames)
        {
            // Delete the blob containers which contain the task input files since we no longer need them
            CloudStorageAccount storageAccount = new CloudStorageAccount(
                new StorageCredentials(this.configurationSettings.StorageAccountName,
                    this.configurationSettings.StorageAccountKey),
                    new Uri(this.configurationSettings.StorageBlobEndpoint),
                    null,
                    null,
                    null);

            await SampleHelpers.DeleteContainersAsync(storageAccount, blobContainerNames);

            // Delete the job to ensure the tasks are cleaned up
            if (!string.IsNullOrEmpty(jobId) && this.configurationSettings.ShouldDeleteJob)
            {
                Console.WriteLine("Deleting job: {0}", jobId);
                await batchClient.JobOperations.DeleteJobAsync(jobId);
            }

            if (this.configurationSettings.ShouldDeletePool)
            {
                Console.WriteLine("Deleting pool: {0}", this.configurationSettings.PoolId);
                await batchClient.PoolOperations.DeletePoolAsync(this.configurationSettings.PoolId);
            }
        }
    }
}
