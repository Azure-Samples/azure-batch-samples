//Copyright (c) Microsoft Corporation

using System;
using System.Xml.Serialization;
using Microsoft.Azure.BatchExplorer.Models;
using Microsoft.Azure.BatchExplorer.PluginInterfaces.AccountPlugin;
using Microsoft.Azure.BatchExplorer.Plugins.AccountPlugin;

namespace Microsoft.Azure.BatchExplorer.Plugins.LegacyAccountSupport
{
    //TODO: Remove this in a future release
    [XmlType("Account")]
    public class LegacyAccount
    {
        public string Alias { get; set; }
        public string AccountName { get; set; }
        public string BatchServiceUrl { get; set; }
        public byte[] SecureKey { get; set; }
        public Guid UniqueIdentifier { get; set; }

        public Account ToAccount(IAccountManager manager)
        {
            return new DefaultAccount(manager)
                       {
                           AccountName = this.AccountName,
                           Alias = this.Alias,
                           BatchServiceUrl = this.BatchServiceUrl,
                           BatchSecureKey = this.SecureKey,
                           UniqueIdentifier = this.UniqueIdentifier,
            };
        }
    }
}
