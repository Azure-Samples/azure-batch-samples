//Copyright (c) Microsoft Corporation

namespace Microsoft.Azure.Batch.Samples.JobManager
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Auth;
    using Common;
    using FileStaging;
    using WindowsAzure.Storage;
    using WindowsAzure.Storage.Auth;

    public class SampleJobManagerTask
    {
        private readonly JobManagerSettings configurationSettings;

        // The SimpleTask project is included via project-depedency, so the 
        // executable produced by that project will be in the same working 
        // directory as JobSubmitter at runtime.
        private const string SimpleTaskExe = "SimpleTask.exe";

        private readonly string accountName;
        private readonly string jobId;
        private readonly string taskId;

        public SampleJobManagerTask()
        {
            //Read some important data from preconfigured environment variables on the Batch compute node.
            this.accountName = Environment.GetEnvironmentVariable("AZ_BATCH_ACCOUNT_NAME");
            this.jobId = Environment.GetEnvironmentVariable("AZ_BATCH_JOB_ID");
            this.taskId = Environment.GetEnvironmentVariable("AZ_BATCH_TASK_ID");

            this.configurationSettings = new JobManagerSettings(
                this.accountName,
                Environment.GetEnvironmentVariable("SAMPLE_BATCH_KEY"),
                Environment.GetEnvironmentVariable("SAMPLE_BATCH_URL"),
                Environment.GetEnvironmentVariable("SAMPLE_STORAGE_ACCOUNT"),
                Environment.GetEnvironmentVariable("SAMPLE_STORAGE_KEY"),
                Environment.GetEnvironmentVariable("SAMPLE_STORAGE_URL"));
        }

        public async Task RunAsync()
        {
            Console.WriteLine("JobManager for account: {0}, job: {1} has started...",
                this.accountName,
                this.jobId);
            Console.WriteLine();

            Console.WriteLine("JobManager running with the following settings: ");
            Console.WriteLine("----------------------------------------");
            Console.WriteLine(this.configurationSettings.ToString());
            
            BatchSharedKeyCredentials credentials = new BatchSharedKeyCredentials(
                this.configurationSettings.BatchAccountUrl, 
                this.configurationSettings.BatchAccountName,
                this.configurationSettings.BatchAccountKey);

            CloudStorageAccount storageAccount = new CloudStorageAccount(
                new StorageCredentials(
                    this.configurationSettings.StorageAccountName,
                    this.configurationSettings.StorageAccountKey), 
                this.configurationSettings.StorageAccountUrl,
                useHttps: true);
            
            using (BatchClient batchClient = BatchClient.Open(credentials))
            {
                HashSet<string> blobContainerNames = new HashSet<string>();

                try
                {
                    // Submit some tasks
                    blobContainerNames = await this.SubmitTasks(batchClient, storageAccount);

                    // Wait for the tasks to finish
                    List<CloudTask> tasks = await batchClient.JobOperations.ListTasks(jobId).ToListAsync();

                    // don't wait for the job manager task since it won't finish until this method exists
                    tasks.RemoveAll(t => t.Id.Equals(this.taskId, StringComparison.CurrentCultureIgnoreCase));
                    
                    await GettingStartedCommon.WaitForTasksAndPrintOutputAsync(batchClient, tasks, TimeSpan.FromMinutes(10));
                }
                finally
                {
                    // Clean up the files for the tasks
                    SampleHelpers.DeleteContainersAsync(storageAccount, blobContainerNames).Wait();
                }
            }
        }

        /// <summary>
        /// Submits a set of tasks to the job
        /// </summary>
        /// <param name="batchClient">The batch client to use.</param>
        /// <param name="cloudStorageAccount">The storage account to upload files to.</param>
        /// <returns>The set of blob artifacts created by file staging.</returns>
        private async Task<HashSet<string>> SubmitTasks(BatchClient batchClient, CloudStorageAccount cloudStorageAccount)
        {
            List<CloudTask> tasksToRun = new List<CloudTask>();

            // Create a task which requires some resource files
            CloudTask taskWithFiles = new CloudTask("task_with_file1", SimpleTaskExe);
            
            // Set up a collection of files to be staged -- these files will be uploaded to Azure Storage
            // when the tasks are submitted to the Azure Batch service.
            taskWithFiles.FilesToStage = new List<IFileStagingProvider>();

            // generate a local file in temp directory
            string localSampleFilePath = GettingStartedCommon.GenerateTemporaryFile("HelloWorld.txt", "hello from Batch JobManager sample!");
            
            StagingStorageAccount fileStagingStorageAccount = new StagingStorageAccount(
                storageAccount: this.configurationSettings.StorageAccountName,
                storageAccountKey: this.configurationSettings.StorageAccountKey,
                blobEndpoint: cloudStorageAccount.BlobEndpoint.ToString());

            // add the files as a task dependency so they will be uploaded to storage before the task 
            // is submitted and downloaded to the node before the task starts execution.
            FileToStage helloWorldFile = new FileToStage(localSampleFilePath, fileStagingStorageAccount);
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
