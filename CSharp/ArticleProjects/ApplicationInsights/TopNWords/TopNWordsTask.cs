// Copyright (c) Microsoft Corporation

namespace Microsoft.Azure.Batch.Samples.TopNWordsSample
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using global::Azure.Storage.Blobs;
    using Microsoft.ApplicationInsights;
    using Microsoft.ApplicationInsights.DataContracts;

    /// <summary>
    /// This class has the code for each task. The task reads the
    /// blob assigned to it (via a SAS URL) and computes top-N words.
    /// </summary>
    public class TopNWordsTask
    {
        public static void TaskMain(string[] args)
        {
            TelemetryClient insightsClient = new TelemetryClient
            {
                InstrumentationKey = "YOUR-IKEY-GOES-HERE",
            };

            if (args == null || args.Length != 3)
            {
                throw new Exception("Usage: TopNWordsSample.exe --Task <bookSasUri> <numtopwords>");
            }

            string blobSasUri = args[1];
            int numTopN = int.Parse(args[2]);

            using (WordCount wordCounter = new WordCount(insightsClient))
            {
                wordCounter.CountWords(blobSasUri, numTopN);
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
                    this.commonProperties = new Dictionary<string, string>
                    {
                        { "JobId", Environment.GetEnvironmentVariable("AZ_BATCH_JOB_ID") },
                        { "TaskId", Environment.GetEnvironmentVariable("AZ_BATCH_TASK_ID") },
                        { "PoolId", Environment.GetEnvironmentVariable("AZ_BATCH_POOL_ID") },
                        { "NodeId", Environment.GetEnvironmentVariable("AZ_BATCH_NODE_ID") },
                    };
                }

                return this.commonProperties;
            }
        }

        private readonly TelemetryClient insightsClient;
        private Dictionary<string, string> commonProperties;
        private readonly string taskId;
        private bool disposed = false;

        public WordCount(TelemetryClient telemetryClient)
        {
            this.insightsClient = telemetryClient;
            this.taskId = Environment.GetEnvironmentVariable("AZ_BATCH_TASK_ID");
            this.insightsClient.Context.Operation.Id = this.taskId;
        }

        public void CountWords(string blobSasUri, int numTopN)
        {
            // Randomly modify a blob name to force an exception
            // This is useful to see how Application Insights will track the exception
            Random rand = new Random();
            if (rand.Next(0, 10) % 10 == 0)
            {
                blobSasUri += ".badUrl";
            }

            insightsClient.TrackTrace($"Download blob {blobSasUri}", SeverityLevel.Verbose, this.CommonProperties);
            BlobClient blob = new BlobClient(new Uri(blobSasUri));

            DateTime start = DateTime.Now;
            using (Stream stream = blob.DownloadStreaming().Value.Content)
            using (StreamReader sr = new StreamReader(stream))
            {
                TimeSpan downloadTime = DateTime.Now.Subtract(start);
                insightsClient.TrackMetric("Blob download in seconds", downloadTime.TotalSeconds, this.CommonProperties);

                string contents = sr.ReadToEnd();
                string[] words = contents.Split(' ');
                this.insightsClient.TrackTrace($"Task {this.taskId}: Found {words.Length} words", SeverityLevel.Verbose, this.CommonProperties);
                this.insightsClient.TrackMetric("Number of words found", words.Length, this.commonProperties);

                var topNWords = words
                    .Where(word => word.Length > 0)
                    .GroupBy(word => word, (key, group) => new KeyValuePair<string, long>(key, group.LongCount()))
                    .OrderByDescending(x => x.Value)
                    .Take(numTopN)
                    .ToList();

                Dictionary<string, double> topWordsMetrics = new Dictionary<string, double>();
                foreach (var pair in topNWords)
                {
                    Console.WriteLine("{0} {1}", pair.Key, pair.Value);
                    topWordsMetrics.Add(pair.Key, pair.Value);
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

                Console.WriteLine("Waiting for insights to emit logs");
                System.Threading.Thread.Sleep(5000);
            }

            disposed = true;
        }
    }
}
