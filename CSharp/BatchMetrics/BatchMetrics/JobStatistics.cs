namespace Microsoft.Azure.Batch.Samples.BatchMetrics
{
    using Microsoft.Azure.Batch.Common;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public sealed class JobStatistics
    {
        private readonly TimeSpan _listTasksLatency;
        private readonly TaskStateCounts _taskStateCounts;

        internal JobStatistics(TimeSpan listTasksLatency, TaskStateCounts taskStateCounts)
        {
            _listTasksLatency = listTasksLatency;
            _taskStateCounts = taskStateCounts;
        }

        public TaskStateCounts TaskStateCounts
        {
            get { return _taskStateCounts; }
        }
    }
}
