// Copyright (c) Microsoft Corporation

namespace Microsoft.Azure.Batch.Samples.Common
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using global::Azure.Compute.Batch;
    using global::Azure.Storage.Blobs;
    using global::Azure.Storage.Blobs.Models;
    using global::Azure.Storage.Sas;

    /// <summary>
    /// Replacement for the deprecated Microsoft.Azure.Batch.FileStaging functionality.
    /// Uploads local files to a single Azure Storage container and produces a list of
    /// <see cref="ResourceFile"/> objects (using a user-delegation container SAS) that
    /// can be passed to Batch tasks.
    ///
    /// All SAS tokens are generated using user-delegation keys (Azure AD) so that this
    /// helper works with <see cref="global::Azure.Identity.DefaultAzureCredential"/>.
    /// </summary>
    public static class FileStager
    {
        /// <summary>
        /// Uploads the specified local files to the given container (creating it if needed),
        /// and returns a single <see cref="ResourceFile"/> that references the container by SAS.
        /// </summary>
        public static async Task<List<ResourceFile>> StageFilesAsContainerAsync(
            BlobServiceClient blobServiceClient,
            string containerName,
            IEnumerable<string> filePaths)
        {
            containerName = containerName.ToLowerInvariant();
            BlobContainerClient container = blobServiceClient.GetBlobContainerClient(containerName);
            await container.CreateIfNotExistsAsync().ConfigureAwait(false);

            foreach (string path in filePaths)
            {
                string blobName = Path.GetFileName(path);
                Console.WriteLine("Uploading {0} to container {1}", path, containerName);
                using (FileStream fs = File.OpenRead(path))
                {
                    await container.GetBlobClient(blobName).UploadAsync(fs, overwrite: true).ConfigureAwait(false);
                }
            }

            Uri sasUri = await GenerateContainerSasUriAsync(
                blobServiceClient,
                container,
                BlobContainerSasPermissions.Read | BlobContainerSasPermissions.List).ConfigureAwait(false);

            return new List<ResourceFile>
            {
                new ResourceFile { StorageContainerUri = sasUri }
            };
        }

        /// <summary>
        /// Uploads the specified local files to the given container (creating it if needed),
        /// and returns one <see cref="ResourceFile"/> per uploaded blob (each with its own blob SAS).
        /// </summary>
        public static async Task<List<ResourceFile>> StageFilesAsBlobsAsync(
            BlobServiceClient blobServiceClient,
            string containerName,
            IEnumerable<string> filePaths)
        {
            containerName = containerName.ToLowerInvariant();
            BlobContainerClient container = blobServiceClient.GetBlobContainerClient(containerName);
            await container.CreateIfNotExistsAsync().ConfigureAwait(false);

            // Fetch a single user-delegation key and reuse it for every blob SAS in this batch.
            DateTimeOffset expiresOn = DateTimeOffset.UtcNow.AddHours(2);
            UserDelegationKey udk = (await blobServiceClient.GetUserDelegationKeyAsync(
                DateTimeOffset.UtcNow.AddMinutes(-5),
                expiresOn).ConfigureAwait(false)).Value;

            var resourceFiles = new List<ResourceFile>();
            foreach (string path in filePaths)
            {
                string blobName = Path.GetFileName(path);
                BlobClient blob = container.GetBlobClient(blobName);
                Console.WriteLine("Uploading {0} to {1}", path, blob.Uri);
                using (FileStream fs = File.OpenRead(path))
                {
                    await blob.UploadAsync(fs, overwrite: true).ConfigureAwait(false);
                }

                Uri blobSasUri = BuildUserDelegationBlobSasUri(blob, udk, BlobSasPermissions.Read, expiresOn);
                resourceFiles.Add(new ResourceFile { HttpUri = blobSasUri, FilePath = blobName });
            }

            return resourceFiles;
        }

        /// <summary>
        /// Generates a container-level user-delegation SAS URL.
        /// </summary>
        public static async Task<Uri> GenerateContainerSasUriAsync(
            BlobServiceClient blobServiceClient,
            BlobContainerClient container,
            BlobContainerSasPermissions permissions)
        {
            DateTimeOffset expiresOn = DateTimeOffset.UtcNow.AddHours(2);
            UserDelegationKey udk = (await blobServiceClient.GetUserDelegationKeyAsync(
                DateTimeOffset.UtcNow.AddMinutes(-5),
                expiresOn).ConfigureAwait(false)).Value;

            var sasBuilder = new BlobSasBuilder(permissions, expiresOn)
            {
                BlobContainerName = container.Name,
                Resource = "c",
            };
            string sasToken = sasBuilder.ToSasQueryParameters(udk, container.AccountName).ToString();
            var ub = new UriBuilder(container.Uri) { Query = sasToken };
            return ub.Uri;
        }

        /// <summary>
        /// Generates a blob-level user-delegation SAS URL.
        /// </summary>
        public static async Task<Uri> GenerateBlobSasUriAsync(
            BlobServiceClient blobServiceClient,
            BlobClient blob,
            BlobSasPermissions permissions)
        {
            DateTimeOffset expiresOn = DateTimeOffset.UtcNow.AddHours(24);
            UserDelegationKey udk = (await blobServiceClient.GetUserDelegationKeyAsync(
                DateTimeOffset.UtcNow.AddMinutes(-5),
                expiresOn).ConfigureAwait(false)).Value;
            return BuildUserDelegationBlobSasUri(blob, udk, permissions, expiresOn);
        }

        private static Uri BuildUserDelegationBlobSasUri(
            BlobClient blob,
            UserDelegationKey udk,
            BlobSasPermissions permissions,
            DateTimeOffset expiresOn)
        {
            var sasBuilder = new BlobSasBuilder(permissions, expiresOn)
            {
                BlobContainerName = blob.BlobContainerName,
                BlobName = blob.Name,
                Resource = "b",
            };
            string sasToken = sasBuilder.ToSasQueryParameters(udk, blob.AccountName).ToString();
            var ub = new UriBuilder(blob.Uri) { Query = sasToken };
            return ub.Uri;
        }
    }
}
