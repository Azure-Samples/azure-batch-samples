namespace Microsoft.Azure.Batch.Samples.JobManager
{
    using System.Text;

    public class JobManagerSettings
    {
        public string BatchAccountName { get; private set; }

        public string BatchAccountKey { get; private set; }

        public string BatchAccountUrl { get; private set; }

        public string StorageAccountName { get; private set; }

        public string StorageAccountKey { get; private set; }

        public string StorageBlobEndpoint { get; private set; }

        public JobManagerSettings(
            string batchAccountName,
            string batchAccountKey,
            string batchAccountUrl,
            string storageAccountName,
            string storageAccountKey,
            string storageBlobEndpoint)
        {
            this.BatchAccountName = batchAccountName;
            this.BatchAccountKey = batchAccountKey;
            this.BatchAccountUrl = batchAccountUrl;

            this.StorageAccountName = storageAccountName;
            this.StorageAccountKey = storageAccountKey;
            this.StorageBlobEndpoint = storageBlobEndpoint;
        }

        public override string ToString()
        {
            StringBuilder stringBuilder = new StringBuilder();

            stringBuilder.AppendFormat("{0} = {1}", "BatchAccountName", this.BatchAccountName).AppendLine();
            stringBuilder.AppendFormat("{0} = {1}", "BatchAccountKey", this.BatchAccountKey).AppendLine();
            stringBuilder.AppendFormat("{0} = {1}", "BatchAccountUrl", this.BatchAccountUrl).AppendLine();

            stringBuilder.AppendFormat("{0} = {1}", "StorageAccountName", this.StorageAccountName).AppendLine();
            stringBuilder.AppendFormat("{0} = {1}", "StorageAccountKey", this.StorageAccountKey).AppendLine();
            stringBuilder.AppendFormat("{0} = {1}", "StorageBlobEndpoint", this.StorageBlobEndpoint).AppendLine();

            return stringBuilder.ToString();
        }
    }
}
