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
        private readonly DateTime _collectionStarted;
        private readonly DateTime _collectionCompleted;
        private readonly TimeSpan _totalLatency;
        private readonly TimeSpan _listJobsLatency;
        private readonly IReadOnlyDictionary<string, JobStatistics> _jobStats;
        private readonly Exception _error;

        private MetricEvent(
            DateTime collectionStarted,
            DateTime collectionCompleted,
            TimeSpan totalLatency,
            TimeSpan listJobsLatency,
            IDictionary<string, JobStatistics> jobStats)
        {
            _collectionStarted = collectionStarted;
            _collectionCompleted = collectionCompleted;
            _totalLatency = totalLatency;
            _listJobsLatency = listJobsLatency;
            _jobStats = new Dictionary<string, JobStatistics>(jobStats);
        }

        internal MetricEvent(DateTime collectionStarted, DateTime collectionCompleted, Exception error)
        {
            _collectionStarted = collectionStarted;
            _collectionCompleted = collectionCompleted;
            _error = error;
        }

        public bool IsError
        {
            get { return _error != null; }
        }

        public Exception Error
        {
            get { return _error; }
        }

        public DateTime CollectionStarted
        {
            get { return _collectionStarted; }
        }

        public DateTime CollectionCompleted
        {
            get { return _collectionCompleted; }
        }

        public IEnumerable<string> JobIds
        {
            get
            {
                if (IsError)
                {
                    throw new InvalidOperationException("Cannot get JobIds on collection error event");
                }
                return _jobStats.Keys;
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
                return _jobStats[jobId];
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
