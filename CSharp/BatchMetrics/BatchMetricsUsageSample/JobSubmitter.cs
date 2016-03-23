using Microsoft.Azure.Batch;
using Microsoft.Azure.Batch.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BatchMetricsUsageSample
{
    public class JobSubmitter
    {
        private readonly BatchClient _batchClient;
        private readonly List<string> _createdJobIds = new List<string>();

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
            _batchClient = batchClient;
            _batchClient.CustomBehaviors.Add(RetryPolicyProvider.LinearRetryProvider(TimeSpan.FromSeconds(10), 3));
        }

        private async Task CreatePoolAsync()
        {
            var pool = _batchClient.PoolOperations.CreatePool(
                poolId: PoolId,
                targetDedicated: PoolNodeCount,
                virtualMachineSize: PoolNodeSize,
                osFamily: PoolOSFamily);

            try
            {
                Console.WriteLine("Attempting to create pool: {0}", pool.Id);

                await pool.CommitAsync();

                Console.WriteLine("Created pool {0} with {1} nodes", PoolId, PoolNodeCount);
            }
            catch (BatchException e)
            {
                if (e.IsBatchErrorCode(BatchErrorCodeStrings.PoolExists))
                {
                    Console.WriteLine("The pool already existed when we tried to create it");
                }
                else
                {
                    throw; // Any other exception is unexpected
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                throw;
            }
        }

        private async Task SubmitJobAsync(string jobId)
        {
            var job = _batchClient.JobOperations.CreateJob();
            job.Id = jobId;
            job.PoolInformation = new PoolInformation() { PoolId = PoolId };

            try
            {
                await job.CommitAsync();
                _createdJobIds.Add(jobId);
            }
            catch (BatchException e)
            {
                if (e.IsBatchErrorCode(BatchErrorCodeStrings.JobExists))
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

            await _batchClient.JobOperations.AddTaskAsync(jobId, tasksToRun);
        }

        private static IEnumerable<CloudTask> CreateTasks(string jobId)
        {
            var tasksToRun = new List<CloudTask>();

            // Create a set of tasks for each configuration 
            // The runtime for each task increases by the timeout factor

            for (int taskIndex = 0; taskIndex < JobTaskCount; taskIndex++)
            {
                var taskId = string.Format("{0}-{1}{2}", jobId, JobTaskIdPrefix, taskIndex);
                var taskCommandTimeout = (int)(taskIndex * JobTaskTimeoutIncrement.TotalSeconds);
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
            catch (BatchException be)
            {
                if (be.RequestInformation != null && be.RequestInformation.AzureError != null)
                {
                    Console.WriteLine("Batch Exception {0}", be.RequestInformation.AzureError.Message);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception {0}", e.Message);
            }
        }

        public async Task CleanUpJobsAsync()
        {
            foreach (var jobId in _createdJobIds)
            {
                try
                {
                    await _batchClient.JobOperations.DeleteJobAsync(jobId);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Failed to delete job {0}: {1}", jobId, ex.Message);
                }
            }
        }
    }
}
