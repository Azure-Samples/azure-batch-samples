//Copyright (c) Microsoft Corporation

namespace Microsoft.Azure.Batch.Samples.Common
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Batch.Common;

    /// <summary>
    /// Static class containing a number of helper methods used by sample projects associated with Azure.com articles.
    /// </summary>
    public static class ArticleHelpers
    {
        /// <summary>
        /// Creates a <see cref="CloudPool"/> associated with the specified Batch account. If an existing pool with the
        /// specified ID is found, the pool is resized to match the specified node count.
        /// </summary>
        /// <param name="batchClient">A fully initialized <see cref="BatchClient"/>.</param>
        /// <param name="poolId">The ID of the <see cref="CloudPool"/>.</param>
        /// <param name="nodeSize">The size of the nodes within the pool.</param>
        /// <param name="nodeCount">The number of nodes to create within the pool.</param>
        /// <param name="maxTasksPerNode">The maximum number of tasks to run concurrently on each node.</param>
        /// <returns>A bound <see cref="CloudPool"/> with the specified properties.</returns>
        public async static Task<CloudPool> CreatePoolIfNotExistAsync(BatchClient batchClient, string poolId, string nodeSize, int nodeCount, int maxTasksPerNode)
        {
            // Create and configure an unbound pool with the specified ID
            CloudPool pool = batchClient.PoolOperations.CreatePool(poolId: poolId,
                virtualMachineSize: nodeSize,
                targetDedicatedComputeNodes: nodeCount,
                cloudServiceConfiguration: new CloudServiceConfiguration("5"));
            
            pool.MaxTasksPerComputeNode = maxTasksPerNode;

            // We want each node to be completely filled with tasks (i.e. up to maxTasksPerNode) before
            // tasks are assigned to the next node in the pool
            pool.TaskSchedulingPolicy = new TaskSchedulingPolicy(ComputeNodeFillType.Pack);
            
            await GettingStartedCommon.CreatePoolIfNotExistAsync(batchClient, pool).ConfigureAwait(continueOnCapturedContext: false);

            return await batchClient.PoolOperations.GetPoolAsync(poolId).ConfigureAwait(continueOnCapturedContext: false);
        }

        /// <summary>
        /// Creates a CloudJob in the specified pool if a job with the specified ID is not found
        /// in the pool, otherwise returns the existing job.
        /// </summary>
        /// <param name="batchClient">A fully initialized <see cref="BatchClient"/>.</param>
        /// <param name="poolId">The ID of the CloudPool in which the job should be created.</param>
        /// <param name="jobId">The ID of the CloudJob.</param>
        /// <returns>A bound version of the newly created CloudJob.</returns>
        public static async Task<CloudJob> CreateJobIfNotExistAsync(BatchClient batchClient, string poolId, string jobId)
        {
            CloudJob job = await SampleHelpers.GetJobIfExistAsync(batchClient, jobId).ConfigureAwait(continueOnCapturedContext: false);

            if (job == null)
            {
                Console.WriteLine("Job {0} not found, creating...", jobId);

                CloudJob unboundJob = batchClient.JobOperations.CreateJob(jobId, new PoolInformation() { PoolId = poolId });
                await unboundJob.CommitAsync().ConfigureAwait(continueOnCapturedContext: false);

                // Get the bound version of the job with all of its properties populated
                job = await batchClient.JobOperations.GetJobAsync(jobId).ConfigureAwait(continueOnCapturedContext: false);
            }

            return job;
        }

        /// <summary>
        /// Asynchronous method that delays execution until the specified pool reaches the specified state.
        /// </summary>
        /// <param name="client">A fully intitialized <see cref="BatchClient"/>.</param>
        /// <param name="poolId">The ID of the pool to monitor for the specified <see cref="AllocationState"/>.</param>
        /// <param name="targetAllocationState">The allocation state to monitor.</param>
        /// <param name="timeout">The maximum time to wait for the pool to reach the specified state.</param>
        /// <returns>A <see cref="System.Threading.Tasks.Task"/> object that represents the asynchronous operation.</returns>
        public static async Task WaitForPoolToReachStateAsync(BatchClient client, string poolId, AllocationState targetAllocationState, TimeSpan timeout)
        {
            Console.WriteLine("Waiting for pool {0} to reach allocation state {1}", poolId, targetAllocationState);

            DateTime startTime = DateTime.UtcNow;
            DateTime timeoutAfterThisTimeUtc = startTime.Add(timeout);

            ODATADetailLevel detail = new ODATADetailLevel(selectClause: "id,allocationState");
            CloudPool pool = await client.PoolOperations.GetPoolAsync(poolId, detail);

            while (pool.AllocationState != targetAllocationState)
            {
                Console.Write(".");

                await Task.Delay(TimeSpan.FromSeconds(10)).ConfigureAwait(continueOnCapturedContext: false);
                await pool.RefreshAsync(detail).ConfigureAwait(continueOnCapturedContext: false);

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
        /// <param name="client">A fully intitialized <see cref="BatchClient"/>.</param>
        /// <param name="jobId">The ID of the job to monitor for the specified <see cref="JobState"/>.</param>
        /// <param name="targetJobState">The job state to monitor.</param>
        /// <param name="timeout">The maximum time to wait for the job to reach the specified state.</param>
        /// <returns>A <see cref="System.Threading.Tasks.Task"/> object that represents the asynchronous operation.</returns>
        public static async Task WaitForJobToReachStateAsync(BatchClient client, string jobId, JobState targetJobState, TimeSpan timeout)
        {
            Console.WriteLine("Waiting for job {0} to reach state {1}", jobId, targetJobState);

            DateTime startTime = DateTime.UtcNow;
            DateTime timeoutAfterThisTimeUtc = startTime.Add(timeout);

            ODATADetailLevel detail = new ODATADetailLevel(selectClause: "id,state");
            CloudJob job = await client.JobOperations.GetJobAsync(jobId, detail);

            while (job.State != targetJobState)
            {
                Console.Write(".");

                await Task.Delay(TimeSpan.FromSeconds(10)).ConfigureAwait(continueOnCapturedContext: false);
                await job.RefreshAsync(detail).ConfigureAwait(continueOnCapturedContext: false);

                if (DateTime.UtcNow > timeoutAfterThisTimeUtc)
                {
                    throw new TimeoutException(string.Format("Timed out waiting for job {0} to reach state {1}", jobId, targetJobState));
                }
            }

            Console.WriteLine();
        }

        /// <summary>
        /// Asynchronous method that delays execution until the nodes within the specified pool reach the specified state.
        /// </summary>
        /// <param name="client">A fully intitialized <see cref="BatchClient"/>.</param>
        /// <param name="poolId">The ID of the pool containing the nodes to monitor.</param>
        /// <param name="targetNodeState">The node state to monitor.</param>
        /// <param name="timeout">The maximum time to wait for the nodes to reach the specified state.</param>
        /// <returns>A <see cref="System.Threading.Tasks.Task"/> object that represents the asynchronous operation.</returns>
        public static async Task WaitForNodesToReachStateAsync(BatchClient client, string poolId, ComputeNodeState targetNodeState, TimeSpan timeout)
        {
            Console.WriteLine("Waiting for nodes to reach state {0}", targetNodeState);

            DateTime startTime = DateTime.UtcNow;
            DateTime timeoutAfterThisTimeUtc = startTime.Add(timeout);

            CloudPool pool = await client.PoolOperations.GetPoolAsync(poolId).ConfigureAwait(continueOnCapturedContext: false);

            ODATADetailLevel detail = new ODATADetailLevel(selectClause: "id,state");
            IEnumerable<ComputeNode> computeNodes = pool.ListComputeNodes(detail);

            while (computeNodes.Any(computeNode => computeNode.State != targetNodeState))
            {
                Console.Write(".");

                await Task.Delay(TimeSpan.FromSeconds(10)).ConfigureAwait(continueOnCapturedContext: false);
                computeNodes = pool.ListComputeNodes(detail).ToList();

                if (DateTime.UtcNow > timeoutAfterThisTimeUtc)
                {
                    throw new TimeoutException(string.Format("Timed out waiting for compute nodes in pool {0} to reach state {1}", poolId, targetNodeState.ToString()));
                }
            }

            Console.WriteLine();
        }
    }
}
