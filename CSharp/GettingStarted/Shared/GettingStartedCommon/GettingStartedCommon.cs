namespace Microsoft.Azure.Batch.Samples.GettingStarted.Common
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using WindowsAzure.Storage;
    using WindowsAzure.Storage.Auth;
    using WindowsAzure.Storage.Blob;
    using Batch.Common;
    using FileStaging;
    using Constants = Batch.Constants;

    public static class GettingStartedCommon
    {
        /// <summary>
        /// Lists all the pools in the Batch account.
        /// </summary>
        /// <param name="batchClient">The BatchClient to use when interacting with the Batch service.</param>
        /// <returns>An asynchronous <see cref="Task"/> representing the operation.</returns>
        public static async Task PrintPoolsAsync(BatchClient batchClient)
        {
            Console.WriteLine("Listing Pools");
            Console.WriteLine("=============");

            // Using optional select clause to return only the properties of interest. Makes query faster and reduces HTTP packet size impact
            IPagedEnumerable<CloudPool> pools = batchClient.PoolOperations.ListPools(new ODATADetailLevel(selectClause: "id,state,currentDedicated,vmSize"));

            await pools.ForEachAsync(pool =>
            {
                Console.WriteLine("State of pool {0} is {1} and it has {2} nodes of size {3}", pool.Id, pool.State, pool.CurrentDedicated, pool.VirtualMachineSize);
            });
            Console.WriteLine("=============");
        }

        /// <summary>
        /// Lists all the jobs in the Batch account.
        /// </summary>
        /// <param name="batchClient">The BatchClient to use when interacting with the Batch service.</param>
        /// <returns>An asynchronous <see cref="Task"/> representing the operation.</returns>
        public static async Task PrintJobsAsync(BatchClient batchClient)
        {
            Console.WriteLine("Listing Jobs");
            Console.WriteLine("============");

            IPagedEnumerable<CloudJob> jobs = batchClient.JobOperations.ListJobs(new ODATADetailLevel(selectClause: "id,state"));
            await jobs.ForEachAsync(job =>
            {
                Console.WriteLine("State of job " + job.Id + " is " + job.State);
            });

            Console.WriteLine("============");
        }

        public static string CreateJobId(string prefix)
        {
            // a job is uniquely identified by its ID so your account name along with a timestamp is added as suffix
            return string.Format("{0}-{1}-{2}", prefix, Environment.GetEnvironmentVariable("USERNAME"), DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        }

        /// <summary>
        /// Waits for all tasks under the specified job to complete and then prints each task's output to the console.
        /// </summary>
        /// <param name="batchClient">The BatchClient to use when interacting with the Batch service.</param>
        /// <param name="jobId">The ID of the job.</param>
        /// <returns>An asynchronous <see cref="Task"/> representing the operation.</returns>
        public static async Task WaitForJobAndPrintOutputAsync(BatchClient batchClient, string jobId)
        {
            Console.WriteLine("Waiting for all tasks to complete on job: {0} ...", jobId);

            // We use the task state monitor to monitor the state of our tasks -- in this case we will wait for them all to complete.
            TaskStateMonitor taskStateMonitor = batchClient.Utilities.CreateTaskStateMonitor();

            // Wait until the tasks are in completed state.
            // If the pool is being resized then enough time is needed for the nodes to reach the idle state in order
            // for tasks to run on them.
            List<CloudTask> ourTasks = await batchClient.JobOperations.ListTasks(jobId).ToListAsync();

            bool timedOut = await taskStateMonitor.WaitAllAsync(ourTasks, TaskState.Completed, TimeSpan.FromMinutes(10));

            if (timedOut)
            {
                throw new TimeoutException("Timed out waiting for tasks");
            }

            // dump task output
            foreach (CloudTask t in ourTasks)
            {
                Console.WriteLine("Task {0}", t.Id);

                //Read the standard out of the task
                NodeFile standardOutFile = await t.GetNodeFileAsync(Constants.StandardOutFileName);
                string standardOutText = await standardOutFile.ReadAsStringAsync();
                Console.WriteLine("Standard out:");
                Console.WriteLine(standardOutText);

                //Read the standard error of the task
                NodeFile standardErrorFile = await t.GetNodeFileAsync(Constants.StandardErrorFileName);
                string standardErrorText = await standardErrorFile.ReadAsStringAsync();
                Console.WriteLine("Standard error:");
                Console.WriteLine(standardErrorText);

                Console.WriteLine();
            }
        }

        /// <summary>
        /// Extracts the name of the container from the file staging artifacts.
        /// </summary>
        /// <param name="fileStagingArtifacts">The file staging artifacts.</param>
        /// <returns>A set containing all containers created by file staging.</returns>
        public static HashSet<string> ExtractBlobContainerNames(ConcurrentBag<ConcurrentDictionary<Type, IFileStagingArtifact>> fileStagingArtifacts)
        {
            HashSet<string> result = new HashSet<string>();

            foreach (ConcurrentDictionary<Type, IFileStagingArtifact> artifactContainer in fileStagingArtifacts)
            {
                foreach (IFileStagingArtifact artifact in artifactContainer.Values)
                {
                    SequentialFileStagingArtifact sequentialStagingArtifact = artifact as SequentialFileStagingArtifact;
                    if (sequentialStagingArtifact != null)
                    {
                        result.Add(sequentialStagingArtifact.BlobContainerCreated);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Deletes the specified containers
        /// </summary>
        /// <param name="storageAccount">The storage account with the containers to delete.</param>
        /// <param name="blobContainerNames">The name of the containers created for the jobs resource files.</param>
        /// <returns>An asynchronous <see cref="Task"/> representing the operation.</returns>
        private static async Task DeleteContainersAsync(CloudStorageAccount storageAccount, IEnumerable<string> blobContainerNames)
        {
            CloudBlobClient cloudBlobClient = storageAccount.CreateCloudBlobClient();
            foreach (string blobContainerName in blobContainerNames)
            {
                CloudBlobContainer container = cloudBlobClient.GetContainerReference(blobContainerName);
                Console.WriteLine("Deleting container: {0}", blobContainerName);

                await container.DeleteAsync();
            }
        }
    }
}
