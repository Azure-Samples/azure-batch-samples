// Copyright (c) Microsoft Corporation
//
// Companion project to the following article:
// https://azure.microsoft.com/documentation/articles/batch-mpi/

namespace Microsoft.Azure.Batch.Samples.MultiInstanceTasks
{
    using System;
    using System.Threading.Tasks;
    using global::Azure;
    using global::Azure.Compute.Batch;
    using global::Azure.Core;
    using global::Azure.ResourceManager.Batch;
    using global::Azure.ResourceManager.Batch.Models;
    using Microsoft.Azure.Batch.Samples.Common;

    public class Program
    {
        public static void Main(string[] args)
        {
            try
            {
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
            const string appPackageId = "MPIHelloWorld";
            const string appPackageVersion = "1.0";

            TimeSpan timeout = TimeSpan.FromMinutes(30);

            AccountSettings accountSettings = SampleHelpers.LoadAccountSettings();

            BatchClient batchClient = ClientFactory.CreateBatchClient(accountSettings);
            BatchAccountResource batchAccount = ClientFactory.CreateBatchAccountResource(accountSettings);

            // Create the pool of compute nodes and the job to which we add the multi-instance task.
            await CreatePoolAsync(batchAccount, accountSettings, poolId, numberOfNodes, appPackageId, appPackageVersion);
            await CreateJobAsync(batchClient, jobId, poolId);

            // The "application command" runs on the primary only, after the coordination
            // command has run on the primary and all subtasks.
            string commandLine = $"cmd /c mpiexec.exe -c 1 -wdir %AZ_BATCH_TASK_SHARED_DIR% %AZ_BATCH_APP_PACKAGE_{appPackageId.ToUpper()}#{appPackageVersion}%\\MPIHelloWorld.exe";
            BatchTaskCreateOptions multiInstanceTask = new BatchTaskCreateOptions(taskId, commandLine)
            {
                MultiInstanceSettings = new MultiInstanceSettings(@"cmd /c start cmd /c smpd.exe -d")
                {
                    NumberOfInstances = numberOfNodes,
                },
            };

            Console.WriteLine($"Adding task [{taskId}] to job [{jobId}]...");
            await batchClient.CreateTaskAsync(jobId, multiInstanceTask);

            Console.WriteLine($"Awaiting task completion, timeout in {timeout}...");
            await WaitForTaskCompletedAsync(batchClient, jobId, taskId, timeout);

            BatchTask mainTask = await batchClient.GetTaskAsync(jobId, taskId);

            string stdOut = (await batchClient.GetTaskFileAsync(jobId, taskId, "stdout.txt")).ToString();
            string stdErr = (await batchClient.GetTaskFileAsync(jobId, taskId, "stderr.txt")).ToString();

            Console.WriteLine();
            Console.WriteLine($"Main task [{mainTask.Id}] is in state [{mainTask.State}] and ran on compute node [{mainTask.NodeInfo?.NodeId}]:");
            Console.WriteLine("---- stdout.txt ----");
            Console.WriteLine(stdOut);
            Console.WriteLine("---- stderr.txt ----");
            Console.WriteLine(stdErr);

            // Need to delay a bit to allow the Batch service to mark the subtasks as Complete
            TimeSpan subtaskTimeout = TimeSpan.FromSeconds(10);
            Console.WriteLine($"Main task completed, waiting {subtaskTimeout} for subtasks to complete...");
            await Task.Delay(subtaskTimeout);

            Console.WriteLine();
            Console.WriteLine("---- Subtask information ----");

            await foreach (BatchSubtask subtask in batchClient.GetSubTasksAsync(jobId, taskId))
            {
                Console.WriteLine("subtask: " + subtask.Id);
                Console.WriteLine("\texit code: " + subtask.ExitCode);

                if (subtask.State == BatchSubtaskState.Completed && subtask.NodeInfo != null)
                {
                    string outPath = subtask.NodeInfo.TaskRootDirectory + "\\stdout.txt";
                    string errPath = subtask.NodeInfo.TaskRootDirectory + "\\stderr.txt";

                    BinaryData outData = await batchClient.GetNodeFileAsync(subtask.NodeInfo.PoolId, subtask.NodeInfo.NodeId, outPath.Trim('\\'));
                    BinaryData errData = await batchClient.GetNodeFileAsync(subtask.NodeInfo.PoolId, subtask.NodeInfo.NodeId, errPath.Trim('\\'));

                    Console.WriteLine("\tnode: " + subtask.NodeInfo.NodeId);
                    Console.WriteLine("\tstdout.txt: " + outData);
                    Console.WriteLine("\tstderr.txt: " + errData);
                }
                else
                {
                    Console.WriteLine($"\tSubtask {subtask.Id} is in state {subtask.State}");
                }
            }

            // Clean up
            Console.WriteLine();
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

        private static async Task CreatePoolAsync(
            BatchAccountResource batchAccount,
            AccountSettings accountSettings,
            string poolId,
            int numberOfNodes,
            string appPackageId,
            string appPackageVersion)
        {
            Console.WriteLine($"Creating pool [{poolId}]...");

            string applicationResourceId =
                $"/subscriptions/{accountSettings.SubscriptionId}" +
                $"/resourceGroups/{accountSettings.ResourceGroupName}" +
                $"/providers/Microsoft.Batch/batchAccounts/{accountSettings.BatchAccountName}" +
                $"/applications/{appPackageId}/versions/{appPackageVersion}";

            BatchAccountPoolData poolData = new BatchAccountPoolData
            {
                VmSize = "standard_d2_v3",
                DeploymentVmConfiguration = new BatchVmConfiguration(
                    new BatchImageReference
                    {
                        Publisher = "MicrosoftWindowsServer",
                        Offer = "WindowsServer",
                        Sku = "2016-Datacenter-smalldisk",
                        Version = "latest",
                    },
                    "batch.node.windows amd64"),
                ScaleSettings = new BatchAccountPoolScaleSettings
                {
                    FixedScale = new BatchAccountFixedScaleSettings
                    {
                        TargetDedicatedNodes = numberOfNodes,
                    },
                },
                InterNodeCommunication = InterNodeCommunicationState.Enabled,
                TaskSlotsPerNode = 1,
                StartTask = new BatchAccountPoolStartTask
                {
                    CommandLine = $"cmd /c %AZ_BATCH_APP_PACKAGE_{appPackageId.ToUpper()}#{appPackageVersion}%\\MSMpiSetup.exe -unattend -force",
                    UserIdentity = new BatchUserIdentity
                    {
                        AutoUser = new BatchAutoUserSpecification
                        {
                            ElevationLevel = BatchUserAccountElevationLevel.Admin,
                        },
                    },
                    WaitForSuccess = true,
                },
            };
            poolData.ApplicationPackages.Add(new global::Azure.ResourceManager.Batch.Models.BatchApplicationPackageReference(new ResourceIdentifier(applicationResourceId)));

            await GettingStartedCommon.CreatePoolIfNotExistAsync(batchAccount, poolId, poolData);
        }

        private static async Task CreateJobAsync(BatchClient batchClient, string jobId, string poolId)
        {
            Console.WriteLine($"Creating job [{jobId}]...");
            try
            {
                await batchClient.CreateJobAsync(new BatchJobCreateOptions(jobId, new BatchPoolInfo { PoolId = poolId }));
            }
            catch (RequestFailedException ex) when (ex.ErrorCode == BatchErrorCode.JobExists.ToString())
            {
                Console.WriteLine("Job {0} already exists.", jobId);
            }
        }

        private static async Task WaitForTaskCompletedAsync(BatchClient batchClient, string jobId, string taskId, TimeSpan timeout)
        {
            DateTime timeoutAt = DateTime.UtcNow.Add(timeout);
            while (true)
            {
                BatchTask task = await batchClient.GetTaskAsync(jobId, taskId);
                if (task.State == BatchTaskState.Completed)
                {
                    return;
                }
                if (DateTime.UtcNow > timeoutAt)
                {
                    throw new TimeoutException($"Task {taskId} did not complete within {timeout}.");
                }
                await Task.Delay(TimeSpan.FromSeconds(15));
            }
        }
    }
}
