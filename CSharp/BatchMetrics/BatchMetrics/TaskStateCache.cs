//Copyright (c) Microsoft Corporation

namespace Microsoft.Azure.Batch.Samples.BatchMetrics
{
    using global::Azure.Compute.Batch;
    using System.Collections.Generic;

    internal sealed class TaskStateCache
    {
        // The key is the task id
        private readonly Dictionary<string, BatchTaskState> taskStateMap = new Dictionary<string, BatchTaskState>();

        public void UpdateTaskState(string taskId, BatchTaskState taskState)
        {
            this.taskStateMap[taskId] = taskState;
        }

        public TaskStateCounts GetTaskStateCounts()
        {
            TaskStateCounts taskStateCounts = new TaskStateCounts();

            foreach (var kvp in this.taskStateMap)
            {
                taskStateCounts.IncrementCount(kvp.Value);
            }

            return taskStateCounts;
        }
    }
}
