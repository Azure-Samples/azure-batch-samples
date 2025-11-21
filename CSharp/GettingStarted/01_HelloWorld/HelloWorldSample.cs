// Copyright (c) Microsoft Corporation. All rights reserved.

using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Batch;
using Azure.ResourceManager.Batch.Models;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Azure.Compute.Batch.Samples.HelloWorld
{
    public class HelloWorldSample
    {
        private ArmClient _armClient;
        private BatchClient _batchClient;

        /// <summary>
        /// Creates a pool with a configurable number of nodes, then submits tasks which print a 'Hello world' message.
        /// The resulting stdout.txt or stderr.txt (depending on each task's exit code) is then printed to the console.
        /// 
        /// After running, the job will be terminated and the pool will be deleted.
        /// </summary>
        /// <param name="batchAccountResourceId">The ARM resource ID of the Batch account.</param>
        /// <returns>A task which completes when the sample has finished running.</returns>
        public async Task Run(string batchAccountResourceId)
        {
            var batchAccountIdentifier = ResourceIdentifier.Parse(batchAccountResourceId);

            var credential = new DefaultAzureCredential();
            _armClient = new ArmClient(credential);

            BatchAccountResource batchAccount = await _armClient.GetBatchAccountResource(batchAccountIdentifier).GetAsync();

            _batchClient = new BatchClient(new Uri($"https://{batchAccount.Data.AccountEndpoint}"), credential);

            var poolName = GenerateUniqueName("HelloWorldPool");
            var imageReference = new BatchImageReference()
            {
                Publisher = "canonical",
                Offer = "0001-com-ubuntu-server-jammy",
                Sku = "22_04-lts",
                Version = "latest"
            };
            string nodeAgentSku = "batch.node.ubuntu 22.04";
            int targetDedicatedNodes = 1;

            Console.WriteLine("Creating pool {0} with {1} dedicated {2} in account {3}", poolName, targetDedicatedNodes, targetDedicatedNodes == 1 ? "node" : "nodes", batchAccount.Data.Name);
            BatchAccountPoolResource pool = (await batchAccount.GetBatchAccountPools().CreateOrUpdateAsync(WaitUntil.Completed, poolName, new BatchAccountPoolData()
            {
                VmSize = "Standard_DS1_v2",
                DeploymentConfiguration = new BatchDeploymentConfiguration()
                {
                    VmConfiguration = new BatchVmConfiguration(imageReference, nodeAgentSku)
                },
                ScaleSettings = new BatchAccountPoolScaleSettings()
                {
                    FixedScale = new BatchAccountFixedScaleSettings()
                    {
                        TargetDedicatedNodes = targetDedicatedNodes
                    }
                }
            })).Value;

            string jobId = GenerateUniqueName("HelloWorldJob");

            try
            {
                Console.WriteLine("Creating job {0}", jobId);
                await _batchClient.CreateJobAsync(new BatchJobCreateOptions(jobId, new BatchPoolInfo() { PoolId = poolName }));

                for (int i = 0; i < 5; i++)
                {
                    string taskId = $"task-{i}";
                    Console.WriteLine("Submitting {0}", taskId);
                    await _batchClient.CreateTaskAsync(jobId, new BatchTaskCreateOptions(taskId, $"echo Hello world from {taskId}"));
                }

                Console.WriteLine("Waiting for all tasks to complete...");
                await waitForTasksToComplete(jobId);

                var completedTasks = _batchClient.GetTasksAsync(jobId, filter: "state eq 'completed'");
                await foreach (BatchTask t in completedTasks)
                {
                    var outputFileName = t.ExecutionInfo.ExitCode == 0 ? "stdout.txt" : "stderr.txt";

                    Console.WriteLine("Task {0} exited with code {1}. Output ({2}):",
                        t.Id, t.ExecutionInfo.ExitCode, outputFileName);

                    BinaryData fileContents = await _batchClient.GetTaskFileAsync(jobId, t.Id, outputFileName);
                    using (var reader = new StreamReader(fileContents.ToStream()))
                    {
                        Console.WriteLine(await reader.ReadLineAsync());
                    }
                }
            }
            finally
            {
                Console.WriteLine("Terminating job and deleting pool");
                await Task.WhenAll([
                    _batchClient.TerminateJobAsync(jobId),
                    pool.DeleteAsync(WaitUntil.Completed)]);
            }
        }

        /// <summary>
        /// Poll all the tasks in the given job and wait for them to reach the completed state.
        /// </summary>
        /// <param name="jobId">The ID of the job to poll</param>
        /// <returns>A task that will complete when all Batch tasks have completed.</returns>
        /// <exception cref="TimeoutException">Thrown if all tasks haven't reached the completed state after a certain period of time</exception>
        private async Task waitForTasksToComplete(String jobId)
        {
            // Note that this timeout should take into account the time it takes for the pool to scale up
            var timeoutAfter = DateTime.Now.AddMinutes(10);
            while (DateTime.Now < timeoutAfter)
            {
                var allComplete = true;
                var tasks = _batchClient.GetTasksAsync(jobId, select: ["id", "state"]);
                await foreach (BatchTask task in tasks)
                {
                    if (task.State != BatchTaskState.Completed)
                    {
                        allComplete = false;
                        break;
                    }
                }

                if (allComplete)
                {
                    return;
                }

                await Task.Delay(10000);
            }

            throw new TimeoutException("Task(s) did not complete within the specified time");
        }

        /// <summary>
        /// Generate a unique name with the given prefix using the current user name and a timestamp.
        /// </summary>
        /// <param name="prefix">The name's prefix.</param>
        /// <returns>The generated name.</returns>
        private static string GenerateUniqueName(string prefix)
        {
            string currentUser = new string(Environment.UserName.Where(char.IsLetterOrDigit).ToArray());
            return string.Format("{0}-{1}-{2}", prefix, currentUser, DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        }
    }
}
