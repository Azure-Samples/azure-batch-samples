// Copyright (c) Microsoft Corporation

namespace Microsoft.Azure.Batch.Samples.Common
{
    using System;
    using global::Azure;
    using global::Azure.Compute.Batch;
    using global::Azure.Core;
    using global::Azure.ResourceManager;
    using global::Azure.ResourceManager.Batch;
    using global::Azure.Storage;
    using global::Azure.Storage.Blobs;

    /// <summary>
    /// Helpers for constructing Azure.Compute.Batch and Azure.Storage.Blobs clients
    /// from <see cref="AccountSettings"/>.
    /// </summary>
    public static class ClientFactory
    {
        /// <summary>
        /// Creates a <see cref="BatchClient"/> using shared-key credentials.
        /// </summary>
        public static BatchClient CreateBatchClient(AccountSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            var credential = new AzureNamedKeyCredential(settings.BatchAccountName, settings.BatchAccountKey);
            return new BatchClient(new Uri(settings.BatchServiceUrl), credential);
        }

        /// <summary>
        /// Creates a <see cref="BlobServiceClient"/> using a storage shared-key credential.
        /// </summary>
        public static BlobServiceClient CreateBlobServiceClient(AccountSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            var credential = new StorageSharedKeyCredential(settings.StorageAccountName, settings.StorageAccountKey);
            // settings.StorageServiceUrl historically held a host suffix like "core.windows.net";
            // use it to build the blob endpoint when present, otherwise default to the public cloud.
            string suffix = string.IsNullOrWhiteSpace(settings.StorageServiceUrl) ? "core.windows.net" : settings.StorageServiceUrl.Trim();
            // Allow either a bare suffix ("core.windows.net") or a full URL ("https://...").
            Uri blobEndpoint;
            if (suffix.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                suffix.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                blobEndpoint = new Uri(suffix);
            }
            else
            {
                blobEndpoint = new Uri($"https://{settings.StorageAccountName}.blob.{suffix}");
            }
            return new BlobServiceClient(blobEndpoint, credential);
        }

        /// <summary>
        /// Creates an <see cref="ArmClient"/> using <see cref="DefaultAzureCredential"/>.
        /// </summary>
        public static ArmClient CreateArmClient(AccountSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            return new ArmClient(new global::Azure.Identity.DefaultAzureCredential());
        }

        /// <summary>
        /// Returns the <see cref="BatchAccountResource"/> for the configured Batch account.
        /// All ARM-based pool operations should hang off this resource.
        /// </summary>
        public static BatchAccountResource CreateBatchAccountResource(AccountSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (string.IsNullOrWhiteSpace(settings.SubscriptionId))
                throw new InvalidOperationException("AccountSettings.SubscriptionId is required for ARM pool operations.");
            if (string.IsNullOrWhiteSpace(settings.ResourceGroupName))
                throw new InvalidOperationException("AccountSettings.ResourceGroupName is required for ARM pool operations.");

            ArmClient arm = CreateArmClient(settings);
            ResourceIdentifier id = BatchAccountResource.CreateResourceIdentifier(
                settings.SubscriptionId,
                settings.ResourceGroupName,
                settings.BatchAccountName);
            return arm.GetBatchAccountResource(id);
        }
    }
}
