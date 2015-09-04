//Copyright (c) Microsoft Corporation

namespace Microsoft.Azure.BatchExplorer.Models
{
    public class AddTaskOptions
    {
        public string JobId { get; set; }

        public string CommandLine { get; set; }

        public string TaskId { get; set; }
    }
}
