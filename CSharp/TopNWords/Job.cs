//Copyright (c) Microsoft Corporation

namespace Microsoft.Azure.Batch.Samples.TopNWordsSample
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Concurrent;
    using System.IO;
    using Microsoft.Azure.Batch.Auth;
    using Microsoft.Azure.Batch.Common;
    using Microsoft.Azure.Batch.FileStaging;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Auth;
    using Microsoft.WindowsAzure.Storage.Blob;
    using Microsoft.Extensions.Configuration;
    using Common;

    /// <summary>
    /// In this sample, the Batch Service is used to process a set of input blobs in parallel on multiple 
    /// compute nodes. Each task finds out the TopNWords for its corresponding blob.
    /// 
    /// The sample creates a run-once job followed by multiple tasks which each task assigned to process a
    /// specific blob. It then waits for each of the tasks to complete where it prints out the topNWords for
    /// each input blob.
    /// </summary>
    public static class Job
    {
        // files that are required on the compute nodes that run the tasks
        private const string TopNWordsExeName = "TopNWords.exe";
        private const string StorageClientDllName = "Microsoft.WindowsAzure.Storage.dll";

        public static void JobMain(string[] args)
        {
            //Load the configuration
            Settings topNWordsConfiguration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("settings.json")
                .Build()
                .Get<Settings>();
            AccountSettings accountSettings = SampleHelpers.LoadAccountSettings();

            CloudStorageAccount cloudStorageAccount = new CloudStorageAccount(
                new StorageCredentials(
                    accountSettings.StorageAccountName,
                    accountSettings.StorageAccountKey), 
                accountSettings.StorageServiceUrl,
                useHttps: true);

            StagingStorageAccount stagingStorageAccount = new StagingStorageAccount(
                accountSettings.StorageAccountName,
                accountSettings.StorageAccountKey,
                cloudStorageAccount.BlobEndpoint.ToString());

            using (BatchClient client = BatchClient.Open(new BatchSharedKeyCredentials(accountSettings.BatchServiceUrl, accountSettings.BatchAccountName, accountSettings.BatchAccountKey)))
            {
                string stagingContainer = null;

                //OSFamily 5 == Windows 2016. You can learn more about os families and versions at:
                //http://msdn.microsoft.com/en-us/library/azure/ee924680.aspx
                CloudPool pool = client.PoolOperations.CreatePool(
                    topNWordsConfiguration.PoolId, 
                    targetDedicatedComputeNodes: topNWordsConfiguration.PoolNodeCount,
                    virtualMachineSize: "standard_d1_v2",
                    cloudServiceConfiguration: new CloudServiceConfiguration(osFamily: "5"));
                Console.WriteLine("Adding pool {0}", topNWordsConfiguration.PoolId);
                GettingStartedCommon.CreatePoolIfNotExistAsync(client, pool).Wait();

                try
                {
                    Console.WriteLine("Creating job: " + topNWordsConfiguration.JobId);
                    // get an empty unbound Job
                    CloudJob unboundJob = client.JobOperations.CreateJob();
                    unboundJob.Id = topNWordsConfiguration.JobId;
                    unboundJob.PoolInformation = new PoolInformation() { PoolId = topNWordsConfiguration.PoolId };

                    // Commit Job to create it in the service
                    unboundJob.Commit();

                    // create file staging objects that represent the executable and its dependent assembly to run as the task.
                    // These files are copied to every node before the corresponding task is scheduled to run on that node.
                    FileToStage topNWordExe = new FileToStage(TopNWordsExeName, stagingStorageAccount);
                    FileToStage storageDll = new FileToStage(StorageClientDllName, stagingStorageAccount);

                    // In this sample, the input data is copied separately to Storage and its URI is passed to the task as an argument.
                    // This approach is appropriate when the amount of input data is large such that copying it to every node via FileStaging
                    // is not desired and the number of tasks is small since a large number of readers of the blob might get throttled
                    // by Storage which will lengthen the overall processing time.
                    //
                    // You'll need to observe the behavior and use published techniques for finding the right balance of performance versus
                    // complexity.
                    string bookFileUri = UploadBookFileToCloudBlob(accountSettings, topNWordsConfiguration.FileName);
                    Console.WriteLine("{0} uploaded to cloud", topNWordsConfiguration.FileName);

                    // initialize a collection to hold the tasks that will be submitted in their entirety
                    List<CloudTask> tasksToRun = new List<CloudTask>(topNWordsConfiguration.NumberOfTasks);

                    for (int i = 1; i <= topNWordsConfiguration.NumberOfTasks; i++)
                    {
                        CloudTask task = new CloudTask(
                            id: $"task_no_{i}",
                            commandline: $"{TopNWordsExeName} --Task {bookFileUri} {topNWordsConfiguration.TopWordCount} {accountSettings.StorageAccountName} {accountSettings.StorageAccountKey}");

                        //This is the list of files to stage to a container -- for each job, one container is created and 
                        //files all resolve to Azure Blobs by their name (so two tasks with the same named file will create just 1 blob in
                        //the container).
                        task.FilesToStage = new List<IFileStagingProvider>
                        {
                            topNWordExe,
                            storageDll
                        };

                        tasksToRun.Add(task);
                    }

                    // Commit all the tasks to the Batch Service. Ask AddTask to return information about the files that were staged.
                    // The container information is used later on to remove these files from Storage.
                    ConcurrentBag<ConcurrentDictionary<Type, IFileStagingArtifact>> fsArtifactBag = new ConcurrentBag<ConcurrentDictionary<Type, IFileStagingArtifact>>();
                    client.JobOperations.AddTask(topNWordsConfiguration.JobId, tasksToRun, fileStagingArtifacts: fsArtifactBag);

                    // loop through the bag of artifacts, looking for the one that matches our staged files. Once there,
                    // capture the name of the container holding the files so they can be deleted later on if that option
                    // was configured in the settings.
                    foreach (var fsBagItem in fsArtifactBag)
                    {
                        IFileStagingArtifact fsValue;
                        if (fsBagItem.TryGetValue(typeof(FileToStage), out fsValue))
                        {
                            SequentialFileStagingArtifact stagingArtifact = fsValue as SequentialFileStagingArtifact;
                            if (stagingArtifact != null)
                            {
                                stagingContainer = stagingArtifact.BlobContainerCreated;
                                Console.WriteLine(
                                    "Uploaded files to container: {0} -- you will be charged for their storage unless you delete them.",
                                    stagingArtifact.BlobContainerCreated);
                            }
                        }
                    }

                    //Get the job to monitor status.
                    CloudJob job = client.JobOperations.GetJob(topNWordsConfiguration.JobId);

                    Console.Write("Waiting for tasks to complete ...   ");
                    // Wait 20 minutes for all tasks to reach the completed state. The long timeout is necessary for the first
                    // time a pool is created in order to allow nodes to be added to the pool and initialized to run tasks.
                    IPagedEnumerable<CloudTask> ourTasks = job.ListTasks(new ODATADetailLevel(selectClause: "id"));
                    client.Utilities.CreateTaskStateMonitor().WaitAll(ourTasks, TaskState.Completed, TimeSpan.FromMinutes(20));
                    Console.WriteLine("tasks are done.");

                    foreach (CloudTask t in ourTasks)
                    {
                        Console.WriteLine("Task " + t.Id);
                        Console.WriteLine("stdout:" + Environment.NewLine + t.GetNodeFile(Batch.Constants.StandardOutFileName).ReadAsString());
                        Console.WriteLine();
                        Console.WriteLine("stderr:" + Environment.NewLine + t.GetNodeFile(Batch.Constants.StandardErrorFileName).ReadAsString());
                    }
                }
                finally
                {
                    //Delete the pool that we created
                    if (topNWordsConfiguration.ShouldDeletePool)
                    {
                        Console.WriteLine("Deleting pool: {0}", topNWordsConfiguration.PoolId);
                        client.PoolOperations.DeletePool(topNWordsConfiguration.PoolId);
                    }

                    //Delete the job that we created
                    if (topNWordsConfiguration.ShouldDeleteJob)
                    {
                        Console.WriteLine("Deleting job: {0}", topNWordsConfiguration.JobId);
                        client.JobOperations.DeleteJob(topNWordsConfiguration.JobId);
                    }

                    //Delete the containers we created
                    if (topNWordsConfiguration.ShouldDeleteContainer)
                    {
                        DeleteContainers(accountSettings, stagingContainer);
                    }
                }
            }
        }

        /// <summary>
        /// create a client for accessing blob storage
        /// </summary>
        private static CloudBlobClient GetCloudBlobClient(string accountName, string accountKey, string accountUrl)
        {
            StorageCredentials cred = new StorageCredentials(accountName, accountKey);
            CloudStorageAccount storageAccount = new CloudStorageAccount(cred, accountUrl, useHttps: true);
            CloudBlobClient client = storageAccount.CreateCloudBlobClient();

            return client;
        }

        private static string booksContainerName = "books";

        /// <summary>
        /// Delete the containers in Azure Storage which are created by this sample.
        /// </summary>
        private static void DeleteContainers(AccountSettings accountSettings, string fileStagingContainer)
        {
            CloudBlobClient client = GetCloudBlobClient(
                accountSettings.StorageAccountName, 
                accountSettings.StorageAccountKey,
                accountSettings.StorageServiceUrl);

            //Delete the books container
            CloudBlobContainer container = client.GetContainerReference(booksContainerName);
            Console.WriteLine("Deleting container: " + booksContainerName);
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
        /// <param name="accountSettings">The account settings.</param>
        /// <param name="fileName">The name of the file to upload</param>
        /// <returns>The URI of the blob.</returns>
        private static string UploadBookFileToCloudBlob(AccountSettings accountSettings, string fileName)
        {
            CloudBlobClient client = GetCloudBlobClient(
                accountSettings.StorageAccountName, 
                accountSettings.StorageAccountKey,
                accountSettings.StorageServiceUrl);

            //Create the "books" container if it doesn't exist.
            CloudBlobContainer container = client.GetContainerReference(booksContainerName);
            container.CreateIfNotExists();

            //Upload the blob.
            CloudBlockBlob blob = container.GetBlockBlobReference(fileName);
            blob.UploadFromFile(fileName);
            return blob.Uri.ToString();
        }
    }
}
