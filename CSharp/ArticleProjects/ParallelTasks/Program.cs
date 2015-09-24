//Copyright (c) Microsoft Corporation

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Batch;
using Microsoft.Azure.Batch.Auth;
using Microsoft.Azure.Batch.Common;
using Microsoft.Azure.Batch.Samples.Common;

namespace Microsoft.Azure.Batch.Samples.Articles.ParallelTasks
{
    public class Program
    {
        public static void Main(string[] args)
        {
            // You may adjust these values to experiment with different compute resource scenarios.
            const string nodeSize     = "small";
            const int nodeCount       = 4;
            const int maxTasksPerNode = 4;
            const int taskCount       = 32;

            // In this sample, the tasks simply ping localhost on the compute nodes; adjust these
            // values to simulate variable task duration
            const int minPings = 30;
            const int maxPings = 60;

            const string poolId = "poolMaxTasks";
            const string jobId  = "jobMaxTasks";

            // Set up access to your Batch account with a BatchClient. Configure your AccountSettings in the
            // Microsoft.Azure.Batch.Samples.Common project within this solution.
            BatchSharedKeyCredentials cred = new BatchSharedKeyCredentials(AccountSettings.Default.BatchServiceUrl,
                                                                           AccountSettings.Default.BatchAccountName,
                                                                           AccountSettings.Default.BatchAccountKey);
            
            using (BatchClient batchClient = BatchClient.Open(cred))
            {
                // Create a CloudPool, or obtain an existing pool with the specified ID
                CreatePool(batchClient,
                           poolId,
                           nodeSize,
                           nodeCount,
                           maxTasksPerNode).Wait();
                CloudPool pool = batchClient.PoolOperations.GetPool(poolId);

                // Create a CloudJob, or obtain an existing job with the specified ID
                CloudJob job = CreateJob(batchClient, pool.Id, jobId);

                // The job's tasks ping localhost a random number of times between minPings and maxPings.
                // Adjust the minPings/maxPings values above to expriment with different task durations.
                Random rand = new Random();
                List<CloudTask> tasks = new List<CloudTask>();
                for (int i = 1; i < taskCount + 1; i++)
                {
                    string taskId = "task" + i.ToString().PadLeft(3, '0');
                    string taskCommandLine = "ping -n " + rand.Next(minPings, maxPings).ToString() + " localhost";
                    CloudTask task = new CloudTask(taskId, taskCommandLine);
                    tasks.Add(task);
                }

                // Pause execution until the pool is steady and its compute nodes are ready to accept jobs.
                // NOTE: This is only for demonstration purposes and is NOT necessary within your own code.
                WaitForPoolToReachStateAsync(batchClient, pool.Id, AllocationState.Steady, TimeSpan.FromMinutes(30)).Wait();
                WaitForNodesToReachStateAsync(batchClient, pool.Id, ComputeNodeState.Idle, TimeSpan.FromMinutes(30)).Wait();

                // To reduce the chances of hitting Batch service throttling limits, we add the tasks in
                // one API call as opposed to a separate AddTask call for each. Bulk task submission is
                // crucial if you are adding large numbers of tasks to your jobs.
                batchClient.JobOperations.AddTask(job.Id, tasks);

                // Pause again to wait until the nodes are running tasks - we do this only so that we can
                // display concurrent tasks running on the nodes.
                // NOTE: This is only for demonstration purposes and is NOT necessary within your own code.
                WaitForNodesToReachStateAsync(batchClient, pool.Id, ComputeNodeState.Running, TimeSpan.FromMinutes(30)).Wait();

                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();

                // Print out task assignment information.
                Console.WriteLine();
                PrintNodeTasks(batchClient, pool.Id);
                Console.WriteLine();

                // Pause execution while we wait for all of the tasks to complete
                Console.WriteLine("Waiting for task completion...");
                Console.WriteLine();
                batchClient.Utilities.CreateTaskStateMonitor().WaitAll(job.ListTasks(),
                                                                   TaskState.Completed,
                                                                   new TimeSpan(0, 30, 0));

                stopwatch.Stop();

                // Obtain the tasks, specifying a detail level to limit the number of properties returned for each task.
                // If you have a large number of tasks, specifying a DetailLevel is extremely important in reducing the
                // amount of data transferred, lowering your query response times in increasing performance.
                ODATADetailLevel detail = new ODATADetailLevel(selectClause: "id,commandLine,nodeInfo,state");
                IPagedEnumerable<CloudTask> allTasks = batchClient.JobOperations.ListTasks(job.Id, detail);

                // Get a collection of the completed tasks sorted by the compute nodes on which they executed
                List<CloudTask> completedTasks = allTasks
                                                .Where(t => t.State == TaskState.Completed)
                                                .OrderBy(t => t.ComputeNodeInformation.ComputeNodeId)
                                                .ToList();

                // Print the completed task information
                string lastNodeId = string.Empty;
                foreach (CloudTask task in completedTasks)
                {
                    if (!string.Equals(lastNodeId, task.ComputeNodeInformation.ComputeNodeId))
                        Console.WriteLine(task.ComputeNodeInformation.ComputeNodeId);

                    lastNodeId = task.ComputeNodeInformation.ComputeNodeId;

                    Console.WriteLine("\t{0}: {1}", task.Id, task.CommandLine);
                }

                // Print some summary information
                Console.WriteLine();
                Console.WriteLine("             Nodes: " + nodeCount);
                Console.WriteLine("         Node size: " + nodeSize);
                Console.WriteLine("Max tasks per node: " + pool.MaxTasksPerComputeNode);
                Console.WriteLine("             Tasks: " + tasks.Count);
                Console.WriteLine("          Duration: " + stopwatch.Elapsed);
                Console.WriteLine();
                Console.WriteLine("Sample complete, hit ENTER to continue...");
                Console.ReadLine();

                // Clean up the resources we've created in the Batch account
                Console.WriteLine("Delete job? [yes] no");
                string response = Console.ReadLine().ToLower();
                if (response != "n" && response != "no")
                {
                    batchClient.JobOperations.DeleteJob(job.Id);
                }

                Console.WriteLine("Delete pool? [yes] no");
                response = Console.ReadLine();
                if (response != "n" && response != "no")
                {
                    batchClient.PoolOperations.DeletePool(pool.Id);
                }
            }
        }

        /// <summary>
        /// Creates a CloudPool associated with the Batch account. If an existing pool with the specified ID is found,
        /// the pool is resized to match the specified node count.
        /// </summary>
        /// <param name="batchClient">A fully initialized <see cref="BatchClient"/>.</param>
        /// <param name="poolId">The ID of the <see cref="CloudPool"/>.</param>
        /// <param name="nodeSize">The size of the nodes within the pool.</param>
        /// <param name="nodeCount">The number of nodes to create within the pool.</param>
        /// <param name="maxTasksPerNode">The maximum number of tasks to run concurrently on each node.</param>
        private async static Task CreatePool(BatchClient batchClient, string poolId, string nodeSize, int nodeCount, int maxTasksPerNode)
        {
            // Create and configure an unbound pool with the specified ID
            CloudPool pool = batchClient.PoolOperations.CreatePool(poolId: poolId,
                                                                   osFamily: "3",
                                                                   virtualMachineSize: nodeSize,
                                                                   targetDedicated: nodeCount);

            pool.MaxTasksPerComputeNode = maxTasksPerNode;

            // We want each node to be completely filled with tasks (i.e. up to maxTasksPerNode) before
            // tasks are assigned to the next node in the pool
            pool.TaskSchedulingPolicy = new TaskSchedulingPolicy(ComputeNodeFillType.Pack);

            await GettingStartedCommon.CreatePoolIfNotExistAsync(batchClient, pool);
        }

        /// <summary>
        /// Creates a CloudJob in the specified pool if a job with the specified ID is not found
        /// in the pool, otherwise returns the existing job.
        /// </summary>
        /// <param name="batchClient">A fully initialized <see cref="BatchClient"/>.</param>
        /// <param name="poolId">The ID of the CloudPool in which the job should be created.</param>
        /// <param name="jobId">The ID of the CloudJob.</param>
        /// <returns>A bound CloudJob.</returns>
        private static CloudJob CreateJob(BatchClient batchClient, string poolId, string jobId)
        {
            CloudJob job = GetJobIfExist(batchClient, jobId);
            if (job == null)
            {
                Console.WriteLine("Job {0} not found, creating...", jobId);

                CloudJob unboundJob = batchClient.JobOperations.CreateJob(jobId, new PoolInformation() { PoolId = poolId });
                unboundJob.Commit();

                job = batchClient.JobOperations.GetJob(jobId);
            }

            // Return the bound version of the job with all of its properties populated
            return job;
        }

        /// <summary>
        /// Returns an existing <see cref="CloudJob"/> if found in the Batch account.
        /// </summary>
        /// <param name="batchClient">A fully initialized <see cref="BatchClient"/>.</param>
        /// <param name="jobId">The <see cref="CloudJob.Id"/> of the desired pool.</param>
        /// <returns>A bound <see cref="CloudJob"/>, or <c>null</c> if the specified <see cref="CloudJob"/> does not exist.</returns>
        private static CloudJob GetJobIfExist(BatchClient batchClient, string jobId)
        {
            Console.WriteLine("Checking for existing job {0}...", jobId);

            // Construct a detail level with a filter clause that specifies the job
            ODATADetailLevel detail = new ODATADetailLevel(filterClause: string.Format("id eq '{0}'", jobId));

            foreach (CloudJob job in batchClient.JobOperations.ListJobs(detailLevel: detail))
            {
                Console.WriteLine("Existing job {0} found.", jobId);
                return job;
            }

            // No existing job found
            return null;
        }

        /// <summary>
        /// Asynchronous method that delays execution until the specified pool reaches the specified state.
        /// </summary>
        /// <param name="client">A fully intitialized <see cref="BatchClient"/>.</param>
        /// <param name="poolId">The ID of the pool to monitor for the specified <see cref="AllocationState"/>.</param>
        /// <param name="targetAllocationState">The allocation state to monitor.</param>
        /// <param name="timeout">The maximum time (in seconds) to wait for the pool to reach the specified state.</param>
        /// <returns></returns>
        private static async Task WaitForPoolToReachStateAsync(BatchClient client, string poolId, AllocationState targetAllocationState, TimeSpan timeout)
        {
            Console.WriteLine("Waiting for pool {0} to reach state {1}", poolId, targetAllocationState);

            DateTime startTime = DateTime.UtcNow;
            DateTime timeoutAfterThisTimeUtc = startTime.Add(timeout);

            ODATADetailLevel detail = new ODATADetailLevel(selectClause: "id,allocationState");
            CloudPool pool = await client.PoolOperations.GetPoolAsync(poolId, detail);

            while (pool.AllocationState != targetAllocationState)
            {
                Console.Write(".");

                await Task.Delay(TimeSpan.FromSeconds(10));
                await pool.RefreshAsync(detail);

                if (DateTime.UtcNow > timeoutAfterThisTimeUtc)
                {
                    Console.WriteLine();
                    Console.WriteLine("Timed out waiting for pool {0} to reach state {1}", poolId, targetAllocationState);

                    return;
                }
            }

            Console.WriteLine();
        }

        /// <summary>
        /// Asynchronous method that delays execution until the nodes within the specified pool reach the specified state.
        /// </summary>
        /// <param name="client">A fully intitialized <see cref="BatchClient"/>.</param>
        /// <param name="poolId">The ID of the pool containing the nodes to monitor.</param>
        /// <param name="targetNodeState">The node state to monitor.</param>
        /// <param name="timeout">The maximum time (in seconds) to wait for the nodes to reach the specified state.</param>
        /// <returns></returns>
        private static async Task WaitForNodesToReachStateAsync(BatchClient client, string poolId, ComputeNodeState targetNodeState, TimeSpan timeout)
        {
            Console.WriteLine("Waiting for nodes to reach state {0}", targetNodeState);

            DateTime startTime = DateTime.UtcNow;
            DateTime timeoutAfterThisTimeUtc = startTime.Add(timeout);

            CloudPool pool = await client.PoolOperations.GetPoolAsync(poolId);
            
            ODATADetailLevel detail = new ODATADetailLevel(selectClause: "id,state");
            IEnumerable<ComputeNode> computeNodes = pool.ListComputeNodes(detail);

            while (computeNodes.Any(computeNode => computeNode.State != targetNodeState))
            {
                Console.Write(".");
                
                await Task.Delay(TimeSpan.FromSeconds(10));
                computeNodes = pool.ListComputeNodes(detail).ToList();

                if (DateTime.UtcNow > timeoutAfterThisTimeUtc)
                {
                    Console.WriteLine();
                    Console.WriteLine("Timed out waiting for compute nodes in pool {0} to reach state {1}", poolId, targetNodeState.ToString());

                    return;
                }
            }

            Console.WriteLine();
        }

        /// <summary>
        /// Prints task information to the console for each of the nodes in the specified pool.
        /// </summary>
        /// <param name="poolId">The ID of the <see cref="CloudPool"/> containing the nodes whose task information
        /// should be printed to the console.</param>
        private static void PrintNodeTasks(BatchClient batchClient, string poolId)
        {
            ODATADetailLevel nodeDetail = new ODATADetailLevel(selectClause: "id,recentTasks");

            // Obtain and print the task information for each of the compute nodes in the pool.
            foreach (ComputeNode node in batchClient.PoolOperations.ListComputeNodes(poolId, nodeDetail))
            {
                Console.WriteLine(node.Id + " tasks:");

                if (node.RecentTasks != null && node.RecentTasks.Count() > 0)
                {
                    foreach (TaskInformation task in node.RecentTasks)
                    {
                        Console.WriteLine("\t{0}: {1}", task.TaskId, task.TaskState);
                    }
                }
                else
                    // No tasks found for the node
                    Console.WriteLine("\tNone");

            }
        }
    }
}