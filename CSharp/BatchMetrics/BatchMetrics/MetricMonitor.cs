//Copyright (c) Microsoft Corporation

namespace Microsoft.Azure.Batch.Samples.BatchMetrics
{
    using Microsoft.Azure.Batch;
    using Microsoft.Azure.Batch.Auth;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Monitors an Azure Batch account and provides aggregate status and metric information
    /// about the jobs in that account.
    /// </summary>
    public sealed class MetricMonitor : IDisposable
    {
        private readonly bool ownsClient;
        private readonly BatchClient batchClient;
        private readonly TimeSpan monitorInterval;
        private readonly Dictionary<string, TaskStateCache> jobStateCache = new Dictionary<string, TaskStateCache>();

        private Task runTask;
        private readonly object runLock = new object();
        private readonly CancellationTokenSource runCancel = new CancellationTokenSource();

        private static readonly TimeSpan DefaultMonitorInterval = TimeSpan.FromSeconds(60);
        private static readonly TimeSpan MaximumClockSkew = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Initializes a new instance of the <see cref="MetricMonitor"/> class.
        /// </summary>
        /// <param name="batchClient">The <see cref="BatchClient"/> to use for accessing the Azure Batch service.</param>
        public MetricMonitor(BatchClient batchClient)
            : this(batchClient, false, DefaultMonitorInterval)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MetricMonitor"/> class.
        /// </summary>
        /// <param name="baseUrl">The Batch service endpoint.</param>
        /// <param name="accountName">The name of the Batch account.</param>
        /// <param name="accountKey">The Base64 encoded account access key.</param>
        public MetricMonitor(string baseUrl, string accountName, string accountKey)
            : this(BatchClient.Open(new BatchSharedKeyCredentials(baseUrl, accountName, accountKey)), true, DefaultMonitorInterval)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MetricMonitor"/> class.
        /// </summary>
        /// <param name="batchClient">The <see cref="BatchClient"/> to use for accessing the Azure Batch service.</param>
        /// <param name="monitorInterval">The interval at which to update the metrics.</param>
        public MetricMonitor(BatchClient batchClient, TimeSpan monitorInterval)
            : this(batchClient, false, monitorInterval)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MetricMonitor"/> class.
        /// </summary>
        /// <param name="baseUrl">The Batch service endpoint.</param>
        /// <param name="accountName">The name of the Batch account.</param>
        /// <param name="accountKey">The Base64 encoded account access key.</param>
        /// <param name="monitorInterval">The interval at which to update the metrics.</param>
        public MetricMonitor(string baseUrl, string accountName, string accountKey, TimeSpan monitorInterval)
            : this(BatchClient.Open(new BatchSharedKeyCredentials(baseUrl, accountName, accountKey)), true, monitorInterval)
        {
        }

        /// <summary>
        /// Gets the job metrics at the last time the <see cref="MetricMonitor"/> updated them.
        /// </summary>
        /// <remarks>If the MetricMonitor has not yet retrieved any metrics, this property is null.
        /// You must call the <see cref="Start"/> method to start retrieving metrics.</remarks>
        public MetricEvent CurrentMetrics
        {
            get; private set;
        }

        /// <summary>
        /// Raised when the <see cref="MetricMonitor"/> has updated the <see cref="CurrentMetrics"/>.
        /// </summary>
        public event EventHandler MetricsUpdated;

        /// <summary>
        /// Starts monitoring the Azure Batch account and gathering metrics.  Once Start has been called,
        /// metrics will be updated periodically: the latest metrics can be found in <see cref="CurrentMetrics"/>,
        /// and the <see cref="MetricsUpdated"/> event is raised on each update.
        /// </summary>
        public void Start()
        {
            lock (this.runLock)
            {
                if (this.runTask == null)
                {
                    this.runTask = Task.Run(() => Run());
                }
            }
        }

        // The main monitoring engine.  This method runs continuously until the monitor
        // is disposed.  Each time round the loop it calls the Batch service to get task
        // metrics, then waits for the monitoring interval before going round the loop
        // again.
        private async Task Run()
        {
            while (!this.runCancel.IsCancellationRequested)
            {
                CurrentMetrics = await CollectMetricsAsync();
                OnMetricsUpdated();
                await TaskHelpers.CancellableDelay(this.monitorInterval, this.runCancel.Token);
            }
        }

        private void OnMetricsUpdated()
        {
            var evt = MetricsUpdated;
            if (evt != null)
            {
                evt(this, EventArgs.Empty);
            }
        }

        private MetricMonitor(BatchClient batchClient, bool ownsClient, TimeSpan monitorInterval)
        {
            if (batchClient == null)
            {
                throw new ArgumentNullException("batchClient");
            }

            if (monitorInterval <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException("monitorInterval", "monitorInterval must be positive");
            }

            this.batchClient = batchClient;
            this.ownsClient = ownsClient;
            this.monitorInterval = monitorInterval;
        }

        // Calls the Batch service to get job metrics. This is done in two parts:
        //
        // 1. List all jobs in the account.
        // 2. For each job, collect metrics for that job (see CollectTaskMetricsAsync).
        //
        // For simplicity, job metrics (step 2) are collected serially.  You could reduce latency
        // by performing the CollectTaskMetricsAsync calls in parallel, but would need to
        // take care to synchronize access to the MetricsBuilder that accumulates the results.
        private async Task<MetricEvent> CollectMetricsAsync()
        {
            MetricEvent.Builder metricsBuilder = new MetricEvent.Builder { CollectionStarted = DateTime.UtcNow };

            try
            {
                var totalLatencyStopWatch = Stopwatch.StartNew();

                var listJobsTimer = Stopwatch.StartNew();
                var jobs = await this.batchClient.JobOperations.ListJobs(DetailLevels.IdAndState.AllEntities).ToListAsync(this.runCancel.Token);
                listJobsTimer.Stop();

                metricsBuilder.ListJobsLatency = listJobsTimer.Elapsed;

                foreach (var job in jobs)
                {
                    await CollectTaskMetricsAsync(metricsBuilder, job);
                }

                totalLatencyStopWatch.Stop();
                metricsBuilder.TotalLatency = totalLatencyStopWatch.Elapsed;
                metricsBuilder.CollectionCompleted = DateTime.UtcNow;

                return metricsBuilder.Build();
            }
            catch (Exception ex)
            {
                return new MetricEvent(metricsBuilder.CollectionStarted, DateTime.UtcNow, ex);
            }
        }

        // Calls the Batch service to get metrics for a single job.  The first time the
        // MetricMonitor sees a job, it creates a TaskStateCache to hold task state information,
        // and queries the states of *all* tasks in the job. Subsequent times, it queries
        // only for tasks whose states have changed since the previous query -- this significant
        // reduces download volumes for large jobs. In either case, it then updates the
        // cached task states and aggregates them into a TaskStateCounts object.
        private async Task CollectTaskMetricsAsync(MetricEvent.Builder metricsBuilder, CloudJob job)
        {
            TaskStateCache taskStateCache;

            bool firstTime = !this.jobStateCache.ContainsKey(job.Id);
            if (firstTime)
            {
                taskStateCache = new TaskStateCache();
                this.jobStateCache.Add(job.Id, taskStateCache);
            }
            else
            {
                taskStateCache = this.jobStateCache[job.Id];
            }

            // If the monitor API is called for the first time, it has to issue a query to enumerate all the tasks once to get its state.
            // This is a relatively slow query.
            // Subsequent calls to the monitor API will only look for changes to the task state since the last time the query was issued and 
            // a clock skew (which is within 30 seconds approximately for Azure). Thus if the monitoring API periodicity is 1 minute, then the query 
            // should look for changes in the last minute and 30 seconds.

            // TODO: it would be better to record the time at which the last query was issued and use that,
            // rather than subtracting the monitor interval from the current time
            DateTime since = DateTime.UtcNow - (this.monitorInterval + MaximumClockSkew);
            var tasksToList = firstTime ? DetailLevels.IdAndState.AllEntities : DetailLevels.IdAndState.OnlyChangedAfter(since);

            var listTasksTimer = Stopwatch.StartNew();
            var tasks = await job.ListTasks(tasksToList).ToListAsync(this.runCancel.Token);
            listTasksTimer.Stop();

            var listTasksLatency = listTasksTimer.Elapsed;

            foreach (var task in tasks)
            {
                taskStateCache.UpdateTaskState(task.Id, task.State.Value);
            }

            var taskStateCounts = taskStateCache.GetTaskStateCounts();

            metricsBuilder.JobStats.Add(job.Id, new JobMetrics(listTasksLatency, taskStateCounts));
        }

        /// <summary>
        /// Stops the <see cref="MetricMonitor"/>, and releases the resources used by the MetricMonitor.
        /// </summary>
        public void Dispose()
        {
            lock (this.runLock)
            {
                if (this.runTask != null)
                {
                    this.runCancel.Cancel();
                    this.runTask.WaitForCompletionOrCancellation();
                }
            }

            this.runCancel.Dispose();

            if (this.ownsClient)
            {
                this.batchClient.Dispose();
            }
        }
    }
}
