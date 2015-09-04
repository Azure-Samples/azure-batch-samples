//Copyright (c) Microsoft Corporation

namespace Microsoft.Azure.Batch.Samples.Common
{
    using System.Text;

    public partial class AccountSettings
    {
        public override string ToString()
        {
            StringBuilder stringBuilder = new StringBuilder();

            AddSetting(stringBuilder, "BatchAccountName", this.BatchAccountName);
            AddSetting(stringBuilder, "BatchAccountKey", this.BatchAccountKey);
            AddSetting(stringBuilder, "BatchServiceUrl", this.BatchServiceUrl);

            AddSetting(stringBuilder, "StorageAccountName", this.StorageAccountName);
            AddSetting(stringBuilder, "StorageAccountKey", this.StorageAccountKey);
            AddSetting(stringBuilder, "StorageServiceUrl", this.StorageServiceUrl);

            return stringBuilder.ToString();
        }

        private static void AddSetting(StringBuilder stringBuilder, string settingName, object settingValue)
        {
            stringBuilder.AppendFormat("{0} = {1}", settingName, settingValue).AppendLine();
        }
    }
}
