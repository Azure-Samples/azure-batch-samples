//Copyright (c) Microsoft Corporation

namespace Microsoft.Azure.Batch.Samples.JobManager
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using global::Azure.Compute.Batch;
    using global::Azure.Storage.Blobs;
    using Microsoft.Azure.Batch.Samples.Common;

    public class SampleJobManagerTask
    {
        private readonly JobManagerSettings configurationSettings;

        // The SimpleTask project's exe will be uploaded by the JobSubmitter as a job-manager
        // resource file (along with the rest of the JobSubmitter output directory contents),
        // so it will exist in the JobManager working directory at runtime.
        private const string SimpleTaskExe = "SimpleTask.exe";

        private readonly string accountName;
        private readonly string jobId;
        private readonly string taskId;

        public SampleJobManagerTask()
        {
            this.accountName = Environment.GetEnvironmentVariable("AZ_BATCH_ACCOUNT_NAME");
            this.jobId = Environment.GetEnvironmentVariable("AZ_BATCH_JOB_ID");
            this.taskId = Environment.GetEnvironmentVariable("AZ_BATCH_TASK_ID");

            this.configurationSettings = new JobManagerSettings(
                this.accountName,
                Environment.GetEnvironmentVariable("SAMPLE_BATCH_URL"),
                Environment.GetEnvironmentVariable("SAMPLE_STORAGE_ACCOUNT"),
                Environment.GetEnvironmentVariable("SAMPLE_STORAGE_URL"));
        }

        public async Task RunAsync()
        {
            Console.WriteLine("JobManager for account: {0}, job: {1} has started...", this.accountName, this.jobId);
            Console.WriteLine();
            Console.WriteLine("JobManager running with the following settings: ");
            Console.WriteLine("----------------------------------------");
            Console.WriteLine(this.configurationSettings.ToString());

            var accountSettings = new AccountSettings
            {
                BatchAccountName = this.configurationSettings.BatchAccountName,
                BatchServiceUrl = this.configurationSettings.BatchAccountUrl,
                StorageAccountName = this.configurationSettings.StorageAccountName,
                StorageServiceUrl = this.configurationSettings.StorageAccountUrl,
            };

            BatchClient batchClient = ClientFactory.CreateBatchClient(accountSettings);
            BlobServiceClient blobServiceClient = ClientFactory.CreateBlobServiceClient(accountSettings);

            HashSet<string> blobContainerNames = new HashSet<string>();
            try
            {
                List<string> taskIds = await this.SubmitTasks(batchClient, blobServiceClient, blobContainerNames);

                // Don't wait for ourselves (the job manager task).
                taskIds.RemoveAll(id => id.Equals(this.taskId, StringComparison.CurrentCultureIgnoreCase));

                await GettingStartedCommon.WaitForTasksAndPrintOutputAsync(batchClient, this.jobId, taskIds, TimeSpan.FromMinutes(10));
            }
            finally
            {
                await SampleHelpers.DeleteContainersAsync(blobServiceClient, blobContainerNames);
            }
        }

        /// <summary>
        /// Submits a set of tasks to the job.
        /// </summary>
        private async Task<List<string>> SubmitTasks(BatchClient batchClient, BlobServiceClient blobServiceClient, HashSet<string> blobContainerNames)
        {
            string localSampleFilePath = GettingStartedCommon.GenerateTemporaryFile("HelloWorld.txt", "hello from Batch JobManager sample!");
            string simpleTaskPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SimpleTaskExe);

            string containerName = ("jobmgr-" + this.jobId).ToLowerInvariant();
            blobContainerNames.Add(containerName);

            List<ResourceFile> resourceFiles = await FileStager.StageFilesAsBlobsAsync(
                blobServiceClient,
                containerName,
                new[] { localSampleFilePath, simpleTaskPath });

            var taskOptions = new BatchTaskCreateOptions("task_with_file1", SimpleTaskExe);
            foreach (var rf in resourceFiles)
            {
                taskOptions.ResourceFiles.Add(rf);
            }

            await batchClient.CreateTaskAsync(this.jobId, taskOptions);

            return new List<string> { taskOptions.Id };
        }
    }
}
