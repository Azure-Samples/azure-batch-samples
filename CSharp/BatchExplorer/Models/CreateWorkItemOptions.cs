using System;

namespace Microsoft.Azure.BatchExplorer.Models
{
    public class CreateWorkItemOptions
    {
        public string WorkItemName { get; set; }

        public int? Priority { get; set; }

        public int? MaxRetryCount { get; set; }

        public bool? CreateSchedule { get; set; }

        public bool? CreateJobManager { get; set; }

        public TimeSpan? MaxWallClockTime { get; set; }

        // Schedule
        public DateTime? DoNotRunUntil { get; set; }

        public DateTime? DoNotRunAfter { get; set; }

        public TimeSpan? StartWindow { get; set; }

        public TimeSpan? RecurrenceInterval { get; set; }

        // Job Manager
        public string JobManagerName { get; set; }

        public string CommandLine { get; set; }

        public int? MaxTaskRetryCount { get; set; }

        public TimeSpan? MaxTaskWallClockTime { get; set; }

        public TimeSpan? RetentionTime { get; set; }

        public bool? KillOnCompletion { get; set; }

        // Pool Settings
        public bool? UseAutoPool { get; set; }

        public string PoolName { get; set; }

        public string AutoPoolPrefix { get; set; }

        public string LifeTimeOption { get; set; }

        public string SelectedLifetimeOption { get; set; }

        public bool? KeepAlive { get; set; }
    }
}
