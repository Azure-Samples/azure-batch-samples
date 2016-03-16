// Copyright (c) Microsoft Corporation

namespace Microsoft.Azure.Batch.Samples.Articles.TaskDependencies
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
            try
            {
                // Call the asynchronous version of the Main() method. This is done so that we can await various
                // calls to async methods within the "Main" method of this console application.
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
            // You may adjust these values to experiment with different compute resource scenarios.
            const string nodeSize = "small";
            const string osFamily = "4";
            const int nodeCount = 1;

            const string poolId = "TaskDependenciesSamplePool";
            const string jobId = "TaskDependenciesSampleJob";

            // Amount of time to wait before timing out long-running tasks.
            TimeSpan timeLimit = TimeSpan.FromMinutes(30);

            // Set up access to your Batch account with a BatchClient. Configure your AccountSettings in the
            // Microsoft.Azure.Batch.Samples.Common project within this solution.
            BatchSharedKeyCredentials cred = new BatchSharedKeyCredentials(AccountSettings.Default.BatchServiceUrl,
                                                                           AccountSettings.Default.BatchAccountName,
                                                                           AccountSettings.Default.BatchAccountKey);

            using (BatchClient batchClient = await BatchClient.OpenAsync(cred))
            {
                // Create the pool.
                Console.WriteLine("Creating pool [{0}]...", poolId);
                CloudPool unboundPool =
                    batchClient.PoolOperations.CreatePool(poolId: poolId,
                                                          osFamily: osFamily,
                                                          virtualMachineSize: nodeSize,
                                                          targetDedicated: nodeCount);
                await unboundPool.CommitAsync();

                // Create the job and specify that it uses tasks dependencies.
                Console.WriteLine("Creating job [{0}]...", jobId);
                CloudJob unboundJob = batchClient.JobOperations.CreateJob( jobId,
                    new PoolInformation { PoolId = poolId });

                // IMPORTANT: This is REQUIRED for using task dependencies.
                unboundJob.UsesTaskDependencies = true;
                
                await unboundJob.CommitAsync();

                // Create the collection of tasks that will be added to the job.
                List<CloudTask> tasks = new List<CloudTask>
                {
                    // 'Rain' and 'Sun' don't depend on any other tasks
                    new CloudTask("Rain", "cmd.exe /c echo Rain"),
                    new CloudTask("Sun", "cmd.exe /c echo Sun"),
 
                    // Task 'Flowers' depends on completion of both 'Rain' and 'Sun'
                    // before it is run.
                    new CloudTask("Flowers", "cmd.exe /c echo Flowers")
                    {
                        DependsOn = TaskDependencies.OnIds("Rain", "Sun")
                    },
 
                    // Tasks 1, 2, and 3 don't depend on any other tasks. Because
                    // we will be using them for a task range dependency, we must
                    // specify string representations of integers as their ids.
                    new CloudTask("1", "cmd.exe /c echo 1"),
                    new CloudTask("2", "cmd.exe /c echo 2"),
                    new CloudTask("3", "cmd.exe /c echo 3"),
 
                    // Task 4 depends on a range of tasks, 1 through 3
                    new CloudTask("4", "cmd.exe /c echo 4")
                    {
                        // To use a range of tasks, their ids must be integer values.
                        // Note that we pass integers as parameters to TaskIdRange,
                        // but their ids (above) are string representations of the ids.
                        DependsOn = TaskDependencies.OnIdRange(1, 3)
                    },

                    // Task 5 depends on a range of tasks, 1 through 3, and 'Flowers'
                    new CloudTask("5", "cmd.exe /c echo 5")
                    {
                        DependsOn = new TaskDependencies(
                            new[] { "Flowers" },
                            new[] { new TaskIdRange(1, 3) })
                    },
                };

                // Add the tasks to the job.
                await batchClient.JobOperations.AddTaskAsync(jobId, tasks);

                // Pause execution while we wait for the tasks to complete, and notify
                // whether the tasks completed successfully.
                Console.WriteLine("Waiting for task completion...");
                Console.WriteLine();
                CloudJob job = await batchClient.JobOperations.GetJobAsync(jobId);
                if (await batchClient.Utilities.CreateTaskStateMonitor().WhenAllAsync(
                        job.ListTasks(),
                        TaskState.Completed,
                        timeLimit))
                {
                    Console.WriteLine("Operation timed out while waiting for submitted tasks to reach state {0}",
                        TaskState.Completed);
                }
                else
                {
                    Console.WriteLine("All tasks completed successfully.");
                    Console.WriteLine();
                }

                // Clean up the resources we've created in the Batch account
                Console.Write("Delete job? [yes] no: ");
                string response = Console.ReadLine().ToLower();
                if (response != "n" && response != "no")
                {
                    await batchClient.JobOperations.DeleteJobAsync(job.Id);
                }

                Console.Write("Delete pool? [yes] no: ");
                response = Console.ReadLine().ToLower();
                if (response != "n" && response != "no")
                {
                    await batchClient.PoolOperations.DeletePoolAsync(poolId);
                }
            }
        }
    }
}