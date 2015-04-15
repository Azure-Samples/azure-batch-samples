using System;
using System.Threading.Tasks;
using Microsoft.Azure.Batch.Auth;
using Microsoft.Azure.Batch.Samples.TextSearch.Properties;

namespace Microsoft.Azure.Batch.Samples.TextSearch
{
    /// <summary>
    /// The reducer task.  This task aggregates the results from mapper tasks and prints the results.
    /// </summary>
    public class ReducerTask
    {
        private readonly Settings configurationSettings;
        private readonly string workItemName;
        private readonly string accountName;
        private readonly string jobName;

        /// <summary>
        /// Constructs a reducer task object.
        /// </summary>
        public ReducerTask()
        {
            this.configurationSettings = Settings.Default;

            //Read some important data from preconfigured environment variables on the Batch VM.
            this.accountName = Environment.GetEnvironmentVariable("WATASK_ACCOUNT_NAME");
            this.workItemName = Environment.GetEnvironmentVariable("WATASK_WORKITEM_NAME");
            this.jobName = Environment.GetEnvironmentVariable("WATASK_JOB_NAME");
        }

        /// <summary>
        /// Runs the reducer task.
        /// </summary>
        public async Task RunAsync()
        {
            //Set up the Batch Service credentials used to authenticate with the Batch Service.
            BatchCredentials credentials = new BatchCredentials(
                this.configurationSettings.BatchAccountName, 
                this.configurationSettings.BatchAccountKey); 

            using (IBatchClient batchClient = BatchClient.Connect(this.configurationSettings.BatchServiceUrl, credentials))
            {
                using (IWorkItemManager workItemManager = batchClient.OpenWorkItemManager())
                {
                    //Gather each Mapper tasks output and write it to standard out.
                    for (int i = 0; i < this.configurationSettings.NumberOfMapperTasks; i++)
                    {
                        string mapperTaskName = Helpers.GetMapperTaskName(i);

                        //Download the standard out from each mapper task.
                        ITaskFile taskFile = await workItemManager.GetTaskFileAsync(
                            this.workItemName, 
                            this.jobName, 
                            mapperTaskName, 
                            Microsoft.Azure.Batch.Constants.StandardOutFileName);

                        string taskFileString = await taskFile.ReadAsStringAsync();
                        Console.WriteLine(taskFileString);

                        Console.WriteLine();
                    }
                }
            }
        }
    }
}
