//Copyright (c) Microsoft Corporation

namespace Microsoft.Azure.Batch.Samples.TopNWordsSample
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Microsoft.ApplicationInsights;
    using Microsoft.WindowsAzure.Storage.Auth;
    using Microsoft.WindowsAzure.Storage.Blob;
    using Microsoft.ApplicationInsights.DataContracts;

    /// <summary>
    /// This class has the code for each task. The task reads the
    /// blob assigned to it and determine TopNWords and writes
    /// them to standard out
    /// </summary>
    public class TopNWordsTask
    {
        public static void TaskMain(string[] args)
        {
            TelemetryClient insightsClient = new TelemetryClient()
            {
                InstrumentationKey = "YOUR-IKEY-GOES-HERE",
            };

            if (args == null || args.Length != 5)
            {
                Exception e = new Exception("Usage: TopNWordsSample.exe --Task <blobpath> <numtopwords> <storageAccountName> <storageAccountKey>");
                throw e;
            }

            string blobName = args[1];
            int numTopN = int.Parse(args[2]);
            string storageAccountName = args[3];
            string storageAccountKey = args[4];

            using (WordCount wordCounter = new WordCount(insightsClient))
            {
                wordCounter.CountWords(blobName, numTopN, storageAccountName, storageAccountKey);
            }
        }
    }

    public class WordCount : IDisposable
    {
        public Dictionary<string, string> CommonProperties
        {
            get
            {
                if (this.commonProperties == null)
                {
                    this.commonProperties = new Dictionary<string, string>();
                    this.commonProperties.Add("JobId", Environment.GetEnvironmentVariable("AZ_BATCH_JOB_ID"));
                    this.commonProperties.Add("TaskId", Environment.GetEnvironmentVariable("AZ_BATCH_TASK_ID"));
                    this.commonProperties.Add("PoolId", Environment.GetEnvironmentVariable("AZ_BATCH_POOL_ID"));
                    this.commonProperties.Add("NodeId", Environment.GetEnvironmentVariable("AZ_BATCH_NODE_ID"));
                }

                return this.commonProperties;
            }
        }

        private TelemetryClient insightsClient;
        private Dictionary<string, string> commonProperties;
        private string taskId;
        bool disposed = false;

        public WordCount(TelemetryClient telemetryClient)
        {
            this.insightsClient = telemetryClient;
            this.taskId = Environment.GetEnvironmentVariable("AZ_BATCH_TASK_ID");
            this.insightsClient.Context.Operation.Id = taskId;
        }

        public void CountWords(string blobName, int numTopN, string storageAccountName, string storageAccountKey)
        {
            // Randomly modify a blob name to force an exception
            // This is useful to see how Application Insights will track the exception
            // Note that there is not try..catch block in the code
            Random rand = new Random();
            if (rand.Next(0, 10) % 10 == 0)
            {
                blobName += ".badUrl";
            }

            // open the cloud blob that contains the book
            var storageCred = new StorageCredentials(storageAccountName, storageAccountKey);
            insightsClient.TrackTrace(string.Format("Download blob {0}", blobName), SeverityLevel.Verbose, this.CommonProperties);
            CloudBlockBlob blob = new CloudBlockBlob(new Uri(blobName), storageCred);
            using (Stream memoryStream = new MemoryStream())
            {
                // Find blob download time
                DateTime start = DateTime.Now;
                blob.DownloadToStream(memoryStream);
                TimeSpan downloadTime = DateTime.Now.Subtract(start);
                insightsClient.TrackMetric("Blob download in seconds", downloadTime.TotalSeconds, this.CommonProperties);

                memoryStream.Position = 0; //Reset the stream

                Dictionary<String, Double> topWordsMetrics = new Dictionary<string, double>();
                using (StreamReader sr = new StreamReader(memoryStream))
                {
                    var myStr = sr.ReadToEnd();
                    string[] words = myStr.Split(' ');
                    this.insightsClient.TrackTrace(string.Format("Task {0}: Found {1} words", this.taskId, words.Length), SeverityLevel.Verbose, this.CommonProperties);
                    this.insightsClient.TrackMetric("Number of words found", words.Length, this.commonProperties);
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
                        topWordsMetrics.Add(pair.Key, pair.Value);
                    }
                }
                insightsClient.TrackEvent("Done counting words", this.CommonProperties, topWordsMetrics);
            }
        }
        
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }

            if (disposing)
            {
                Console.WriteLine("Flush client events");
                insightsClient.Flush();

                // allow insights client to write out
                Console.WriteLine("Waiting for insights to emit logs");
                System.Threading.Thread.Sleep(5000);
            }

            disposed = true;
        }
    }
}
