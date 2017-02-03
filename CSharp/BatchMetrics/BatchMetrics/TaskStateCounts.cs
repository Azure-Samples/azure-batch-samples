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
    /// Contains information about how many tasks in a job are in each
    /// <see cref="TaskState"/>.
    /// </summary>
    public sealed class TaskStateCounts
    {
        private readonly int[] counts;

        internal TaskStateCounts()
        {
            // The counts array is indexed by TaskState. The constructor examines the TaskState
            // type to determine how to size the array.
            var maxStateIndex = EnumHelpers.GetMaxValue(typeof(TaskState));
            this.counts = new int[maxStateIndex + 1];
        }

        internal void IncrementCount(TaskState taskState)
        {
            this.counts[(int)taskState]++;
        }

        /// <summary>
        /// Gets the number of tasks in the specified state.
        /// </summary>
        /// <param name="state">The <see cref="TaskState"/> for which to get the number of tasks in that state.</param>
        /// <returns>The number of tasks in the specified state.</returns>
        public int this[TaskState state]
        {
            get { return this.counts[(int)state]; }
        }
    }
}
