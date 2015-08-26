namespace Microsoft.Azure.Batch.Samples.TextSearch
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Batch.Common;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Auth;
    using Microsoft.WindowsAzure.Storage.Blob;

    /// <summary>
    /// Class containing helpers for the TextSearch sample.
    /// </summary>
    public static class Helpers
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
        /// Gets the mapper task id corresponding to the specified task number.
        /// </summary>
        /// <param name="taskNumber">The mapper task number.</param>
        /// <returns>The mapper task id corresponding to the specified task number.</returns>
        public static string GetMapperTaskId(int taskNumber)
        {
            return String.Format("{0}_{1}", Constants.MapperTaskPrefix, taskNumber);
        }

        /// <summary>
        /// Gets the file name corresponding to the specified file number.
        /// </summary>
        /// <param name="fileNumber">The file number.</param>
        /// <returns>The file name corresponding to the specified file number.</returns>
        public static string GetSplitFileName(int fileNumber)
        {
            return String.Format("TextFile_{0}.txt", fileNumber);
        }

        /// <summary>
        /// Checks for a task's success or failure, and optionally dumps the output of the task.  In the case that the task hit a scheduler or execution error,
        /// dumps that information as well.
        /// </summary>
        /// <param name="boundTask">The task.</param>
        /// <param name="dumpStandardOutOnTaskSuccess">True to log the standard output file of the task even if it succeeded.  False to not log anything if the task succeeded.</param>
        /// <returns>The string containing the standard out of the file, or null if stdout could not be gathered.</returns>
        public static async Task<string> CheckForTaskSuccessAsync(CloudTask boundTask, bool dumpStandardOutOnTaskSuccess)
        {
            if (boundTask.State == TaskState.Completed)
            {
                string result = null;

                //Check to see if the task has execution information metadata.
                if (boundTask.ExecutionInformation != null)
                {
                    //Dump the task scheduling error if there was one.
                    if (boundTask.ExecutionInformation.SchedulingError != null)
                    {
                        TaskSchedulingError schedulingError = boundTask.ExecutionInformation.SchedulingError;
                        Console.WriteLine("Task {0} hit scheduling error.", boundTask.Id);
                        Console.WriteLine("SchedulingError Code: {0}", schedulingError.Code);
                        Console.WriteLine("SchedulingError Message: {0}", schedulingError.Message);
                        Console.WriteLine("SchedulingError Category: {0}", schedulingError.Category);
                        Console.WriteLine("SchedulingError Details:");

                        foreach (NameValuePair detail in schedulingError.Details)
                        {
                            Console.WriteLine("{0} : {1}", detail.Name, detail.Value);
                        }

                        throw new TextSearchException(String.Format("Task {0} failed with a scheduling error", boundTask.Id));
                    }
                    
                    //Read the content of the output files if the task exited.
                    if (boundTask.ExecutionInformation.ExitCode.HasValue)
                    {
                        Console.WriteLine("Task {0} exit code: {1}", boundTask.Id, boundTask.ExecutionInformation.ExitCode);

                        if (dumpStandardOutOnTaskSuccess && boundTask.ExecutionInformation.ExitCode.Value == 0 || boundTask.ExecutionInformation.ExitCode.Value != 0)
                        {
                            //Dump the standard out file of the task.
                            NodeFile taskStandardOut = await boundTask.GetNodeFileAsync(Batch.Constants.StandardOutFileName);

                            Console.WriteLine("Task {0} StdOut:", boundTask.Id);
                            Console.WriteLine("----------------------------------------");
                            string stdOutString = await taskStandardOut.ReadAsStringAsync();
                            result = stdOutString;
                            Console.WriteLine(stdOutString);
                        }

                        //Check for nonzero exit code and dump standard error if there was a nonzero exit code.
                        if (boundTask.ExecutionInformation.ExitCode.Value != 0)
                        {
                            NodeFile taskErrorFile = await boundTask.GetNodeFileAsync(Batch.Constants.StandardErrorFileName);

                            Console.WriteLine("Task {0} StdErr:", boundTask.Id);
                            Console.WriteLine("----------------------------------------");
                            string stdErrString = await taskErrorFile.ReadAsStringAsync();
                            Console.WriteLine(stdErrString);

                            throw new TextSearchException(String.Format("Task {0} failed with a nonzero exit code", boundTask.Id));
                        }
                    }
                }

                return result;
            }
            else
            {
                throw new TextSearchException(String.Format("Task {0} is not completed yet.  Current state: {1}", boundTask.Id, boundTask.State));
            }
        }

        /// <summary>
        /// Processes all the exceptions inside an <see cref="AggregateException"/> and writes each inner exception to the console.
        /// </summary>
        /// <param name="aggregateException">The <see cref="AggregateException"/> to process.</param>
        public static void ProcessAggregateException(AggregateException aggregateException)
        {
            // Go through all exceptions and dump useful information
            foreach (Exception exception in aggregateException.InnerExceptions)
            {
                Console.WriteLine(exception.ToString());
                Console.WriteLine();
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

    /// <summary>
    /// Custom exception type for the Text Search sample.
    /// </summary>
    public class TextSearchException : Exception
    {
        public TextSearchException(string message) : base(message)
        {
        }
    }
}
