namespace Microsoft.Azure.Batch.Samples.HelloWorld
{
    using System.Text;
    using Common;

    public partial class Settings
    {
        public string PoolId { get; set; }
        public int PoolTargetNodeCount { get; set; }
        public string PoolOSFamily { get; set; }
        public string PoolNodeVirtualMachineSize { get; set; }
        public bool ShouldDeleteJob { get; set; }
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
            SampleHelpers.AddSetting(stringBuilder, "PoolOSFamily", this.PoolOSFamily);
            SampleHelpers.AddSetting(stringBuilder, "PoolNodeVirtualMachineSize", this.PoolNodeVirtualMachineSize);
            SampleHelpers.AddSetting(stringBuilder, "ShouldDeleteJob", this.ShouldDeleteJob);
            SampleHelpers.AddSetting(stringBuilder, "ImagePublisher", this.ImagePublisher);
            SampleHelpers.AddSetting(stringBuilder, "ImageOffer", this.ImageOffer);
            SampleHelpers.AddSetting(stringBuilder, "ImageSku", this.ImageSku);
            SampleHelpers.AddSetting(stringBuilder, "ImageVersion", this.ImageVersion);
            SampleHelpers.AddSetting(stringBuilder, "NodeAgentSkuId", this.NodeAgentSkuId);


            return stringBuilder.ToString();
        }
    }
}
