//Copyright (c) Microsoft Corporation

namespace Microsoft.Azure.Batch.Samples.Common
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Blob;
    using Microsoft.Extensions.Configuration;


    /// <summary>
    /// Class containing helpers for the GettingStarted samples.
    /// </summary>
    public static class SampleHelpers
    {
        /// <summary>
        /// Constructs a container shared access signature.
        /// </summary>
        /// <param name="cloudStorageAccount">The cloud storage account.</param>
        /// <param name="containerName">The container name to construct a SAS for.</param>
        /// <param name="permissions">The permissions to generate the SAS with.</param>
        /// <returns>The container URL with the SAS and specified permissions.</returns>
        public static string ConstructContainerSas(
            CloudStorageAccount cloudStorageAccount,
            string containerName,
            SharedAccessBlobPermissions permissions = SharedAccessBlobPermissions.Read)
        {
            //Lowercase the container name because containers must always be all lower case
            containerName = containerName.ToLower();
            
            CloudBlobClient client = cloudStorageAccount.CreateCloudBlobClient();

            CloudBlobContainer container = client.GetContainerReference(containerName);

            DateTimeOffset sasStartTime = DateTime.UtcNow;
            TimeSpan sasDuration = TimeSpan.FromHours(2);
            DateTimeOffset sasEndTime = sasStartTime.Add(sasDuration);

            SharedAccessBlobPolicy sasPolicy = new SharedAccessBlobPolicy()
            {
                Permissions = permissions,
                SharedAccessExpiryTime = sasEndTime
            };

            string sasString = container.GetSharedAccessSignature(sasPolicy);
            return $"{container.Uri}{sasString}";
        }

        /// <summary>
        /// Constructs a collection of <see cref="ResourceFile"/> objects based on the files specified.
        /// </summary>
        /// <param name="containerSas">The container SAS which refers to the Azure Storage container which contains the resource file blobs.</param>
        /// <param name="dependencies">The files to construct the <see cref="ResourceFile"/> objects with.</param>
        /// <returns>A list of resource file objects.</returns>
        public static List<ResourceFile> GetResourceFiles(string containerSas, IEnumerable<string> dependencies)
        {
            List<ResourceFile> resourceFiles = new List<ResourceFile>();

            foreach (string dependency in dependencies)
            {
                ResourceFile resourceFile = ResourceFile.FromUrl(ConstructBlobSource(containerSas, dependency), dependency);
                resourceFiles.Add(resourceFile);
            }

            return resourceFiles;
        }

        /// <summary>
        /// Combine container and blob into a URL.
        /// </summary>
        /// <param name="containerSasUrl">Container SAS url.</param>
        /// <param name="blobName">Blob name.</param>
        /// <returns>Full url to the blob.</returns>
        public static string ConstructBlobSource(string containerSasUrl, string blobName)
        {
            int index = containerSasUrl.IndexOf("?");

            if (index != -1)
            {
                //SAS
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
        /// <param name="cloudStorageAccount">The cloud storage account to upload the file to.</param>
        /// <param name="containerName">The name of the container to upload the resources to.</param>
        /// <param name="filesToUpload">Additional files to upload.</param>
        public static async Task UploadResourcesAsync(
            CloudStorageAccount cloudStorageAccount, 
            string containerName, 
            IEnumerable<string> filesToUpload)
        {
            containerName = containerName.ToLower(); //Force lower case because Azure Storage only allows lower case container names.
            Console.WriteLine("Uploading resources to storage container: {0}", containerName);

            List<Task> asyncTasks = new List<Task>();
            CloudBlobClient client = cloudStorageAccount.CreateCloudBlobClient();
            CloudBlobContainer container = client.GetContainerReference(containerName);

            //Upload any additional files specified.
            foreach (string fileName in filesToUpload)
            {
                asyncTasks.Add(UploadFileToBlobAsync(container, fileName));
            }

            await Task.WhenAll(asyncTasks).ConfigureAwait(continueOnCapturedContext: false); //Wait for all the uploads to finish.
        }

        /// <summary>
        /// Uploads some files and creates a collection of resource file references to the blob paths.
        /// </summary>
        /// <param name="cloudStorageAccount">The cloud storage account to upload the resources to.</param>
        /// <param name="blobContainerName">The name of the blob container to upload the files to.</param>
        /// <param name="filePaths">The files to upload.</param>
        /// <returns>A collection of resource files.</returns>
        public static async Task<List<ResourceFile>> UploadResourcesAndCreateResourceFileReferencesAsync(CloudStorageAccount cloudStorageAccount, string blobContainerName, IEnumerable<string> filePaths)
        {
            // Upload the file for the start task to Azure Storage
            await SampleHelpers.UploadResourcesAsync(
                cloudStorageAccount,
                blobContainerName,
                filePaths).ConfigureAwait(continueOnCapturedContext: false);

            // Generate resource file references to the blob we just uploaded
            string containerSas = SampleHelpers.ConstructContainerSas(
                cloudStorageAccount,
                blobContainerName,
                permissions: SharedAccessBlobPermissions.Read | SharedAccessBlobPermissions.List);

            List<string> fileNames = filePaths.Select(Path.GetFileName).ToList();
            List<ResourceFile> resourceFiles = new List<ResourceFile> { ResourceFile.FromStorageContainerUrl(containerSas) };
            
            return resourceFiles;
        }

        /// <summary>
        /// Upload a file as a blob.
        /// </summary>
        /// <param name="container">The container to upload the blob to.</param>
        /// <param name="filePath">The path of the file to upload.</param>
        private static async Task UploadFileToBlobAsync(CloudBlobContainer container, string filePath)
        {
            try
            {
                string fileName = Path.GetFileName(filePath);
                CloudBlockBlob blob = container.GetBlockBlobReference(fileName);

                //Create the container if it doesn't exist.
                await container.CreateIfNotExistsAsync(BlobContainerPublicAccessType.Off, null, null)
                    .ConfigureAwait(continueOnCapturedContext: false); //Forbid public access

                Console.WriteLine("Uploading {0} to {1}", filePath, blob.Uri);
                using (FileStream fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    //Upload the file to the specified container.
                    await blob.UploadFromStreamAsync(fileStream).ConfigureAwait(continueOnCapturedContext: false);
                }
                Console.WriteLine("Done uploading {0}", filePath);
            }
            catch (AggregateException aggregateException)
            {
                //If there was an AggregateException process it and dump the useful information.
                foreach (Exception e in aggregateException.InnerExceptions)
                {
                    StorageException storageException = e as StorageException;
                    if (storageException != null)
                    {
                        if (storageException.RequestInformation != null &&
                            storageException.RequestInformation.ExtendedErrorInformation != null)
                        {
                            StorageExtendedErrorInformation errorInfo =
                                storageException.RequestInformation.ExtendedErrorInformation;
                            Console.WriteLine("Extended error information. Code: {0}, Message: {1}",
                                errorInfo.ErrorMessage,
                                errorInfo.ErrorCode);

                            if (errorInfo.AdditionalDetails != null)
                            {
                                foreach (KeyValuePair<string, string> keyValuePair in errorInfo.AdditionalDetails)
                                {
                                    Console.WriteLine("Key: {0}, Value: {1}", keyValuePair.Key, keyValuePair.Value);
                                }
                            }
                        }
                    }
                }

                throw; //Rethrow on blob upload failure.
            }
        }

        /// <summary>
        /// Downloads the specified blobs text.
        /// </summary>
        /// <param name="storageAccount">The storage account to download the blob from.</param>
        /// <param name="containerName">The container name.</param>
        /// <param name="blobName">The blob name.</param>
        /// <returns>The text of the blob.</returns>
        public static async Task<string> DownloadBlobTextAsync(CloudStorageAccount storageAccount, string containerName, string blobName)
        {
            containerName = containerName.ToLower(); //Force lower case because Azure Storage only allows lower case container names.

            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer container = blobClient.GetContainerReference(containerName);
            CloudBlockBlob blob = container.GetBlockBlobReference(blobName);

            string text = await blob.DownloadTextAsync().ConfigureAwait(continueOnCapturedContext: false);

            return text;
        }

        /// <summary>
        /// Uploads the specified text to a blob.
        /// </summary>
        /// <param name="storageAccount">The storage account.</param>
        /// <param name="containerName">The container name.</param>
        /// <param name="blobName">The blob name.</param>
        /// <param name="text">The text to upload.</param>
        /// <returns></returns>
        public static async Task UploadBlobTextAsync(CloudStorageAccount storageAccount, string containerName, string blobName, string text)
        {
            containerName = containerName.ToLower(); //Force lower case because Azure Storage only allows lower case container names.

            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer blobContainer = blobClient.GetContainerReference(containerName);

            CloudBlockBlob blob = blobContainer.GetBlockBlobReference(blobName);

            await blob.UploadTextAsync(text).ConfigureAwait(continueOnCapturedContext: false);
        }

        /// <summary>
        /// Deletes the specified containers
        /// </summary>
        /// <param name="storageAccount">The storage account with the containers to delete.</param>
        /// <param name="blobContainerNames">The name of the containers created for the jobs resource files.</param>
        /// <returns>An asynchronous <see cref="Task"/> representing the operation.</returns>
        public static async Task DeleteContainersAsync(CloudStorageAccount storageAccount, IEnumerable<string> blobContainerNames)
        {
            CloudBlobClient cloudBlobClient = storageAccount.CreateCloudBlobClient();
            foreach (string blobContainerName in blobContainerNames)
            {
                CloudBlobContainer container = cloudBlobClient.GetContainerReference(blobContainerName);
                Console.WriteLine("Deleting container: {0}", blobContainerName);

                await container.DeleteAsync().ConfigureAwait(continueOnCapturedContext: false);
            }
        }

        /// <summary>
        /// Processes all the exceptions inside an <see cref="AggregateException"/> and writes each inner exception to the console.
        /// </summary>
        /// <param name="aggregateException">The <see cref="AggregateException"/> to process.</param>
        public static void PrintAggregateException(AggregateException aggregateException)
        {
            // Go through all exceptions and dump useful information
            foreach (Exception exception in aggregateException.InnerExceptions)
            {
                Console.WriteLine(exception.ToString());
                Console.WriteLine();
            }
        }

        /// <summary>
        /// Deletes the pools and jobs specified.
        /// </summary>
        /// <param name="batchClient">The <see cref="BatchClient"/> to use to delete the pools and jobs</param>
        /// <param name="jobIds">The job ids to delete.</param>
        /// <param name="poolIds">The pool ids to delete.</param>
        /// <returns>An asynchronous <see cref="Task"/> representing the operation.</returns>
        public static async Task DeleteBatchResourcesAsync(BatchClient batchClient, List<string> jobIds, List<string> poolIds)
        {
            // Delete the jobs
            foreach (string jobId in jobIds)
            {
                Console.WriteLine("Deleting job: {0}", jobId);
                await batchClient.JobOperations.DeleteJobAsync(jobId).ConfigureAwait(continueOnCapturedContext: false);
            }

            foreach (string poolId in poolIds)
            {
                Console.WriteLine("Deleting pool: {0}", poolId);
                await batchClient.PoolOperations.DeletePoolAsync(poolId).ConfigureAwait(continueOnCapturedContext: false);
            }
        }

        public static void AddSetting(StringBuilder stringBuilder, string settingName, object settingValue)
        {
            stringBuilder.AppendFormat("{0} = {1}", settingName, settingValue).AppendLine();
        }

        /// <summary>
        /// Returns an existing <see cref="CloudJob"/> if found in the Batch account.
        /// </summary>
        /// <param name="batchClient">A fully initialized <see cref="BatchClient"/>.</param>
        /// <param name="jobId">The <see cref="CloudJob.Id"/> of the desired pool.</param>
        /// <returns>A bound <see cref="CloudJob"/>, or <c>null</c> if the specified <see cref="CloudJob"/> does not exist.</returns>
        public static async Task<CloudJob> GetJobIfExistAsync(BatchClient batchClient, string jobId)
        {
            Console.WriteLine("Checking for existing job {0}...", jobId);

            // Construct a detail level with a filter clause that specifies the job ID so that only
            // a single CloudJob is returned by the Batch service (if that job exists)
            ODATADetailLevel detail = new ODATADetailLevel(filterClause: string.Format("id eq '{0}'", jobId));
            List<CloudJob> jobs = await batchClient.JobOperations.ListJobs(detailLevel: detail).ToListAsync().ConfigureAwait(continueOnCapturedContext: false);
            
            return jobs.FirstOrDefault();
        }

        public static async Task<ImageInformation> GetNodeAgentSkuReferenceAsync(BatchClient client, Func<ImageReference, bool> scanFunc)
        {
            List<ImageInformation> imageInformationList = await client.PoolOperations.ListSupportedImages().ToListAsync();

            var result = imageInformationList.First(imageInfo => scanFunc(imageInfo.ImageReference));
            return result;
        }

        public static string GetFailureInfoDetails(TaskFailureInformation failureInfo)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine($"Category: {failureInfo.Category}");
            builder.AppendLine($"Code: {failureInfo.Code}");
            builder.AppendLine($"Message: {failureInfo.Message}");
            builder.AppendLine("Details:");
            foreach (var detail in failureInfo.Details)
            {
                builder.AppendLine($"    {detail.Name}: {detail.Value}");
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
