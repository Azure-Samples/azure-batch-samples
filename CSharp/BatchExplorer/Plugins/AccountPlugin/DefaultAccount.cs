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
        /// See <see cref="Account.BatchServiceKey"/>.
        /// </summary>
        [XmlIgnore]
        public override string BatchServiceKey
        {
            get
            {
                return GetSecureKey(this.BatchSecureKey);
            }
            set
            {
                this.BatchSecureKey = DecryptSecureKey(value);
            }
        }

        /// <summary>
        /// The secure (encrypted) batch key which will be written to disk.
        /// </summary>
        public byte[] BatchSecureKey { get; set; }

        /// <summary>
        /// See <see cref="Account.UniqueIdentifier"/>.
        /// </summary>
        public override Guid UniqueIdentifier { get; set; }

        public DefaultAccount(IAccountManager parentAccountManager) : base(parentAccountManager)
        {
            this.UniqueIdentifier = Guid.NewGuid();
        }

        /// <summary>
        /// See <see cref="Account.LinkedStorageAccountName"/>.
        /// </summary>
        public override string LinkedStorageAccountName { get; set; }

        /// <summary>
        /// See <see cref="Account.LinkedStorageAccountKey"/>.
        /// </summary>
        [XmlIgnore]
        public override string LinkedStorageAccountKey
        {
            get
            {
                return GetSecureKey(this.LinkedStorageSecureKey);
            }
            set
            {
                this.LinkedStorageSecureKey = DecryptSecureKey(value);
            }
        }

        /// <summary>
        /// The secure (encrypted) linked storage key which will be written to disk.
        /// </summary>
        public byte[] LinkedStorageSecureKey { get; set; }

        /// <summary>
        /// Decrypts the secure key
        /// </summary>
        /// <param name="secureKey"></param>
        /// <returns>The plain key</returns>
        private static string GetSecureKey(byte[] secureKey)
        {
            return secureKey == null ? null : Encoding.ASCII.GetString(ProtectedData.Unprotect(secureKey, null, DataProtectionScope.CurrentUser));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="plainKey"></param>
        /// <returns>The secureKey (encrypted) key</returns>
        private static byte[] DecryptSecureKey(string plainKey)
        {
            return plainKey != null ? ProtectedData.Protect(Encoding.ASCII.GetBytes(plainKey), null, DataProtectionScope.CurrentUser) : null;
        }
    }
}
