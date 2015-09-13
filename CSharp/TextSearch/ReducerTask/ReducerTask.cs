//Copyright (c) Microsoft Corporation

namespace Microsoft.Azure.Batch.Samples.TextSearch
{
    using System;
    using System.Threading.Tasks;
    using Common;
    using Microsoft.Azure.Batch.Auth;

    /// <summary>
    /// The reducer task.  This task aggregates the results from mapper tasks and prints the results.
    /// </summary>
    public class ReducerTask
    {
        private readonly Settings textSearchSettings;
        private readonly AccountSettings accountSettings;

        private readonly string accountName;
        private readonly string jobId;

        /// <summary>
        /// Constructs a reducer task object.
        /// </summary>
        public ReducerTask()
        {
            this.textSearchSettings = Settings.Default;
            this.accountSettings = AccountSettings.Default;

            //Read some important data from preconfigured environment variables on the Batch compute node.
            this.accountName = Environment.GetEnvironmentVariable("AZ_BATCH_ACCOUNT_NAME");
            this.jobId = Environment.GetEnvironmentVariable("AZ_BATCH_JOB_ID");
        }

        /// <summary>
        /// Runs the reducer task.
        /// </summary>
        public async Task RunAsync()
        {
            //Set up the Batch Service credentials used to authenticate with the Batch Service.
            BatchSharedKeyCredentials credentials = new BatchSharedKeyCredentials(
                this.accountSettings.BatchServiceUrl,
                this.accountSettings.BatchAccountName,
                this.accountSettings.BatchAccountKey); 

            using (BatchClient batchClient = await BatchClient.OpenAsync(credentials))
            {
                //Gather each Mapper tasks output and write it to standard out.
                for (int i = 0; i < this.textSearchSettings.NumberOfMapperTasks; i++)
                {
                    string mapperTaskId = Helpers.GetMapperTaskId(i);

                    //Download the standard out from each mapper task.
                    NodeFile mapperFile = await batchClient.JobOperations.GetNodeFileAsync(
                        this.jobId, 
                        mapperTaskId, 
                        Batch.Constants.StandardOutFileName);

                    string taskFileString = await mapperFile.ReadAsStringAsync();
                    Console.WriteLine(taskFileString);

                    Console.WriteLine();
                }
            }
        }
    }
}
