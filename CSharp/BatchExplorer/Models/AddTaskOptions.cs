//Copyright (c) Microsoft Corporation

using System.Collections.Generic;

namespace Microsoft.Azure.BatchExplorer.Models
{
    public class AddTaskOptions
    {
        public string JobId { get; set; }

        public string CommandLine { get; set; }

        public string TaskId { get; set; }

        public List<ResourceFileInfo> ResourceFiles { get; set; }
    }
}
