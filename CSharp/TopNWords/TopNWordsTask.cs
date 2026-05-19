// Copyright (c) Microsoft Corporation

namespace Microsoft.Azure.Batch.Samples.TopNWordsSample
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using global::Azure.Storage.Blobs;

    /// <summary>
    /// This class has the code for each task. The task reads the
    /// blob assigned to it (via a SAS URL) and prints the top-N words.
    /// </summary>
    public class TopNWordsTask
    {
        public static void TaskMain(string[] args)
        {
            if (args == null || args.Length != 3)
            {
                throw new Exception("Usage: TopNWordsSample.exe --Task <bookSasUri> <numtopwords>");
            }

            string blobSasUri = args[1];
            int numTopN = int.Parse(args[2]);

            BlobClient blob = new BlobClient(new Uri(blobSasUri));
            using (Stream stream = blob.DownloadStreaming().Value.Content)
            using (StreamReader sr = new StreamReader(stream))
            {
                string contents = sr.ReadToEnd();
                string[] words = contents.Split(' ');
                var topNWords = words
                    .Where(word => word.Length > 0)
                    .GroupBy(word => word, (key, group) => new KeyValuePair<string, long>(key, group.LongCount()))
                    .OrderByDescending(x => x.Value)
                    .Take(numTopN)
                    .ToList();

                foreach (var pair in topNWords)
                {
                    Console.WriteLine("{0} {1}", pair.Key, pair.Value);
                }
            }
        }
    }
}
