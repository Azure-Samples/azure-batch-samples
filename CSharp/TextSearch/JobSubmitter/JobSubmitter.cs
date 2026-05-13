// Copyright (c) Microsoft Corporation

namespace Microsoft.Azure.Batch.Samples.TextSearch
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using global::Azure;
    using global::Azure.Compute.Batch;
    using global::Azure.Storage.Blobs;
    using global::Azure.Storage.Sas;
    using Microsoft.Azure.Batch.Samples.Common;
    using Microsoft.Extensions.Configuration;

    /// <summary>
    /// Submits the job to the Batch Service and waits for it to complete.
    /// Once it has completed, it downloads the reducer task output and prints it to the console.
    /// </summary>
    public class JobSubmitter
    {
        private readonly Settings textSearchSettings;
        private readonly AccountSettings accountSettings;

        public JobSubmitter()
        {
            this.textSearchSettings = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("settings.json")
                .Build()
                .Get<Settings>();
            this.accountSettings = SampleHelpers.LoadAccountSettings();
        }

        public async Task RunAsync()
        {
            Console.WriteLine("Running with the following settings: ");
            Console.WriteLine("----------------------------------------");
            Console.WriteLine(this.textSearchSettings.ToString());
            Console.WriteLine(this.accountSettings.ToString());

            BlobServiceClient blobServiceClient = ClientFactory.CreateBlobServiceClient(this.accountSettings);
            BatchClient batchClient = ClientFactory.CreateBatchClient(this.accountSettings);

            Console.WriteLine($"Creating container {this.textSearchSettings.OutputBlobContainer} if it doesn't exist...");
            BlobContainerClient outputContainer = blobServiceClient.GetBlobContainerClient(this.textSearchSettings.OutputBlobContainer.ToLowerInvariant());
            await outputContainer.CreateIfNotExistsAsync();

            if (this.textSearchSettings.ShouldUploadResources)
            {
                Console.WriteLine("Splitting file: {0} into {1} subfiles",
                    Constants.TextFilePath,
                    this.textSearchSettings.NumberOfMapperTasks);

                FileSplitter splitter = new FileSplitter();
                List<string> mapperTaskFiles = await splitter.SplitAsync(
                    Constants.TextFilePath,
                    this.textSearchSettings.NumberOfMapperTasks);

                // Stage every dll/exe/config + the mapper input files.
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                List<string> binaries = Directory.EnumerateFiles(baseDir, "*.dll")
                    .Concat(Directory.EnumerateFiles(baseDir, "*.exe"))
                    .Concat(Directory.EnumerateFiles(baseDir, "*.config"))
                    .Concat(Directory.EnumerateFiles(baseDir, "*.json"))
                    .ToList();
                List<string> files = binaries.Concat(mapperTaskFiles).ToList();

                await SampleHelpers.UploadResourcesAsync(
                    blobServiceClient,
                    this.textSearchSettings.InputBlobContainer,
                    files);
            }

            string inputContainerSasUrl = await SampleHelpers.ConstructContainerSasAsync(
                blobServiceClient,
                this.textSearchSettings.InputBlobContainer,
                permissions: BlobContainerSasPermissions.Read);

            string outputContainerSasUrl = await SampleHelpers.ConstructContainerSasAsync(
                blobServiceClient,
                this.textSearchSettings.OutputBlobContainer,
                permissions: BlobContainerSasPermissions.Read | BlobContainerSasPermissions.Write);

            // Configure an auto-pool that lives for the duration of the job.
            int numberOfPoolComputeNodes = this.textSearchSettings.NumberOfMapperTasks;
            BatchPoolSpecification poolSpec = new BatchPoolSpecification(this.textSearchSettings.PoolNodeVirtualMachineSize)
            {
                TargetDedicatedNodes = numberOfPoolComputeNodes,
                VirtualMachineConfiguration = new VirtualMachineConfiguration(
                    new BatchVmImageReference
                    {
                        Publisher = this.textSearchSettings.ImagePublisher,
                        Offer = this.textSearchSettings.ImageOffer,
                        Sku = this.textSearchSettings.ImageSku,
                        Version = this.textSearchSettings.ImageVersion,
                    },
                    this.textSearchSettings.NodeAgentSkuId),
            };

            BatchPoolInfo poolInfo = new BatchPoolInfo
            {
                AutoPoolSpecification = new BatchAutoPoolSpecification(BatchPoolLifetimeOption.JobOption)
                {
                    AutoPoolIdPrefix = "TextSearchPool",
                    KeepAlive = false,
                    Pool = poolSpec,
                },
            };

            string jobId = Environment.GetEnvironmentVariable("USERNAME") + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");

            try
            {
                Console.WriteLine($"Adding job: {jobId} to the Batch Service.");
                await batchClient.CreateJobAsync(new BatchJobCreateOptions(jobId, poolInfo)
                {
                    UsesTaskDependencies = true,
                });

                IEnumerable<BatchTaskCreateOptions> mapperTasks = CreateMapperTasks(inputContainerSasUrl, outputContainerSasUrl);
                BatchTaskCreateOptions reducerTask = CreateReducerTask(inputContainerSasUrl, outputContainerSasUrl, mapperTasks);
                List<BatchTaskCreateOptions> tasksToAdd = mapperTasks.Concat(new[] { reducerTask }).ToList();

                Console.WriteLine("Submitting {0} mapper tasks", this.textSearchSettings.NumberOfMapperTasks);
                Console.WriteLine("Submitting 1 reducer task");
                await batchClient.CreateTaskCollectionAsync(jobId, new BatchTaskGroup(tasksToAdd));

                // Auto-terminate the job once all tasks are complete.
                await batchClient.UpdateJobAsync(jobId, new BatchJobUpdateOptions
                {
                    AllTasksCompleteMode = BatchAllTasksCompleteMode.TerminateJob,
                });

                Console.WriteLine("Waiting for job's tasks to complete");
                TimeSpan maxJobCompletionTimeout = TimeSpan.FromMinutes(30);

                try
                {
                    await WaitForAllTasksCompletedAsync(batchClient, jobId, maxJobCompletionTimeout);
                }
                finally
                {
                    Console.WriteLine("Done waiting for all tasks to complete");

                    await foreach (BatchTask task in batchClient.GetTasksAsync(jobId))
                    {
                        await Helpers.CheckForTaskSuccessAsync(batchClient, jobId, task, dumpStandardOutOnTaskSuccess: false);
                    }
                }

                string reducerText = await SampleHelpers.DownloadBlobTextAsync(
                    blobServiceClient,
                    this.textSearchSettings.OutputBlobContainer,
                    Constants.ReducerTaskResultBlobName);
                Console.WriteLine("Reducer reuslts:");
                Console.WriteLine(reducerText);
            }
            finally
            {
                if (this.textSearchSettings.ShouldDeleteJob)
                {
                    Console.WriteLine($"Deleting job {jobId}");
                    await batchClient.DeleteJobAsync(WaitUntil.Started, jobId);
                }

                if (this.textSearchSettings.ShouldDeleteContainers)
                {
                    Console.WriteLine("Deleting containers");
                    await blobServiceClient.GetBlobContainerClient(this.textSearchSettings.InputBlobContainer.ToLowerInvariant()).DeleteIfExistsAsync();
                    await outputContainer.DeleteIfExistsAsync();
                }
            }
        }

        private IEnumerable<BatchTaskCreateOptions> CreateMapperTasks(string inputContainerSas, string outputContainerSas)
        {
            for (int i = 0; i < this.textSearchSettings.NumberOfMapperTasks; i++)
            {
                string taskId = Helpers.GetMapperTaskId(i);
                string fileBlobName = Helpers.GetSplitFileName(i);

                string commandLine = $"{Constants.MapperTaskExecutable} {fileBlobName}";

                var task = new BatchTaskCreateOptions(taskId, commandLine);
                task.ResourceFiles.Add(new ResourceFile { StorageContainerUri = new Uri(inputContainerSas) });
                task.OutputFiles.Add(new OutputFile(
                    "..\\stdout.txt",
                    new OutputFileDestination
                    {
                        Container = new OutputFileBlobContainerDestination(new Uri(outputContainerSas))
                        {
                            Path = taskId,
                        },
                    },
                    new OutputFileUploadConfig(OutputFileUploadCondition.TaskSuccess)));

                yield return task;
            }
        }

        private BatchTaskCreateOptions CreateReducerTask(string inputContainerSas, string outputContainerSas, IEnumerable<BatchTaskCreateOptions> mapperTasks)
        {
            var reducer = new BatchTaskCreateOptions(Constants.ReducerTaskId, Constants.ReducerTaskExecutable);

            // Pull binaries / settings from the input container.
            reducer.ResourceFiles.Add(new ResourceFile { StorageContainerUri = new Uri(inputContainerSas) });

            // Pull each mapper task's stdout (uploaded to the output container under that task id).
            for (int i = 0; i < this.textSearchSettings.NumberOfMapperTasks; i++)
            {
                string mapperTaskId = Helpers.GetMapperTaskId(i);
                string blobUrl = SampleHelpers.ConstructBlobSource(outputContainerSas, $"{mapperTaskId}/stdout.txt");
                reducer.ResourceFiles.Add(new ResourceFile
                {
                    HttpUri = new Uri(blobUrl),
                    FilePath = mapperTaskId,
                });
            }

            reducer.OutputFiles.Add(new OutputFile(
                "..\\stdout.txt",
                new OutputFileDestination
                {
                    Container = new OutputFileBlobContainerDestination(new Uri(outputContainerSas))
                    {
                        Path = Constants.ReducerTaskResultBlobName,
                    },
                },
                new OutputFileUploadConfig(OutputFileUploadCondition.TaskSuccess)));

            var deps = new BatchTaskDependencies();
            foreach (var t in mapperTasks)
            {
                deps.TaskIds.Add(t.Id);
            }
            reducer.DependsOn = deps;

            return reducer;
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
