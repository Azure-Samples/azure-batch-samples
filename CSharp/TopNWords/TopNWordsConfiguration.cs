using System;
using System.Configuration;

namespace Microsoft.Azure.Batch.Samples.TopNWordsSample
{
    /// <summary>
    /// The configuration for TopNWords.
    /// </summary>
    public class TopNWordsConfiguration
    {
        /// <summary>
        /// The Batch service URL.
        /// </summary>
        public string BatchServiceUrl { get; private set; }

        /// <summary>
        /// The Batch account name to run the sample against.
        /// </summary>
        public string BatchAccountName { get; private set; }

        /// <summary>
        /// The Batch account key.
        /// </summary>
        public string BatchAccountKey { get; private set; }

        /// <summary>
        /// The number of tasks to create.
        /// </summary>
        public int NumberOfTasks { get; private set; }

        /// <summary>
        /// The size of the VMs to use in the pool.
        /// </summary>
        public int PoolSize { get; private set; }

        /// <summary>
        /// The number of top N words to calculate (5 would mean the top 5 words).
        /// </summary>
        public int NumberOfTopWords { get; private set; }

        /// <summary>
        /// The ID of the pool.
        /// </summary>
        public string PoolId { get; private set; }

        /// <summary>
        /// If a pool should be created.
        /// </summary>
        public bool ShouldCreatePool { get; private set; }

        /// <summary>
        /// The ID of the job.
        /// </summary>
        public string JobId { get; private set; }

        /// <summary>
        /// The name of the storage account to store the files required to run the tasks.
        /// </summary>
        public string StorageAccountName { get; private set; }

        /// <summary>
        /// The key of the storage account to store the files required to run the tasks.
        /// </summary>
        public string StorageAccountKey { get; private set; }

        /// <summary>
        /// The storage accounts blob endpoint.
        /// </summary>
        public string StorageAccountBlobEndpoint { get; private set; }

        /// <summary>
        /// The file name containing the book to process.
        /// </summary>
        public string BookFileName { get; private set; }

        /// <summary>
        /// If the job should be deleted when the sample ends.
        /// </summary>
        public bool ShouldDeleteJob { get; private set; }

        /// <summary>
        /// If the container should be deleted when the sample ends.
        /// </summary>
        public bool ShouldDeleteContainer { get; private set; }

        /// <summary>
        /// Loads the configuration from the App.Config file
        /// </summary>
        /// <returns></returns>
        public static TopNWordsConfiguration LoadConfigurationFromAppConfig()
        {
            TopNWordsConfiguration configuration = new TopNWordsConfiguration();

            configuration.BatchServiceUrl = ConfigurationManager.AppSettings["BatchServiceUrl"];
            configuration.BatchAccountName = ConfigurationManager.AppSettings["BatchAccount"];
            configuration.BatchAccountKey = ConfigurationManager.AppSettings["BatchKey"];

            configuration.NumberOfTasks = Int32.Parse(ConfigurationManager.AppSettings["NumTasks"]);
            configuration.PoolSize = Int32.Parse(ConfigurationManager.AppSettings["PoolSize"]);
            configuration.NumberOfTopWords = Int32.Parse(ConfigurationManager.AppSettings["NumTopWords"]);

            configuration.PoolId = ConfigurationManager.AppSettings["PoolId"];
            configuration.ShouldCreatePool = string.IsNullOrEmpty(configuration.PoolId);

            if (configuration.ShouldCreatePool)
            {
                configuration.PoolId = "TopNWordsPool" + DateTime.Now.ToString("_yyMMdd_HHmmss_") + Guid.NewGuid().ToString("N");
            }

            configuration.JobId = "TopNWordsJob" + DateTime.Now.ToString("_yyMMdd_HHmmss_") + Guid.NewGuid().ToString("N");

            configuration.StorageAccountName = ConfigurationManager.AppSettings["StorageAccountName"];
            configuration.StorageAccountKey = ConfigurationManager.AppSettings["StorageAccountKey"];
            configuration.StorageAccountBlobEndpoint = ConfigurationManager.AppSettings["StorageAccountBlobEndpoint"];
            configuration.BookFileName = ConfigurationManager.AppSettings["BookFileName"];

            configuration.ShouldDeleteJob = bool.Parse(ConfigurationManager.AppSettings["DeleteJob"]);
            configuration.ShouldDeleteContainer = bool.Parse(ConfigurationManager.AppSettings["DeleteContainer"]);


            return configuration;
        }
    }
}
