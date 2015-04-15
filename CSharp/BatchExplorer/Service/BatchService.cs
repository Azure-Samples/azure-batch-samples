using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.Batch;
using Microsoft.Azure.Batch.Auth;
using Microsoft.Azure.Batch.Common;
using Microsoft.Azure.Batch.Protocol;
using Microsoft.Azure.BatchExplorer.Models;

namespace Microsoft.Azure.BatchExplorer.Service
{
    /// <summary>
    /// Manages communication with the Batch service
    /// </summary>
    public class BatchService : IDisposable
    {
        public Uri BaseUri { get; private set; }
        public BatchCredentials Credentials { get; private set; }

        private IBatchClient Client { get; set; }
        private readonly IRetryPolicy retryPolicy;
        private bool disposed;

        public BatchService(string baseUrl, BatchCredentials credentials)
        {
            this.Client = BatchClient.Connect(baseUrl, credentials);
            this.BaseUri = new Uri(baseUrl);
            this.Credentials = credentials;
            this.retryPolicy = new LinearRetry(TimeSpan.FromSeconds(10), 5);
            
            this.Client.CustomBehaviors.Add(new SetRetryPolicy(this.retryPolicy));
            this.Client.CustomBehaviors.Add(new RequestInterceptor((req) => { req.MaximumExecutionTime = TimeSpan.FromMinutes(2); }));
        }

        #region WorkItem related operations

        /// <summary>
        /// Returns a list of WorkItems
        /// </summary>
        /// <returns></returns>
        public IEnumerable<ICloudWorkItem> ListWorkItems(DetailLevel detailLevel = null)
        {
            using (IWorkItemManager wiManager = this.Client.OpenWorkItemManager())
            {
                return wiManager.ListWorkItems(detailLevel);
            }
        }

        /// <summary>
        /// Creates a work item with the specified work item options.
        /// </summary>
        /// <param name="options">The options describing the work item to create.</param>
        /// <returns></returns>
        public async Task CreateWorkItemAsync(CreateWorkItemOptions options)
        {
            try
            {
                using (IWorkItemManager workItemManager = this.Client.OpenWorkItemManager())
                {
                    ICloudWorkItem unboundWorkItem = workItemManager.CreateWorkItem(options.WorkItemName);

                    IJobExecutionEnvironment jobExecutionEnvironment = new JobExecutionEnvironment();
                    if (options.UseAutoPool.HasValue && options.UseAutoPool.Value)
                    {
                            IAutoPoolSpecification autoPoolSpecification = new AutoPoolSpecification()
                            {
                                AutoPoolNamePrefix = options.AutoPoolPrefix,
                                KeepAlive = options.KeepAlive,
                                PoolLifeTimeOption = options.LifeTimeOption.Equals("Job", StringComparison.OrdinalIgnoreCase) ? PoolLifeTimeOption.Job : PoolLifeTimeOption.WorkItem
                            };

                            jobExecutionEnvironment.AutoPoolSpecification = autoPoolSpecification;
                    }
                    else
                    {
                        jobExecutionEnvironment.PoolName = options.PoolName;
                    }

                    unboundWorkItem.JobExecutionEnvironment = jobExecutionEnvironment;
                    unboundWorkItem.JobSpecification = new JobSpecification()
                    {
                        Priority = options.Priority
                    };

                    // TODO: These are read only
                    unboundWorkItem.JobSpecification.JobConstraints = new JobConstraints(options.MaxWallClockTime, options.MaxRetryCount);

                    if (options.CreateSchedule.HasValue && options.CreateSchedule.Value == true)
                    {
                        IWorkItemSchedule schedule = new WorkItemSchedule()
                        {
                            DoNotRunAfter = options.DoNotRunAfter,
                            DoNotRunUntil = options.DoNotRunUntil,
                            RecurrenceInterval = options.RecurrenceInterval,
                            StartWindow = options.StartWindow
                        };

                        unboundWorkItem.Schedule = schedule;
                    }

                    if (options.CreateJobManager.HasValue && options.CreateJobManager.Value == true)
                    {
                        IJobManager jobManager = new JobManager()
                        {
                            CommandLine = options.CommandLine,
                            KillJobOnCompletion = options.KillOnCompletion,
                            Name = options.JobManagerName
                        };

                        jobManager.TaskConstraints = new TaskConstraints(options.MaxTaskWallClockTime, options.RetentionTime, options.MaxTaskRetryCount);

                        unboundWorkItem.JobSpecification.JobManager = jobManager;
                    }

                    await unboundWorkItem.CommitAsync();
                }
            }
            catch
            {
                throw;
            }
        }

        #endregion

        #region Pool related operations

        /// <summary>
        /// Returns a list of pools
        /// </summary>
        /// <returns></returns>
        public IEnumerable<ICloudPool> ListPools(DetailLevel detailLevel = null)
        {
            using (IPoolManager poolManager = this.Client.OpenPoolManager())
            {
                return poolManager.ListPools(detailLevel);
            }
        }

        public Task<ICloudPool> GetPoolAsync(string poolName)
        {
            using (IPoolManager poolManager = this.Client.OpenPoolManager())
            {
                return poolManager.GetPoolAsync(poolName);
            }
        }

        /// <summary>
        /// Creates a pool.
        /// </summary>
        /// <param name="poolName"></param>
        /// <param name="vmSize"></param>
        /// <param name="targetDedicated"></param>
        /// <param name="autoScaleFormula"></param>
        /// <param name="communicationEnabled"></param>
        /// <param name="osFamily"></param>
        /// <param name="osVersion"></param>
        /// <param name="maxTasksPerVM"></param>
        /// <param name="timeout"></param>
        /// <returns></returns>
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
            using (IPoolManager poolManager = this.Client.OpenPoolManager())
            {
                ICloudPool unboundPool = poolManager.CreatePool(
                    poolName, 
                    osFamily: osFamily, 
                    vmSize: vmSize, 
                    targetDedicated: targetDedicated);
                    
                unboundPool.TargetOSVersion = osVersion;
                unboundPool.Communication = communicationEnabled;
                unboundPool.ResizeTimeout = timeout;
                unboundPool.MaxTasksPerVM = maxTasksPerVM;
                
                if (!string.IsNullOrEmpty(autoScaleFormula))
                {
                    unboundPool.AutoScaleEnabled = true;
                    unboundPool.AutoScaleFormula = autoScaleFormula;
                }

                await unboundPool.CommitAsync();
            }
        }

        public async Task ResizePool(
            string poolName,
            int targetDedicated,
            TimeSpan? timeout,
            TVMDeallocationOption? deallocationOption)
        {
            using (IPoolManager poolManager = this.Client.OpenPoolManager())
            {
                await poolManager.ResizePoolAsync(poolName, targetDedicated, timeout, deallocationOption);
            }
        }

        #endregion

        #region Job related operations
        //
        // Once full OM support is added, we can remove all protocol based job operations
        //

        /// <summary>
        /// TODO: This is a hack because Job.Refresh breaks using the job due to its ParentWorkItem name being invalidated in the property router
        /// </summary>
        public ICloudJob GetJob(string workItemName, string jobName, DetailLevel detailLevel)
        {
            using (System.Threading.Tasks.Task<ICloudJob> getJobTask = this.GetJobAsync(workItemName, jobName, detailLevel))
            {
                getJobTask.Wait();
                return getJobTask.Result;
            }
        }

        public Task<ICloudJob> GetJobAsync(string workItemName, string jobName, DetailLevel detailLevel)
        {
            using (IWorkItemManager wiManager = this.Client.OpenWorkItemManager())
            {
                return wiManager.GetJobAsync(workItemName, jobName, detailLevel);
            }
        }
        
        #endregion

        #region Task related operations
        //
        // Once full OM support is enabled we can remove all protocol task operations
        //
        /// <summary>
        /// TODO: This is a hack because Task.Refresh breaks using the job due to its ParentWorkItem name being invalidated in the property router
        /// </summary>
        public ICloudTask GetTask(string workItemName, string jobName, string taskName, DetailLevel detailLevel)
        {
            using (Task<ICloudTask> getTaskTask = this.GetTaskAsync(workItemName, jobName, taskName, detailLevel))
            {
                getTaskTask.Wait();
                return getTaskTask.Result;
            }
        }

        public Task<ICloudTask> GetTaskAsync(string workItemName, string jobName, string taskName, DetailLevel detailLevel)
        {
            using (IWorkItemManager wiManager = this.Client.OpenWorkItemManager())
            {
                return wiManager.GetTaskAsync(workItemName, jobName, taskName, detailLevel);
            }
        }

        /// <summary>
        /// Adds a task.
        /// </summary>
        /// <param name="options">The options describing the task to add.</param>
        /// <returns></returns>
        public async Task AddTaskAsync(AddTaskOptions options)
        {
            using (IWorkItemManager workItemManager = this.Client.OpenWorkItemManager())
            {
                ICloudTask unboundTask = new CloudTask(options.TaskName, options.CommandLine);
                await workItemManager.AddTaskAsync(options.WorkItemName, options.JobName, unboundTask);
            }
        }
        #endregion

        #region VM related operations

        public Task CreateVMUserAsync(string poolName, string vmName, string userName, string password, DateTime expiryTime, bool admin)
        {
            using (IPoolManager poolManager = this.Client.OpenPoolManager())
            {
                IUser user = poolManager.CreateUser(poolName, vmName);
                user.Name = userName;
                user.Password = password;
                user.ExpiryTime = expiryTime;
                user.IsAdmin = admin;

                return user.CommitAsync();
            }
        }

        #endregion

        /// <summary>
        /// Dispose of this object and all members
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);   
        }

        /// <summary>
        /// Disposes of this object
        /// </summary>
        private void Dispose(bool disposing)
        {
            if (this.disposed)
            {
                return;
            }

            if (disposing)
            {
                this.Client.Dispose();
            }

            this.disposed = true;
        }
    }
}
