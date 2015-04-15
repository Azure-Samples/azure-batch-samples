using System;
using System.Collections.Generic;
using Microsoft.Azure.Batch;
using Microsoft.Azure.Batch.Auth;
using Microsoft.Azure.Batch.Common;
using Microsoft.Azure.Batch.FileStaging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Microsoft.Azure.Batch.Samples.TopNWordsSample
{
    /// <summary>
    /// In this sample, we have a set of input blobs and we use the Batch Service to process
    /// these blobs in parallel on multiple VMs. Each Task finds out the TopNWords for
    /// the corresponding blob.
    /// 
    /// The sample creates a run-once workitem with no job manager. We then create 
    /// multiple tasks and assign each task to a blob. We then wait for each of the 
    /// task to complete and then print out the topNWords for each input blob.
    /// </summary>
    public class Job
    {
        private const string TopNWordsExeName = "TopNWordsSample.exe";
        private const string StorageClientDllName = "Microsoft.WindowsAzure.Storage.dll";
        
        public static void JobMain(string[] args)
        {
            //Load the configuration
            TopNWordsConfiguration configuration = TopNWordsConfiguration.LoadConfigurationFromAppConfig();
            
            StagingStorageAccount stagingStorageAccount = new StagingStorageAccount(
                configuration.StorageAccountName, 
                configuration.StorageAccountKey, 
                configuration.StorageAccountBlobEndpoint);

            IBatchClient client = BatchClient.Connect(configuration.BatchServiceUrl, new BatchCredentials(configuration.BatchAccountName, configuration.BatchAccountKey));
            string stagingContainer = null;

            //Create a pool (if user hasn't provided one)
            if (configuration.ShouldCreatePool)
            {
                using (IPoolManager pm = client.OpenPoolManager())
                {
                    //OSFamily 4 == OS 2012 R2
                    //You can learn more about os families and versions at:
                    //http://msdn.microsoft.com/en-us/library/azure/ee924680.aspx
                    ICloudPool pool = pm.CreatePool(configuration.PoolName, targetDedicated: configuration.PoolSize, osFamily: "4", vmSize: "small");
                    Console.WriteLine("Adding pool {0}", configuration.PoolName);
                    pool.Commit();
                }
            }
            
            try
            {
                using (IWorkItemManager wm = client.OpenWorkItemManager())
                {
                    IToolbox toolbox = client.OpenToolbox();

                    //Use the TaskSubmissionHelper to help us create a WorkItem and add tasks to it.
                    ITaskSubmissionHelper taskSubmissionHelper = toolbox.CreateTaskSubmissionHelper(wm, configuration.PoolName);
                    taskSubmissionHelper.WorkItemName = configuration.WorkItemName;

                    FileToStage topNWordExe = new FileToStage(TopNWordsExeName, stagingStorageAccount);
                    FileToStage storageDll = new FileToStage(StorageClientDllName, stagingStorageAccount);

                    string bookFileUri = UploadBookFileToCloudBlob(configuration, configuration.BookFileName);
                    Console.WriteLine("{0} uploaded to cloud", configuration.BookFileName);
                    
                    for (int i = 1; i <= configuration.NumberOfTasks; i++)
                    {
                        ICloudTask task = new CloudTask("task_no_" + i, String.Format("{0} --Task {1} {2} {3} {4}", 
                            TopNWordsExeName, 
                            bookFileUri, 
                            configuration.NumberOfTopWords,
                            configuration.StorageAccountName, 
                            configuration.StorageAccountKey));

                        //This is the list of files to stage to a container -- for each TaskSubmissionHelper one container is created and 
                        //files all resolve to Azure Blobs by their name (so two tasks with the same named file will create just 1 blob in
                        //the TaskSubmissionHelper's container).
                        task.FilesToStage = new List<IFileStagingProvider>
                                            {
                                                topNWordExe, 
                                                storageDll
                                            };

                        taskSubmissionHelper.AddTask(task);
                    }

                    //Commit all the tasks to the Batch Service.
                    IJobCommitUnboundArtifacts artifacts = taskSubmissionHelper.Commit() as IJobCommitUnboundArtifacts;
                    
                    foreach (var fileStagingArtifact in artifacts.FileStagingArtifacts)
                    {
                        SequentialFileStagingArtifact stagingArtifact = fileStagingArtifact.Value as SequentialFileStagingArtifact;
                        if (stagingArtifact != null)
                        {
                            stagingContainer = stagingArtifact.BlobContainerCreated;
                            Console.WriteLine("Uploaded files to container: {0} -- you will be charged for their storage unless you delete them.", 
                                stagingArtifact.BlobContainerCreated);
                        }
                    }

                    //Get the job to monitor status.
                    ICloudJob job = wm.GetJob(artifacts.WorkItemName, artifacts.JobName);

                    Console.Write("Waiting for tasks to complete ...");
                    // Wait 1 minute for all tasks to reach the completed state
                    client.OpenToolbox().CreateTaskStateMonitor().WaitAll(job.ListTasks(), TaskState.Completed,  TimeSpan.FromMinutes(20));
                    Console.WriteLine("Done.");

                    foreach (ICloudTask task in job.ListTasks())
                    {
                        Console.WriteLine("Task " + task.Name + " says:\n" + task.GetTaskFile(Constants.StandardOutFileName).ReadAsString());
                        Console.WriteLine(task.GetTaskFile(Constants.StandardErrorFileName).ReadAsString());
                    }
                }
            }
            finally
            {
                //Delete the pool that we created
                if (configuration.ShouldCreatePool)
                {
                    using (IPoolManager pm = client.OpenPoolManager())
                    {
                        Console.WriteLine("Deleting pool: {0}", configuration.PoolName);
                        pm.DeletePool(configuration.PoolName);
                    }
                }

                //Delete the workitem that we created
                if (configuration.ShouldDeleteWorkItem)
                {
                    using (IWorkItemManager wm = client.OpenWorkItemManager())
                    {
                        Console.WriteLine("Deleting work item: {0}", configuration.WorkItemName);
                        wm.DeleteWorkItem(configuration.WorkItemName);
                    }
                }

                //Delete the containers we created
                if(configuration.ShouldDeleteContainer)
                {
                    DeleteContainers(configuration, stagingContainer);
                }
            }
        }

        /// <summary>
        /// Delete the containers in Azure Storage which are created by this sample.
        /// </summary>
        /// <param name="configuration"></param>
        /// <param name="fileStagingContainer"></param>
        private static void DeleteContainers(TopNWordsConfiguration configuration, string fileStagingContainer)
        {
            StorageCredentials cred = new StorageCredentials(configuration.StorageAccountName, configuration.StorageAccountKey);
            CloudStorageAccount storageAccount = new CloudStorageAccount(cred, true);
            CloudBlobClient client = storageAccount.CreateCloudBlobClient();

            //Delete the books container
            CloudBlobContainer container = client.GetContainerReference("books");
            Console.WriteLine("Deleting container: {0}", "books");
            container.DeleteIfExists();

            //Delete the file staging container
            if (!string.IsNullOrEmpty(fileStagingContainer))
            {
                container = client.GetContainerReference(fileStagingContainer);
                Console.WriteLine("Deleting container: {0}", fileStagingContainer);
                container.DeleteIfExists();   
            }
        }

        /// <summary>
        /// Upload a text file to a cloud blob.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <param name="fileName">The name of the file to upload</param>
        /// <returns>The URI of the blob.</returns>
        private static string UploadBookFileToCloudBlob(TopNWordsConfiguration configuration, string fileName)
        {
            StorageCredentials cred = new StorageCredentials(configuration.StorageAccountName, configuration.StorageAccountKey);
            CloudStorageAccount storageAccount = new CloudStorageAccount(cred, true);
            CloudBlobClient client = storageAccount.CreateCloudBlobClient();
            
            //Create the "books" container if it doesn't exist.
            CloudBlobContainer container = client.GetContainerReference("books");
            container.CreateIfNotExists();

            //Upload the blob.
            CloudBlockBlob blob = container.GetBlockBlobReference(fileName);
            blob.UploadFromFile(fileName, System.IO.FileMode.Open);
            return blob.Uri.ToString();
        }
    }
}
