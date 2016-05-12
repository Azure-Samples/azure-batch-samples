﻿//Copyright (c) Microsoft Corporation

namespace Microsoft.Azure.BatchExplorer.Service
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Batch;
    using Microsoft.Azure.Batch.Common;
    using Microsoft.Azure.BatchExplorer.Models;
    using BatchSharedKeyCredential=Microsoft.Azure.Batch.Auth.BatchSharedKeyCredentials;

    /// <summary>
    /// Manages communication with the Batch service
    /// </summary>
    public class BatchService : IDisposable
    {
        public BatchSharedKeyCredential Credentials { get; private set; }

        private BatchClient Client { get; set; }
        private readonly IRetryPolicy retryPolicy;
        private bool disposed;

        public BatchService(BatchSharedKeyCredential credentials)
        {
            this.Client = BatchClient.Open(credentials);
            this.Credentials = credentials;
            this.retryPolicy = new LinearRetry(TimeSpan.FromSeconds(10), 5);

            this.Client.CustomBehaviors.Add(new RetryPolicyProvider(this.retryPolicy));
        }

        #region JobSchedule related operations

        /// <summary>
        /// Returns a list of JobSchedules
        /// </summary>
        /// <returns></returns>
        public IPagedEnumerable<CloudJobSchedule> ListJobSchedules(DetailLevel detailLevel = null)
        {
            return this.Client.JobScheduleOperations.ListJobSchedules(detailLevel);
        }

        /// <summary>
        /// Creates a job schedule with the specified options.
        /// </summary>
        /// <param name="options">The options describing the job schedule to create.</param>
        /// <returns></returns>
        public async Task CreateJobScheduleAsync(CreateJobScheduleOptions options)
        {
            CloudJobSchedule unboundJobSchedule = this.Client.JobScheduleOperations.CreateJobSchedule();
            unboundJobSchedule.Id = options.JobScheduleId;
                
            PoolInformation poolInformation = new PoolInformation();
            if (options.AutoPoolOptions.UseAutoPool.HasValue && options.AutoPoolOptions.UseAutoPool.Value)
            {
                    AutoPoolSpecification autoPoolSpecification = new AutoPoolSpecification()
                    {
                        AutoPoolIdPrefix = options.AutoPoolOptions.AutoPoolPrefix,
                        KeepAlive = options.AutoPoolOptions.KeepAlive,
                        PoolLifetimeOption = (PoolLifetimeOption)Enum.Parse(typeof(PoolLifetimeOption), options.AutoPoolOptions.LifeTimeOption),
                        PoolSpecification = new PoolSpecification()
                        {
                            CloudServiceConfiguration = new CloudServiceConfiguration(options.AutoPoolOptions.OSFamily),
                            VirtualMachineSize = options.AutoPoolOptions.VirutalMachineSize,
                            TargetDedicated = options.AutoPoolOptions.TargetDedicated
                        }
                    };

                    poolInformation.AutoPoolSpecification = autoPoolSpecification;
            }
            else
            {
                poolInformation.PoolId = options.PoolId;
            }
                
            unboundJobSchedule.JobSpecification = new JobSpecification()
            {
                Priority = options.Priority,
                PoolInformation = poolInformation
            };

            // TODO: These are read only
            unboundJobSchedule.JobSpecification.Constraints = new JobConstraints(options.MaxWallClockTime, options.MaxRetryCount);

            Schedule schedule = new Schedule()
            {
                DoNotRunAfter = options.DoNotRunAfter,
                DoNotRunUntil = options.DoNotRunUntil,
                RecurrenceInterval = options.RecurrenceInterval,
                StartWindow = options.StartWindow
            };
            unboundJobSchedule.Schedule = schedule;

            if (options.CreateJobManager.HasValue && options.CreateJobManager.Value == true)
            {
                JobManagerTask jobManager = new JobManagerTask()
                {
                    CommandLine = options.JobManagerOptions.CommandLine,
                    KillJobOnCompletion = options.JobManagerOptions.KillOnCompletion,
                    Id = options.JobManagerOptions.JobManagerId
                };

                jobManager.Constraints = new TaskConstraints(options.JobManagerOptions.MaxTaskWallClockTime, options.JobManagerOptions.RetentionTime, options.JobManagerOptions.MaxTaskRetryCount);

                unboundJobSchedule.JobSpecification.JobManagerTask = jobManager;
            }

            await unboundJobSchedule.CommitAsync();
        }

        #endregion

        #region Pool related operations

        /// <summary>
        /// Returns a list of pools
        /// </summary>
        /// <returns></returns>
        public IPagedEnumerable<CloudPool> ListPools(DetailLevel detailLevel = null)
        {
            return this.Client.PoolOperations.ListPools(detailLevel);
        }

        public Task<CloudPool> GetPoolAsync(string poolId)
        {
            return this.Client.PoolOperations.GetPoolAsync(poolId);
        }

        /// <summary>
        /// Creates a pool.
        /// </summary>
        /// <returns></returns>
        public async Task CreatePoolAsync(
            string poolId, 
            string virtualMachineSize, 
            int? targetDedicated, 
            string autoScaleFormula, 
            bool communicationEnabled,
            CloudServiceConfigurationOptions cloudServiceConfigurationOptions,
            VirtualMachineConfigurationOptions virtualMachineConfigurationOptions,
            int maxTasksPerComputeNode,
            TimeSpan? timeout,
            StartTaskOptions startTask)
        {
            CloudPool unboundPool;

            if (cloudServiceConfigurationOptions != null)
            {
                unboundPool = this.Client.PoolOperations.CreatePool(
                    poolId,
                    virtualMachineSize: virtualMachineSize,
                    cloudServiceConfiguration: new CloudServiceConfiguration(cloudServiceConfigurationOptions.OSFamily, cloudServiceConfigurationOptions.OSVersion),
                    targetDedicated: targetDedicated);
            }
            else if (virtualMachineConfigurationOptions != null)
            {
                unboundPool = this.Client.PoolOperations.CreatePool(
                    poolId,
                    virtualMachineSize: virtualMachineSize,
                    virtualMachineConfiguration: new VirtualMachineConfiguration(
                        new ImageReference(
                            publisher: virtualMachineConfigurationOptions.Publisher,
                            offer: virtualMachineConfigurationOptions.Offer,
                            skuId: virtualMachineConfigurationOptions.SkuId,
                            version: virtualMachineConfigurationOptions.Version),
                        virtualMachineConfigurationOptions.NodeAgentSkuId,
                        virtualMachineConfigurationOptions.EnableWindowsAutomaticUpdates.HasValue ? new WindowsConfiguration(virtualMachineConfigurationOptions.EnableWindowsAutomaticUpdates) : null),
                    targetDedicated: targetDedicated);
            }
            else
            {
                throw new ArgumentException("Must set cloudServiceConfiguration or virtualMachineConfiguration");
            }

            unboundPool.InterComputeNodeCommunicationEnabled = communicationEnabled;
            unboundPool.ResizeTimeout = timeout;
            unboundPool.MaxTasksPerComputeNode = maxTasksPerComputeNode;
                
            if (!string.IsNullOrEmpty(autoScaleFormula))
            {
                unboundPool.AutoScaleEnabled = true;
                unboundPool.AutoScaleFormula = autoScaleFormula;
            }

            if (startTask != null)
            {
                unboundPool.StartTask = new StartTask
                {
                    CommandLine = startTask.CommandLine,
                    RunElevated = startTask.RunElevated,
                    ResourceFiles = startTask.ResourceFiles.ConvertAll(f => new ResourceFile(f.BlobSource, f.FilePath)),
                    WaitForSuccess = true,
                };
            }

            await unboundPool.CommitAsync();
        }

        public async Task ResizePool(
            string poolId,
            int targetDedicated,
            TimeSpan? timeout,
            ComputeNodeDeallocationOption? deallocationOption)
        {
            await this.Client.PoolOperations.ResizePoolAsync(poolId, targetDedicated, timeout, deallocationOption);
        }

        public IPagedEnumerable<NodeAgentSku> ListNodeAgentSkus()
        {
            return this.Client.PoolOperations.ListNodeAgentSkus();
        }

        #endregion

        #region Job related operations

        public async Task CreateJobAsync(CreateJobOptions createJobOptions)
        {
            CloudJob unboundJob = this.Client.JobOperations.CreateJob();

            unboundJob.Id = createJobOptions.JobId;
            unboundJob.Priority = createJobOptions.Priority;
            unboundJob.Constraints = new JobConstraints(createJobOptions.MaxWallClockTime, createJobOptions.MaxRetryCount);

            PoolInformation poolInformation = new PoolInformation();
            if (createJobOptions.AutoPoolOptions.UseAutoPool.HasValue && createJobOptions.AutoPoolOptions.UseAutoPool.Value)
            {
                AutoPoolSpecification autoPoolSpecification = new AutoPoolSpecification()
                {
                    AutoPoolIdPrefix = createJobOptions.AutoPoolOptions.AutoPoolPrefix,
                    KeepAlive = createJobOptions.AutoPoolOptions.KeepAlive,
                    PoolLifetimeOption = (PoolLifetimeOption)Enum.Parse(typeof(PoolLifetimeOption), createJobOptions.AutoPoolOptions.LifeTimeOption),
                    PoolSpecification = new PoolSpecification()
                    {
                        CloudServiceConfiguration = new CloudServiceConfiguration(createJobOptions.AutoPoolOptions.OSFamily),
                        VirtualMachineSize = createJobOptions.AutoPoolOptions.VirutalMachineSize,
                        TargetDedicated = createJobOptions.AutoPoolOptions.TargetDedicated
                    }
                };

                poolInformation.AutoPoolSpecification = autoPoolSpecification;
            }
            else
            {
                poolInformation.PoolId = createJobOptions.PoolId;
            }

            unboundJob.PoolInformation = poolInformation;

            if (createJobOptions.CreateJobManager.HasValue && createJobOptions.CreateJobManager.Value == true)
            {
                JobManagerTask jobManager = new JobManagerTask()
                {
                    CommandLine = createJobOptions.JobManagerOptions.CommandLine,
                    KillJobOnCompletion = createJobOptions.JobManagerOptions.KillOnCompletion,
                    Id = createJobOptions.JobManagerOptions.JobManagerId
                };

                jobManager.Constraints = new TaskConstraints(
                    createJobOptions.JobManagerOptions.MaxTaskWallClockTime, 
                    createJobOptions.JobManagerOptions.RetentionTime, 
                    createJobOptions.JobManagerOptions.MaxTaskRetryCount);

                unboundJob.JobManagerTask = jobManager;
            }

            await unboundJob.CommitAsync();
        }

        /// <summary>
        /// Returns a list of jobs.
        /// </summary>
        /// <param name="detailLevel"></param>
        /// <returns></returns>
        public IPagedEnumerable<CloudJob> ListJobs(DetailLevel detailLevel = null)
        {
            return this.Client.JobOperations.ListJobs(detailLevel);
        }

        public CloudJob GetJob(string jobId, DetailLevel detailLevel)
        {
            using (System.Threading.Tasks.Task<CloudJob> getJobTask = this.GetJobAsync(jobId, detailLevel))
            {
                getJobTask.Wait();
                return getJobTask.Result;
            }
        }

        public Task<CloudJob> GetJobAsync(string jobId, DetailLevel detailLevel)
        {
            return this.Client.JobOperations.GetJobAsync(jobId, detailLevel);
        }
        
        #endregion

        #region Task related operations

        public CloudTask GetTask(string jobId, string taskId, DetailLevel detailLevel)
        {
            using (Task<CloudTask> getTaskTask = this.GetTaskAsync(jobId, taskId, detailLevel))
            {
                getTaskTask.Wait();
                return getTaskTask.Result;
            }
        }

        public Task<CloudTask> GetTaskAsync(string jobId, string taskId, DetailLevel detailLevel)
        {
            return this.Client.JobOperations.GetTaskAsync(jobId, taskId, detailLevel);
         
        }

        /// <summary>
        /// Adds a task.
        /// </summary>
        /// <param name="options">The options describing the task to add.</param>
        /// <returns></returns>
        public async Task AddTaskAsync(AddTaskOptions options)
        {
            CloudTask unboundTask = new CloudTask(options.TaskId, options.CommandLine);
            if (options.IsMultiInstanceTask)
            {
                unboundTask.MultiInstanceSettings = new MultiInstanceSettings(options.InstanceNumber);
                unboundTask.MultiInstanceSettings.CoordinationCommandLine = options.BackgroundCommand;
                unboundTask.MultiInstanceSettings.CommonResourceFiles = options.CommonResourceFiles.ConvertAll(f => new ResourceFile(f.BlobSource, f.FilePath));
            }
            unboundTask.RunElevated = options.RunElevated;
            unboundTask.Constraints = new TaskConstraints(null, null, options.MaxTaskRetryCount);
            unboundTask.ResourceFiles = options.ResourceFiles.ConvertAll(f => new ResourceFile(f.BlobSource, f.FilePath));
            await this.Client.JobOperations.AddTaskAsync(options.JobId, unboundTask);
        }
        #endregion

        #region Node related operations

        public Task CreateNodeUserAsync(string poolId, string computeNodeId, string userName, string password, DateTime expiryTime, bool admin)
        {
            ComputeNodeUser user = this.Client.PoolOperations.CreateComputeNodeUser(poolId, computeNodeId);
            user.Name = userName;
            user.Password = password;
            user.ExpiryTime = expiryTime;
            user.IsAdmin = admin;

            return user.CommitAsync();
        }

        #endregion

        #region Certificate related operations

        /// <summary>
        /// Returns a list of certificates
        /// </summary>
        /// <returns></returns>
        public IPagedEnumerable<Certificate> ListCertificates(DetailLevel detailLevel = null)
        {
            return this.Client.CertificateOperations.ListCertificates(detailLevel);
        }

        public Task<Certificate> GetCertificateAsync(string thumbprint, string thumbprintAlgorithm)
        {
            return this.Client.CertificateOperations.GetCertificateAsync(thumbprint, thumbprintAlgorithm);
        }

        public Task CreateCertificateAsync(CreateCertificateOptions options)
        {
            Certificate certificate;

            switch (options.CertificateFormat)
            {
                case CertificateFormat.Pfx:
                    certificate = this.Client.CertificateOperations.CreateCertificate(options.FilePath, options.Password);
                    break;
                case CertificateFormat.Cer:
                    certificate = this.Client.CertificateOperations.CreateCertificate(options.FilePath);
                    break;
                default:
                    throw new ArgumentException("Invalid certificate format " + options.CertificateFormat);
            }

            return certificate.CommitAsync();
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

        ~BatchService()
        {
            this.Dispose(false);
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
