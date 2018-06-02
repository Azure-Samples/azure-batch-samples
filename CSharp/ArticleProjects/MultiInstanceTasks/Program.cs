// Copyright (c) Microsoft Corporation
//
// Companion project to the following article:
// https://azure.microsoft.com/documentation/articles/batch-mpi/

namespace Microsoft.Azure.Batch.Samples.MultiInstanceTasks
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using Microsoft.Azure.Batch;
    using Microsoft.Azure.Batch.Common;
    using Microsoft.Azure.Batch.Auth;
    using Microsoft.Azure.Batch.Samples.Common;

    public class Program
    {
        public static void Main(string[] args)
        {
            try
            {
                // Call the asynchronous version of the Main() method. This is done so that we can await various
                // calls to async methods within the "Main" method of this console application.
                MainAsync().Wait();
            }
            catch (AggregateException ae)
            {
                Console.WriteLine();
                Console.WriteLine("One or more exceptions occurred.");
                Console.WriteLine();

                SampleHelpers.PrintAggregateException(ae);
            }
            finally
            {
                Console.WriteLine();
                Console.WriteLine("Sample complete, hit ENTER to exit...");
                Console.ReadLine();
            }
        }
        
        public static async Task MainAsync()
        {
            const string poolId = "MultiInstanceSamplePool";
            const string jobId  = "MultiInstanceSampleJob";
            const string taskId = "MultiInstanceSampleTask";

            const int numberOfNodes = 3;

            // The application package and version to deploy to the compute nodes.
            // It should contain your MPIHelloWorld sample MS-MPI program:
            // https://blogs.technet.microsoft.com/windowshpc/2015/02/02/how-to-compile-and-run-a-simple-ms-mpi-program/
            // And the MSMpiSetup.exe installer:
            // https://www.microsoft.com/download/details.aspx?id=52981
            // Then upload it as an application package:
            // https://azure.microsoft.com/documentation/articles/batch-application-packages/
            const string appPackageId = "MPIHelloWorld";
            const string appPackageVersion = "1.0";

            TimeSpan timeout = TimeSpan.FromMinutes(30);

            AccountSettings accountSettings = SampleHelpers.LoadAccountSettings();

            // Configure your AccountSettings in the Microsoft.Azure.Batch.Samples.Common project within this solution
            BatchSharedKeyCredentials cred = new BatchSharedKeyCredentials(
                accountSettings.BatchServiceUrl,
                accountSettings.BatchAccountName,
                accountSettings.BatchAccountKey);

            using (BatchClient batchClient = BatchClient.Open(cred))
            {
                // Create the pool of compute nodes and the job to which we add the multi-instance task.
                await CreatePoolAsync(batchClient, poolId, numberOfNodes, appPackageId, appPackageVersion);
                await CreateJobAsync(batchClient, jobId, poolId);

                // Create the multi-instance task. The MultiInstanceSettings property (configured
                // below) tells Batch to create one primary and several subtasks, the total number
                // of which matches the number of instances you specify in the MultiInstanceSettings.
                // This main task's command line is the "application command," and is executed *only*
                // by the primary, and only after the primary and all subtasks have executed the
                // "coordination command" (the MultiInstanceSettings.CoordinationCommandLine).
                CloudTask multiInstanceTask = new CloudTask(id: taskId,
                    commandline: $"cmd /c mpiexec.exe -c 1 -wdir %AZ_BATCH_TASK_SHARED_DIR% %AZ_BATCH_APP_PACKAGE_{appPackageId.ToUpper()}#{appPackageVersion}%\\MPIHelloWorld.exe");

                // Configure the task's MultiInstanceSettings. Specify the number of nodes
                // to allocate to the multi-instance task, and the "coordination command".
                // The CoordinationCommandLine is run by the primary and subtasks, and is
                // used in this sample to start SMPD on the compute nodes.
                multiInstanceTask.MultiInstanceSettings =
                    new MultiInstanceSettings(@"cmd /c start cmd /c smpd.exe -d", numberOfNodes);

                // Submit the task to the job. Batch will take care of creating one primary and
                // enough subtasks to match the total number of nodes allocated to the task,
                // and schedule them for execution on the nodes.
                Console.WriteLine($"Adding task [{taskId}] to job [{jobId}]...");
                await batchClient.JobOperations.AddTaskAsync(jobId, multiInstanceTask);

                // Get the "bound" version of the multi-instance task.
                CloudTask mainTask = await batchClient.JobOperations.GetTaskAsync(jobId, taskId);

                // We use a TaskStateMonitor to monitor the state of our tasks. In this case,
                // we will wait for the task to reach the Completed state.
                Console.WriteLine($"Awaiting task completion, timeout in {timeout}...");
                TaskStateMonitor taskStateMonitor = batchClient.Utilities.CreateTaskStateMonitor();
                await taskStateMonitor.WhenAll(new List<CloudTask> { mainTask }, TaskState.Completed, timeout);

                // Refresh the task to obtain up-to-date property values from Batch, such as
                // its current state and information about the node on which it executed.
                await mainTask.RefreshAsync();

                string stdOut = mainTask.GetNodeFile(Constants.StandardOutFileName).ReadAsString();
                string stdErr = mainTask.GetNodeFile(Constants.StandardErrorFileName).ReadAsString();

                Console.WriteLine();
                Console.WriteLine($"Main task [{mainTask.Id}] is in state [{mainTask.State}] and ran on compute node [{mainTask.ComputeNodeInformation.ComputeNodeId}]:");
                Console.WriteLine("---- stdout.txt ----");
                Console.WriteLine(stdOut);
                Console.WriteLine("---- stderr.txt ----");
                Console.WriteLine(stdErr);

                // Need to delay a bit to allow the Batch service to mark the subtasks as Complete
                TimeSpan subtaskTimeout = TimeSpan.FromSeconds(10);
                Console.WriteLine($"Main task completed, waiting {subtaskTimeout} for subtasks to complete...");
                System.Threading.Thread.Sleep(subtaskTimeout);

                Console.WriteLine();
                Console.WriteLine("---- Subtask information ----");

                // Obtain the collection of subtasks for the multi-instance task, and print
                // some information about each.
                IPagedEnumerable<SubtaskInformation> subtasks = mainTask.ListSubtasks();
                await subtasks.ForEachAsync(async (subtask) =>
                {
                    Console.WriteLine("subtask: " + subtask.Id);
                    Console.WriteLine("\texit code: " + subtask.ExitCode);

                    if (subtask.State == SubtaskState.Completed)
                    {
                        // Obtain the file from the node on which the subtask executed. For normal CloudTasks,
                        // we could simply call CloudTask.GetNodeFile(Constants.StandardOutFileName), but the
                        // subtasks are not "normal" tasks in Batch, and thus must be handled differently.
                        ComputeNode node =
                            await batchClient.PoolOperations.GetComputeNodeAsync(subtask.ComputeNodeInformation.PoolId,
                                                                                 subtask.ComputeNodeInformation.ComputeNodeId);

                        string outPath = subtask.ComputeNodeInformation.TaskRootDirectory + "\\" + Constants.StandardOutFileName;
                        string errPath = subtask.ComputeNodeInformation.TaskRootDirectory + "\\" + Constants.StandardErrorFileName;

                        NodeFile stdOutFile = await node.GetNodeFileAsync(outPath.Trim('\\'));
                        NodeFile stdErrFile = await node.GetNodeFileAsync(errPath.Trim('\\'));

                        stdOut = await stdOutFile.ReadAsStringAsync();
                        stdErr = await stdErrFile.ReadAsStringAsync();

                        Console.WriteLine($"\tnode: " + node.Id);
                        Console.WriteLine("\tstdout.txt: " + stdOut);
                        Console.WriteLine("\tstderr.txt: " + stdErr);
                    }
                    else
                    {
                        Console.WriteLine($"\tSubtask {subtask.Id} is in state {subtask.State}");
                    }
                });

                // Clean up the resources we've created in the Batch account
                Console.WriteLine();
                Console.Write("Delete job? [yes] no: ");
                string response = Console.ReadLine().ToLower();
                if (response != "n" && response != "no")
                {
                    await batchClient.JobOperations.DeleteJobAsync(jobId);
                }

                Console.Write("Delete pool? [yes] no: ");
                response = Console.ReadLine().ToLower();
                if (response != "n" && response != "no")
                {
                    await batchClient.PoolOperations.DeletePoolAsync(poolId);
                }
            }
        }

        /// <summary>
        /// Creates a pool of "small" Windows Server 2012 R2 compute nodes in the Batch service with the specified configuration.
        /// </summary>
        /// <param name="batchClient">The client to use for creating the pool.</param>
        /// <param name="poolId">The id of the pool to create.</param>
        /// <param name="numberOfNodes">The target number of compute nodes for the pool.</param>
        /// <param name="appPackageId">The id of the application package to install on the compute nodes.</param>
        /// <param name="appPackageVersion">The application package version to install on the compute nodes.</param>
        private static async Task CreatePoolAsync(BatchClient batchClient, string poolId, int numberOfNodes, string appPackageId, string appPackageVersion)
        {
            // Create the unbound pool. Until we call CloudPool.Commit() or CommitAsync(),
            // the pool isn't actually created in the Batch service. This CloudPool instance
            // is therefore considered "unbound," and we can modify its properties.
            Console.WriteLine($"Creating pool [{poolId}]...");
            CloudPool unboundPool =
                batchClient.PoolOperations.CreatePool(
                    poolId: poolId,
                    virtualMachineSize: "standard_d1_v2",
                    targetDedicatedComputeNodes: numberOfNodes,
                    cloudServiceConfiguration: new CloudServiceConfiguration(osFamily: "5"));

            // REQUIRED for communication between the MS-MPI processes (in this
            // sample, MPIHelloWorld.exe) running on the different nodes
            unboundPool.InterComputeNodeCommunicationEnabled = true;
            // REQUIRED for multi-instance tasks
            unboundPool.MaxTasksPerComputeNode = 1;

            // Specify the application and version to deploy to the compute nodes.
            unboundPool.ApplicationPackageReferences = new List<ApplicationPackageReference>
            {
                new ApplicationPackageReference
                {
                    ApplicationId = appPackageId,
                    Version = appPackageVersion
                }
            };

            // Create a StartTask for the pool that we use to install MS-MPI on the nodes
            // as they join the pool.
            StartTask startTask = new StartTask
            {
                CommandLine = $"cmd /c %AZ_BATCH_APP_PACKAGE_{appPackageId.ToUpper()}#{appPackageVersion}%\\MSMpiSetup.exe -unattend -force",
                UserIdentity = new UserIdentity(new AutoUserSpecification(elevationLevel: ElevationLevel.Admin)),
                WaitForSuccess = true
            };
            unboundPool.StartTask = startTask;

            // Commit the fully configured pool to the Batch service to actually create
            // the pool and its compute nodes.
            await unboundPool.CommitAsync();
        }

        /// <summary>
        /// Creates a job in Batch service with the specified id and associated with the specified pool.
        /// </summary>
        /// <param name="batchClient"></param>
        /// <param name="jobId"></param>
        /// <param name="poolId"></param>
        private static async Task CreateJobAsync(BatchClient batchClient, string jobId, string poolId)
        {
            // Create the job to which the multi-instance task will be added.
            Console.WriteLine($"Creating job [{jobId}]...");
            CloudJob unboundJob = batchClient.JobOperations.CreateJob(jobId, new PoolInformation() { PoolId = poolId });
            await unboundJob.CommitAsync();
        }
    }
}
