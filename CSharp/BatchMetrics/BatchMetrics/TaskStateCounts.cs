using Microsoft.Azure.Batch.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BatchMetrics
{
    public sealed class TaskStateCounts
    {
        private readonly int[] _counts;

        internal TaskStateCounts()
        {
            var maxStateIndex = Enum.GetValues(typeof(TaskState)).Cast<int>().Max();
            _counts = new int[maxStateIndex + 1];
        }

        private TaskStateCounts(int[] snapshot)
        {
            _counts = snapshot;
        }

        internal void IncrementCount(TaskState taskState)
        {
            _counts[(int)taskState]++;
        }

        public int this[TaskState state]
        {
            get { return _counts[(int)state]; }
        }
    }
}
