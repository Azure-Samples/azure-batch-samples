// Copyright (c) Microsoft Corporation

namespace Microsoft.Azure.Batch.Samples.Common
{
    using System;
    using System.Threading.Tasks;
    using global::Azure;
    using global::Azure.Compute.Batch;
    using global::Azure.ResourceManager.Batch;
    using global::Azure.ResourceManager.Batch.Models;
    using ArmBatchTaskSchedulingPolicy = global::Azure.ResourceManager.Batch.Models.BatchTaskSchedulingPolicy;
    using ArmBatchNodeFillType = global::Azure.ResourceManager.Batch.Models.BatchNodeFillType;

    /// <summary>
    /// Static class containing a number of helper methods used by sample projects associated with Azure.com articles.
    /// </summary>
    public static class ArticleHelpers
    {
        /// <summary>
        /// Creates a pool via the ARM (control plane) SDK. If an existing pool with the
        /// specified ID is found, the pool is resized to match the specified node count.
        /// </summary>
        public static async Task<BatchAccountPoolResource> CreatePoolIfNotExistAsync(
            BatchAccountResource batchAccount,
            string poolId,
            string nodeSize,
            int nodeCount,
            int taskSlotsPerNode)
        {
            var imageReference = new BatchImageReference
            {
                Publisher = "MicrosoftWindowsServer",
                Offer = "WindowsServer",
                Sku = "2016-Datacenter-smalldisk",
                Version = "latest",
            };

            var poolData = new BatchAccountPoolData
            {
                VmSize = nodeSize,
                DeploymentConfiguration = new BatchDeploymentConfiguration
                {
                    VmConfiguration = new BatchVmConfiguration(imageReference, "batch.node.windows amd64"),
                },
                ScaleSettings = new BatchAccountPoolScaleSettings
                {
                    FixedScale = new BatchAccountFixedScaleSettings
                    {
                        TargetDedicatedNodes = nodeCount,
                    },
                },
                TaskSlotsPerNode = taskSlotsPerNode,
                TaskSchedulingPolicy = new ArmBatchTaskSchedulingPolicy(ArmBatchNodeFillType.Pack),
            };

            await GettingStartedCommon.CreatePoolIfNotExistAsync(batchAccount, poolId, poolData).ConfigureAwait(false);

            return await batchAccount.GetBatchAccountPools().GetAsync(poolId).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a <see cref="BatchJob"/> in the specified pool if a job with the specified ID is not found
        /// in the pool, otherwise returns the existing job.
        /// </summary>
        public static async Task<BatchJob> CreateJobIfNotExistAsync(BatchClient batchClient, string poolId, string jobId)
        {
            BatchJob job = await SampleHelpers.GetJobIfExistAsync(batchClient, jobId).ConfigureAwait(false);

            if (job == null)
            {
                Console.WriteLine("Job {0} not found, creating...", jobId);

                var jobOptions = new BatchJobCreateOptions(jobId, new BatchPoolInfo { PoolId = poolId });
                await batchClient.CreateJobAsync(jobOptions).ConfigureAwait(false);

                job = await batchClient.GetJobAsync(jobId).ConfigureAwait(false);
            }

            return job;
        }

        /// <summary>
        /// Asynchronous method that delays execution until the specified pool reaches the specified state.
        /// </summary>
        public static async Task WaitForPoolToReachStateAsync(BatchClient client, string poolId, AllocationState targetAllocationState, TimeSpan timeout)
        {
            Console.WriteLine("Waiting for pool {0} to reach allocation state {1}", poolId, targetAllocationState);

            DateTime timeoutAfterThisTimeUtc = DateTime.UtcNow.Add(timeout);

            BatchPool pool = await client.GetPoolAsync(poolId, select: new[] { "id", "allocationState" }).ConfigureAwait(false);
            while (pool.AllocationState != targetAllocationState)
            {
                Console.Write(".");
                await Task.Delay(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
                pool = await client.GetPoolAsync(poolId, select: new[] { "id", "allocationState" }).ConfigureAwait(false);

                if (DateTime.UtcNow > timeoutAfterThisTimeUtc)
                {
                    throw new TimeoutException(string.Format("Timed out waiting for pool {0} to reach state {1}", poolId, targetAllocationState));
                }
            }

            Console.WriteLine();
        }

        /// <summary>
        /// Asynchronous method that delays execution until the specified job reaches the specified state.
        /// </summary>
        public static async Task WaitForJobToReachStateAsync(BatchClient client, string jobId, BatchJobState targetJobState, TimeSpan timeout)
        {
            Console.WriteLine("Waiting for job {0} to reach state {1}", jobId, targetJobState);

            DateTime timeoutAfterThisTimeUtc = DateTime.UtcNow.Add(timeout);

            BatchJob job = await client.GetJobAsync(jobId, select: new[] { "id", "state" }).ConfigureAwait(false);
            while (job.State != targetJobState)
            {
                Console.Write(".");
                await Task.Delay(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
                job = await client.GetJobAsync(jobId, select: new[] { "id", "state" }).ConfigureAwait(false);

                if (DateTime.UtcNow > timeoutAfterThisTimeUtc)
                {
                    throw new TimeoutException(string.Format("Timed out waiting for job {0} to reach state {1}", jobId, targetJobState));
                }
            }

            Console.WriteLine();
        }

        /// <summary>
        /// Asynchronous method that delays execution until all nodes within the specified pool reach the specified state.
        /// </summary>
        public static async Task WaitForNodesToReachStateAsync(BatchClient client, string poolId, BatchNodeState targetNodeState, TimeSpan timeout)
        {
            Console.WriteLine("Waiting for nodes to reach state {0}", targetNodeState);

            DateTime timeoutAfterThisTimeUtc = DateTime.UtcNow.Add(timeout);

            while (true)
            {
                bool allInTargetState = true;
                bool anyNodes = false;
                await foreach (BatchNode node in client
                    .GetNodesAsync(poolId, select: new[] { "id", "state" })
                    .ConfigureAwait(false))
                {
                    anyNodes = true;
                    if (node.State != targetNodeState)
                    {
                        allInTargetState = false;
                        break;
                    }
                }

                if (anyNodes && allInTargetState)
                {
                    break;
                }

                Console.Write(".");
                await Task.Delay(TimeSpan.FromSeconds(10)).ConfigureAwait(false);

                if (DateTime.UtcNow > timeoutAfterThisTimeUtc)
                {
                    throw new TimeoutException(string.Format("Timed out waiting for compute nodes in pool {0} to reach state {1}", poolId, targetNodeState));
                }
            }

            Console.WriteLine();
        }
    }
}
