//Copyright (c) Microsoft Corporation

using System;
using Microsoft.Azure.BatchExplorer.PluginInterfaces.AccountPlugin;
using Microsoft.Azure.BatchExplorer.ViewModels;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Microsoft.Azure.BatchExplorer.Models
{
    /// <summary>
    /// A Batch Account.
    /// </summary>
    public abstract class Account
    {
        /// <summary>
        /// A friendly name for the account.  This is what BatchExplorer will refer to the account as in all dropdowns.
        /// </summary>
        public abstract string Alias { get; set; }

        /// <summary>
        /// The name of the account.
        /// </summary>
        public abstract string AccountName { get; set; }

        /// <summary>
        /// The service url for the Batch account.
        /// </summary>
        public abstract string BatchServiceUrl { get; set; }

        /// <summary>
        /// The key for the account.
        /// </summary>
        public abstract string BatchServiceKey { get; set; }

        /// <summary>
        /// The service URL of the Linked Storage Account
        /// </summary>
        public abstract string LinkedStorageAccountName { get; set; }

        /// <summary>
        /// They access key of the Linked Storage Account
        /// </summary>
        public abstract string LinkedStorageAccountKey { get; set; }

        private CloudBlobClient linkedStorageBlobClient;

        public CloudBlobClient LinkedStorageBlobClient
        {
            get
            {
                if (linkedStorageBlobClient == null)
                {
                    if (!string.IsNullOrWhiteSpace(LinkedStorageAccountName) && !string.IsNullOrWhiteSpace(LinkedStorageAccountKey))
                    {
                        var storageAccount = CloudStorageAccount.Parse($"DefaultEndpointsProtocol=https;AccountName={LinkedStorageAccountName};AccountKey={LinkedStorageAccountKey}");
                        linkedStorageBlobClient = storageAccount.CreateCloudBlobClient();
                    }
                }
                return linkedStorageBlobClient;
            }
        }

        /// <summary>
        /// The unique identifier for this account.
        /// </summary>
        public abstract Guid UniqueIdentifier { get; set; }
        
        /// <summary>
        /// The parent account manager.
        /// </summary>
        public IAccountManager ParentAccountManager { get; set; }

        /// <summary>
        /// Creates an account.
        /// </summary>
        /// <param name="parentAccountManager">The parent account manager.</param>
        protected Account(IAccountManager parentAccountManager)
        {
            this.ParentAccountManager = parentAccountManager;
        }

        protected bool Equals(Account other)
        {
            return string.Equals(this.Alias, other.Alias, StringComparison.CurrentCultureIgnoreCase);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((Account)obj);
        }

        public override int GetHashCode()
        {
            return (this.Alias != null ? this.Alias.GetHashCode() : 0);
        }
    }
}
