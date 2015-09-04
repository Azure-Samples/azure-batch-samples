//Copyright (c) Microsoft Corporation

using System;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Serialization;
using Microsoft.Azure.BatchExplorer.Models;
using Microsoft.Azure.BatchExplorer.PluginInterfaces.AccountPlugin;

namespace Microsoft.Azure.BatchExplorer.Plugins.AccountPlugin
{
    /// <summary>
    /// Account used by the default BatchExplorer plugin.
    /// </summary>
    public sealed class DefaultAccount : Account
    {
        /// <summary>
        /// See <see cref="Account.Alias"/>.
        /// </summary>
        public override string Alias { get; set; }
        
        /// <summary>
        /// See <see cref="Account.AccountName"/>.
        /// </summary>
        public override string AccountName { get; set; }

        /// <summary>
        /// See <see cref="Account.BatchServiceUrl"/>.
        /// </summary>
        public override string BatchServiceUrl { get; set; }

        /// <summary>
        /// See <see cref="Account.Key"/>.
        /// </summary>
        [XmlIgnore]
        public override string Key
        {
            get
            {
                string result;
                if (this.SecureKey == null)
                {
                    result = null;
                }
                else
                {
                    result = Encoding.ASCII.GetString(ProtectedData.Unprotect(this.SecureKey, null, DataProtectionScope.CurrentUser));
                }

                return result;
            }
            set
            {
                if (value != null)
                {
                    this.SecureKey = ProtectedData.Protect(Encoding.ASCII.GetBytes(value), null, DataProtectionScope.CurrentUser);
                }
                else
                {
                    this.SecureKey = null;
                }
            }
        }

        /// <summary>
        /// The secure (encrypted) key which will be written to disk.
        /// </summary>
        public byte[] SecureKey { get; set; }

        /// <summary>
        /// See <see cref="Account.UniqueIdentifier"/>.
        /// </summary>
        public override Guid UniqueIdentifier { get; set; }

        public DefaultAccount(IAccountManager parentAccountManager) : base(parentAccountManager)
        {
            this.UniqueIdentifier = Guid.NewGuid();
        }
    }
}
