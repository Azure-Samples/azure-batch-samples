// Copyright (c) Microsoft Corporation
//
// Companion project to the following article:
// https://azure.microsoft.com/documentation/articles/batch-parallel-node-tasks/


namespace Microsoft.Azure.Batch.Samples.Articles.ParallelTasks
{
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

    public class Program
    {
        public static void Main(string[] args)
        {
            // You may adjust these values to experiment with different compute resource scenarios.
            const string nodeSize     = "small";
            const int nodeCount       = 4;
            const int maxTasksPerNode = 4;
            const int taskCount       = 32;
            
            // Ensure there are enough tasks to satisfy some wait conditions below
            const int minimumTaskCount = nodeCount * maxTasksPerNode * 2;
            if (taskCount < minimumTaskCount)
            {
                throw new Exception(string.Format("Must have at least two tasks per node core for this sample ({0} tasks in this configuration).", minimumTaskCount));
            }
            
            // In this sample, the tasks simply ping localhost on the compute nodes; adjust these
            // values to simulate variable task duration
            const int minPings = 30;
            const int maxPings = 60;

            const string poolId = "poolMaxTasks";
            const string jobId  = "jobMaxTasks";

            // Amount of time to wait before timing out (potentially) long-running tasks
            TimeSpan longTaskDurationLimit = TimeSpan.FromMinutes(30);

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

                CloudJob job = ArticleHelpers.CreateJobAsync(batchClient, poolId, jobId).Result;
                
                // The job's tasks ping localhost a random number of times between minPings and maxPings.
                // Adjust the minPings/maxPings values above to experiment with different task durations.
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
                // NOTE: Such a pause is not necessary within your own code. Tasks can be added to a job at any point and will be 
                // scheduled to execute on a compute node as soon any node has reached Idle state. Because the focus of this sample 
                // is the demonstration of running tasks in parallel on multiple compute nodes, we wait for all compute nodes to 
                // complete initialization and reach the Idle state in order to maximize the number of compute nodes available for 
                // parallelization.
                ArticleHelpers.WaitForPoolToReachStateAsync(batchClient, pool.Id, AllocationState.Steady, longTaskDurationLimit).Wait();
                ArticleHelpers.WaitForNodesToReachStateAsync(batchClient, pool.Id, ComputeNodeState.Idle, longTaskDurationLimit).Wait();

                // To reduce the chances of hitting Batch service throttling limits, we add the tasks in
                // one API call as opposed to a separate AddTask call for each. Bulk task submission is
                // crucial if you are adding large numbers of tasks to your jobs.
                batchClient.JobOperations.AddTask(job.Id, tasks);

                // Pause again to wait until *all* nodes are running tasks
                ArticleHelpers.WaitForNodesToReachStateAsync(batchClient, pool.Id, ComputeNodeState.Running, TimeSpan.FromMinutes(2)).Wait();

                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();

                // Print out task assignment information.
                Console.WriteLine();
                Console.WriteLine("Current task state:");
                PrintNodeTasks(batchClient, pool.Id);
                Console.WriteLine();

                // Pause execution while we wait for all of the tasks to complete
                Console.WriteLine("Waiting for task completion...");
                Console.WriteLine();
                batchClient.Utilities.CreateTaskStateMonitor().WaitAll(job.ListTasks(),
                                                                   TaskState.Completed,
                                                                   longTaskDurationLimit);

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
                Console.WriteLine();
                Console.WriteLine("Completed tasks:");
                string lastNodeId = string.Empty;
                foreach (CloudTask task in completedTasks)
                {
                    if (!string.Equals(lastNodeId, task.ComputeNodeInformation.ComputeNodeId))
                    {
                        Console.WriteLine();
                        Console.WriteLine(task.ComputeNodeInformation.ComputeNodeId);
                    }

                    lastNodeId = task.ComputeNodeInformation.ComputeNodeId;

                    Console.WriteLine("\t{0}: {1}", task.Id, task.CommandLine);
                }

                // Get a collection of the uncompleted tasks which may exist if the TaskMonitor timeout was hit
                List<CloudTask> uncompletedTasks = allTasks
                                                   .Where(t => t.State != TaskState.Completed)
                                                   .ToList();

                // Print a list of uncompleted tasks, if any
                Console.WriteLine();
                Console.WriteLine("Uncompleted tasks:");
                Console.WriteLine();
                if (uncompletedTasks.Any())
                {
                    foreach (CloudTask task in uncompletedTasks)
                    {
                        Console.WriteLine("\t{0}: {1}", task.Id, task.CommandLine);
                    }
                }
                else
                {
                    Console.WriteLine("\t<none>");
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

            }
        }
    }
}