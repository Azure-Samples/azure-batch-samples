//Copyright (c) Microsoft Corporation

namespace Microsoft.Azure.Batch.Samples.Common
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Batch.Common;

    /// <summary>
    /// Static class containing a number of helper methods used by sample projects associated with Azure.com articles.
    /// </summary>
    public static class ArticleHelpers
    {
        /// <summary>
        /// Creates a CloudJob in the specified pool if a job with the specified ID is not found
        /// in the pool, otherwise returns the existing job.
        /// </summary>
        /// <param name="batchClient">A fully initialized <see cref="BatchClient"/>.</param>
        /// <param name="poolId">The ID of the CloudPool in which the job should be created.</param>
        /// <param name="jobId">The ID of the CloudJob.</param>
        /// <returns>A bound version of the newly created CloudJob.</returns>
        public static async Task<CloudJob> CreateJobAsync(BatchClient batchClient, string poolId, string jobId)
        {
            CloudJob job = await SampleHelpers.GetJobIfExistAsync(batchClient, jobId);

            if (job == null)
            {
                Console.WriteLine("Job {0} not found, creating...", jobId);

                CloudJob unboundJob = batchClient.JobOperations.CreateJob(jobId, new PoolInformation() { PoolId = poolId });
                await unboundJob.CommitAsync();

                // Get the bound version of the job with all of its properties populated
                job = await batchClient.JobOperations.GetJobAsync(jobId);
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

                await Task.Delay(TimeSpan.FromSeconds(10));
                await pool.RefreshAsync(detail);

                if (DateTime.UtcNow > timeoutAfterThisTimeUtc)
                {
                    Console.WriteLine();
                    Console.WriteLine("Timed out waiting for pool {0} to reach state {1}", poolId, targetAllocationState);

                    return;
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

            CloudPool pool = await client.PoolOperations.GetPoolAsync(poolId);

            ODATADetailLevel detail = new ODATADetailLevel(selectClause: "id,state");
            IEnumerable<ComputeNode> computeNodes = pool.ListComputeNodes(detail);

            while (computeNodes.Any(computeNode => computeNode.State != targetNodeState))
            {
                Console.Write(".");

                await Task.Delay(TimeSpan.FromSeconds(10));
                computeNodes = pool.ListComputeNodes(detail).ToList();

                if (DateTime.UtcNow > timeoutAfterThisTimeUtc)
                {
                    Console.WriteLine();
                    Console.WriteLine("Timed out waiting for compute nodes in pool {0} to reach state {1}", poolId, targetNodeState.ToString());

                    return;
                }
            }

            Console.WriteLine();
        }
    }
}
