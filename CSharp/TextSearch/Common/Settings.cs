namespace Microsoft.Azure.Batch.Samples.TextSearch
{
    using System.Text;

    public partial class Settings
    {
        public override string ToString()
        {
            StringBuilder stringBuilder = new StringBuilder();

            stringBuilder.AppendFormat("{0} = {1}", "BatchAccountName", this.BatchAccountName).AppendLine();
            stringBuilder.AppendFormat("{0} = {1}", "BatchAccountKey", this.BatchAccountKey).AppendLine();
            stringBuilder.AppendFormat("{0} = {1}", "BatchServiceUrl", this.BatchServiceUrl).AppendLine();
            
            stringBuilder.AppendFormat("{0} = {1}", "StorageAccountName", this.StorageAccountName).AppendLine();
            stringBuilder.AppendFormat("{0} = {1}", "StorageAccountKey", this.StorageAccountKey).AppendLine();
            stringBuilder.AppendFormat("{0} = {1}", "StorageServiceUrl", this.StorageServiceUrl).AppendLine();
            
            stringBuilder.AppendFormat("{0} = {1}", "NumberOfMapperTasks", this.NumberOfMapperTasks).AppendLine();
            stringBuilder.AppendFormat("{0} = {1}", "ShouldUploadResources", this.ShouldUploadResources).AppendLine();
            stringBuilder.AppendFormat("{0} = {1}", "ShouldDeleteJob", this.ShouldDeleteJob).AppendLine();
            stringBuilder.AppendFormat("{0} = {1}", "RegularExpression", this.RegularExpression).AppendLine();
            
            return stringBuilder.ToString();
        }
    }
}
