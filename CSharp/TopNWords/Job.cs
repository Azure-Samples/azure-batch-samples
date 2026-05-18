// Copyright (c) Microsoft Corporation

namespace Microsoft.Azure.Batch.Samples.TopNWordsSample
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using global::Azure;
    using global::Azure.Compute.Batch;
    using global::Azure.ResourceManager.Batch;
    using global::Azure.ResourceManager.Batch.Models;
    using global::Azure.Storage.Blobs;
    using global::Azure.Storage.Sas;
    using Microsoft.Azure.Batch.Samples.Common;
    using Microsoft.Extensions.Configuration;

    /// <summary>
    /// In this sample the Batch service is used to process an input blob in parallel on multiple
    /// compute nodes. Each task computes the top-N words for the input blob.
    /// </summary>
    public static class Job
    {
        private const string TopNWordsExeName = "TopNWords.exe";
        private const string BooksContainerName = "books";

        public static void JobMain(string[] args)
        {
            JobMainAsync(args).GetAwaiter().GetResult();
        }

        private static async Task JobMainAsync(string[] args)
        {
            Settings topNWordsConfiguration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("settings.json")
                .Build()
                .Get<Settings>();
            AccountSettings accountSettings = SampleHelpers.LoadAccountSettings();

            BatchClient batchClient = ClientFactory.CreateBatchClient(accountSettings);
            BatchAccountResource batchAccount = SampleHelpers.GetBatchAccountResource(accountSettings);
            BlobServiceClient blobServiceClient = ClientFactory.CreateBlobServiceClient(accountSettings);

            string stagingContainer = $"topnwords-staging-{Guid.NewGuid():N}";

            try
            {
                // Create the pool via ARM.
                Console.WriteLine("Adding pool {0}", topNWordsConfiguration.PoolId);
                BatchAccountPoolData poolData = new BatchAccountPoolData
                {
                    VmSize = topNWordsConfiguration.PoolNodeVirtualMachineSize,
                    DeploymentVmConfiguration = new BatchVmConfiguration(
                        new BatchImageReference
                        {
                            Publisher = topNWordsConfiguration.ImagePublisher,
                            Offer = topNWordsConfiguration.ImageOffer,
                            Sku = topNWordsConfiguration.ImageSku,
                            Version = topNWordsConfiguration.ImageVersion,
                        },
                        topNWordsConfiguration.NodeAgentSkuId),
                    ScaleSettings = new BatchAccountPoolScaleSettings
                    {
                        FixedScale = new BatchAccountFixedScaleSettings
                        {
                            TargetDedicatedNodes = topNWordsConfiguration.PoolNodeCount,
                        },
                    },
                };
                await GettingStartedCommon.CreatePoolIfNotExistAsync(batchAccount, topNWordsConfiguration.PoolId, poolData);

                // Create the job.
                Console.WriteLine("Creating job: " + topNWordsConfiguration.JobId);
                try
                {
                    await batchClient.CreateJobAsync(new BatchJobCreateOptions(
                        topNWordsConfiguration.JobId,
                        new BatchPoolInfo { PoolId = topNWordsConfiguration.PoolId }));
                }
                catch (RequestFailedException ex) when (ex.ErrorCode == BatchErrorCode.JobExists.ToString())
                {
                    Console.WriteLine("Job {0} already exists.", topNWordsConfiguration.JobId);
                }

                // Upload the book to a blob and get a read-only SAS URL.
                string bookSasUri = await UploadBookFileToCloudBlobAsync(blobServiceClient, topNWordsConfiguration.FileName);
                Console.WriteLine("{0} uploaded to cloud", topNWordsConfiguration.FileName);

                // Stage the executable + its dependencies (every .exe / .dll next to TopNWords.exe).
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                List<string> filesToStage = Directory.EnumerateFiles(baseDir, "*.dll")
                    .Concat(Directory.EnumerateFiles(baseDir, "*.exe"))
                    .Concat(Directory.EnumerateFiles(baseDir, "*.config"))
                    .ToList();
                List<ResourceFile> stagedFiles = await FileStager.StageFilesAsContainerAsync(
                    blobServiceClient,
                    stagingContainer,
                    filesToStage);

                // Build the task collection.
                var tasksToRun = new List<BatchTaskCreateOptions>(topNWordsConfiguration.NumberOfTasks);
                for (int i = 1; i <= topNWordsConfiguration.NumberOfTasks; i++)
                {
                    var task = new BatchTaskCreateOptions(
                        $"task_no_{i}",
                        $"{TopNWordsExeName} --Task {bookSasUri} {topNWordsConfiguration.TopWordCount}");
                    foreach (ResourceFile rf in stagedFiles)
                    {
                        task.ResourceFiles.Add(rf);
                    }
                    tasksToRun.Add(task);
                }

                // Bulk submit.
                const int batchSize = 100;
                for (int offset = 0; offset < tasksToRun.Count; offset += batchSize)
                {
                    int count = Math.Min(batchSize, tasksToRun.Count - offset);
                    var slice = tasksToRun.GetRange(offset, count);
                    await batchClient.CreateTaskCollectionAsync(topNWordsConfiguration.JobId, new BatchTaskGroup(slice));
                }

                Console.Write("Waiting for tasks to complete ...   ");
                await WaitForAllTasksCompletedAsync(batchClient, topNWordsConfiguration.JobId, TimeSpan.FromMinutes(20));
                Console.WriteLine("tasks are done.");

                await foreach (BatchTask t in batchClient.GetTasksAsync(topNWordsConfiguration.JobId, select: new[] { "id" }))
                {
                    Console.WriteLine("Task " + t.Id);
                    Console.WriteLine("stdout:" + Environment.NewLine + await ReadNodeFileAsync(batchClient, topNWordsConfiguration.JobId, t.Id, "stdout.txt"));
                    Console.WriteLine();
                    Console.WriteLine("stderr:" + Environment.NewLine + await ReadNodeFileAsync(batchClient, topNWordsConfiguration.JobId, t.Id, "stderr.txt"));
                }
            }
            finally
            {
                if (topNWordsConfiguration.ShouldDeletePool)
                {
                    Console.WriteLine("Deleting pool: {0}", topNWordsConfiguration.PoolId);
                    BatchAccountPoolResource pool = await batchAccount.GetBatchAccountPools().GetAsync(topNWordsConfiguration.PoolId);
                    await pool.DeleteAsync(WaitUntil.Started);
                }

                if (topNWordsConfiguration.ShouldDeleteJob)
                {
                    Console.WriteLine("Deleting job: {0}", topNWordsConfiguration.JobId);
                    await batchClient.DeleteJobAsync(WaitUntil.Started, topNWordsConfiguration.JobId);
                }

                if (topNWordsConfiguration.ShouldDeleteContainer)
                {
                    await DeleteContainersAsync(blobServiceClient, stagingContainer);
                }
            }
        }

        private static async Task DeleteContainersAsync(BlobServiceClient blobServiceClient, string fileStagingContainer)
        {
            Console.WriteLine("Deleting container: " + BooksContainerName);
            await blobServiceClient.GetBlobContainerClient(BooksContainerName).DeleteIfExistsAsync();

            if (!string.IsNullOrEmpty(fileStagingContainer))
            {
                Console.WriteLine("Deleting container: {0}", fileStagingContainer);
                await blobServiceClient.GetBlobContainerClient(fileStagingContainer).DeleteIfExistsAsync();
            }
        }

        private static async Task<string> UploadBookFileToCloudBlobAsync(BlobServiceClient blobServiceClient, string fileName)
        {
            BlobContainerClient container = blobServiceClient.GetBlobContainerClient(BooksContainerName);
            await container.CreateIfNotExistsAsync();

            BlobClient blob = container.GetBlobClient(fileName);
            using (FileStream fs = File.OpenRead(fileName))
            {
                await blob.UploadAsync(fs, overwrite: true);
            }

            Uri sasUri = await Microsoft.Azure.Batch.Samples.Common.FileStager.GenerateBlobSasUriAsync(
                blobServiceClient,
                blob,
                BlobSasPermissions.Read);
            return sasUri.ToString();
        }

        private static async Task<string> ReadNodeFileAsync(BatchClient batchClient, string jobId, string taskId, string fileName)
        {
            try
            {
                BinaryData data = await batchClient.GetTaskFileAsync(jobId, taskId, fileName);
                return data.ToString();
            }
            catch (RequestFailedException ex)
            {
                return $"<unable to read {fileName}: {ex.Message}>";
            }
        }

        private static async Task WaitForAllTasksCompletedAsync(BatchClient batchClient, string jobId, TimeSpan timeout)
        {
            DateTime timeoutAt = DateTime.UtcNow.Add(timeout);
            string[] select = new[] { "id", "state" };

            while (true)
            {
                bool allCompleted = true;
                bool anyTasks = false;

                await foreach (BatchTask task in batchClient.GetTasksAsync(jobId, select: select))
                {
                    anyTasks = true;
                    if (task.State != BatchTaskState.Completed)
                    {
                        allCompleted = false;
                        break;
                    }
                }

                if (anyTasks && allCompleted)
                {
                    return;
                }

                if (DateTime.UtcNow > timeoutAt)
                {
                    throw new TimeoutException($"Timed out waiting for tasks in job {jobId} to complete.");
                }

                await Task.Delay(TimeSpan.FromSeconds(15));
            }
        }
    }
}
