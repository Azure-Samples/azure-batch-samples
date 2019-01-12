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

            return stringBuilder.ToString();
        }
    }
}
