//Copyright (c) Microsoft Corporation

namespace Microsoft.Azure.Batch.Samples.BatchMetrics
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public sealed class MetricEvent
    {
        private readonly DateTime collectionStarted;
        private readonly DateTime collectionCompleted;
        private readonly TimeSpan totalLatency;
        private readonly TimeSpan listJobsLatency;
        private readonly IReadOnlyDictionary<string, JobStatistics> jobStats;
        private readonly Exception error;

        private MetricEvent(
            DateTime collectionStarted,
            DateTime collectionCompleted,
            TimeSpan totalLatency,
            TimeSpan listJobsLatency,
            IDictionary<string, JobStatistics> jobStats)
        {
            this.collectionStarted = collectionStarted;
            this.collectionCompleted = collectionCompleted;
            this.totalLatency = totalLatency;
            this.listJobsLatency = listJobsLatency;
            this.jobStats = new Dictionary<string, JobStatistics>(jobStats);
        }

        internal MetricEvent(DateTime collectionStarted, DateTime collectionCompleted, Exception error)
        {
            this.collectionStarted = collectionStarted;
            this.collectionCompleted = collectionCompleted;
            this.error = error;
        }

        public bool IsError
        {
            get { return this.error != null; }
        }

        public Exception Error
        {
            get { return this.error; }
        }

        public DateTime CollectionStarted
        {
            get { return this.collectionStarted; }
        }

        public DateTime CollectionCompleted
        {
            get { return this.collectionCompleted; }
        }

        public IEnumerable<string> JobIds
        {
            get
            {
                if (IsError)
                {
                    throw new InvalidOperationException("Cannot get JobIds on collection error event");
                }
                return this.jobStats.Keys;
            }
        }

        public JobStatistics GetMetrics(string jobId)
        {
            if (IsError)
            {
                throw new InvalidOperationException("Cannot get metrics on collection error event");
            }

            try
            {
                return this.jobStats[jobId];
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
            public Dictionary<string, JobStatistics> JobStats = new Dictionary<string,JobStatistics>();

            public MetricEvent Build()
            {
                return new MetricEvent(CollectionStarted, CollectionCompleted, TotalLatency, ListJobsLatency, JobStats);
            }
        }
    }
}
