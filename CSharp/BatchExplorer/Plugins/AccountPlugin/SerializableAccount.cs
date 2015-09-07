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
        public string Key { get; set; }

        public byte[] SecureKey { get; set; }

        public Guid UniqueIdentifier { get; set; }

        public SerializableAccount()
        {
        }

        public SerializableAccount(DefaultAccount account)
        {
            this.Alias = account.Alias;
            this.AccountName = account.AccountName;
            this.BatchServiceUrl = account.BatchServiceUrl;
            this.Key = account.Key;
            this.SecureKey = account.SecureKey;
            this.UniqueIdentifier = account.UniqueIdentifier;
        }

        public DefaultAccount CreateAccountFromSerialization(IAccountManager parentAccountManager)
        {
            DefaultAccount account = new DefaultAccount(parentAccountManager)
            {
                AccountName = this.AccountName,
                Alias = this.Alias,
                BatchServiceUrl = this.BatchServiceUrl,
                Key = this.Key,
                SecureKey = this.SecureKey,
                UniqueIdentifier = this.UniqueIdentifier
            };

            return account;
        }
    }
}
