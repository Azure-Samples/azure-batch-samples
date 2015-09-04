//Copyright (c) Microsoft Corporation

namespace Microsoft.Azure.Batch.Samples.TextSearch
{
    using System.Text;

    public partial class Settings
    {
        public override string ToString()
        {
            StringBuilder stringBuilder = new StringBuilder();
            
            stringBuilder.AppendFormat("{0} = {1}", "NumberOfMapperTasks", this.NumberOfMapperTasks).AppendLine();
            stringBuilder.AppendFormat("{0} = {1}", "ShouldUploadResources", this.ShouldUploadResources).AppendLine();
            stringBuilder.AppendFormat("{0} = {1}", "ShouldDeleteJob", this.ShouldDeleteJob).AppendLine();
            stringBuilder.AppendFormat("{0} = {1}", "RegularExpression", this.RegularExpression).AppendLine();
            
            return stringBuilder.ToString();
        }
    }
}
