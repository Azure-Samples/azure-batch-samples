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
        /// TODO: Remove this once OM has full parity with protocol
        /// </summary>
        BatchService Service { get; }
        
        /// <summary>
        /// Get a collection of WorkItems from the default source (if any)
        /// </summary>
        /// <returns>a collection of WorkItems, or null if no WorkItems are available</returns>
        IList<WorkItemModel> GetWorkItemCollection();

        /// <summary>
        /// Creates a Work Item
        /// </summary>
        Task CreateWorkItem(CreateWorkItemOptions options);

        /// <summary>
        /// Add a task
        /// </summary>
        /// <param name="options">add task options - name, cli, etc.</param>
        Task AddTask(AddTaskOptions options);

        /// <summary>
        /// Get a collection of Pools from the default source (if any)
        /// </summary>
        /// <returns>a collection of Pools, or null if no Pools are available</returns>
        IList<PoolModel> GetPoolCollection();

        /// <summary>
        /// Creates a Pool
        /// </summary>
        Task CreatePoolAsync(
            string poolName, 
            string vmSize, 
            int? targetDedicated, 
            string autoScaleFormula, 
            bool communicationEnabled,
            string osFamily,
            string osVersion,
            int maxTasksPerVM,
            TimeSpan? timeout);

        /// <summary>
        /// Creates a VM user
        /// </summary>
        /// <returns></returns>
        Task CreateVMUserAsync(string poolName, string vmName, string userName, string password, DateTime expiryTime, bool admin);

        /// <summary>
        /// Resizes a pool
        /// </summary>
        /// <param name="poolName"></param>
        /// <param name="targetDedicated"></param>
        /// <param name="timeout"></param>
        /// <param name="deallocationOption"></param>
        /// <returns></returns>
        Task ResizePoolAsync(string poolName, int targetDedicated, TimeSpan? timeout, TVMDeallocationOption? deallocationOption);
    }
}
