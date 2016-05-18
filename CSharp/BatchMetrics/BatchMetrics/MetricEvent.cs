//Copyright (c) Microsoft Corporation

namespace Microsoft.Azure.Batch.Samples.BatchMetrics
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents aggregate information about the jobs in an account at a point in time.
    /// </summary>
    public sealed class MetricEvent
    {
        private readonly DateTime collectionStarted;
        private readonly DateTime collectionCompleted;
        private readonly TimeSpan totalLatency;
        private readonly TimeSpan listJobsLatency;
        private readonly IReadOnlyDictionary<string, JobMetrics> jobMetrics;
        private readonly Exception error;
        private readonly Latency latency;

        private MetricEvent(
            DateTime collectionStarted,
            DateTime collectionCompleted,
            TimeSpan totalLatency,
            TimeSpan listJobsLatency,
            IDictionary<string, JobMetrics> jobStats)
        {
            this.collectionStarted = collectionStarted;
            this.collectionCompleted = collectionCompleted;
            this.totalLatency = totalLatency;
            this.listJobsLatency = listJobsLatency;
            this.jobMetrics = new Dictionary<string, JobMetrics>(jobStats);
            this.latency = new Latency(totalLatency, listJobsLatency, jobStats.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ListTasksLatency));
        }

        internal MetricEvent(DateTime collectionStarted, DateTime collectionCompleted, Exception error)
        {
            this.collectionStarted = collectionStarted;
            this.collectionCompleted = collectionCompleted;
            this.error = error;
        }

        /// <summary>
        /// Gets whether an error occurred retrieving the data for the <see cref="MetricEvent"/>.
        /// </summary>
        public bool IsError
        {
            get { return this.error != null; }
        }

        /// <summary>
        /// Gets the error that occurred retrieving the data for the <see cref="MetricEvent"/>, if any.
        /// </summary>
        public Exception Error
        {
            get { return this.error; }
        }

        /// <summary>
        /// Gets the time at which the <see cref="MetricMonitor"/> started gathering data for this <see cref="MetricEvent"/>.
        /// </summary>
        public DateTime CollectionStarted
        {
            get { return this.collectionStarted; }
        }

        /// <summary>
        /// Gets the time at which the <see cref="MetricMonitor"/> finished gathering data for this <see cref="MetricEvent"/>.
        /// </summary>
        public DateTime CollectionCompleted
        {
            get { return this.collectionCompleted; }
        }

        /// <summary>
        /// Gets information about how long it took the <see cref="MetricMonitor"/> to gather data for this <see cref="MetricEvent"/>.
        /// </summary>
        public Latency Latency
        {
            get { return this.latency; }
        }

        /// <summary>
        /// Gets the ids of the jobs for which this <see cref="MetricEvent"/> contains metrics.
        /// </summary>
        public IEnumerable<string> JobIds
        {
            get
            {
                if (IsError)
                {
                    throw new InvalidOperationException("Cannot get JobIds on collection error event");
                }
                return this.jobMetrics.Keys;
            }
        }

        /// <summary>
        /// Gets the metrics for the given job.
        /// </summary>
        /// <param name="jobId">The job whose metrics to get.</param>
        /// <returns>The metrics for the job.</returns>
        public JobMetrics GetMetrics(string jobId)
        {
            if (IsError)
            {
                throw new InvalidOperationException("Cannot get metrics on collection error event");
            }

            try
            {
                return this.jobMetrics[jobId];
            }
            catch (KeyNotFoundException)
            {
                throw new ArgumentException("No metrics collected for job " + jobId, "jobId");
            }
        }

        internal sealed class Builder
        {
            public DateTime CollectionStarted;
            public DateTime CollectionCompleted;
            public TimeSpan TotalLatency;
            public TimeSpan ListJobsLatency;
            public Dictionary<string, JobMetrics> JobStats = new Dictionary<string,JobMetrics>();

            public MetricEvent Build()
            {
                return new MetricEvent(CollectionStarted, CollectionCompleted, TotalLatency, ListJobsLatency, JobStats);
            }
        }
    }
}
