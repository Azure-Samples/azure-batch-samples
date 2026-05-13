// Copyright (c) Microsoft Corporation

namespace Microsoft.Azure.Batch.Samples.Common
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using global::Azure.Compute.Batch;
    using global::Azure.Storage.Blobs;
    using global::Azure.Storage.Sas;

    /// <summary>
    /// Replacement for the deprecated Microsoft.Azure.Batch.FileStaging functionality.
    /// Uploads local files to a single Azure Storage container and produces a list of
    /// <see cref="ResourceFile"/> objects (using a container SAS) that can be passed to
    /// Batch tasks.
    /// </summary>
    public static class FileStager
    {
        /// <summary>
        /// Uploads the specified local files to the given container (creating it if needed),
        /// and returns a single <see cref="ResourceFile"/> that references the container by SAS.
        /// All blobs in the container are downloaded onto the node.
        /// </summary>
        /// <param name="blobServiceClient">Blob service client.</param>
        /// <param name="containerName">Container name (will be lowercased).</param>
        /// <param name="filePaths">Local file paths to upload.</param>
        /// <returns>A list with a single <see cref="ResourceFile"/> referencing the container SAS.</returns>
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

            string containerSasUrl = GenerateContainerSasUrl(container, BlobContainerSasPermissions.Read | BlobContainerSasPermissions.List);
            return new List<ResourceFile>
            {
                new ResourceFile { StorageContainerUri = new Uri(containerSasUrl) }
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

                Uri blobSasUri = blob.GenerateSasUri(BlobSasPermissions.Read, DateTimeOffset.UtcNow.AddHours(2));
                resourceFiles.Add(new ResourceFile { HttpUri = blobSasUri, FilePath = blobName });
            }

            return resourceFiles;
        }

        /// <summary>
        /// Generates a container-level SAS URL.
        /// </summary>
        public static string GenerateContainerSasUrl(BlobContainerClient container, BlobContainerSasPermissions permissions)
        {
            Uri sasUri = container.GenerateSasUri(permissions, DateTimeOffset.UtcNow.AddHours(2));
            return sasUri.ToString();
        }
    }
}
