// Copyright (c) Microsoft Corporation

namespace Microsoft.Azure.Batch.Samples.Articles.TaskDependencies
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Batch;
    using Auth;
    using Batch.Common;
    using Common;

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
            const string successMessage = "All tasks reached state Completed.";

            // Amount of time to wait before timing out long-running tasks.
            TimeSpan timeLimit = TimeSpan.FromMinutes(30);

            // Set up access to your Batch account with a BatchClient. Configure your AccountSettings in the
            // Microsoft.Azure.Batch.Samples.Common project within this solution.
            BatchSharedKeyCredentials cred = new BatchSharedKeyCredentials(AccountSettings.Default.BatchServiceUrl,
                                                                           AccountSettings.Default.BatchAccountName,
                                                                           AccountSettings.Default.BatchAccountKey);

            try
            {
                using (BatchClient batchClient = await BatchClient.OpenAsync(cred))
                {
                    // Create the pool.
                    Console.WriteLine("Creating pool [{0}]...", poolId);
                    CloudPool unboundPool =
                        batchClient.PoolOperations.CreatePool(poolId: poolId,
                                                              cloudServiceConfiguration: new CloudServiceConfiguration(osFamily),
                                                              virtualMachineSize: nodeSize,
                                                              targetDedicated: nodeCount);
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

                        // Task A is a parent task that exits with a non-zero exit code.
                        new CloudTask("A", "cmd.exe /c echo A exit 1"),
                        // Task B depends on task A
                        new CloudTask("B", "cmd.exe /c echo B")
                        {
                            DependsOn = TaskDependencies.OnId("A"),
                            ExitConditions = new ExitConditions()
                            {
                                // If task A exits with a non-zero exit code, run downstream task B.
                                Default = new ExitOptions()
                                {
                                    DependencyAction = DependencyAction.Satisfy
                                }
                            }
                        },

                        // Task C is a parent task that cannot run and so causes a scheduling error.
                        new CloudTask("C", "NonExistentProgram.exe"),
                        // Task D depends on task C.
                        new CloudTask("D", "cmd.exe /c echo D")
                        {
                            DependsOn = TaskDependencies.OnId("C"),
                            ExitConditions = new ExitConditions()
                            {
                                // When task C exits with a scheduling error, the downstream task D will not run.
                                SchedulingError = new ExitOptions()
                                {
                                    DependencyAction = DependencyAction.Block
                                },
                            }
                        },
                    };

                    // Add the tasks to the job.
                    await batchClient.JobOperations.AddTaskAsync(jobId, tasks);

                    // Pause execution while we wait for the tasks to complete, and notify
                    // whether the tasks completed successfully.
                    Console.WriteLine("Waiting for task completion...");
                    Console.WriteLine();
                    CloudJob job = await batchClient.JobOperations.GetJobAsync(jobId);

                    try
                    {
                        await batchClient.Utilities.CreateTaskStateMonitor().WhenAll(
                            job.ListTasks(),
                            TaskState.Completed,
                            timeLimit);

                        Console.WriteLine("All tasks completed successfully.");
                        Console.WriteLine();
                    }
                    catch (TimeoutException e)
                    {
                        Console.WriteLine(e);
                    }

                    await batchClient.JobOperations.TerminateJobAsync(jobId, successMessage);

                    ODATADetailLevel detail = new ODATADetailLevel()
                    {
                        SelectClause = "id, executionInfo"
                    };

                    foreach (CloudTask task in tasks)
                    {
                        // Populate the task's properties with the latest info from the Batch service
                        await task.RefreshAsync(detail);

                        Console.WriteLine(task.Id);

                        if (task.ExecutionInformation.ExitCode.HasValue)
                        {
                            Console.WriteLine("Exit code value: {0}", task.ExecutionInformation.ExitCode.Value);
                        }

                        if (task.ExecutionInformation.SchedulingError != null)
                        {
                            // A scheduling error indicates a problem starting the task on the node. It is important to note that
                            // the task's state can be "Completed," yet still have encountered a scheduling error.

                            //allTasksSuccessful = false;

                            Console.WriteLine("WARNING: Task [{0}] encountered a scheduling error: {1}", task.Id, task.ExecutionInformation.SchedulingError.Message);
                        }
                        else if (task.ExecutionInformation.ExitCode != 0)
                        {
                            // A non-zero exit code may indicate that the application executed by the task encountered an error
                            // during execution. As not every application returns non-zero on failure by default (e.g. robocopy),
                            // your implementation of error checking may differ from this example.

                            //allTasksSuccessful = false;

                            Console.WriteLine("WARNING: Task [{0}] returned a non-zero exit code - this may indicate task execution or completion failure.", task.Id);
                        }

                    }

                }
            }
            finally
            {
                BatchClient batchClient = await BatchClient.OpenAsync(cred);

                // Clean up the resources we've created in the Batch account
                Console.Write("Delete job? [yes] no: ");
                string response = Console.ReadLine().ToLower();
                if (response != "n" && response != "no")
                {
                    await batchClient.JobOperations.DeleteJobAsync(jobId);
                    Console.WriteLine("Job '{0}' deleted.", jobId);
                }

                Console.Write("Delete pool? [yes] no: ");
                response = Console.ReadLine().ToLower();
                if (response != "n" && response != "no")
                {
                    await batchClient.PoolOperations.DeletePoolAsync(poolId);
                    Console.WriteLine("Pool '{0}' deleted.", poolId);
                }
            }                
        }
    }
}