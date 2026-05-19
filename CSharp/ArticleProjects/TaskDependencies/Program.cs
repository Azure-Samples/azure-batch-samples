// Copyright (c) Microsoft Corporation

namespace Microsoft.Azure.Batch.Samples.Articles.TaskDependencies
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using global::Azure;
    using global::Azure.Compute.Batch;
    using global::Azure.ResourceManager.Batch;
    using Microsoft.Azure.Batch.Samples.Common;

    public class Program
    {
        public static async Task Main(string[] args)
        {
            try
            {
                await RunAsync(args);
            }
            catch (Exception e)
            {
                Console.WriteLine();
                Console.WriteLine("An exception occurred.");
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
            }
            finally
            {
                Console.WriteLine();
                Console.WriteLine("Sample complete, hit ENTER to exit...");
                Console.ReadLine();
            }
        }

        private static async Task RunAsync(string[] args)
        {
            const string nodeSize = "standard_d2_v3";
            const int nodeCount = 1;

            const string poolId = "TaskDependenciesSamplePool";
            const string jobId = "TaskDependenciesSampleJob";

            TimeSpan timeLimit = TimeSpan.FromMinutes(30);

            AccountSettings accountSettings = SampleHelpers.LoadAccountSettings();

            BatchClient batchClient = ClientFactory.CreateBatchClient(accountSettings);
            BatchAccountResource batchAccount = SampleHelpers.GetBatchAccountResource(accountSettings);

            try
            {
                // Create the pool via ARM (or get the existing one).
                Console.WriteLine("Creating pool [{0}]...", poolId);
                await ArticleHelpers.CreatePoolIfNotExistAsync(
                    batchAccount,
                    poolId,
                    nodeSize,
                    nodeCount,
                    taskSlotsPerNode: 1);

                // Create the job and enable task dependencies.
                // IMPORTANT: UsesTaskDependencies must be set to true on the job (default is false) in order to use task dependencies.
                Console.WriteLine("Creating job [{0}]...", jobId);
                var jobOptions = new BatchJobCreateOptions(jobId, new BatchPoolInfo { PoolId = poolId })
                {
                    UsesTaskDependencies = true,
                };
                await batchClient.CreateJobAsync(jobOptions);

                // Build the dependency graph.
                var tasks = new List<BatchTaskCreateOptions>
                {
                    // 'Rain' and 'Sun' don't depend on any other tasks.
                    new BatchTaskCreateOptions("Rain", "cmd.exe /c echo Rain"),
                    new BatchTaskCreateOptions("Sun", "cmd.exe /c echo Sun"),

                    // 'Flowers' depends on completion of both 'Rain' and 'Sun'.
                    new BatchTaskCreateOptions("Flowers", "cmd.exe /c echo Flowers")
                    {
                        DependsOn = new BatchTaskDependencies
                        {
                            TaskIds = { "Rain", "Sun" },
                        },
                    },

                    // Tasks 1, 2 and 3 are referenced by a task range elsewhere.
                    new BatchTaskCreateOptions("1", "cmd.exe /c echo 1"),
                    new BatchTaskCreateOptions("2", "cmd.exe /c echo 2"),
                    new BatchTaskCreateOptions("3", "cmd.exe /c echo 3"),

                    // Task A is the parent task; B depends on A.
                    new BatchTaskCreateOptions("A", "cmd.exe /c echo A")
                    {
                        ExitConditions = new ExitConditions
                        {
                            PreProcessingError = new ExitOptions
                            {
                                DependencyAction = DependencyAction.Block,
                            },
                            DefaultExitOptions = new ExitOptions
                            {
                                DependencyAction = DependencyAction.Satisfy,
                            },
                        },
                    },
                    new BatchTaskCreateOptions("B", "cmd.exe /c echo B")
                    {
                        DependsOn = new BatchTaskDependencies
                        {
                            TaskIds = { "A" },
                        },
                    },
                };

                // Add per-exit-code dependency mappings to task A.
                tasks[5].ExitConditions.ExitCodes.Add(new ExitCodeMapping(10, new ExitOptions { DependencyAction = DependencyAction.Block }));
                tasks[5].ExitConditions.ExitCodes.Add(new ExitCodeMapping(20, new ExitOptions { DependencyAction = DependencyAction.Block }));

                // Bulk-add the tasks.
                await batchClient.CreateTaskCollectionAsync(jobId, new BatchTaskGroup(tasks));

                Console.WriteLine("Waiting for task completion...");
                Console.WriteLine();

                await WaitForAllTasksCompletedAsync(batchClient, jobId, timeLimit);

                Console.WriteLine("All tasks completed successfully.");
                Console.WriteLine();
            }
            catch (Exception e)
            {
                Console.WriteLine();
                Console.WriteLine("An exception occurred.");
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
            }
            finally
            {
                Console.Write("Delete job? [yes] no: ");
                string response = Console.ReadLine().ToLower();
                if (response != "n" && response != "no")
                {
                    await batchClient.DeleteJobAsync(WaitUntil.Started, jobId);
                }

                Console.Write("Delete pool? [yes] no: ");
                response = Console.ReadLine().ToLower();
                if (response != "n" && response != "no")
                {
                    BatchAccountPoolResource pool = await batchAccount.GetBatchAccountPools().GetAsync(poolId);
                    await pool.DeleteAsync(WaitUntil.Started);
                }
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
