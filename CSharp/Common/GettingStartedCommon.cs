// Copyright (c) Microsoft Corporation

namespace Microsoft.Azure.Batch.Samples.Common
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
    using BatchTaskSchedulingPolicy = global::Azure.Compute.Batch.BatchTaskSchedulingPolicy;
    using ArmBatchAccountFixedScaleSettings = global::Azure.ResourceManager.Batch.Models.BatchAccountFixedScaleSettings;
    using ArmBatchAccountPoolScaleSettings = global::Azure.ResourceManager.Batch.Models.BatchAccountPoolScaleSettings;

    public static class GettingStartedCommon
    {
        /// <summary>
        /// Lists all the pools in the Batch account using the ARM (control plane) SDK.
        /// </summary>
        public static async Task PrintPoolsAsync(BatchAccountResource batchAccount)
        {
            Console.WriteLine("Listing Pools");
            Console.WriteLine("=============");

            await foreach (BatchAccountPoolResource pool in batchAccount.GetBatchAccountPools().GetAllAsync().ConfigureAwait(false))
            {
                BatchAccountPoolData data = pool.Data;
                Console.WriteLine("State of pool {0} is {1} and it has {2} dedicated nodes and {3} low-priority nodes of size {4}",
                    data.Name,
                    data.AllocationState,
                    data.CurrentDedicatedNodes,
                    data.CurrentLowPriorityNodes,
                    data.VmSize);
            }
            Console.WriteLine("=============");
        }

        /// <summary>
        /// Lists all the jobs in the Batch account.
        /// </summary>
        public static async Task PrintJobsAsync(BatchClient batchClient)
        {
            Console.WriteLine("Listing Jobs");
            Console.WriteLine("============");

            await foreach (BatchJob job in batchClient
                .GetJobsAsync(select: new[] { "id", "state" })
                .ConfigureAwait(false))
            {
                Console.WriteLine("State of job " + job.Id + " is " + job.State);
            }

            Console.WriteLine("============");
        }

        /// <summary>
        /// Prints task information to the console for each of the nodes in the specified pool.
        /// </summary>
        public static async Task PrintNodeTasksAsync(BatchClient batchClient, string poolId)
        {
            Console.WriteLine("Listing Node Tasks");
            Console.WriteLine("==================");

            await foreach (BatchNode node in batchClient
                .GetNodesAsync(poolId, select: new[] { "id", "recentTasks" })
                .ConfigureAwait(false))
            {
                Console.WriteLine();
                Console.WriteLine(node.Id + " tasks:");

                if (node.RecentTasks != null && node.RecentTasks.Any())
                {
                    foreach (BatchTaskInfo task in node.RecentTasks)
                    {
                        Console.WriteLine("\t{0}: {1}", task.TaskId, task.TaskState);
                    }
                }
                else
                {
                    Console.WriteLine("\tNone");
                }
            }

            Console.WriteLine("==================");
        }

        /// <summary>
        /// Prints running task and task slot counts to the console for each of the nodes in the specified pool.
        /// </summary>
        public static async Task PrintNodeTaskCountsAsync(BatchClient batchClient, string poolId)
        {
            Console.WriteLine("Listing Node Running Task Counts");
            Console.WriteLine("==================");

            await foreach (BatchNode node in batchClient
                .GetNodesAsync(poolId, select: new[] { "id", "runningTasksCount", "runningTaskSlotsCount" })
                .ConfigureAwait(false))
            {
                Console.WriteLine();
                Console.WriteLine(node.Id + " :");
                Console.WriteLine($"RunningTasks = {node.RunningTasksCount}, RunningTaskSlots = {node.RunningTaskSlotsCount}");
            }

            Console.WriteLine("==================");
        }

        /// <summary>
        /// Prints task and task slot counts per task state for the specified job.
        /// </summary>
        public static async Task PrintJobTaskCountsAsync(BatchClient batchClient, string jobId)
        {
            Console.WriteLine("Listing Job Task Counts");
            Console.WriteLine("==================");

            BatchTaskCountsResult result = await batchClient.GetJobTaskCountsAsync(jobId).ConfigureAwait(false);

            Console.WriteLine();
            Console.WriteLine(jobId + " :");
            Console.WriteLine("\t\tActive\tRunning\tCompleted");
            Console.WriteLine($"TaskCounts:\t{result.TaskCounts.Active}\t{result.TaskCounts.Running}\t{result.TaskCounts.Completed}");
            Console.WriteLine($"TaskSlotCounts:\t{result.TaskSlotCounts.Active}\t{result.TaskSlotCounts.Running}\t{result.TaskSlotCounts.Completed}");

            Console.WriteLine("==================");
        }

        public static string CreateJobId(string prefix)
        {
            return string.Format("{0}-{1}-{2}", prefix, new string(Environment.UserName.Where(char.IsLetterOrDigit).ToArray()), DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        }

        /// <summary>
        /// Waits for the specified tasks under the specified job to complete and then prints each task's
        /// standard out / standard error to the console.
        /// </summary>
        public static async Task WaitForTasksAndPrintOutputAsync(BatchClient batchClient, string jobId, IEnumerable<string> taskIds, TimeSpan timeout)
        {
            List<string> ids = taskIds.ToList();
            DateTime deadline = DateTime.UtcNow.Add(timeout);

            // Poll each task until completed (or timeout).
            foreach (string taskId in ids)
            {
                while (true)
                {
                    BatchTask task = await batchClient.GetTaskAsync(jobId, taskId).ConfigureAwait(false);
                    if (task.State == BatchTaskState.Completed)
                    {
                        break;
                    }
                    if (DateTime.UtcNow > deadline)
                    {
                        throw new TimeoutException($"Timed out waiting for task {taskId} in job {jobId} to complete.");
                    }
                    await Task.Delay(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
                }
            }

            // Print each task's standard out/err.
            foreach (string taskId in ids)
            {
                Console.WriteLine("Task {0}", taskId);

                try
                {
                    BinaryData stdout = await batchClient.GetTaskFileAsync(jobId, taskId, "stdout.txt").ConfigureAwait(false);
                    Console.WriteLine("Standard out:");
                    Console.WriteLine(stdout.ToString());
                }
                catch (RequestFailedException ex) when (ex.Status == 404)
                {
                    Console.WriteLine("Standard out: <not found>");
                }

                try
                {
                    BinaryData stderr = await batchClient.GetTaskFileAsync(jobId, taskId, "stderr.txt").ConfigureAwait(false);
                    Console.WriteLine("Standard error:");
                    Console.WriteLine(stderr.ToString());
                }
                catch (RequestFailedException ex) when (ex.Status == 404)
                {
                    Console.WriteLine("Standard error: <not found>");
                }

                Console.WriteLine();
            }
        }

        /// <summary>
        /// Creates a pool via the ARM (control plane) SDK if it doesn't already exist. If the pool already exists,
        /// the fixed scale settings are updated to match <paramref name="poolData"/> when they differ.
        /// </summary>
        public static async Task<CreatePoolResult> CreatePoolIfNotExistAsync(BatchAccountResource batchAccount, string poolId, BatchAccountPoolData poolData)
        {
            BatchAccountPoolCollection pools = batchAccount.GetBatchAccountPools();

            bool exists = await pools.ExistsAsync(poolId).ConfigureAwait(false);
            if (!exists)
            {
                Console.WriteLine("Attempting to create pool: {0}", poolId);
                await pools.CreateOrUpdateAsync(WaitUntil.Completed, poolId, poolData).ConfigureAwait(false);
                Console.WriteLine("Created pool {0} with VM size {1}", poolId, poolData.VmSize);
                return CreatePoolResult.CreatedNew;
            }

            Console.WriteLine("The pool already existed");
            BatchAccountPoolResource existing = await pools.GetAsync(poolId).ConfigureAwait(false);
            BatchAccountPoolData existingData = existing.Data;

            ArmBatchAccountFixedScaleSettings desiredFixed = poolData.ScaleSettings?.FixedScale;
            ArmBatchAccountFixedScaleSettings currentFixed = existingData.ScaleSettings?.FixedScale;
            bool autoScaleConfigured = existingData.ScaleSettings?.AutoScale != null;

            bool needsResize = !autoScaleConfigured
                && desiredFixed != null
                && (existingData.CurrentDedicatedNodes != desiredFixed.TargetDedicatedNodes
                    || existingData.CurrentLowPriorityNodes != desiredFixed.TargetLowPriorityNodes);

            if (needsResize)
            {
                existingData.ScaleSettings = new ArmBatchAccountPoolScaleSettings
                {
                    FixedScale = new ArmBatchAccountFixedScaleSettings
                    {
                        TargetDedicatedNodes = desiredFixed.TargetDedicatedNodes,
                        TargetLowPriorityNodes = desiredFixed.TargetLowPriorityNodes,
                        ResizeTimeout = desiredFixed.ResizeTimeout,
                        NodeDeallocationOption = desiredFixed.NodeDeallocationOption,
                    },
                };
                await pools.CreateOrUpdateAsync(WaitUntil.Started, poolId, existingData).ConfigureAwait(false);
                return CreatePoolResult.ResizedExisting;
            }

            return CreatePoolResult.PoolExisted;
        }

        /// <summary>
        /// Generates a file in a temp location with the specified name and text.
        /// </summary>
        public static string GenerateTemporaryFile(string fileName, string fileText)
        {
            string filePath = Path.Combine(Path.GetTempPath(), fileName);
            File.WriteAllText(filePath, fileText);

            return filePath;
        }
    }
}
