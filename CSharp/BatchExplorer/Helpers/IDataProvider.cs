using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.Batch.Common;
using Microsoft.Azure.BatchExplorer.Models;
using Microsoft.Azure.BatchExplorer.Service;

namespace Microsoft.Azure.BatchExplorer.Helpers
{
    /// <summary>
    /// Provides a layer of abstraction between the display logic and the data-fetching logic
    /// </summary>
    public interface IDataProvider
    {
        /// <summary>
        /// The current account the data provider is using
        /// </summary>
        Account CurrentAccount { get; }

        /// <summary>
        /// The batch service associated with this data provider
        /// </summary>
        BatchService Service { get; }

        /// <summary>
        /// Get a collection of Job Schedules from the default source (if any)
        /// </summary>
        /// <returns>a collection of Job Schedules, or null if no JobSchedules are available</returns>
        Task<IList<JobScheduleModel>> GetJobScheduleCollectionAsync();

        /// <summary>
        /// Creates a job schedule
        /// </summary>
        Task CreateJobScheduleAsync(CreateJobScheduleOptions options);

        /// <summary>
        /// Get a collection of Jobs from the default source (if any)
        /// </summary>
        /// <returns>A <see cref="Task"/> whose result is a collection of Jobs</returns>
        Task<IList<JobModel>> GetJobCollectionAsync();

        /// <summary>
        /// Creates a job
        /// </summary>
        Task CreateJobAsync(CreateJobOptions options);

        /// <summary>
        /// Add a task
        /// </summary>
        /// <param name="options">add task options - id, cli, etc.</param>
        Task AddTaskAsync(AddTaskOptions options);

        /// <summary>
        /// Get a collection of Pools from the default source (if any)
        /// </summary>
        /// <returns>A <see cref="Task"/> whose result is a collection of Pools</returns>
        Task<IList<PoolModel>> GetPoolCollectionAsync();

        /// <summary>
        /// Creates a Pool
        /// </summary>
        Task CreatePoolAsync(
            string poolId, 
            string virtualMachineSize, 
            int? targetDedicated, 
            string autoScaleFormula, 
            bool communicationEnabled,
            string osFamily,
            string osVersion,
            int maxTasksPerComputeNode,
            TimeSpan? timeout,
            StartTaskOptions startTask);

        /// <summary>
        /// Creates a ComputeNode user.
        /// </summary>
        /// <returns></returns>
        Task CreateComputeNodeUserAsync(string poolId, string nodeId, string userName, string password, DateTime expiryTime, bool admin);

        /// <summary>
        /// Resizes a pool
        /// </summary>
        /// <param name="poolId"></param>
        /// <param name="targetDedicated"></param>
        /// <param name="timeout"></param>
        /// <param name="computeNodeDeallocationOption"></param>
        /// <returns></returns>
        Task ResizePoolAsync(string poolId, int targetDedicated, TimeSpan? timeout, ComputeNodeDeallocationOption? computeNodeDeallocationOption);
    }
}
