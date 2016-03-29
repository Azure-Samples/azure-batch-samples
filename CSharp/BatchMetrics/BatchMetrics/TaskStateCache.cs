namespace Microsoft.Azure.Batch.Samples.BatchMetrics
{
    using Microsoft.Azure.Batch.Common;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    internal sealed class TaskStateCache
    {
        // The key is the task id
        private readonly Dictionary<string, TaskState> _impl = new Dictionary<string, TaskState>();

        public void UpdateTaskState(string taskId, TaskState taskState)
        {
            _impl[taskId] = taskState;
        }

        public TaskStateCounts GetTaskStateCounts()
        {
            TaskStateCounts taskStateCounts = new TaskStateCounts();

            foreach (var kvp in _impl)
            {
                taskStateCounts.IncrementCount(kvp.Value);
            }

            return taskStateCounts;
        }
    }
}
