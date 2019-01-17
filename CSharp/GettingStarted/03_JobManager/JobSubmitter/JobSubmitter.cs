//Copyright (c) Microsoft Corporation

namespace Microsoft.Azure.Batch.Samples.JobManager
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using Common;
    using Microsoft.Azure.Batch;
    using Microsoft.Azure.Batch.Auth;
    using Microsoft.Extensions.Configuration;
    using WindowsAzure.Storage;
    using WindowsAzure.Storage.Auth;

    /// <summary>
    /// Manages submission and lifetime of the Azure Batch job and pool.
    /// </summary>
    public class JobSubmitter
    {
        private readonly Settings jobManagerSettings;
        private readonly AccountSettings accountSettings;

        private const string JobManagerTaskExe = "SampleJobManagerTask.exe";
        private const string JobManagerTaskId = "SampleJobManager";
        private static readonly IReadOnlyList<string> JobManagerRequiredFiles = new List<string>()
        {
            JobManagerTaskExe,
            JobManagerTaskExe + ".config",
            "SampleJobManagerTask.pdb",
            "SimpleTask.exe",
            "Microsoft.Azure.Batch.Samples.Common.dll",
            "Microsoft.WindowsAzure.Storage.dll",
            "Microsoft.Azure.Batch.dll",
            "Microsoft.Azure.Batch.FileStaging.dll",
            "Microsoft.Rest.ClientRuntime.dll",
            "Microsoft.Rest.ClientRuntime.Azure.dll",
            "Newtonsoft.Json.dll",
            "System.Net.Http.dll"
        };

        public JobSubmitter()
        {
            this.jobManagerSettings = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("settings.json")
                .Build()
                .Get<Settings>();
            this.accountSettings = SampleHelpers.LoadAccountSettings();
        }

        public JobSubmitter(AccountSettings accountSettings, Settings settings)
        {
            this.jobManagerSettings = settings;
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
            Console.WriteLine(this.jobManagerSettings.ToString());
            Console.WriteLine(this.accountSettings.ToString());

            // Set up the Batch Service credentials used to authenticate with the Batch Service.
            BatchSharedKeyCredentials credentials = new BatchSharedKeyCredentials(
                this.accountSettings.BatchServiceUrl,
                this.accountSettings.BatchAccountName,
                this.accountSettings.BatchAccountKey);

            CloudStorageAccount cloudStorageAccount = new CloudStorageAccount(
                new StorageCredentials(this.accountSettings.StorageAccountName,
                    this.accountSettings.StorageAccountKey),
                    this.accountSettings.StorageServiceUrl,
                    useHttps: true);

            // Get an instance of the BatchClient for a given Azure Batch account.
            using (BatchClient batchClient = BatchClient.Open(credentials))
            {
                // add a retry policy. The built-in policies are No Retry (default), Linear Retry, and Exponential Retry
                batchClient.CustomBehaviors.Add(RetryPolicyProvider.ExponentialRetryProvider(TimeSpan.FromSeconds(5), 3));

                string jobId = null;

                try
                {
                    // Allocate a pool
                    await this.CreatePoolIfNotExistAsync(batchClient, cloudStorageAccount);

                    // Submit the job
                    jobId = GettingStartedCommon.CreateJobId("SimpleJob");
                    await this.SubmitJobAsync(batchClient, cloudStorageAccount, jobId);

                    // Print out the status of the pools/jobs under this account
                    await GettingStartedCommon.PrintJobsAsync(batchClient);
                    await GettingStartedCommon.PrintPoolsAsync(batchClient);

                    // Wait for the job manager to complete
                    CloudTask jobManagerTask = await batchClient.JobOperations.GetTaskAsync(jobId, JobManagerTaskId);
                    await GettingStartedCommon.WaitForTasksAndPrintOutputAsync(batchClient,
                        new List<CloudTask> {jobManagerTask}, TimeSpan.FromMinutes(10));
                }
                finally
                {
                    // Delete Azure Batch resources
                    List<string> jobIdsToDelete = new List<string>();
                    List<string> poolIdsToDelete = new List<string>();

                    if (this.jobManagerSettings.ShouldDeleteJob)
                    {
                        jobIdsToDelete.Add(jobId);
                    }

                    if (this.jobManagerSettings.ShouldDeletePool)
                    {
                        poolIdsToDelete.Add(this.jobManagerSettings.PoolId);
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
                poolId: this.jobManagerSettings.PoolId,
                targetDedicatedComputeNodes: this.jobManagerSettings.PoolTargetNodeCount,
                virtualMachineSize: this.jobManagerSettings.PoolNodeVirtualMachineSize,
                cloudServiceConfiguration: new CloudServiceConfiguration(this.jobManagerSettings.PoolOsFamily));

            // Create a new start task to facilitate pool-wide file management or installation.
            // In this case, we just add a single dummy data file to the StartTask.
            string localSampleFilePath = GettingStartedCommon.GenerateTemporaryFile("StartTask.txt", "hello from Batch JobManager sample!");
            List<string> files = new List<string> { localSampleFilePath };
            
            List<ResourceFile> resourceFiles = await SampleHelpers.UploadResourcesAndCreateResourceFileReferencesAsync(
                cloudStorageAccount,
                this.jobManagerSettings.BlobContainer,
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
        /// <param name="storageAccount">The cloud storage account to upload files to.</param>
        /// <param name="jobId">The ID of the job.</param>
        /// <returns>An asynchronous <see cref="Task"/> representing the operation.</returns>
        private async Task SubmitJobAsync(BatchClient batchClient, CloudStorageAccount storageAccount, string jobId)
        {
            // create an empty unbound Job
            CloudJob unboundJob = batchClient.JobOperations.CreateJob();
            unboundJob.Id = jobId;
            unboundJob.PoolInformation = new PoolInformation() { PoolId = this.jobManagerSettings.PoolId };
            
            // Upload the required files for the job manager task
            await SampleHelpers.UploadResourcesAsync(storageAccount, this.jobManagerSettings.BlobContainer, JobManagerRequiredFiles);

            List<ResourceFile> jobManagerResourceFiles = await SampleHelpers.UploadResourcesAndCreateResourceFileReferencesAsync(
                storageAccount,
                this.jobManagerSettings.BlobContainer,
                JobManagerRequiredFiles);

            // Set up the JobManager environment settings
            List<EnvironmentSetting> jobManagerEnvironmentSettings = new List<EnvironmentSetting>()
            {
                // No need to pass the batch account name as an environment variable since the batch service provides
                // an environment variable for each task which contains the account name

                new EnvironmentSetting("SAMPLE_BATCH_KEY", this.accountSettings.BatchAccountKey),
                new EnvironmentSetting("SAMPLE_BATCH_URL", this.accountSettings.BatchServiceUrl),

                new EnvironmentSetting("SAMPLE_STORAGE_ACCOUNT", this.accountSettings.StorageAccountName),
                new EnvironmentSetting("SAMPLE_STORAGE_KEY", this.accountSettings.StorageAccountKey),
                new EnvironmentSetting("SAMPLE_STORAGE_URL", this.accountSettings.StorageServiceUrl),
            };

            unboundJob.JobManagerTask = new JobManagerTask()
            {
                Id = JobManagerTaskId,
                CommandLine = JobManagerTaskExe,
                ResourceFiles = jobManagerResourceFiles,
                KillJobOnCompletion = true,
                EnvironmentSettings = jobManagerEnvironmentSettings
            };

            // Commit Job to create it in the service
            await unboundJob.CommitAsync();
        }
    }
}
