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
        private readonly bool _ownsClient;
        private readonly BatchClient _batchClient;
        private readonly TimeSpan _monitorInterval;
        private readonly Dictionary<string, TaskStateCache> _jobStateCache = new Dictionary<string, TaskStateCache>();

        private Task _runTask;
        private readonly object _runLock = new object();
        private readonly CancellationTokenSource _runCancel = new CancellationTokenSource();

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
            lock (_runLock)
            {
                if (_runTask == null)
                {
                    _runTask = Task.Run(() => Run());
                }
            }
        }

        private async Task Run()
        {
            while (!_runCancel.IsCancellationRequested)
            {
                CurrentMetrics = await CollectMetricsAsync();
                OnMetricsUpdated();
                await TaskHelpers.CancellableDelay(_monitorInterval, _runCancel.Token);
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

            _batchClient = batchClient;
            _ownsClient = ownsClient;
            _monitorInterval = monitorInterval;
        }

        private async Task<MetricEvent> CollectMetricsAsync()
        {
            MetricEvent.Builder metricsBuilder = new MetricEvent.Builder { CollectionStarted = DateTime.UtcNow };

            try
            {
                var totalLatencyStopWatch = Stopwatch.StartNew();

                var listJobsTimer = Stopwatch.StartNew();
                var jobs = await _batchClient.JobOperations.ListJobs(DetailLevels.IdAndState.AllEntities).ToListAsync(_runCancel.Token);
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

            bool firstTime = !_jobStateCache.ContainsKey(job.Id);
            if (firstTime)
            {
                taskStateCache = new TaskStateCache();
                _jobStateCache.Add(job.Id, taskStateCache);
            }
            else
            {
                taskStateCache = _jobStateCache[job.Id];
            }

            // If the monitor API is called for the first time, it has to issue a query to enumerate all the tasks once to get its state.
            // This is a relatively slow query.
            // Subsequent calls to the monitor API will only look for changes to the task state since the last time the query was issued and 
            // a clock skew (which is within 30 seconds approximately for Azure). Thus if the monitoring API periodicity is 1 minute, then the query 
            // should look for changes in the last minute and 30 seconds.

            // TODO: it would be better to record the time at which the last query was issued and use that,
            // rather than subtracting the monitor interval from the current time
            DateTime since = DateTime.UtcNow - (_monitorInterval + MaximumClockSkew);
            var tasksToList = firstTime ? DetailLevels.IdAndState.AllEntities : DetailLevels.IdAndState.OnlyChangedAfter(since);

            var listTasksTimer = Stopwatch.StartNew();
            var tasks = await job.ListTasks(tasksToList).ToListAsync(_runCancel.Token);
            listTasksTimer.Stop();

            var listTasksLatency = listTasksTimer.Elapsed;

            foreach (var task in tasks)
            {
                taskStateCache.UpdateTaskState(task.Id, task.State.Value);
            }

            var taskStateCounts = taskStateCache.GetTaskStateCounts();

            metricsBuilder.JobStats.Add(job.Id, new JobStatistics(listTasksLatency, taskStateCounts));
        }

        public void Dispose()
        {
            lock (_runLock)
            {
                if (_runTask != null)
                {
                    _runCancel.Cancel();
                    _runTask.Wait();
                }
            }

            _runCancel.Dispose();

            if (_ownsClient)
            {
                _batchClient.Dispose();
            }
        }
    }
}
