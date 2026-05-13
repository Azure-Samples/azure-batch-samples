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
    using System.Threading.Tasks;
    using global::Azure;
    using global::Azure.Compute.Batch;
    using global::Azure.ResourceManager.Batch;
    using Microsoft.Azure.Batch.Samples.Common;

    public class Program
    {
        public static void Main(string[] args)
        {
            try
            {
                MainAsync(args).Wait();
            }
            catch (AggregateException ae)
            {
                Console.WriteLine();
                Console.WriteLine("One or more exceptions occurred.");
                Console.WriteLine();

                SampleHelpers.PrintAggregateException(ae.Flatten());
            }
            finally
            {
                Console.WriteLine();
                Console.WriteLine("Sample complete, hit ENTER to exit...");
                Console.ReadLine();
            }
        }

        private static async Task MainAsync(string[] args)
        {
            const string nodeSize      = "standard_d2_v3";
            const int nodeCount        = 4;
            const int taskSlotsPerNode = 4;
            const int taskCount        = 32;

            int minimumTaskCount = nodeCount * taskSlotsPerNode * 2;
            if (taskCount < minimumTaskCount)
            {
                Console.WriteLine("You must specify at least two tasks per node core for this sample ({0} tasks in this configuration).", minimumTaskCount);
                return;
            }

            const int maxTaskSlots     = 2;
            if (maxTaskSlots > taskSlotsPerNode)
            {
                Console.WriteLine("Invalid task slot configuration: maxTaskSlots for task should not be greater than pool's TaskSlotsPerNode");
                return;
            }

            const int minPings = 30;
            const int maxPings = 60;

            const string poolId = "ParallelTasksSamplePool";
            const string jobId  = "ParallelTasksSampleJob";

            TimeSpan longTaskDurationLimit = TimeSpan.FromMinutes(30);

            AccountSettings accountSettings = SampleHelpers.LoadAccountSettings();

            BatchClient batchClient = ClientFactory.CreateBatchClient(accountSettings);
            BatchAccountResource batchAccount = ClientFactory.CreateBatchAccountResource(accountSettings);

            // Create the pool via ARM (or get the existing one).
            BatchAccountPoolResource pool = await ArticleHelpers.CreatePoolIfNotExistAsync(
                batchAccount,
                poolId,
                nodeSize,
                nodeCount,
                taskSlotsPerNode);

            // Create a job (or get the existing one) via the data-plane SDK.
            BatchJob job = await ArticleHelpers.CreateJobIfNotExistAsync(batchClient, poolId, jobId);

            // Tasks ping localhost a random number of times between minPings and maxPings.
            Random rand = new Random();
            var tasks = new List<BatchTaskCreateOptions>();
            for (int i = 1; i <= taskCount; i++)
            {
                string taskId = "task" + i.ToString().PadLeft(3, '0');
                string taskCommandLine = "ping -n " + rand.Next(minPings, maxPings + 1).ToString() + " localhost";
                tasks.Add(new BatchTaskCreateOptions(taskId, taskCommandLine)
                {
                    RequiredSlots = rand.Next(1, maxTaskSlots + 1),
                });
            }

            // Wait for the pool to reach Steady allocation state and for nodes to be Idle.
            await ArticleHelpers.WaitForPoolToReachStateAsync(batchClient, poolId, AllocationState.Steady, longTaskDurationLimit);
            await ArticleHelpers.WaitForNodesToReachStateAsync(batchClient, poolId, BatchNodeState.Idle, longTaskDurationLimit);

            // Bulk task submission.
            const int batchSize = 100;
            for (int offset = 0; offset < tasks.Count; offset += batchSize)
            {
                int count = Math.Min(batchSize, tasks.Count - offset);
                var slice = tasks.GetRange(offset, count);
                await batchClient.CreateTaskCollectionAsync(job.Id, new BatchTaskGroup(slice));
            }

            // Pause again to wait until *all* nodes are running tasks
            await ArticleHelpers.WaitForNodesToReachStateAsync(batchClient, poolId, BatchNodeState.Running, TimeSpan.FromMinutes(2));

            Stopwatch stopwatch = Stopwatch.StartNew();

            Console.WriteLine();
            await GettingStartedCommon.PrintNodeTasksAsync(batchClient, poolId);
            Console.WriteLine();

            Console.WriteLine();
            await GettingStartedCommon.PrintNodeTaskCountsAsync(batchClient, poolId);
            Console.WriteLine();

            await Task.Delay(TimeSpan.FromSeconds(5));
            Console.WriteLine();
            await GettingStartedCommon.PrintJobTaskCountsAsync(batchClient, jobId);
            Console.WriteLine();

            Console.WriteLine("Waiting for task completion...");
            Console.WriteLine();

            try
            {
                await WaitForAllTasksCompletedAsync(batchClient, job.Id, longTaskDurationLimit);
            }
            catch (TimeoutException e)
            {
                Console.WriteLine(e.ToString());
            }

            stopwatch.Stop();

            await Task.Delay(TimeSpan.FromSeconds(5));
            Console.WriteLine();
            await GettingStartedCommon.PrintJobTaskCountsAsync(batchClient, jobId);
            Console.WriteLine();

            // Pull the tasks back, selecting only the properties we need.
            var allTasks = new List<BatchTask>();
            await foreach (BatchTask t in batchClient.GetTasksAsync(
                job.Id,
                select: new[] { "id", "commandLine", "nodeInfo", "state", "requiredSlots" }))
            {
                allTasks.Add(t);
            }

            List<BatchTask> completedTasks = allTasks
                .Where(t => t.State == BatchTaskState.Completed)
                .OrderBy(t => t.NodeInfo?.NodeId)
                .ToList();

            Console.WriteLine();
            Console.WriteLine("Completed tasks:");
            string lastNodeId = string.Empty;
            foreach (BatchTask task in completedTasks)
            {
                string nodeId = task.NodeInfo?.NodeId;
                if (!string.Equals(lastNodeId, nodeId))
                {
                    Console.WriteLine();
                    Console.WriteLine(nodeId);
                }

                lastNodeId = nodeId;

                Console.WriteLine($"\t{task.Id} (slots={task.RequiredSlots}): {task.CommandLine}");
            }

            List<BatchTask> uncompletedTasks = allTasks
                .Where(t => t.State != BatchTaskState.Completed)
                .OrderBy(t => t.Id)
                .ToList();

            Console.WriteLine();
            Console.WriteLine("Uncompleted tasks:");
            Console.WriteLine();
            if (uncompletedTasks.Any())
            {
                foreach (BatchTask task in uncompletedTasks)
                {
                    Console.WriteLine("\t{0}: {1}", task.Id, task.CommandLine);
                }
            }
            else
            {
                Console.WriteLine("\t<none>");
            }

            Console.WriteLine();
            Console.WriteLine("              Nodes: " + nodeCount);
            Console.WriteLine("          Node size: " + nodeSize);
            Console.WriteLine("Task slots per node: " + (pool.Data.TaskSlotsPerNode?.ToString() ?? taskSlotsPerNode.ToString()));
            Console.WriteLine(" Max slots per task: " + maxTaskSlots);
            Console.WriteLine("              Tasks: " + tasks.Count);
            Console.WriteLine("           Duration: " + stopwatch.Elapsed);
            Console.WriteLine();
            Console.WriteLine("Done!");
            Console.WriteLine();

            Console.WriteLine("Delete job? [yes] no");
            string response = Console.ReadLine().ToLower();
            if (response != "n" && response != "no")
            {
                await batchClient.DeleteJobAsync(WaitUntil.Started, job.Id);
            }

            Console.WriteLine("Delete pool? [yes] no");
            response = Console.ReadLine();
            if (response != "n" && response != "no")
            {
                await pool.DeleteAsync(WaitUntil.Started);
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
