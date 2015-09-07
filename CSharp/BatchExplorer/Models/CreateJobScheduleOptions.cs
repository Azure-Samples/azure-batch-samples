//Copyright (c) Microsoft Corporation

using System;

namespace Microsoft.Azure.BatchExplorer.Models
{
    public class CreateJobScheduleOptions
    {
        public string JobScheduleId { get; set; }

        public int? Priority { get; set; }

        public int? MaxRetryCount { get; set; }
        
        public bool? CreateJobManager { get; set; }

        public TimeSpan? MaxWallClockTime { get; set; }

        // Schedule
        public DateTime? DoNotRunUntil { get; set; }

        public DateTime? DoNotRunAfter { get; set; }

        public TimeSpan? StartWindow { get; set; }

        public TimeSpan? RecurrenceInterval { get; set; }

        public CreateJobManagerOptions JobManagerOptions { get; set; }

        public string PoolId { get; set; }

        public CreateAutoPoolOptions AutoPoolOptions { get; set; }
    }
}
