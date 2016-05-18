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

        public MetricMonitor(BatchClient batchClient)
            : this(batchClient, false, DefaultMonitorInterval)
        {
        }

        public MetricMonitor(string baseUrl, string accountName, string accountKey)
            : this(BatchClient.Open(new BatchSharedKeyCredentials(baseUrl, accountName, accountKey)), true, DefaultMonitorInterval)
        {
        }

        public MetricMonitor(BatchClient batchClient, TimeSpan monitorInterval)
            : this(batchClient, false, monitorInterval)
        {
        }

        public MetricMonitor(string baseUrl, string accountName, string accountKey, TimeSpan monitorInterval)
            : this(BatchClient.Open(new BatchSharedKeyCredentials(baseUrl, accountName, accountKey)), true, monitorInterval)
        {
        }

        public MetricEvent CurrentMetrics
        {
            get; private set;
        }

        public event EventHandler MetricsUpdated;

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

        public void Dispose()
        {
            lock (this.runLock)
            {
                if (this.runTask != null)
                {
                    this.runCancel.Cancel();
                    this.runTask.WaitIgnoringCancellations();
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
