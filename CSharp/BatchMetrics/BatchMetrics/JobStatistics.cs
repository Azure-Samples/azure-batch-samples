//Copyright (c) Microsoft Corporation

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
        private readonly TimeSpan listTasksLatency;
        private readonly TaskStateCounts taskStateCounts;

        internal JobStatistics(TimeSpan listTasksLatency, TaskStateCounts taskStateCounts)
        {
            this.listTasksLatency = listTasksLatency;
            this.taskStateCounts = taskStateCounts;
        }

        public TaskStateCounts TaskStateCounts
        {
            get { return this.taskStateCounts; }
        }
    }
}
