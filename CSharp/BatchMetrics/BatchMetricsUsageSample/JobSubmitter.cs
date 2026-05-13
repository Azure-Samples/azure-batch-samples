//Copyright (c) Microsoft Corporation

namespace Microsoft.Azure.Batch.Samples.BatchMetricsUsageSample
{
    using global::Azure;
    using global::Azure.Compute.Batch;
    using global::Azure.ResourceManager.Batch;
    using global::Azure.ResourceManager.Batch.Models;
    using Microsoft.Azure.Batch.Samples.Common;
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    // This class is responsible for submitting and running sample jobs, just so that the
    // MetricMonitor in the Program class has something to monitor.
    public class JobSubmitter
    {
        private readonly AccountSettings accountSettings;
        private readonly BatchClient batchClient;
        private readonly BatchAccountResource batchAccount;
        private readonly List<string> createdJobIds = new List<string>();
        private bool createdNewPool;

        private const string PoolId = "batchmetrics-testpool";
        private const int PoolNodeCount = 5;
        private const string PoolNodeSize = "standard_d2_v3";

        private const int TestJobCount = 10;

        private const string JobIdPrefix = "batchmetrics-testjob-";
        private const int JobTaskCount = 20;
        private static readonly TimeSpan JobTaskTimeoutIncrement = TimeSpan.FromSeconds(10);
        private const string JobTaskIdPrefix = "testtask-";
        private static readonly TimeSpan JobInterval = TimeSpan.FromMinutes(2);

        public JobSubmitter(AccountSettings accountSettings)
        {
            this.accountSettings = accountSettings;
            this.batchClient = ClientFactory.CreateBatchClient(accountSettings);
            this.batchAccount = ClientFactory.CreateBatchAccountResource(accountSettings);
        }

        // Creates a pool so that the sample jobs have somewhere to run, so that they can
        // make progress and you can see their progress being tracked by the MetricMonitor.
        private async Task CreatePoolAsync()
        {
            var poolData = new BatchAccountPoolData
            {
                VmSize = PoolNodeSize,
                TaskSlotsPerNode = 2,
                DeploymentConfiguration = new BatchDeploymentConfiguration
                {
                    VmConfiguration = new BatchVmConfiguration(
                        new BatchImageReference
                        {
                            Publisher = "MicrosoftWindowsServer",
                            Offer = "WindowsServer",
                            Sku = "2016-Datacenter-smalldisk",
                            Version = "latest",
                        },
                        "batch.node.windows amd64"),
                },
                ScaleSettings = new BatchAccountPoolScaleSettings
                {
                    FixedScale = new BatchAccountFixedScaleSettings
                    {
                        TargetDedicatedNodes = PoolNodeCount,
                    },
                },
            };

            var createPoolResult = await GettingStartedCommon.CreatePoolIfNotExistAsync(this.batchAccount, PoolId, poolData);
            this.createdNewPool = (createPoolResult == CreatePoolResult.CreatedNew);
        }

        // Adds a sample job with a few sample tasks to the Batch account.
        private async Task SubmitJobAsync(string jobId)
        {
            try
            {
                await this.batchClient.CreateJobAsync(new BatchJobCreateOptions(jobId, new BatchPoolInfo { PoolId = PoolId }));
                this.createdJobIds.Add(jobId);
            }
            catch (RequestFailedException ex) when (ex.ErrorCode == BatchErrorCode.JobExists.ToString())
            {
                Console.WriteLine("The job already existed when we tried to create it");
                return;
            }

            foreach (var taskOptions in CreateTasks(jobId))
            {
                await this.batchClient.CreateTaskAsync(jobId, taskOptions);
            }
        }

        private static IEnumerable<BatchTaskCreateOptions> CreateTasks(string jobId)
        {
            for (int taskIndex = 0; taskIndex < JobTaskCount; taskIndex++)
            {
                var taskId = string.Format("{0}-{1}{2}", jobId, JobTaskIdPrefix, taskIndex);
                var taskCommandTimeout = (int)((taskIndex + 1) * JobTaskTimeoutIncrement.TotalSeconds);
                var taskCommandLine = string.Format("cmd /c ping -n {0} 127.0.0.100", taskCommandTimeout);
                yield return new BatchTaskCreateOptions(taskId, taskCommandLine);
            }
        }

        public async Task SubmitJobsAsync()
        {
            try
            {
                await this.CreatePoolAsync();

                for (int i = 0; i < TestJobCount; i++)
                {
                    var jobId = JobIdPrefix + i;
                    Console.WriteLine("Submitting job {0}", jobId);
                    await SubmitJobAsync(jobId);
                    await Task.Delay(JobInterval);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: {0}", ex);
            }
        }

        // Cleans up jobs and (if we created it) the pool.
        public async Task CleanUpAsync()
        {
            var poolIdsToDelete = new List<string>();
            if (this.createdNewPool)
            {
                poolIdsToDelete.Add(PoolId);
            }

            await SampleHelpers.DeleteBatchResourcesAsync(this.batchClient, this.batchAccount, this.createdJobIds, poolIdsToDelete);
        }
    }
}
