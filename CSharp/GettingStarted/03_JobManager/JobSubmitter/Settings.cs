//Copyright (c) Microsoft Corporation

namespace Microsoft.Azure.Batch.Samples.JobManager
{
    using System.Text;
    using Microsoft.Azure.Batch.Samples.Common;

    public class Settings
    {
        public string PoolId { get; set; }
        public int PoolTargetNodeCount { get; set; }
        public string PoolOsFamily { get; set; }
        public string PoolNodeVirtualMachineSize { get; set; }
        public bool ShouldDeletePool { get; set; }
        public bool ShouldDeleteJob { get; set; }
        public string BlobContainer { get; set; }

        public override string ToString()
        {
            StringBuilder stringBuilder = new StringBuilder();

            SampleHelpers.AddSetting(stringBuilder, "PoolId", this.PoolId);
            SampleHelpers.AddSetting(stringBuilder, "PoolTargetNodeCount", this.PoolTargetNodeCount);
            SampleHelpers.AddSetting(stringBuilder, "PoolOSFamily", this.PoolOsFamily);
            SampleHelpers.AddSetting(stringBuilder, "PoolNodeVirtualMachineSize", this.PoolNodeVirtualMachineSize);
            SampleHelpers.AddSetting(stringBuilder, "ShouldDeletePool", this.ShouldDeletePool);
            SampleHelpers.AddSetting(stringBuilder, "ShouldDeleteJob", this.ShouldDeleteJob);
            SampleHelpers.AddSetting(stringBuilder, "BlobContainer", this.BlobContainer);
            
            return stringBuilder.ToString();
        }
    }
}
