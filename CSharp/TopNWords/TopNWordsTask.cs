//Copyright (c) Microsoft Corporation

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Microsoft.Azure.Batch.Samples.TopNWordsSample
{
    /// <summary>
    /// This class has the code for each task. The task reads the
    /// blob assigned to it and determine TopNWords and writes
    /// them to standard out
    /// </summary>
    public class TopNWordsTask
    {
        public static void TaskMain(string[] args)
        {
            if (args == null || args.Length != 5)
            {
                throw new Exception("Usage: TopNWordsSample.exe --Task <blobpath> <numtopwords> <storageAccountName> <storageAccountKey>");
            }

            string blobName = args[1];
            int numTopN = int.Parse(args[2]);
            string storageAccountName = args[3];
            string storageAccountKey = args[4];

            // open the cloud blob that contains the book
            var storageCred = new StorageCredentials(storageAccountName, storageAccountKey);
            CloudBlockBlob blob = new CloudBlockBlob(new Uri(blobName), storageCred);
            using (Stream memoryStream = new MemoryStream())
            {
                blob.DownloadToStream(memoryStream);
                memoryStream.Position = 0; //Reset the stream
                var sr = new StreamReader(memoryStream);
                var myStr = sr.ReadToEnd();
                string[] words = myStr.Split(' ');
                var topNWords =
                    words.
                     Where(word => word.Length > 0).
                     GroupBy(word => word, (key, group) => new KeyValuePair<String, long>(key, group.LongCount())).
                     OrderByDescending(x => x.Value).
                     Take(numTopN).
                     ToList();
                foreach (var pair in topNWords)
                {
                    Console.WriteLine("{0} {1}", pair.Key, pair.Value);
                }
            }
        }
    }
}
