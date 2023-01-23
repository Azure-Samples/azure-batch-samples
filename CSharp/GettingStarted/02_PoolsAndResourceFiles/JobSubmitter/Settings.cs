//Copyright (c) Microsoft Corporation

namespace Microsoft.Azure.Batch.Samples.PoolsAndResourceFiles
{
    using System.Text;
    using Common;

    public class Settings
    {
        public string PoolId { get; set; }
        public int PoolTargetNodeCount { get; set; }
        public string PoolOsFamily { get; set; }
        public string PoolNodeVirtualMachineSize { get; set; }
        public bool ShouldDeletePool { get; set; }
        public bool ShouldDeleteJob { get; set; }
        public string BlobContainer { get; set; }
        public string ImagePublisher { get; set; }
        public string ImageOffer { get; set; }
        public string ImageSku { get; set; }
        public string ImageVersion { get; set; }
        public string NodeAgentSkuId { get; set; }        

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
            SampleHelpers.AddSetting(stringBuilder, "ImagePublisher", this.ImagePublisher);
            SampleHelpers.AddSetting(stringBuilder, "ImageOffer", this.ImageOffer);
            SampleHelpers.AddSetting(stringBuilder, "ImageSku", this.ImageSku);
            SampleHelpers.AddSetting(stringBuilder, "ImageVersion", this.ImageVersion);
            SampleHelpers.AddSetting(stringBuilder, "NodeAgentSkuId", this.NodeAgentSkuId);
            
            return stringBuilder.ToString();
        }


    }
}
