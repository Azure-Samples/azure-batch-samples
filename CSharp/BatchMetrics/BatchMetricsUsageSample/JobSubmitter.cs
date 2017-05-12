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

    // This class is responsible for submitting and running sample jobs, just so that the
    // MetricMonitor in the Program class has something to monitor.
    public class JobSubmitter
    {
        private readonly BatchClient batchClient;
        private readonly List<string> createdJobIds = new List<string>();
        private bool createdNewPool;

        private const string PoolId = "batchmetrics-testpool";
        private const int PoolNodeCount = 5;
        private const string PoolNodeSize = "small";
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
            this.batchClient.CustomBehaviors.Add(RetryPolicyProvider.ExponentialRetryProvider(TimeSpan.FromSeconds(5), 3));
        }

        // Creates a pool so that the sample jobs have somewhere to run, so that they can
        // make progress and you can see their progress being tracked by the MetricMonitor.
        private async Task CreatePoolAsync()
        {
            var pool = this.batchClient.PoolOperations.CreatePool(
                poolId: PoolId,
                targetDedicatedComputeNodes: PoolNodeCount,
                virtualMachineSize: PoolNodeSize,
                cloudServiceConfiguration: new CloudServiceConfiguration(PoolOSFamily));

            pool.MaxTasksPerComputeNode = 2;

            var createPoolResult = await GettingStartedCommon.CreatePoolIfNotExistAsync(this.batchClient, pool);

            this.createdNewPool = (createPoolResult == CreatePoolResult.CreatedNew);
        }

        // Adds a sample job with a few sample tasks to the Batch account.  The tasks
        // take varying amounts of time to run so you can see the numbers of tasks in
        // the active, running, and completed states changing over time in each job.
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

            var tasksToRun = CreateTasks(jobId);

            await this.batchClient.JobOperations.AddTaskAsync(jobId, tasksToRun);
        }

        // Creates a set of sample tasks to allow you to see the MetricMonitor showing
        // the job progressing.  Each task in the job takes progressively longer than the
        // previous one.
        private static IEnumerable<CloudTask> CreateTasks(string jobId)
        {
            for (int taskIndex = 0; taskIndex < JobTaskCount; taskIndex++)
            {
                var taskId = string.Format("{0}-{1}{2}", jobId, JobTaskIdPrefix, taskIndex);
                var taskCommandTimeout = (int)((taskIndex + 1) * JobTaskTimeoutIncrement.TotalSeconds);
                var taskCommandLine = string.Format("cmd /c ping -n {0} 127.0.0.100", taskCommandTimeout);
                yield return new CloudTask(taskId, taskCommandLine);
            }
        }

        // Adds sample jobs to the Batch account.  Jobs are added periodically so that you
        // can see how each job progresses over time.
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

        // Deletes any jobs that were created specifically for the MetricMonitor
        // to report on.
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

        // Deletes the pool that was created specifically for the MetricMonitor
        // to report on, if a new pool was created.
        public async Task CleanUpPoolIfRequiredAsync()
        {
            if (this.createdNewPool)
            {
                try
                {
                    await this.batchClient.PoolOperations.DeletePoolAsync(PoolId);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Failed to delete pool {0}: {1}", PoolId, ex.Message);
                }
            }
        }
    }
}
