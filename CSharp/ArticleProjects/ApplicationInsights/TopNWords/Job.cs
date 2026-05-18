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
    /// In this sample the Batch service is used to process a set of input blobs in parallel on multiple
    /// compute nodes. Each task computes the top-N words for its corresponding blob.
    /// </summary>
    public static class Job
    {
        private const string TopNWordsExeName = "TopNWords.exe";
        private const string BooksContainerName = "documents";
        private const string AIBlobContainerName = "batchmonitoringassemblies";
        private const string BatchStartTaskTelemetryRunnerName = "Microsoft.Azure.Batch.Samples.TelemetryStartTask.exe";

        public static async Task JobMain(string[] args)
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
                // Upload Application Insights assemblies + telemetry start task and reference them as a single
                // container resource file used by the pool's start task.
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                List<string> aiFiles = new List<string>
                {
                    BatchStartTaskTelemetryRunnerName,
                    "ApplicationInsights.config",
                };
                aiFiles.AddRange(new[]
                {
                    "Microsoft.ApplicationInsights.dll",
                    "Microsoft.AI.Agent.Intercept.dll",
                    "Microsoft.AI.DependencyCollector.dll",
                    "Microsoft.AI.PerfCounterCollector.dll",
                    "Microsoft.AI.ServerTelemetryChannel.dll",
                    "Microsoft.AI.WindowsServer.dll",
                    "Microsoft.Azure.Batch.Samples.TelemetryInitializer.dll",
                });
                List<string> aiFullPaths = aiFiles
                    .Select(f => Path.Combine(baseDir, f))
                    .Where(File.Exists)
                    .ToList();
                List<ResourceFile> startTaskResources = await SampleHelpers.UploadResourcesAndCreateResourceFileReferencesAsync(
                    blobServiceClient,
                    AIBlobContainerName,
                    aiFullPaths);

                // Create the pool via ARM with the AI start task.
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
                    StartTask = new BatchAccountPoolStartTask
                    {
                        CommandLine = $"cmd /c {BatchStartTaskTelemetryRunnerName}",
                    },
                };
                foreach (ResourceFile rf in startTaskResources)
                {
                    poolData.StartTask.ResourceFiles.Add(new BatchResourceFile
                    {
                        BlobContainerUri = rf.StorageContainerUri,
                        HttpUri = rf.HttpUri,
                        FilePath = rf.FilePath,
                    });
                }

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

                // Upload all input documents to a "documents" container and produce per-blob SAS URLs.
                string[] documents = Directory.GetFiles(topNWordsConfiguration.DocumentsRootPath);
                List<ResourceFile> documentBlobs = await FileStager.StageFilesAsBlobsAsync(
                    blobServiceClient,
                    BooksContainerName,
                    documents);

                // Stage TopNWords binaries + AI assemblies into a single container used per task.
                List<string> binaries = Directory.EnumerateFiles(baseDir, "*.dll")
                    .Concat(Directory.EnumerateFiles(baseDir, "*.exe"))
                    .Concat(Directory.EnumerateFiles(baseDir, "*.config"))
                    .ToList();
                List<ResourceFile> binaryResources = await FileStager.StageFilesAsContainerAsync(
                    blobServiceClient,
                    stagingContainer,
                    binaries);

                // Build the task collection.
                var tasksToRun = new List<BatchTaskCreateOptions>(documentBlobs.Count);
                for (int i = 0; i < documentBlobs.Count; i++)
                {
                    ResourceFile docRf = documentBlobs[i];
                    string commandLine = $"{TopNWordsExeName} --Task {docRf.HttpUri} {topNWordsConfiguration.TopWordCount}";
                    var task = new BatchTaskCreateOptions("task_no_" + i, commandLine);
                    foreach (ResourceFile rf in binaryResources)
                    {
                        task.ResourceFiles.Add(rf);
                    }
                    tasksToRun.Add(task);
                }

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
                    Console.WriteLine("stdout:" + Environment.NewLine + await ReadTaskFileAsync(batchClient, topNWordsConfiguration.JobId, t.Id, "stdout.txt"));
                    Console.WriteLine();
                    Console.WriteLine("stderr:" + Environment.NewLine + await ReadTaskFileAsync(batchClient, topNWordsConfiguration.JobId, t.Id, "stderr.txt"));
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
                    Console.WriteLine("Deleting container: " + BooksContainerName);
                    await blobServiceClient.GetBlobContainerClient(BooksContainerName).DeleteIfExistsAsync();
                    if (!string.IsNullOrEmpty(stagingContainer))
                    {
                        Console.WriteLine("Deleting container: {0}", stagingContainer);
                        await blobServiceClient.GetBlobContainerClient(stagingContainer).DeleteIfExistsAsync();
                    }
                }
            }
        }

        private static async Task<string> ReadTaskFileAsync(BatchClient batchClient, string jobId, string taskId, string fileName)
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
