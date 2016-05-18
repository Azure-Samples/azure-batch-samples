using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Azure.Batch.Samples.BatchMetrics
{
    public struct Latency
    {
        private readonly TimeSpan total;
        private readonly TimeSpan listJobs;
        private readonly IReadOnlyDictionary<string, TimeSpan> listTasks;

        public Latency(TimeSpan total, TimeSpan listJobs, IDictionary<string, TimeSpan> listTasks)
        {
            this.total = total;
            this.listJobs = listJobs;
            this.listTasks = new ReadOnlyDictionary<string, TimeSpan>(listTasks);
        }

        public TimeSpan Total
        {
            get { return this.total; }
        }

        public TimeSpan ListJobs
        {
            get { return this.listJobs; }
        }

        public TimeSpan ListTasks(string jobId)
        {
            return this.listTasks[jobId];
        }
    }
}
