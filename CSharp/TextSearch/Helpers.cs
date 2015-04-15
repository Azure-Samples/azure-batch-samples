using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.Batch.Common;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Microsoft.Azure.Batch.Samples.TextSearch
{
    /// <summary>
    /// Class containing helpers for the TextSearch sample.
    /// </summary>
    public class Helpers
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
            return string.Format("{0}{1}", container.Uri, sasString); ;
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
        
        /// <summary>
        /// Gets the mapper task name corresponding to the specified task number.
        /// </summary>
        /// <param name="taskNumber">The mapper task number.</param>
        /// <returns>The mapper task name corresponding to the specified task number.</returns>
        public static string GetMapperTaskName(int taskNumber)
        {
            return string.Format("{0}_{1}", Constants.MapperTaskPrefix, taskNumber);
        }

        /// <summary>
        /// Gets the file name corresponding to the specified file number.
        /// </summary>
        /// <param name="fileNumber">The file number.</param>
        /// <returns>The file name corresponding to the specified file number.</returns>
        public static string GetSplitFileName(int fileNumber)
        {
            return string.Format("TextFile_{0}.txt", fileNumber);
        }

        /// <summary>
        /// Checks for a tasks success or failure, and optionally dumps the output of the task.  In the case that the task hit a scheduler or execution error,
        /// dumps that information as well.
        /// </summary>
        /// <param name="boundTask">The task.</param>
        /// <param name="dumpStandardOutOnTaskSuccess">True to log the standard output file of the task even if it succeeded.  False to not log anything if the task succeeded.</param>
        public static async Task CheckForTaskSuccessAsync(ICloudTask boundTask, bool dumpStandardOutOnTaskSuccess)
        {
            if (boundTask.State == TaskState.Completed)
            {
                //Check to see if the task has execution information metadata.
                if (boundTask.ExecutionInformation != null)
                {
                    //Dump the task scheduling error if there was one.
                    if (boundTask.ExecutionInformation.SchedulingError != null)
                    {
                        TaskSchedulingError schedulingError = boundTask.ExecutionInformation.SchedulingError;
                        Console.WriteLine("Task {0} hit scheduling error.", boundTask.Name);
                        Console.WriteLine("SchedulingError Code: {0}", schedulingError.Code);
                        Console.WriteLine("SchedulingError Message: {0}", schedulingError.Message);
                        Console.WriteLine("SchedulingError Category: {0}", schedulingError.Category);
                        Console.WriteLine("SchedulingError Details:");

                        foreach (NameValuePair detail in schedulingError.Details)
                        {
                            Console.WriteLine("{0} : {1}", detail.Name, detail.Value);
                        }

                        throw new TextSearchException(string.Format("Task {0} failed with a scheduling error", boundTask.Name));
                    }
                    
                    //Read the content of the output files if the task exited.
                    if (boundTask.ExecutionInformation.ExitCode.HasValue)
                    {
                        Console.WriteLine("Task {0} exit code: {1}", boundTask.Name, boundTask.ExecutionInformation.ExitCode);

                        if (dumpStandardOutOnTaskSuccess && boundTask.ExecutionInformation.ExitCode.Value == 0 || boundTask.ExecutionInformation.ExitCode.Value != 0)
                        {
                            //Dump the standard out file of the task.
                            ITaskFile taskStandardOut = await boundTask.GetTaskFileAsync(
                                Microsoft.Azure.Batch.Constants.StandardOutFileName);

                            Console.WriteLine("Task {0} StdOut:", boundTask.Name);
                            Console.WriteLine("----------------------------------------");
                            string stdOutString = await taskStandardOut.ReadAsStringAsync();
                            Console.WriteLine(stdOutString);
                        }

                        //Check for nonzero exit code and dump standard error if there was a nonzero exit code.
                        if (boundTask.ExecutionInformation.ExitCode.Value != 0)
                        {
                            ITaskFile taskErrorFile = await boundTask.GetTaskFileAsync(
                                Microsoft.Azure.Batch.Constants.StandardErrorFileName);

                            Console.WriteLine("Task {0} StdErr:", boundTask.Name);
                            Console.WriteLine("----------------------------------------");
                            string stdErrString = await taskErrorFile.ReadAsStringAsync();
                            Console.WriteLine(stdErrString);

                            throw new TextSearchException(string.Format("Task {0} failed with a nonzero exit code", boundTask.Name));
                        }
                    }
                }
                else
                {
                    throw new TextSearchException(string.Format("Task {0} is not completed yet.  Current state: {1}", boundTask.Name, boundTask.State));
                }
            }
        }

        /// <summary>
        /// Wait for an active job to be created.
        /// </summary>
        /// <param name="boundWorkItem">The work item to monitor for job creation.</param>
        /// <returns>The name of the job.</returns>
        public static async Task<string> WaitForActiveJobAsync(ICloudWorkItem boundWorkItem)
        {
            //Wait for job to be created
            while (boundWorkItem.ExecutionInformation == null ||
                   boundWorkItem.ExecutionInformation.RecentJob == null)
            {
                await Task.Delay(TimeSpan.FromSeconds(5));
                Console.WriteLine("Getting bound work item {0}", boundWorkItem.Name);
                await boundWorkItem.RefreshAsync();
            }
            
            //Get the job which is running - once this job is complete our work is done
            string boundJobName = boundWorkItem.ExecutionInformation.RecentJob.Name;

            return boundJobName;
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
