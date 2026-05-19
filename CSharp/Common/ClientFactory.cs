// Copyright (c) Microsoft Corporation

namespace Microsoft.Azure.Batch.Samples.Common
{
    using System;
    using global::Azure.Compute.Batch;
    using global::Azure.Core;
    using global::Azure.Identity;
    using global::Azure.ResourceManager;
    using global::Azure.Storage.Blobs;

    /// <summary>
    /// Helpers for constructing Azure.Compute.Batch and Azure.Storage.Blobs clients
    /// from <see cref="AccountSettings"/>. All clients use <see cref="DefaultAzureCredential"/>
    /// for AAD-based authentication (no shared keys).
    /// </summary>
    public static class ClientFactory
    {
        private static TokenCredential CreateCredential() => new DefaultAzureCredential();

        /// <summary>
        /// Creates a <see cref="BatchClient"/> using <see cref="DefaultAzureCredential"/>.
        /// </summary>
        public static BatchClient CreateBatchClient(AccountSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            return new BatchClient(new Uri(settings.BatchServiceUrl), CreateCredential());
        }

        /// <summary>
        /// Creates a <see cref="BlobServiceClient"/> using <see cref="DefaultAzureCredential"/>.
        /// </summary>
        public static BlobServiceClient CreateBlobServiceClient(AccountSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            string suffix = string.IsNullOrWhiteSpace(settings.StorageServiceUrl) ? "core.windows.net" : settings.StorageServiceUrl.Trim();
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
            return new BlobServiceClient(blobEndpoint, CreateCredential());
        }

        /// <summary>
        /// Creates an <see cref="ArmClient"/> using <see cref="DefaultAzureCredential"/>.
        /// </summary>
        public static ArmClient CreateArmClient()
        {
            return new ArmClient(CreateCredential());
        }
    }
}
