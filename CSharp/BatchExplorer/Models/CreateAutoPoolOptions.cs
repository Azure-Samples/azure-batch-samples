//Copyright (c) Microsoft Corporation

namespace Microsoft.Azure.BatchExplorer.Models
{
    public class CreateAutoPoolOptions
    {
        public bool? UseAutoPool { get; set; }
        
        public string AutoPoolPrefix { get; set; }

        public string LifeTimeOption { get; set; }

        public string SelectedLifetimeOption { get; set; }

        public bool? KeepAlive { get; set; }

        public string VirutalMachineSize { get; set; }

        public string OSFamily { get; set; }

        public int TargetDedicated { get; set; }
    }
}
