//Copyright (c) Microsoft Corporation

namespace Microsoft.Azure.Batch.Samples.BatchMetrics
{
    using global::Azure.Compute.Batch;
    using System.Collections.Generic;

    /// <summary>
    /// Contains information about how many tasks in a job are in each
    /// <see cref="BatchTaskState"/>.
    /// </summary>
    public sealed class TaskStateCounts
    {
        private readonly Dictionary<BatchTaskState, int> counts = new Dictionary<BatchTaskState, int>();

        internal TaskStateCounts()
        {
        }

        internal void IncrementCount(BatchTaskState taskState)
        {
            this.counts.TryGetValue(taskState, out int current);
            this.counts[taskState] = current + 1;
        }

        /// <summary>
        /// Gets the number of tasks in the specified state.
        /// </summary>
        public int this[BatchTaskState state]
        {
            get
            {
                this.counts.TryGetValue(state, out int count);
                return count;
            }
        }
    }
}
