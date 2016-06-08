//Copyright (c) Microsoft Corporation

namespace Microsoft.Azure.Batch.Samples.BatchMetrics
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    /// <summary>
    /// Contains information about how long it took a <see cref="MetricMonitor"/> to gather data
    /// for a <see cref="MetricEvent"/>.
    /// </summary>
    public struct Latency
    {
        private readonly TimeSpan totalTime;
        private readonly TimeSpan listJobsTime;
        private readonly IReadOnlyDictionary<string, TimeSpan> listTasksTimes;

        internal Latency(TimeSpan totalTime, TimeSpan listJobsTime, IDictionary<string, TimeSpan> listTasksTimes)
        {
            this.totalTime = totalTime;
            this.listJobsTime = listJobsTime;
            this.listTasksTimes = new ReadOnlyDictionary<string, TimeSpan>(listTasksTimes);
        }

        /// <summary>
        /// Gets the total time taken to gather data for the <see cref="MetricEvent"/>.
        /// </summary>
        public TimeSpan Total
        {
            get { return this.totalTime; }
        }

        /// <summary>
        /// Gets the time taken to list the jobs in the Batch account.
        /// </summary>
        public TimeSpan ListJobs
        {
            get { return this.listJobsTime; }
        }

        /// <summary>
        /// Gets the time taken to list the task status changes for the given job.
        /// </summary>
        /// <param name="jobId">The id of the job.</param>
        /// <returns>The time taken to list the task status changes for the given job.</returns>
        public TimeSpan ListTasks(string jobId)
        {
            return this.listTasksTimes[jobId];
        }
    }
}
