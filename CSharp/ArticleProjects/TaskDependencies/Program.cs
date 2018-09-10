// Copyright (c) Microsoft Corporation

namespace Microsoft.Azure.Batch.Samples.Articles.TaskDependencies
{
    using System;
    using System.Collections.Generic;
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

        private static async Task MainAsync(string[] args)
        {
            // You may adjust these values to experiment with different compute resource scenarios.
            const string nodeSize = "standard_d1_v2";
            const string osFamily = "5";
            const int nodeCount = 1;

            const string poolId = "TaskDependenciesSamplePool";
            const string jobId = "TaskDependenciesSampleJob";

            // Amount of time to wait before timing out long-running tasks.
            TimeSpan timeLimit = TimeSpan.FromMinutes(30);

            // Set up access to your Batch account with a BatchClient. Configure your AccountSettings in the
            // Microsoft.Azure.Batch.Samples.Common project within this solution.
            AccountSettings accountSettings = SampleHelpers.LoadAccountSettings();

            BatchSharedKeyCredentials cred = new BatchSharedKeyCredentials(
                accountSettings.BatchServiceUrl,
                accountSettings.BatchAccountName,
                accountSettings.BatchAccountKey);

            try
            {
                using (BatchClient batchClient = BatchClient.Open(cred))
                {
                    // Create the pool.
                    Console.WriteLine("Creating pool [{0}]...", poolId);
                    CloudPool unboundPool =
                        batchClient.PoolOperations.CreatePool(
                            poolId: poolId,
                            cloudServiceConfiguration: new CloudServiceConfiguration(osFamily),
                            virtualMachineSize: nodeSize,
                            targetDedicatedComputeNodes: nodeCount);
                    await unboundPool.CommitAsync();

                    // Create the job and specify that it uses tasks dependencies.
                    Console.WriteLine("Creating job [{0}]...", jobId);
                    CloudJob unboundJob = batchClient.JobOperations.CreateJob(jobId,
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

                        // Task A is the parent task.
                        new CloudTask("A", "cmd.exe /c echo A")
                        {
                            // Specify exit conditions for task A and their dependency actions.
                            ExitConditions = new ExitConditions
                            {
                                // If task A exits with a pre-processing error, block any downstream tasks (in this example, task B).
                                PreProcessingError = new ExitOptions
                                {
                                    DependencyAction = DependencyAction.Block
                                },
                                // If task A exits with the specified error codes, block any downstream tasks (in this example, task B).
                                ExitCodes = new List<ExitCodeMapping>
                                {
                                    new ExitCodeMapping(10, new ExitOptions() { DependencyAction = DependencyAction.Block }),
                                    new ExitCodeMapping(20, new ExitOptions() { DependencyAction = DependencyAction.Block })
                                },
                                // If task A succeeds or fails with any other error, any downstream tasks become eligible to run 
                                // (in this example, task B).
                                Default = new ExitOptions
                                {
                                    DependencyAction = DependencyAction.Satisfy
                                }
                            }
                        },
                        // Task B depends on task A. Whether it becomes eligible to run depends on how task A exits.
                        new CloudTask("B", "cmd.exe /c echo B")
                        {
                            DependsOn = TaskDependencies.OnId("A")
                        },
                    };

                    // Add the tasks to the job.
                    await batchClient.JobOperations.AddTaskAsync(jobId, tasks);

                    // Pause execution while we wait for the tasks to complete, and notify
                    // whether the tasks completed successfully.
                    Console.WriteLine("Waiting for task completion...");
                    Console.WriteLine();
                    CloudJob job = await batchClient.JobOperations.GetJobAsync(jobId);

                    await batchClient.Utilities.CreateTaskStateMonitor().WhenAll(
                        job.ListTasks(),
                        TaskState.Completed,
                        timeLimit);

                    Console.WriteLine("All tasks completed successfully.");
                    Console.WriteLine();
                }
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
                using (BatchClient batchClient = BatchClient.Open(cred))
                {
                    CloudJob job = await batchClient.JobOperations.GetJobAsync(jobId);

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
}