using Microsoft.Azure.Batch;
using Microsoft.Azure.Batch.Auth;
using Microsoft.Azure.Batch.Common;
using Microsoft.Azure.BatchExplorer.Models;
using Microsoft.Azure.BatchExplorer.Service;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.BatchExplorer.Helpers
{
    /// <summary>
    /// Gets all jobs, tasks, workitems, and pools for the current account with no caching or other frills
    /// </summary>
    public class BasicDataProvider : IDataProvider
    {
        private const int MaxJobRequestsInFlight = 10;

        public Account CurrentAccount { get; private set; }

        //TODO: This is public currently to allow Models access to the BatchService object for the purpose of making protocol calls
        //TODO: We should make this private once all protocol calls are gone.
        public BatchService Service { get; private set; }

        public BasicDataProvider(Account currentAccount)
        {
            this.CurrentAccount = currentAccount;
            this.Service = new BatchService(this.CurrentAccount.BatchServiceUrl, 
                new BatchCredentials(this.CurrentAccount.AccountName, this.CurrentAccount.Key));
        }

        public IList<WorkItemModel> GetWorkItemCollection()
        {
            IEnumerable<ICloudWorkItem> workItems = this.Service.ListWorkItems(OptionsModel.Instance.ListDetailLevel);

            using (var sem = new SemaphoreSlim(MaxJobRequestsInFlight))
            {
                var workItemsTasks = workItems.Select(
                    async cloudWorkItem =>
                        {
                            if (cloudWorkItem.ExecutionInformation != null && cloudWorkItem.ExecutionInformation.RecentJob != null)
                            {
                                try
                                {
                                    await sem.WaitAsync();
                                    try
                                    {
                                        var latestJob = await cloudWorkItem.GetJobAsync(cloudWorkItem.ExecutionInformation.RecentJob.Name);
                                        return new WorkItemModel(cloudWorkItem, latestJob);
                                    }
                                    finally
                                    {
                                        sem.Release();
                                    }
                                }
                                catch (BatchException be)
                                {
                                    if (be.RequestInformation != null && be.RequestInformation.AzureError != null && be.RequestInformation.AzureError.Code == BatchErrorCodeStrings.JobNotFound)
                                    {
                                        return new WorkItemModel(cloudWorkItem);
                                    }

                                    return null;
                                }
                                catch (Exception)
                                {
                                    // eat the exception for now
                                    return null;
                                }
                            }
                            else
                            {
                                return new WorkItemModel(cloudWorkItem);
                            }
                        })
                    .ToArray();

                Task.WaitAll(workItemsTasks);

                return workItemsTasks
                    .Select(task => task.Result)
                    .Where(workItemModel => workItemModel != null)
                    .ToList();
            }
        }

        public async Task CreateWorkItem(CreateWorkItemOptions options)
        {
            await this.Service.CreateWorkItemAsync(options);
        }

        public async Task AddTask(AddTaskOptions options)
        {
            await this.Service.AddTaskAsync(options);
        }

        public IList<PoolModel> GetPoolCollection()
        {
            IEnumerable<ICloudPool> pools = this.Service.ListPools(OptionsModel.Instance.ListDetailLevel);
            IList<PoolModel> poolModels = pools.Select(pool => new PoolModel(pool)).ToList();
            return poolModels;
        }

        public async Task CreatePoolAsync(
            string poolName, 
            string vmSize, 
            int? targetDedicated, 
            string autoScaleFormula, 
            bool communicationEnabled,
            string osFamily,
            string osVersion,
            int maxTasksPerVM,
            TimeSpan? timeout)
        {
            await this.Service.CreatePoolAsync(poolName, vmSize, targetDedicated, autoScaleFormula, communicationEnabled, osFamily, osVersion, maxTasksPerVM, timeout);
        }

        public async Task CreateVMUserAsync(string poolName, string vmName, string userName, string password, DateTime expiryTime, bool admin)
        {
            await this.Service.CreateVMUserAsync(poolName, vmName, userName, password, expiryTime, admin);
        }

        public async Task ResizePoolAsync(string poolName, int targetDedicated, TimeSpan? timeout, TVMDeallocationOption? deallocationOption)
        {
            await this.Service.ResizePool(poolName, targetDedicated, timeout, deallocationOption);
        }
    }
}
