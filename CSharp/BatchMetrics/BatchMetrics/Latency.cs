using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Azure.Batch.Samples.BatchMetrics
{
    /// <summary>
    /// Contains information about how long it took a <see cref="MetricMonitor"/> to gather data
    /// for a <see cref="MetricEvent"/>.
    /// </summary>
    public struct Latency
    {
        private readonly TimeSpan total;
        private readonly TimeSpan listJobs;
        private readonly IReadOnlyDictionary<string, TimeSpan> listTasks;

        internal Latency(TimeSpan total, TimeSpan listJobs, IDictionary<string, TimeSpan> listTasks)
        {
            this.total = total;
            this.listJobs = listJobs;
            this.listTasks = new ReadOnlyDictionary<string, TimeSpan>(listTasks);
        }

        /// <summary>
        /// Gets the total time taken to gather data for the <see cref="MetricEvent"/>.
        /// </summary>
        public TimeSpan Total
        {
            get { return this.total; }
        }

        /// <summary>
        /// Gets the time taken to list the jobs in the Batch account.
        /// </summary>
        public TimeSpan ListJobs
        {
            get { return this.listJobs; }
        }

        /// <summary>
        /// Gets the time taken to list the task status changes for the given job.
        /// </summary>
        /// <param name="jobId">The id of the job.</param>
        /// <returns>The time taken to list the task status changes for the given job.</returns>
        public TimeSpan ListTasks(string jobId)
        {
            return this.listTasks[jobId];
        }
    }
}
