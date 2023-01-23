//Copyright (c) Microsoft Corporation

namespace Microsoft.Azure.Batch.Samples.TextSearch
{
    using System.Text;

    public class Settings
    {
        public bool ShouldUploadResources { get; set; }
        public bool ShouldDeleteJob { get; set; }
        public string RegularExpression { get; set; }
        public string InputBlobContainer { get; set; }
        public string OutputBlobContainer { get; set; }
        public int NumberOfMapperTasks { get; set; }
        public bool ShouldDeleteContainers { get; set; }
        public string PoolNodeVirtualMachineSize { get; set; }
        public string ImagePublisher { get; set; }
        public string ImageOffer { get; set; }
        public string ImageSku { get; set; }
        public string ImageVersion { get; set; }
        public string NodeAgentSkuId { get; set; }   

        public override string ToString()
        {
            StringBuilder stringBuilder = new StringBuilder();
            
            stringBuilder.AppendFormat("{0} = {1}", "NumberOfMapperTasks", this.NumberOfMapperTasks).AppendLine();
            stringBuilder.AppendFormat("{0} = {1}", "ShouldUploadResources", this.ShouldUploadResources).AppendLine();
            stringBuilder.AppendFormat("{0} = {1}", "ShouldDeleteJob", this.ShouldDeleteJob).AppendLine();
            stringBuilder.AppendFormat("{0} = {1}", "RegularExpression", this.RegularExpression).AppendLine();
            stringBuilder.AppendFormat("{0} = {1}", "InputBlobContainer", this.InputBlobContainer).AppendLine();
            stringBuilder.AppendFormat("{0} = {1}", "OutputBlobContainer", this.OutputBlobContainer).AppendLine();
            stringBuilder.AppendFormat("{0} = {1}", "ShouldDeleteContainers", this.ShouldDeleteContainers).AppendLine();
            stringBuilder.AppendFormat("{0} = {1}", "PoolNodeVirtualMachineSize", this.PoolNodeVirtualMachineSize).AppendLine();
            stringBuilder.AppendFormat("{0} = {1}", "ImagePublisher", this.ImagePublisher).AppendLine();
            stringBuilder.AppendFormat("{0} = {1}", "ImageOffer", this.ImageOffer).AppendLine();
            stringBuilder.AppendFormat("{0} = {1}", "ImageSku", this.ImageSku).AppendLine();
            stringBuilder.AppendFormat("{0} = {1}", "NodeAgentSkuId", this.NodeAgentSkuId).AppendLine();

            return stringBuilder.ToString();
        }
    }
}
