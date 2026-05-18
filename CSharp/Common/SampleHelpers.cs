// Copyright (c) Microsoft Corporation

namespace Microsoft.Azure.Batch.Samples.Common
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using global::Azure;
    using global::Azure.Compute.Batch;
    using global::Azure.Core;
    using global::Azure.ResourceManager;
    using global::Azure.ResourceManager.Batch;
    using global::Azure.Storage.Blobs;
    using global::Azure.Storage.Sas;
    using Microsoft.Extensions.Configuration;

    /// <summary>
    /// Class containing helpers for the GettingStarted samples.
    /// </summary>
    public static class SampleHelpers
    {
        /// <summary>
        /// Returns the <see cref="BatchAccountResource"/> for the configured Batch account.
        /// All ARM-based pool operations should hang off this resource.
        /// </summary>
        public static BatchAccountResource GetBatchAccountResource(AccountSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (string.IsNullOrWhiteSpace(settings.SubscriptionId))
                throw new InvalidOperationException("AccountSettings.SubscriptionId is required for ARM pool operations.");
            if (string.IsNullOrWhiteSpace(settings.ResourceGroupName))
                throw new InvalidOperationException("AccountSettings.ResourceGroupName is required for ARM pool operations.");

            ArmClient arm = ClientFactory.CreateArmClient();
            ResourceIdentifier id = BatchAccountResource.CreateResourceIdentifier(
                settings.SubscriptionId,
                settings.ResourceGroupName,
                settings.BatchAccountName);
            return arm.GetBatchAccountResource(id);
        }

        /// <summary>
        /// Constructs a container shared access signature URL using a user-delegation key.
        /// </summary>
        public static async Task<string> ConstructContainerSasAsync(
            BlobServiceClient blobServiceClient,
            string containerName,
            BlobContainerSasPermissions permissions = BlobContainerSasPermissions.Read)
        {
            // Container names must always be lower case.
            containerName = containerName.ToLowerInvariant();
            BlobContainerClient container = blobServiceClient.GetBlobContainerClient(containerName);
            Uri sasUri = await FileStager.GenerateContainerSasUriAsync(blobServiceClient, container, permissions).ConfigureAwait(false);
            return sasUri.ToString();
        }

        /// <summary>
        /// Constructs a collection of <see cref="ResourceFile"/> objects based on the files specified.
        /// </summary>
        public static List<ResourceFile> GetResourceFiles(string containerSas, IEnumerable<string> dependencies)
        {
            List<ResourceFile> resourceFiles = new List<ResourceFile>();

            foreach (string dependency in dependencies)
            {
                ResourceFile resourceFile = new ResourceFile
                {
                    HttpUri = new Uri(ConstructBlobSource(containerSas, dependency)),
                    FilePath = dependency,
                };
                resourceFiles.Add(resourceFile);
            }

            return resourceFiles;
        }

        /// <summary>
        /// Combine container SAS and blob name into a URL.
        /// </summary>
        public static string ConstructBlobSource(string containerSasUrl, string blobName)
        {
            int index = containerSasUrl.IndexOf("?");

            if (index != -1)
            {
                string containerAbsoluteUrl = containerSasUrl.Substring(0, index);
                return containerAbsoluteUrl + "/" + blobName + containerSasUrl.Substring(index);
            }
            else
            {
                return containerSasUrl + "/" + blobName;
            }
        }

        /// <summary>
        /// Upload resources required for this job to Azure Storage.
        /// </summary>
        public static async Task UploadResourcesAsync(
            BlobServiceClient blobServiceClient,
            string containerName,
            IEnumerable<string> filesToUpload)
        {
            containerName = containerName.ToLowerInvariant();
            Console.WriteLine("Uploading resources to storage container: {0}", containerName);

            BlobContainerClient container = blobServiceClient.GetBlobContainerClient(containerName);
            await container.CreateIfNotExistsAsync().ConfigureAwait(false);

            List<Task> asyncTasks = new List<Task>();
            foreach (string fileName in filesToUpload)
            {
                asyncTasks.Add(UploadFileToBlobAsync(container, fileName));
            }

            await Task.WhenAll(asyncTasks).ConfigureAwait(false);
        }

        /// <summary>
        /// Uploads files and creates a single ResourceFile referencing the container by SAS.
        /// </summary>
        public static async Task<List<ResourceFile>> UploadResourcesAndCreateResourceFileReferencesAsync(
            BlobServiceClient blobServiceClient,
            string blobContainerName,
            IEnumerable<string> filePaths)
        {
            await UploadResourcesAsync(blobServiceClient, blobContainerName, filePaths).ConfigureAwait(false);

            string containerSas = await ConstructContainerSasAsync(
                blobServiceClient,
                blobContainerName,
                permissions: BlobContainerSasPermissions.Read | BlobContainerSasPermissions.List).ConfigureAwait(false);

            List<ResourceFile> resourceFiles = new List<ResourceFile>
            {
                new ResourceFile { StorageContainerUri = new Uri(containerSas) }
            };

            return resourceFiles;
        }

        private static async Task UploadFileToBlobAsync(BlobContainerClient container, string filePath)
        {
            try
            {
                string fileName = Path.GetFileName(filePath);
                BlobClient blob = container.GetBlobClient(fileName);

                Console.WriteLine("Uploading {0} to {1}", filePath, blob.Uri);
                using (FileStream fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    await blob.UploadAsync(fileStream, overwrite: true).ConfigureAwait(false);
                }
                Console.WriteLine("Done uploading {0}", filePath);
            }
            catch (RequestFailedException ex)
            {
                Console.WriteLine("Storage error uploading {0}. Status: {1}, ErrorCode: {2}, Message: {3}",
                    filePath, ex.Status, ex.ErrorCode, ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Downloads the specified blob's text.
        /// </summary>
        public static async Task<string> DownloadBlobTextAsync(BlobServiceClient blobServiceClient, string containerName, string blobName)
        {
            containerName = containerName.ToLowerInvariant();
            BlobClient blob = blobServiceClient.GetBlobContainerClient(containerName).GetBlobClient(blobName);
            var response = await blob.DownloadContentAsync().ConfigureAwait(false);
            return response.Value.Content.ToString();
        }

        /// <summary>
        /// Uploads the specified text to a blob.
        /// </summary>
        public static async Task UploadBlobTextAsync(BlobServiceClient blobServiceClient, string containerName, string blobName, string text)
        {
            containerName = containerName.ToLowerInvariant();
            BlobContainerClient container = blobServiceClient.GetBlobContainerClient(containerName);
            await container.CreateIfNotExistsAsync().ConfigureAwait(false);
            await container.GetBlobClient(blobName).UploadAsync(BinaryData.FromString(text), overwrite: true).ConfigureAwait(false);
        }

        /// <summary>
        /// Deletes the specified containers.
        /// </summary>
        public static async Task DeleteContainersAsync(BlobServiceClient blobServiceClient, IEnumerable<string> blobContainerNames)
        {
            foreach (string blobContainerName in blobContainerNames)
            {
                Console.WriteLine("Deleting container: {0}", blobContainerName);
                await blobServiceClient.GetBlobContainerClient(blobContainerName).DeleteIfExistsAsync().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Processes all the exceptions inside an <see cref="AggregateException"/> and writes each inner exception to the console.
        /// </summary>
        public static void PrintAggregateException(AggregateException aggregateException)
        {
            foreach (Exception exception in aggregateException.InnerExceptions)
            {
                Console.WriteLine(exception.ToString());
                Console.WriteLine();
            }
        }

        /// <summary>
        /// Deletes the specified jobs (data plane) and pools (ARM control plane).
        /// </summary>
        public static async Task DeleteBatchResourcesAsync(
            BatchClient batchClient,
            global::Azure.ResourceManager.Batch.BatchAccountResource batchAccount,
            List<string> jobIds,
            List<string> poolIds)
        {
            foreach (string jobId in jobIds)
            {
                Console.WriteLine("Deleting job: {0}", jobId);
                await batchClient.DeleteJobAsync(WaitUntil.Started, jobId).ConfigureAwait(false);
            }

            if (poolIds.Count > 0 && batchAccount != null)
            {
                global::Azure.ResourceManager.Batch.BatchAccountPoolCollection pools = batchAccount.GetBatchAccountPools();
                foreach (string poolId in poolIds)
                {
                    Console.WriteLine("Deleting pool: {0}", poolId);
                    if (await pools.ExistsAsync(poolId).ConfigureAwait(false))
                    {
                        global::Azure.ResourceManager.Batch.BatchAccountPoolResource pool = await pools.GetAsync(poolId).ConfigureAwait(false);
                        await pool.DeleteAsync(WaitUntil.Started).ConfigureAwait(false);
                    }
                }
            }
        }

        public static void AddSetting(StringBuilder stringBuilder, string settingName, object settingValue)
        {
            stringBuilder.AppendFormat("{0} = {1}", settingName, settingValue).AppendLine();
        }

        /// <summary>
        /// Returns an existing <see cref="BatchJob"/> if found in the Batch account, or null otherwise.
        /// </summary>
        public static async Task<BatchJob> GetJobIfExistAsync(BatchClient batchClient, string jobId)
        {
            Console.WriteLine("Checking for existing job {0}...", jobId);
            try
            {
                Response<BatchJob> response = await batchClient.GetJobAsync(jobId).ConfigureAwait(false);
                return response.Value;
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                return null;
            }
        }

        /// <summary>
        /// Finds the first supported image whose ImageReference matches the predicate.
        /// </summary>
        public static async Task<BatchSupportedImage> GetNodeAgentSkuReferenceAsync(BatchClient client, Func<BatchVmImageReference, bool> scanFunc)
        {
            await foreach (BatchSupportedImage image in client.GetSupportedImagesAsync().ConfigureAwait(false))
            {
                if (scanFunc(image.ImageReference))
                {
                    return image;
                }
            }
            throw new InvalidOperationException("No supported image matched the supplied predicate.");
        }

        public static string GetFailureInfoDetails(BatchTaskFailureInfo failureInfo)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine($"Category: {failureInfo.Category}");
            builder.AppendLine($"Code: {failureInfo.Code}");
            builder.AppendLine($"Message: {failureInfo.Message}");
            builder.AppendLine("Details:");
            if (failureInfo.Details != null)
            {
                foreach (var detail in failureInfo.Details)
                {
                    builder.AppendLine($"    {detail.Name}: {detail.Value}");
                }
            }

            return builder.ToString();
        }

        public static AccountSettings LoadAccountSettings()
        {
            AccountSettings accountSettings = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("accountsettings.json")
                .Build()
                .Get<AccountSettings>();
            return accountSettings;
        }
    }
}
