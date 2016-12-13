//Copyright (c) Microsoft Corporation

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Batch;
using Microsoft.Azure.Batch.Auth;
using Microsoft.Azure.Batch.Common;
using Microsoft.Azure.BatchExplorer.Models;
using Microsoft.Azure.BatchExplorer.Service;

namespace Microsoft.Azure.BatchExplorer.Helpers
{
    /// <summary>
    /// Gets all jobs, tasks, jobschedules, and pools for the current account with no caching or other frills
    /// </summary>
    public class BasicDataProvider : IDataProvider
    {
        public Account CurrentAccount { get; private set; }

        public BatchService Service { get; private set; }

        public BasicDataProvider(Account currentAccount)
        {
            this.CurrentAccount = currentAccount;
            this.Service = new BatchService(new BatchSharedKeyCredentials(this.CurrentAccount.BatchServiceUrl, this.CurrentAccount.AccountName, this.CurrentAccount.BatchServiceKey));
        }

        public async Task<IList<JobScheduleModel>> GetJobScheduleCollectionAsync()
        {
            IPagedEnumerable<CloudJobSchedule> jobSchedules = this.Service.ListJobSchedules(OptionsModel.Instance.ListDetailLevel);
            List<JobScheduleModel> jobScheduleModels = new List<JobScheduleModel>();

            await jobSchedules.ForEachAsync(item => jobScheduleModels.Add(new JobScheduleModel(item)));

            return jobScheduleModels;
        }

        public async Task CreateJobScheduleAsync(CreateJobScheduleOptions options)
        {
            await this.Service.CreateJobScheduleAsync(options);
        }

        public async Task<IList<JobModel>> GetJobCollectionAsync(string jobSearchFilter)
        {
            IPagedEnumerable<CloudJob> jobs = this.Service.ListJobs(OptionsModel.Instance.ListDetailLevel);
            List<JobModel> jobModels = new List<JobModel>();

            // The properties we want to filter on are not available in a ODATADetailLevel query. Filter them client side
            if (!String.IsNullOrWhiteSpace(jobSearchFilter))
            {
                var jobSearchFilterL = jobSearchFilter.ToLowerInvariant();
                var filteredJobs = jobs.Where(f => f.DisplayName.ToLowerInvariant().Contains(jobSearchFilterL) ||
                                                                   f.Id.ToLowerInvariant().Contains(jobSearchFilterL) ||
                                                                   f.Metadata.Any(g => g.Name.ToLowerInvariant().Contains(jobSearchFilterL)) ||
                                                                   f.Metadata.Any(g => g.Value.ToLowerInvariant().Contains(jobSearchFilterL)));

                jobModels.AddRange(filteredJobs.Select(item => new JobModel(item)));
            }
            else
            {
                await jobs.ForEachAsync(item => jobModels.Add(new JobModel(item)));
            }

            return jobModels.OrderByDescending(t => t.CreationTime).ToList();
        }

        public Task CreateJobAsync(CreateJobOptions options)
        {
            return this.Service.CreateJobAsync(options);
        }

        public async Task AddTaskAsync(AddTaskOptions options)
        {
            await this.Service.AddTaskAsync(options);
        }

        public async Task<IList<PoolModel>> GetPoolCollectionAsync()
        {
            IPagedEnumerable<CloudPool> pools = this.Service.ListPools(OptionsModel.Instance.ListDetailLevel);
            IList<PoolModel> poolModels = new List<PoolModel>();

            await pools.ForEachAsync(item => poolModels.Add(new PoolModel(item)));

            return poolModels;
        }

        public async Task<IList<CertificateModel>> GetCertificatesCollectionAsync()
        {
            IPagedEnumerable<Certificate> certificates = this.Service.ListCertificates(null);
            IList<CertificateModel> certificateModels = new List<CertificateModel>();

            await certificates.ForEachAsync(item => certificateModels.Add(new CertificateModel(item)));

            return certificateModels;
        }

        public Task CreateCertificateAsync(CreateCertificateOptions options)
        {
            return this.Service.CreateCertificateAsync(options);
        }

        public async Task CreatePoolAsync(
            string poolId, 
            string virtualMachineSize, 
            int? targetDedicated, 
            string autoScaleFormula, 
            bool communicationEnabled,
            string subnetId,
            CloudServiceConfigurationOptions cloudServiceConfigurationOptions,
            VirtualMachineConfigurationOptions virtualMachineConfigurationOptions,
            int maxTasksPerComputeNode,
            TimeSpan? timeout,
            StartTaskOptions startTask)
        {
            await this.Service.CreatePoolAsync(
                poolId, 
                virtualMachineSize, 
                targetDedicated, 
                autoScaleFormula, 
                communicationEnabled, 
                subnetId,
                cloudServiceConfigurationOptions, 
                virtualMachineConfigurationOptions, 
                maxTasksPerComputeNode, 
                timeout, 
                startTask);
        }

        public async Task CreateComputeNodeUserAsync(string poolId, string nodeId, string userName, string password, DateTime expiryTime, bool admin)
        {
            await this.Service.CreateNodeUserAsync(poolId, nodeId, userName, password, expiryTime, admin);
        }

        public async Task ResizePoolAsync(string poolId, int targetDedicated, TimeSpan? timeout, ComputeNodeDeallocationOption? computeNodeDeallocationOption)
        {
            await this.Service.ResizePool(poolId, targetDedicated, timeout, computeNodeDeallocationOption);
        }

        public async Task<IList<NodeAgentSku>> ListNodeAgentSkusAsync()
        {
            IPagedEnumerable<NodeAgentSku> nodeAgentSkus = this.Service.ListNodeAgentSkus();

            IList<NodeAgentSku> results = await nodeAgentSkus.ToListAsync();

            return results;
        }

        public async Task<string> EvaluateAutoScaleFormulaAsync(string poolId, string autoScaleFormula)
        {
            return await this.Service.EvaluateAutoScaleFormula(poolId, autoScaleFormula);
        }

        public async Task EnableAutoScaleAsync(string poolId, string autoScaleFormula)
        {
            await this.Service.EnableAutoScale(poolId, autoScaleFormula);
        }
    }
}
