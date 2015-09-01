using System.Text;

namespace Microsoft.Azure.Batch.Samples.PoolsAndResourceFiles
{
    public partial class Settings
    {
        public override string ToString()
        {
            StringBuilder stringBuilder = new StringBuilder();

            AddSetting(stringBuilder, "BatchAccountName", this.BatchAccountName);
            AddSetting(stringBuilder, "BatchAccountKey", this.BatchAccountKey);
            AddSetting(stringBuilder, "BatchServiceUrl", this.BatchServiceUrl);

            AddSetting(stringBuilder, "StorageAccountName", this.StorageAccountName);
            AddSetting(stringBuilder, "StorageAccountKey", this.StorageAccountKey);
            AddSetting(stringBuilder, "StorageBlobEndpoint", this.StorageBlobEndpoint);

            AddSetting(stringBuilder, "PoolId", this.PoolId);
            AddSetting(stringBuilder, "PoolTargetNodeCount", this.PoolTargetNodeCount);
            AddSetting(stringBuilder, "PoolOSFamily", this.PoolOSFamily);
            AddSetting(stringBuilder, "PoolNodeVirtualMachineSize", this.PoolNodeVirtualMachineSize);
            AddSetting(stringBuilder, "ShouldDeletePool", this.ShouldDeletePool);
            AddSetting(stringBuilder, "ShouldDeleteJob", this.ShouldDeleteJob);
            
            return stringBuilder.ToString();
        }

        private static void AddSetting(StringBuilder stringBuilder, string settingName, object settingValue)
        {
            stringBuilder.AppendFormat("{0} = {1}", settingName, settingValue).AppendLine();
        }
    }
}
