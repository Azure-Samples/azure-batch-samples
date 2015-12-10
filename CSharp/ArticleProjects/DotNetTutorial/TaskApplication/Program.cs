// Copyright (c) Microsoft Corporation

namespace Microsoft.Azure.Batch.Samples.DotNetTutorial.TaskApplication
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Blob;

    /// <summary>
    /// This application is executed by a job's tasks. It evaluates the text within a
    /// file (whose path is passed as the first command line argument) and returns the
    /// top N words that most commonly appear in the file (where N is the second command
    /// line argument).
    /// </summary>
    /// <remarks>Pass the path of the file as it exists on the compute node as the first
    /// command line argument, a number specifying how many words should be returned
    /// based on their highest count within the specified file as the second argument
    /// (for example, passing '3' returns a list of the top 3 words found within the file),
    /// and the shared access signature (SAS) URL of the blob container in Storage as the third.
    /// </remarks>
    public class Program
    {
        public static void Main(string[] args)
        {
            // The first argument passed to this executable should be the path to a text file to be processed.
            // The path may include a compute node's environment variables, such as %AZ_BATCH_NODE_SHARED_DIR%\filename.txt
            string inputFile = args[0];
            
            // The second argument passed to this executable is a number specifying how many words should
            // be returned based on their highest count within the specified file (e.g. 3 would return the
            // top 3 words found).
            int numTopN = int.Parse(args[1]);

            // The third argument should be the shared access signature for the container in Azure Storage
            // to which this task application will upload its output. This shared access signature should
            // provide WRITE access to the container.
            string outputContainerSas = args[2];

            // Read all of the text contained in the input file
            string content = File.ReadAllText(inputFile);
            string[] words = content.Split(null as char[], StringSplitOptions.RemoveEmptyEntries);
            
            // Get the word counts, pulling the top N words
            var topNWords = words
                .Where(word => word.Length > 0)
                .GroupBy(word => word, (word, count) => new { Word = word, Count = count.Count() })
                .OrderByDescending(x => x.Count)
                .Take(numTopN)
                .ToList();

            // Send the output to text file
            string outputFile = String.Format("{0}_OUTPUT{1}", Path.GetFileNameWithoutExtension(inputFile), Path.GetExtension(inputFile));
            using (System.IO.StreamWriter file = new System.IO.StreamWriter(outputFile))
            {
                file.WriteLine("Count\tWord");
                
                foreach (var topWord in topNWords)
                {
                    file.WriteLine("{0}\t\t{1}", topWord.Count.ToString().PadLeft(3), topWord.Word);
                }

                // Write out some task information using some of the node's environment variables
                file.WriteLine("------------------------------");
                file.WriteLine("Node: " + Environment.GetEnvironmentVariable("AZ_BATCH_NODE_ID"));
                file.WriteLine("Task: " + Environment.GetEnvironmentVariable("AZ_BATCH_TASK_ID"));
                file.WriteLine("Job:  " + Environment.GetEnvironmentVariable("AZ_BATCH_JOB_ID"));
                file.WriteLine("Pool: " + Environment.GetEnvironmentVariable("AZ_BATCH_POOL_ID"));
            }
            
            // Upload the output file to blob container in Azure Storage
            UploadFileToContainer(outputFile, outputContainerSas);
        }

        /// <summary>
        /// Uploads the specified file to the container represented by the specified
        /// container shared access signature (SAS).
        /// </summary>
        /// <param name="filePath">The path of the file to upload to the Storage container.</param>
        /// <param name="containerSas">The shared access signature granting write access to the specified container.</param>
        private static void UploadFileToContainer(string filePath, string containerSas)
        {
            string blobName = Path.GetFileName(filePath);

            // Obtain a reference to the container using the SAS URI.
            CloudBlobContainer container = new CloudBlobContainer(new Uri(containerSas));

            // Upload the file (as a new blob) to the container
            try
            {
                CloudBlockBlob blob = container.GetBlockBlobReference(blobName);
                blob.UploadFromFile(filePath, FileMode.Open);

                Console.WriteLine("Write operation succeeded for SAS URL " + containerSas);
                Console.WriteLine();
            }
            catch (StorageException e)
            {

                Console.WriteLine("Write operation failed for SAS URL " + containerSas);
                Console.WriteLine("Additional error information: " + e.Message);
                Console.WriteLine();

                // Indicate that a failure has occurred so that when the Batch service sets the
                // CloudTask.ExecutionInformation.ExitCode for the task that executed this application,
                // it properly indicates that there was a problem with the task.
                Environment.ExitCode = -1;
            }
        }
    }
}
