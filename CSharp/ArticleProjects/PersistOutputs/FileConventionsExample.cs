namespace Microsoft.Azure.Batch.Samples.Articles.PersistOutputs
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using global::Azure.Storage.Blobs;
    using Batch.Common;
    using Common;
    using Conventions.Files;

    public static class FileConventionsExample
    {
        public static async Task<BlobContainerClient> Run(
            BatchClient batchClient,
            BlobServiceClient blobClient,
            string poolId,
            int nodeCount,
            string jobId)
        {
            const string appPackageId = "PersistOutputsTask";
            const string appPackageVersion = "1.0";

            // Create and configure an unbound pool.
            CloudPool pool = batchClient.PoolOperations.CreatePool(
                poolId: poolId,
                virtualMachineSize: "standard_d1_v2",
                targetDedicatedComputeNodes: nodeCount,
                cloudServiceConfiguration: new CloudServiceConfiguration(osFamily: "5"));

            // Specify the application and version to deploy to the compute nodes. You must
            // first build PersistOutputsTask, then upload it as an application package.
            // See https://azure.microsoft.com/documentation/articles/batch-application-packages/
            pool.ApplicationPackageReferences = new List<ApplicationPackageReference>
            {
                new ApplicationPackageReference
                {
                    ApplicationId = appPackageId,
                    Version = appPackageVersion
                }
            };

            // Commit the pool to the Batch service
            await GettingStartedCommon.CreatePoolIfNotExistAsync(batchClient, pool);

            CloudJob job = batchClient.JobOperations.CreateJob(jobId, new PoolInformation { PoolId = poolId });

            // Create the blob storage container for the outputs.
            await job.PrepareOutputStorageAsync(blobClient);

            // Create an environment variable on the compute nodes that the
            // task application can reference when persisting its outputs.
            string containerName = job.OutputStorageContainerName();
            BlobContainerClient container = blobClient.GetBlobContainerClient(containerName);
            string containerUrl = job.GetOutputStorageContainerUrl(blobClient);
            job.CommonEnvironmentSettings = new[] { new EnvironmentSetting("JOB_CONTAINER_URL", containerUrl) };

            // Commit the job to the Batch service
            await job.CommitAsync();
            Console.WriteLine($"Created job {jobId}");

            // Obtain the bound job from the Batch service
            await job.RefreshAsync();

            IEnumerable<CloudTask> tasks = Enumerable.Range(1, 20).Select(i =>
                new CloudTask(i.ToString().PadLeft(3, '0'), $"cmd /c %AZ_BATCH_APP_PACKAGE_{appPackageId.ToUpper()}#{appPackageVersion}%\\PersistOutputsTask.exe")
            );

            // Add the tasks to the job; the tasks are automatically
            // scheduled for execution on the nodes by the Batch service.
            await job.AddTaskAsync(tasks);

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

                foreach (OutputFileReference output in task.OutputStorage(blobClient).ListOutputs(TaskOutputKind.TaskOutput))
                {
                    Console.WriteLine($"output file: {output.FilePath}");
                    await output.DownloadToFileAsync($"{jobId}-{output.FilePath}");
                }
            }

            return container;
        }
    }
}
