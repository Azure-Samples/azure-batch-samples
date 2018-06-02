// Copyright (c) Microsoft Corporation
//
// Companion project to the following article:
// https://azure.microsoft.com/documentation/articles/batch-task-output/

namespace Microsoft.Azure.Batch.Samples.Articles.PersistOutputs
{
    using Microsoft.Azure.Batch;
    using Microsoft.Azure.Batch.Auth;
    using Microsoft.Azure.Batch.Conventions.Files;
    using Microsoft.Azure.Batch.Samples.Common;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Auth;
    using Microsoft.WindowsAzure.Storage.Blob;
    using System;

    // This sample application demonstrates how to use the Azure Batch File Conventions
    // library to persist task output to Azure Storage, and retrieve the stored output
    // using the library.

    // REQUIRED: You must first build the PersistOutputsTask project and upload it and its
    // dependencies as a Batch application package. See the following for instruction:
    // https://azure.microsoft.com/documentation/articles/batch-application-packages/

    public class Program
    {
        public static void Main(string[] args)
        {
            // Configure your AccountSettings in the Microsoft.Azure.Batch.Samples.Common project within this solution
            AccountSettings accountSettings = SampleHelpers.LoadAccountSettings();

            BatchSharedKeyCredentials cred = new BatchSharedKeyCredentials(
                accountSettings.BatchServiceUrl,
                accountSettings.BatchAccountName,
                accountSettings.BatchAccountKey);

            StorageCredentials storageCred = new StorageCredentials(
                accountSettings.StorageAccountName,
                accountSettings.StorageAccountKey);

            CloudStorageAccount storageAccount = new CloudStorageAccount(storageCred, true);

            using (BatchClient batchClient = BatchClient.Open(cred))
            {
                string jobId = "PersistOutput-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
                const string poolId = "PersistOutputsSamplePool";
                const int nodeCount = 1;
                CloudBlobContainer container;

                Console.Write("Which persistence technology would you like to use? 1) File conventions, 2) OutputFiles, or 3) OutputFiles implementing conventions: ");
                string response = Console.ReadLine().ToLower();
                if (response == "1")
                {
                    container = FileConventionsExample.Run(batchClient, storageAccount, poolId, nodeCount, jobId).Result;

                    Console.WriteLine();
                    Console.WriteLine("All tasks completed and outputs downloaded. You can view the task outputs in the Azure portal");
                    Console.WriteLine("before deleting the job.");
                }
                else if (response == "2")
                {
                    container = OutputFilesExample.Run(batchClient, storageAccount, poolId, nodeCount, jobId).Result;

                    Console.WriteLine();
                    Console.WriteLine("All tasks completed and outputs downloaded.");
                }
                else if (response == "3")
                {
                    container = OutputFilesExample.RunWithConventions(batchClient, storageAccount, poolId, nodeCount, jobId).Result;

                    Console.WriteLine();
                    Console.WriteLine("All tasks completed and outputs downloaded. You can view the task outputs in the Azure portal");
                    Console.WriteLine("before deleting the job."); 
                }
                else
                {
                    throw new ArgumentException($"Unexpected response: {response}");
                }

                // Clean up the resources we've created (job, pool, and blob storage container)
                Console.WriteLine();
                Console.Write("Delete job? [yes] no: ");
                response = Console.ReadLine().ToLower();
                if (response != "n" && response != "no")
                {
                    batchClient.JobOperations.DeleteJob(jobId);
                }

                Console.Write("Delete pool? [yes] no: ");
                response = Console.ReadLine().ToLower();
                if (response != "n" && response != "no")
                {
                    batchClient.PoolOperations.DeletePool(poolId);
                }

                Console.Write("Delete storage container? [yes] no: ");
                response = Console.ReadLine().ToLower();
                if (response != "n" && response != "no")
                {
                    container.Delete();
                }

                Console.WriteLine();
                Console.WriteLine("Sample complete, hit ENTER to exit...");
                Console.ReadLine();
            }
        }
    }
}
