using System;
using System.Collections.Generic;
using Microsoft.Azure.Batch;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Azure.Batch.SDK.Samples.JobScheduling.JMScheduling
{
    class Helpers
    {
        /// <summary>
        /// Constructs a container shared access signature.
        /// </summary>
        /// <param name="storageAccountName">The Azure Storage account name.</param>
        /// <param name="storageAccountKey">The Azure Storage account key.</param>
        /// <param name="storageEndpoint">The Azure Storage endpoint.</param>
        /// <param name="containerName">The container name to construct a SAS for.</param>
        /// <returns>The container URL with the SAS.</returns>
        public static string ConstructContainerSas(
            string storageAccountName,
            string storageAccountKey,
            string storageEndpoint,
            string containerName)
        {
            //Lowercase the container name because containers must always be all lower case
            containerName = containerName.ToLower();

            StorageCredentials credentials = new StorageCredentials(storageAccountName, storageAccountKey);
            CloudStorageAccount storageAccount = new CloudStorageAccount(credentials, storageEndpoint, true);

            CloudBlobClient client = storageAccount.CreateCloudBlobClient();

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
            return string.Format("{0}{1}", container.Uri, sasString);
        }

        /// <summary>
        /// Constructs a collection of <see cref="IResourceFile"/> objects based on the files specified.
        /// </summary>
        /// <param name="containerSas">The container sas which refers to the Azure Storage container which contains the resource file blobs.</param>
        /// <param name="dependencies">The files to construct the <see cref="IResourceFile"/> objects with.</param>
        /// <returns>A list of resource file objects.</returns>
        public static List<IResourceFile> GetResourceFiles(string containerSas, IEnumerable<string> dependencies)
        {
            List<IResourceFile> resourceFiles = new List<IResourceFile>();

            foreach (string dependency in dependencies)
            {
                IResourceFile resourceFile = new ResourceFile(ConstructBlobSource(containerSas, dependency), dependency);
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
    }
}
