namespace Microsoft.Azure.Batch.Samples.Common
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Blob;

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
        /// <returns>The container URL with the SAS.</returns>
        public static string ConstructContainerSas(
            CloudStorageAccount cloudStorageAccount,
            string containerName)
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
                Permissions = SharedAccessBlobPermissions.Read,
                SharedAccessExpiryTime = sasEndTime
            };

            string sasString = container.GetSharedAccessSignature(sasPolicy);
            return String.Format("{0}{1}", container.Uri, sasString); ;
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
                ResourceFile resourceFile = new ResourceFile(ConstructBlobSource(containerSas, dependency), dependency);
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
            Console.WriteLine("Uploading resources to storage container: {0}", containerName);

            List<Task> asyncTasks = new List<Task>();
            
            //Upload any additional files specified.
            foreach (string fileName in filesToUpload)
            {
                asyncTasks.Add(SampleHelpers.UploadFileToBlobAsync(cloudStorageAccount, containerName, fileName));
            }

            await Task.WhenAll(asyncTasks); //Wait for all the uploads to finish.
        }

        /// <summary>
        /// Upload a file as a blob.
        /// </summary>
        /// <param name="cloudStorageAccount">The cloud storage account to upload the file to.</param>
        /// <param name="containerName">The name of the container to upload the blob to.</param>
        /// <param name="filePath">The path of the file to upload.</param>
        private static async Task UploadFileToBlobAsync(CloudStorageAccount cloudStorageAccount, string containerName, string filePath)
        {
            containerName = containerName.ToLower(); //Force lower case because Azure Storage only allows lower case container names.

            try
            {
                string fileName = Path.GetFileName(filePath);
                CloudBlobClient client = cloudStorageAccount.CreateCloudBlobClient();
                CloudBlobContainer container = client.GetContainerReference(containerName);
                CloudBlockBlob blob = container.GetBlockBlobReference(fileName);
                
                //Create the container if it doesn't exist.
                await container.CreateIfNotExistsAsync(BlobContainerPublicAccessType.Off, null, null); //Forbid public access

                Console.WriteLine("Uploading {0} to {1}", filePath, blob.Uri);
                using (FileStream fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    //Upload the file to the specified container.
                    await blob.UploadFromStreamAsync(fileStream);
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
                            StorageExtendedErrorInformation errorInfo = storageException.RequestInformation.ExtendedErrorInformation;
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

            string text = await blob.DownloadTextAsync();

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

            await blob.UploadTextAsync(text);
        }
    }
}
