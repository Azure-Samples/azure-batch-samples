//Copyright (c) Microsoft Corporation

namespace Microsoft.Azure.Batch.Samples.Common
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
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
            IPagedEnumerable<CloudPool> pools = batchClient.PoolOperations.ListPools(new ODATADetailLevel(selectClause: "id,state,currentDedicatedNodes,currentLowPriorityNodes,vmSize"));

            await pools.ForEachAsync(pool =>
            {
                Console.WriteLine("State of pool {0} is {1} and it has {2} dedicated nodes and {3} low-priority nodes of size {4}", 
                    pool.Id,
                    pool.State,
                    pool.CurrentDedicatedComputeNodes,
                    pool.CurrentLowPriorityComputeNodes,
                    pool.VirtualMachineSize);
            }).ConfigureAwait(continueOnCapturedContext: false);
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
            }).ConfigureAwait(continueOnCapturedContext: false);

            Console.WriteLine("============");
        }

        /// <summary>
        /// Prints task information to the console for each of the nodes in the specified pool.
        /// </summary>
        /// <param name="batchClient">The Batch client.</param>
        /// <param name="poolId">The ID of the <see cref="CloudPool"/> containing the nodes whose task information should be printed to the console.</param>
        /// <returns>A <see cref="System.Threading.Tasks.Task"/> object that represents the asynchronous operation.</returns>
        public static async Task PrintNodeTasksAsync(BatchClient batchClient, string poolId)
        {
            Console.WriteLine("Listing Node Tasks");
            Console.WriteLine("==================");

            ODATADetailLevel nodeDetail = new ODATADetailLevel(selectClause: "id,recentTasks");
            IPagedEnumerable<ComputeNode> nodes = batchClient.PoolOperations.ListComputeNodes(poolId, nodeDetail);
            
            await nodes.ForEachAsync(node =>
            {
                Console.WriteLine();
                Console.WriteLine(node.Id + " tasks:");

                if (node.RecentTasks != null && node.RecentTasks.Any())
                {
                    foreach (TaskInformation task in node.RecentTasks)
                    {
                        Console.WriteLine("\t{0}: {1}", task.TaskId, task.TaskState);
                    }
                }
                else
                {
                    // No tasks found for the node
                    Console.WriteLine("\tNone");
                }
            }).ConfigureAwait(continueOnCapturedContext: false);

            Console.WriteLine("==================");
        }

        public static string CreateJobId(string prefix)
        {
            // a job is uniquely identified by its ID so your account name along with a timestamp is added as suffix
            return string.Format("{0}-{1}-{2}", prefix, new string(Environment.UserName.Where(char.IsLetterOrDigit).ToArray()), DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        }

        /// <summary>
        /// Waits for all tasks under the specified job to complete and then prints each task's output to the console.
        /// </summary>
        /// <param name="batchClient">The BatchClient to use when interacting with the Batch service.</param>
        /// <param name="tasks">The tasks to wait for.</param>
        /// <param name="timeout">The timeout.  After this time has elapsed if the job is not complete and exception will be thrown.</param>
        /// <returns>An asynchronous <see cref="Task"/> representing the operation.</returns>
        public static async Task WaitForTasksAndPrintOutputAsync(BatchClient batchClient, IEnumerable<CloudTask> tasks, TimeSpan timeout)
        {
            // We use the task state monitor to monitor the state of our tasks -- in this case we will wait for them all to complete.
            TaskStateMonitor taskStateMonitor = batchClient.Utilities.CreateTaskStateMonitor();

            // Wait until the tasks are in completed state.
            List<CloudTask> ourTasks = tasks.ToList();

            await taskStateMonitor.WhenAll(ourTasks, TaskState.Completed, timeout).ConfigureAwait(continueOnCapturedContext: false);

            // dump task output
            foreach (CloudTask t in ourTasks)
            {
                Console.WriteLine("Task {0}", t.Id);

                //Read the standard out of the task
                NodeFile standardOutFile = await t.GetNodeFileAsync(Constants.StandardOutFileName).ConfigureAwait(continueOnCapturedContext: false);
                string standardOutText = await standardOutFile.ReadAsStringAsync().ConfigureAwait(continueOnCapturedContext: false);
                Console.WriteLine("Standard out:");
                Console.WriteLine(standardOutText);

                //Read the standard error of the task
                NodeFile standardErrorFile = await t.GetNodeFileAsync(Constants.StandardErrorFileName).ConfigureAwait(continueOnCapturedContext: false);
                string standardErrorText = await standardErrorFile.ReadAsStringAsync().ConfigureAwait(continueOnCapturedContext: false);
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
        /// Creates a pool if it doesn't already exist.  If the pool already exists, this method resizes it to meet the expected
        /// targets specified in settings.
        /// </summary>
        /// <param name="batchClient">The BatchClient to create the pool with.</param>
        /// <param name="pool">The pool to create.</param>
        /// <returns>An asynchronous <see cref="Task"/> representing the operation.</returns>
        public static async Task<CreatePoolResult> CreatePoolIfNotExistAsync(BatchClient batchClient, CloudPool pool)
        {
            bool successfullyCreatedPool = false;

            int targetDedicatedNodeCount = pool.TargetDedicatedComputeNodes ?? 0;
            int targetLowPriorityNodeCount = pool.TargetLowPriorityComputeNodes ?? 0;
            string poolNodeVirtualMachineSize = pool.VirtualMachineSize;
            string poolId = pool.Id;

            // Attempt to create the pool
            try
            {
                // Create an in-memory representation of the Batch pool which we would like to create.  We are free to modify/update 
                // this pool object in memory until we commit it to the service via the CommitAsync method.
                Console.WriteLine("Attempting to create pool: {0}", pool.Id);

                // Create the pool on the Batch Service
                await pool.CommitAsync().ConfigureAwait(continueOnCapturedContext: false);

                successfullyCreatedPool = true;
                Console.WriteLine("Created pool {0} with {1} dedicated and {2} low priority {3} nodes",
                    poolId,
                    targetDedicatedNodeCount,
                    targetLowPriorityNodeCount,
                    poolNodeVirtualMachineSize);
            }
            catch (BatchException e)
            {
                // Swallow the specific error code PoolExists since that is expected if the pool already exists
                if (e.RequestInformation?.BatchError?.Code == BatchErrorCodeStrings.PoolExists)
                {
                    // The pool already existed when we tried to create it
                    successfullyCreatedPool = false;
                    Console.WriteLine("The pool already existed when we tried to create it");
                }
                else
                {
                    throw; // Any other exception is unexpected
                }
            }

            // If the pool already existed, make sure that its targets are correct
            if (!successfullyCreatedPool)
            {
                CloudPool existingPool = await batchClient.PoolOperations.GetPoolAsync(poolId).ConfigureAwait(continueOnCapturedContext: false);

                // If the pool doesn't have the right number of nodes, isn't resizing, and doesn't have
                // automatic scaling enabled, then we need to ask it to resize
                if ((existingPool.CurrentDedicatedComputeNodes != targetDedicatedNodeCount || existingPool.CurrentLowPriorityComputeNodes != targetLowPriorityNodeCount) &&
                    existingPool.AllocationState != AllocationState.Resizing &&
                    existingPool.AutoScaleEnabled == false)
                {
                    // Resize the pool to the desired target. Note that provisioning the nodes in the pool may take some time
                    await existingPool.ResizeAsync(targetDedicatedNodeCount, targetLowPriorityNodeCount).ConfigureAwait(continueOnCapturedContext: false);
                    return CreatePoolResult.ResizedExisting;
                }
                else
                {
                    return CreatePoolResult.PoolExisted;
                }
            }

            return CreatePoolResult.CreatedNew;
        }

        /// <summary>
        /// Generates a file in a temp location with the specified name and text.
        /// </summary>
        /// <param name="fileName">The name of the file.</param>
        /// <param name="fileText">The text of the file.</param>
        /// <returns>The full path to the file.</returns>
        public static string GenerateTemporaryFile(string fileName, string fileText)
        {
            string filePath = Path.Combine(Environment.GetEnvironmentVariable("TEMP"), fileName);
            File.WriteAllText(filePath, fileText);

            return filePath;
        }
    }
}
