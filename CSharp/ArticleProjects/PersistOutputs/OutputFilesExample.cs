namespace Microsoft.Azure.Batch.Samples.Articles.PersistOutputs
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using WindowsAzure.Storage;
    using WindowsAzure.Storage.Blob;
    using Batch.Common;
    using Microsoft.Azure.Batch;
    using Common;
    using Conventions.Files;

    public class OutputFilesExample
    {
        /// <summary>
        /// Runs a series of tasks, using the OutputFiles feature to upload the tasks files to a container.
        /// Then downloads the files from the container to the local machine.
        /// </summary>
        public static async Task<CloudBlobContainer> Run(
            BatchClient batchClient,
            CloudStorageAccount storageAccount,
            string poolId,
            int nodeCount,
            string jobId)
        {
            const string containerName = "outputfilescontainer";
            await CreatePoolAsync(batchClient, poolId, nodeCount);

            CloudJob job = batchClient.JobOperations.CreateJob(jobId, new PoolInformation { PoolId = poolId });

            CloudBlobContainer container = storageAccount.CreateCloudBlobClient().GetContainerReference(containerName);
            await container.CreateIfNotExistsAsync();

            string containerSas = container.GetSharedAccessSignature(new SharedAccessBlobPolicy()
            {
                Permissions = SharedAccessBlobPermissions.Write,
                SharedAccessExpiryTime = DateTimeOffset.UtcNow.AddDays(1)
            });
            string containerUrl = container.Uri.AbsoluteUri + containerSas;

            // Commit the job to the Batch service
            await job.CommitAsync();
            Console.WriteLine($"Created job {jobId}");

            // Obtain the bound job from the Batch service
            await job.RefreshAsync();

            // Create a series of simple tasks which dump the task environment to a file and then write random values to a text file
            IEnumerable<CloudTask> tasksToAdd = Enumerable.Range(1, 20).Select(i =>
                {
                    var taskId = i.ToString().PadLeft(3, '0');
                    return new CloudTask(taskId, "cmd /v:ON /c \"echo off && set && (FOR /L %i IN (1,1,100000) DO (ECHO !RANDOM!)) > output.txt\"")
                    {
                        OutputFiles = new List<OutputFile>
                        {
                            new OutputFile(
                                filePattern: @"..\std*.txt",
                                destination: new OutputFileDestination(new OutputFileBlobContainerDestination(
                                    containerUrl: containerUrl,
                                    path: taskId)),
                                uploadOptions: new OutputFileUploadOptions(
                                    uploadCondition: OutputFileUploadCondition.TaskCompletion)),
                            new OutputFile(
                                filePattern: @"output.txt",
                                destination: new OutputFileDestination(new OutputFileBlobContainerDestination(
                                    containerUrl: containerUrl,
                                    path: taskId + @"\output.txt")),
                                uploadOptions: new OutputFileUploadOptions(
                                    uploadCondition: OutputFileUploadCondition.TaskCompletion)),
                        }
                    };
                }
            );

            // Add the tasks to the job; the tasks are automatically
            // scheduled for execution on the nodes by the Batch service.
            await job.AddTaskAsync(tasksToAdd);

            Console.WriteLine($"All tasks added to job {job.Id}");
            Console.WriteLine();

            Console.WriteLine($"Downloading outputs to {Directory.GetCurrentDirectory()}");

            foreach (CloudTask task in job.CompletedTasks())
            {
                if (task.ExecutionInformation.Result != TaskExecutionResult.Success)
                {
                    Console.WriteLine($"Task {task.Id} failed");
                    Console.WriteLine(SampleHelpers.GetFailureInfoDetails(task.ExecutionInformation.FailureInformation));
                }
                else
                {
                    Console.WriteLine($"Task {task.Id} completed successfully");
                }

                CloudBlobDirectory directory = container.GetDirectoryReference(task.Id);
                Directory.CreateDirectory(task.Id);
                foreach (var blobInDirectory in directory.ListBlobs())
                {
                    CloudBlockBlob blockBlob = blobInDirectory as CloudBlockBlob;
                    Console.WriteLine($"  {blockBlob.Name}");
                    await blockBlob.DownloadToFileAsync(blockBlob.Name, FileMode.Create);
                }
            }

            return container;
        }

        /// <summary>
        /// Runs a series of tasks, using the OutputFiles feature in conjunction with the file conventions library to upload the tasks to a container.
        /// Then downloads the files from the container to the local machine.
        /// </summary>
        public static async Task<CloudBlobContainer> RunWithConventions(
            BatchClient batchClient,
            CloudStorageAccount linkedStorageAccount,
            string poolId,
            int nodeCount,
            string jobId)
        {
            await CreatePoolAsync(batchClient, poolId, nodeCount);

            CloudJob job = batchClient.JobOperations.CreateJob(jobId, new PoolInformation { PoolId = poolId });

            // Get the container URL to use
            string containerName = job.OutputStorageContainerName();
            CloudBlobContainer container = linkedStorageAccount.CreateCloudBlobClient().GetContainerReference(containerName);
            await container.CreateIfNotExistsAsync();
            string containerUrl = job.GetOutputStorageContainerUrl(linkedStorageAccount);

            // Commit the job to the Batch service
            await job.CommitAsync();
            Console.WriteLine($"Created job {jobId}");

            // Obtain the bound job from the Batch service
            await job.RefreshAsync();

            // Create a series of simple tasks which dump the task environment to a file and then write random values to a text file
            IEnumerable<CloudTask> tasksToAdd = Enumerable.Range(1, 20).Select(i =>
                {
                    var taskId = i.ToString().PadLeft(3, '0');
                    var task = new CloudTask(taskId, "cmd /v:ON /c \"echo off && set && (FOR /L %i IN (1,1,100000) DO (ECHO !RANDOM!)) > output.txt\"");

                    task.WithOutputFile(@"..\std*.txt", containerUrl, TaskOutputKind.TaskLog, OutputFileUploadCondition.TaskCompletion)
                        .WithOutputFile(@"output.txt", containerUrl, TaskOutputKind.TaskOutput, OutputFileUploadCondition.TaskSuccess);

                    return task;
                }
            );

            // Add the tasks to the job; the tasks are automatically
            // scheduled for execution on the nodes by the Batch service.
            await job.AddTaskAsync(tasksToAdd);

            Console.WriteLine($"All tasks added to job {job.Id}");
            Console.WriteLine();

            Console.WriteLine($"Downloading outputs to {Directory.GetCurrentDirectory()}");

            foreach (CloudTask task in job.CompletedTasks())
            {
                if (task.ExecutionInformation.Result != TaskExecutionResult.Success)
                {
                    Console.WriteLine($"Task {task.Id} failed");
                    Console.WriteLine(SampleHelpers.GetFailureInfoDetails(task.ExecutionInformation.FailureInformation));
                }
                else
                {
                    Console.WriteLine($"Task {task.Id} completed successfully");
                }

                foreach (OutputFileReference output in task.OutputStorage(linkedStorageAccount).ListOutputs(TaskOutputKind.TaskOutput))
                {
                    Console.WriteLine($"output file: {output.FilePath}");
                    await output.DownloadToFileAsync($"{jobId}-{output.FilePath}", System.IO.FileMode.Create);
                }
            }

            return container;
        }

        private static async Task CreatePoolAsync(BatchClient batchClient, string poolId, int nodeCount)
        {
            Func<ImageReference, bool> imageScanner = imageRef =>
                imageRef.Publisher.Equals("MicrosoftWindowsServer", StringComparison.InvariantCultureIgnoreCase) &&
                imageRef.Offer.Equals("WindowsServer", StringComparison.InvariantCultureIgnoreCase) &&
                imageRef.Sku.IndexOf("2012-R2-Datacenter", StringComparison.InvariantCultureIgnoreCase) > -1;

            ImageInformation imageInfo = await SampleHelpers.GetNodeAgentSkuReferenceAsync(batchClient, imageScanner);

            // Create and configure an unbound pool.
            CloudPool pool = batchClient.PoolOperations.CreatePool(poolId: poolId,
                virtualMachineSize: "standard_d1_v2",
                targetDedicatedComputeNodes: nodeCount,
                virtualMachineConfiguration: new VirtualMachineConfiguration(
                    imageInfo.ImageReference,
                    imageInfo.NodeAgentSkuId));

            // Commit the pool to the Batch service
            await GettingStartedCommon.CreatePoolIfNotExistAsync(batchClient, pool);
        }
    }
}
