//Copyright (c) Microsoft Corporation

namespace Microsoft.Azure.Batch.Samples.PoolsAndResourceFiles
{
    using System.Text;
    using Common;

    public partial class Settings
    {
        public override string ToString()
        {
            StringBuilder stringBuilder = new StringBuilder();
            
            SampleHelpers.AddSetting(stringBuilder, "PoolId", this.PoolId);
            SampleHelpers.AddSetting(stringBuilder, "PoolTargetNodeCount", this.PoolTargetNodeCount);
            SampleHelpers.AddSetting(stringBuilder, "PoolOSFamily", this.PoolOSFamily);
            SampleHelpers.AddSetting(stringBuilder, "PoolNodeVirtualMachineSize", this.PoolNodeVirtualMachineSize);
            SampleHelpers.AddSetting(stringBuilder, "ShouldDeletePool", this.ShouldDeletePool);
            SampleHelpers.AddSetting(stringBuilder, "ShouldDeleteJob", this.ShouldDeleteJob);
            SampleHelpers.AddSetting(stringBuilder, "BlobContainer", this.BlobContainer);
            
            return stringBuilder.ToString();
        }


    }
}
