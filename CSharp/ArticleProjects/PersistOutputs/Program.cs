// Copyright (c) Microsoft Corporation
//
// Companion project to the following article:
// https://azure.microsoft.com/documentation/articles/batch-task-output/

namespace Microsoft.Azure.Batch.Samples.Articles.PersistOutputs
{
    using Microsoft.Azure.Batch;
    using Microsoft.Azure.Batch.Auth;
    using Microsoft.Azure.Batch.Conventions.Files;
    using Microsoft.Azure.Batch.Samples.Common;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Auth;
    using Microsoft.WindowsAzure.Storage.Blob;
    using System;
    using System.Collections.Generic;
    using System.Linq;

    class Program
    {
        static void Main(string[] args)
        {
            // Configure your AccountSettings in the Microsoft.Azure.Batch.Samples.Common project within this solution
            BatchSharedKeyCredentials cred = new BatchSharedKeyCredentials(AccountSettings.Default.BatchServiceUrl,
                                                                           AccountSettings.Default.BatchAccountName,
                                                                           AccountSettings.Default.BatchAccountKey);

            StorageCredentials storageCred = new StorageCredentials(AccountSettings.Default.StorageAccountName,
                                                                    AccountSettings.Default.StorageAccountKey);

            string jobId = Guid.NewGuid().ToString();
            string poolId = "PersistOutputsSamplePool";
            int nodeCount = 3;
            string appPackageId = "PersistOutputTask";
            string appPackageVersion = "1.1";

            using (BatchClient batchClient = BatchClient.Open(cred))
            {
                // Create and configure an unbound pool.
                CloudPool pool = batchClient.PoolOperations.CreatePool(poolId: poolId,
                    virtualMachineSize: "small",
                    targetDedicated: nodeCount,
                    cloudServiceConfiguration: new CloudServiceConfiguration("4"));

                // Specify the application and version to deploy to the compute nodes
                pool.ApplicationPackageReferences = new List<ApplicationPackageReference>
                {
                    new ApplicationPackageReference {
                        ApplicationId = appPackageId,
                        Version = appPackageVersion }
                };

                // Commit the pool to the Batch service
                pool.Commit();


                CloudJob job = batchClient.JobOperations.CreateJob(jobId, new PoolInformation { PoolId = poolId });

                CloudStorageAccount linkedStorageAccount = new CloudStorageAccount(storageCred, true);

                // Create the blob storage container for the outputs.
                job.PrepareOutputStorageAsync(linkedStorageAccount).Wait();

                // Create an environment variable on the compute nodes that the
                // task application can reference when persisting its outputs.
                string containerName = job.OutputStorageContainerName();
                CloudBlobContainer container = linkedStorageAccount.CreateCloudBlobClient().GetContainerReference(containerName);
                string containerSas = container.GetSharedAccessSignature(FullAccessPolicy());
                string containerUrl = container.Uri.AbsoluteUri + containerSas;
                job.CommonEnvironmentSettings = new[] { new EnvironmentSetting("JOB_CONTAINER_URL", containerUrl) };

                // Commit the job to the Batch service
                job.Commit();
                Console.WriteLine($"Created job {jobId}");

                // Obtain the bound job from the Batch service
                job = batchClient.JobOperations.GetJob(jobId);


                IEnumerable<CloudTask> tasks = Enumerable.Range(1, 20).Select(i =>
                    new CloudTask(i.ToString().PadLeft(3, '0'), $"cmd /c %AZ_BATCH_APP_PACKAGE_{appPackageId.ToUpper()}#{appPackageVersion}%\\PersistOutputsTask.exe")
                );

                // Add the tasks to the job; the tasks are automatically
                // scheduled for execution on the nodes by the Batch service.
                job.AddTask(tasks);

                Console.WriteLine($"All tasks added to job {job.Id}");
                Console.WriteLine();

                foreach (CloudTask task in CompletedTasks(batchClient, job))
                {
                    Console.Write($"Task {task.Id} completed, ");
                    foreach (OutputFileReference output in task.OutputStorage(linkedStorageAccount).ListOutputs(TaskOutputKind.TaskOutput))
                    {
                        Console.WriteLine($"output file: {output.FilePath}");
                        output.DownloadToFileAsync($"{jobId}-{output.FilePath}", System.IO.FileMode.Create).Wait();
                    }
                }

                Console.WriteLine();
                Console.WriteLine("All tasks completed, you may now view the outputs in the Azure portal.");

                // Clean up the resources we've created (job, pool, and blob storage container)
                Console.WriteLine();
                Console.Write("Delete job? [yes] no: ");
                string response = Console.ReadLine().ToLower();
                if (response != "n" && response != "no")
                {
                    batchClient.JobOperations.DeleteJob(job.Id);
                }

                Console.Write("Delete pool? [yes] no: ");
                response = Console.ReadLine().ToLower();
                if (response != "n" && response != "no")
                {
                    batchClient.PoolOperations.DeletePool(poolId);
                }

                Console.Write("Delete storage container? [yes] no: ");
                response = Console.ReadLine().ToLower();
                if (response != "n" && response != "no")
                {
                    container.Delete();
                }

                Console.WriteLine();
                Console.WriteLine("Sample complete, hit ENTER to exit...");
                Console.ReadLine();
            }
        }

        /// <summary>
        /// Monitors the specified job's tasks and returns each as they complete. When all
        /// of the tasks in the job have completed, the method returns.
        /// </summary>
        /// <param name="client">The <see cref="<see cref="BatchClient"/>" for the account containing the job.</param>
        /// <param name="job">The <see cref="CloudJob"/> containing the tasks to monitor.</param>
        /// <returns>One or more completed <see cref="CloudTask"/>.</returns>
        private static IEnumerable<CloudTask> CompletedTasks(BatchClient client, CloudJob job)
        {
            HashSet<string> yieldedTasks = new HashSet<string>();

            while (true)
            {
                List<CloudTask> tasks = job.ListTasks().ToList();

                List<CloudTask> newlyCompleted = tasks.Where(t => t.State == Microsoft.Azure.Batch.Common.TaskState.Completed)
                                          .Where(t => !yieldedTasks.Contains(t.Id))
                                          .ToList();

                foreach (var task in newlyCompleted)
                {
                    yield return task;
                    yieldedTasks.Add(task.Id);
                }

                if (yieldedTasks.Count == tasks.Count)
                {
                    yield break;
                }
            }
        }

        /// <summary>
        /// Gets a <see cref="SharedAccessBlobPolicy"/> with full CRUD permissions, valid for 1 day.
        /// </summary>
        /// <returns>Full-access <see cref="SharedAccessBlobPolicy"/>.</returns>
        private static SharedAccessBlobPolicy FullAccessPolicy()
        {
            return new SharedAccessBlobPolicy
            {
                Permissions = SharedAccessBlobPermissions.Add |
                              SharedAccessBlobPermissions.Create |
                              SharedAccessBlobPermissions.List |
                              SharedAccessBlobPermissions.Read |
                              SharedAccessBlobPermissions.Write,
                SharedAccessExpiryTime = DateTime.UtcNow.AddDays(1),
            };
        }
    }
}
