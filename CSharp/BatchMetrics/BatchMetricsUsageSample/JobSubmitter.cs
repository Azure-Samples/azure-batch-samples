//Copyright (c) Microsoft Corporation

namespace Microsoft.Azure.Batch.Samples.BatchMetricsUsageSample
{
    using Microsoft.Azure.Batch;
    using Microsoft.Azure.Batch.Common;
    using Microsoft.Azure.Batch.Samples.Common;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public class JobSubmitter
    {
        private readonly BatchClient batchClient;
        private readonly List<string> createdJobIds = new List<string>();

        private const string PoolId = "batchmetrics-testpool";
        private const int PoolNodeCount = 10;
        private const string PoolNodeSize = "medium";
        private const string PoolOSFamily = "4";

        private const int TestJobCount = 10;

        private const string JobIdPrefix = "batchmetrics-testjob-";
        private const int JobTaskCount = 20;
        private static readonly TimeSpan JobTaskTimeoutIncrement = TimeSpan.FromSeconds(10);
        private const string JobTaskIdPrefix = "testtask-";
        private static readonly TimeSpan JobInterval = TimeSpan.FromMinutes(2);
      
        public JobSubmitter(BatchClient batchClient)
        {
            this.batchClient = batchClient;
            this.batchClient.CustomBehaviors.Add(RetryPolicyProvider.LinearRetryProvider(TimeSpan.FromSeconds(10), 3));
        }

        private async Task CreatePoolAsync()
        {
            var pool = this.batchClient.PoolOperations.CreatePool(
                poolId: PoolId,
                targetDedicated: PoolNodeCount,
                virtualMachineSize: PoolNodeSize,
                cloudServiceConfiguration: new CloudServiceConfiguration(PoolOSFamily));

            await GettingStartedCommon.CreatePoolIfNotExistAsync(this.batchClient, pool);
        }

        private async Task SubmitJobAsync(string jobId)
        {
            var job = this.batchClient.JobOperations.CreateJob();
            job.Id = jobId;
            job.PoolInformation = new PoolInformation() { PoolId = PoolId };

            try
            {
                await job.CommitAsync();
                this.createdJobIds.Add(jobId);
            }
            catch (BatchException ex)
            {
                if (ex.IsBatchErrorCode(BatchErrorCodeStrings.JobExists))
                {
                    Console.WriteLine("The job already existed when we tried to create it");
                    return;  // no point trying to add tasks, as the task IDs probably exist already from a previous run
                }
                else
                {
                    throw; // Any other exception is unexpected
                }
            }

            var tasksToRun = CreateTasks(jobId).ToList();

            await this.batchClient.JobOperations.AddTaskAsync(jobId, tasksToRun);
        }

        private static IEnumerable<CloudTask> CreateTasks(string jobId)
        {
            // Create a set of tasks for each configuration 
            // The runtime for each task increases by the timeout factor

            for (int taskIndex = 0; taskIndex < JobTaskCount; taskIndex++)
            {
                var taskId = string.Format("{0}-{1}{2}", jobId, JobTaskIdPrefix, taskIndex);
                var taskCommandTimeout = (int)((taskIndex + 1) * JobTaskTimeoutIncrement.TotalSeconds);
                var taskCommandLine = string.Format("cmd /c ping -n {0} 127.0.0.100", taskCommandTimeout);
                yield return new CloudTask(taskId, taskCommandLine);
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
            catch (BatchException ex)
            {
                Console.WriteLine("Batch service exception: {0}" + ex.ToString());
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: {0}", ex.ToString());
            }
        }

        public async Task CleanUpJobsAsync()
        {
            foreach (var jobId in this.createdJobIds)
            {
                try
                {
                    await this.batchClient.JobOperations.DeleteJobAsync(jobId);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Failed to delete job {0}: {1}", jobId, ex.Message);
                }
            }
        }
    }
}
