//Copyright (c) Microsoft Corporation

using System;
using System.Xml.Serialization;
using Microsoft.Azure.BatchExplorer.PluginInterfaces.AccountPlugin;

namespace Microsoft.Azure.BatchExplorer.Plugins.AccountPlugin
{
    public sealed class SerializableAccount
    {
        public string Alias { get; set; }

        public string AccountName { get; set; }

        public string BatchServiceUrl { get; set; }

        [XmlIgnore]
        public string BatchServiceKey { get; set; }

        public byte[] BatchSecureKey { get; set; }

        public string LinkedStorageAccountName { get; set; }

        [XmlIgnore]
        public string LinkedStorageAccountKey { get; set; }

        public byte[] LinkedStorageSecureKey { get; set; }

        public Guid UniqueIdentifier { get; set; }

        public SerializableAccount()
        {
        }

        public SerializableAccount(DefaultAccount account)
        {
            this.Alias = account.Alias;
            this.AccountName = account.AccountName;
            this.BatchServiceUrl = account.BatchServiceUrl;
            this.BatchServiceKey = account.BatchServiceKey;
            this.BatchSecureKey = account.BatchSecureKey;
            this.UniqueIdentifier = account.UniqueIdentifier;
            this.LinkedStorageAccountName = account.LinkedStorageAccountName;
            this.LinkedStorageAccountKey = account.LinkedStorageAccountKey;
            this.LinkedStorageSecureKey = account.LinkedStorageSecureKey;
        }

        public DefaultAccount CreateAccountFromSerialization(IAccountManager parentAccountManager)
        {
            DefaultAccount account = new DefaultAccount(parentAccountManager)
            {
                AccountName = this.AccountName,
                Alias = this.Alias,
                BatchServiceUrl = this.BatchServiceUrl,
                BatchServiceKey = this.BatchServiceKey,
                BatchSecureKey = this.BatchSecureKey,
                UniqueIdentifier = this.UniqueIdentifier,
                LinkedStorageAccountName = this.LinkedStorageAccountName,
                LinkedStorageAccountKey = this.LinkedStorageAccountKey,
                LinkedStorageSecureKey = this.LinkedStorageSecureKey,
            };

            return account;
        }
    }
}
