//Copyright (c) Microsoft Corporation

namespace Microsoft.Azure.Batch.Samples.BatchMetrics
{
    using global::Azure;
    using global::Azure.Compute.Batch;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Monitors an Azure Batch account and provides aggregate status and metric information
    /// about the jobs in that account.
    /// </summary>
    public sealed class MetricMonitor : IDisposable
    {
        private readonly BatchClient batchClient;
        private readonly TimeSpan monitorInterval;
        private readonly Dictionary<string, TaskStateCache> jobStateCache = new Dictionary<string, TaskStateCache>();

        private Task runTask;
        private readonly object runLock = new object();
        private readonly CancellationTokenSource runCancel = new CancellationTokenSource();

        private static readonly TimeSpan DefaultMonitorInterval = TimeSpan.FromSeconds(60);
        private static readonly TimeSpan MaximumClockSkew = TimeSpan.FromSeconds(30);

        private static readonly IEnumerable<string> IdAndStateSelect = new[] { "id", "state" };

        /// <summary>
        /// Initializes a new instance of the <see cref="MetricMonitor"/> class.
        /// </summary>
        public MetricMonitor(BatchClient batchClient)
            : this(batchClient, DefaultMonitorInterval)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MetricMonitor"/> class.
        /// </summary>
        public MetricMonitor(BatchClient batchClient, TimeSpan monitorInterval)
        {
            if (batchClient == null)
            {
                throw new ArgumentNullException(nameof(batchClient));
            }

            if (monitorInterval <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(monitorInterval), "monitorInterval must be positive");
            }

            this.batchClient = batchClient;
            this.monitorInterval = monitorInterval;
        }

        /// <summary>
        /// Gets the job metrics at the last time the <see cref="MetricMonitor"/> updated them.
        /// </summary>
        public MetricEvent CurrentMetrics
        {
            get; private set;
        }

        /// <summary>
        /// Raised when the <see cref="MetricMonitor"/> has updated the <see cref="CurrentMetrics"/>.
        /// </summary>
        public event EventHandler MetricsUpdated;

        /// <summary>
        /// Starts monitoring the Azure Batch account and gathering metrics.
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
            MetricsUpdated?.Invoke(this, EventArgs.Empty);
        }

        private async Task<MetricEvent> CollectMetricsAsync()
        {
            var metricsBuilder = new MetricEvent.Builder { CollectionStarted = DateTime.UtcNow };

            try
            {
                var totalLatencyStopWatch = Stopwatch.StartNew();

                var listJobsTimer = Stopwatch.StartNew();
                var jobs = new List<BatchJob>();
                await foreach (BatchJob job in this.batchClient.GetJobsAsync(select: IdAndStateSelect, cancellationToken: this.runCancel.Token))
                {
                    jobs.Add(job);
                }
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

        private async Task CollectTaskMetricsAsync(MetricEvent.Builder metricsBuilder, BatchJob job)
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

            // First time: enumerate all tasks. Subsequent times: only those whose state has
            // transitioned within the last interval (plus a clock skew buffer).
            DateTime since = DateTime.UtcNow - (this.monitorInterval + MaximumClockSkew);
            string filter = firstTime
                ? null
                : string.Format("stateTransitionTime gt DateTime'{0:o}'", since);

            var listTasksTimer = Stopwatch.StartNew();
            var tasks = new List<BatchTask>();
            await foreach (BatchTask task in this.batchClient.GetTasksAsync(job.Id, select: IdAndStateSelect, filter: filter, cancellationToken: this.runCancel.Token))
            {
                tasks.Add(task);
            }
            listTasksTimer.Stop();

            var listTasksLatency = listTasksTimer.Elapsed;

            foreach (var task in tasks)
            {
                taskStateCache.UpdateTaskState(task.Id, task.State);
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
        }
    }
}
