//Copyright (c) Microsoft Corporation

namespace Microsoft.Azure.Batch.Samples.BatchMetrics
{
    using Microsoft.Azure.Batch.Common;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    /// <summary>
    /// Contains aggregate information about a job at a point in time.
    /// </summary>
    public sealed class JobMetrics
    {
        private readonly TimeSpan listTasksLatency;
        private readonly TaskStateCounts taskStateCounts;

        internal JobMetrics(TimeSpan listTasksLatency, TaskStateCounts taskStateCounts)
        {
            this.listTasksLatency = listTasksLatency;
            this.taskStateCounts = taskStateCounts;
        }

        /// <summary>
        /// Gets the number of tasks in each <see cref="TaskState"/> in the job.
        /// </summary>
        public TaskStateCounts TaskStateCounts
        {
            get { return this.taskStateCounts; }
        }

        internal TimeSpan ListTasksLatency
        {
            get { return this.listTasksLatency; }
        }
    }
}
