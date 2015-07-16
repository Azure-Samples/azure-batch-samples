namespace Microsoft.Azure.Batch.Samples.TextSearch
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Batch.Auth;
    using Microsoft.Azure.Batch.Common;
    using Microsoft.Azure.Batch.Samples.TextSearch.Properties;

    /// <summary>
    /// The job manager task.  This task manages the other tasks in the job.  It first submits the 
    /// mapper tasks and once the mapper tasks are completed it submits the reducer task.
    /// </summary>
    public class TextSearchJobManagerTask
    {
        private readonly Settings configurationSettings;
        private readonly string accountName;
        private readonly string jobId;

        /// <summary>
        /// Constructs a TextSearchJobManagerTask object with default values.
        /// </summary>
        public TextSearchJobManagerTask()
        {
            this.configurationSettings = Settings.Default;

            //Read some important data from preconfigured environment variables on the Batch compute node.
            this.accountName = Environment.GetEnvironmentVariable("AZ_BATCH_ACCOUNT_NAME");
            this.jobId = Environment.GetEnvironmentVariable("AZ_BATCH_JOB_ID");
        }

        /// <summary>
        /// Runs the job manager task.
        /// </summary>
        public async Task RunAsync()
        {
            Console.WriteLine("JobManager for account: {0}, job: {1} has started...",
                this.accountName,
                this.jobId);
            Console.WriteLine();

            Console.WriteLine("JobManager running with the following settings: ");
            Console.WriteLine("----------------------------------------");
            Console.WriteLine(this.configurationSettings.ToString());

            //Set up the Batch Service credentials used to authenticate with the Batch Service.
            BatchSharedKeyCredentials batchSharedKeyCredentials = new BatchSharedKeyCredentials(
                this.configurationSettings.BatchServiceUrl,
                this.configurationSettings.BatchAccountName,
                this.configurationSettings.BatchAccountKey);

            using (BatchClient batchClient = await BatchClient.OpenAsync(batchSharedKeyCredentials))
            {
                //Construct a container SAS to provide the Batch Service access to the files required to
                //run the mapper and reducer tasks.
                string containerSas = Helpers.ConstructContainerSas(
                    this.configurationSettings.StorageAccountName,
                    this.configurationSettings.StorageAccountKey,
                    this.configurationSettings.StorageServiceUrl,
                    this.configurationSettings.BlobContainer);

                //
                // Submit mapper tasks.
                //
                Console.WriteLine("Submitting {0} mapper tasks.", this.configurationSettings.NumberOfMapperTasks);

                //The collection of tasks to add to the Batch Service.
                List<CloudTask> tasksToAdd = new List<CloudTask>();

                for (int i = 0; i < this.configurationSettings.NumberOfMapperTasks; i++)
                {
                    string taskId = Helpers.GetMapperTaskId(i);
                    string fileBlobName = Helpers.GetSplitFileName(i);
                    string fileBlobPath = Helpers.ConstructBlobSource(containerSas, fileBlobName);

                    string commandLine = string.Format("{0} -MapperTask {1}", Constants.TextSearchExe, fileBlobPath);
                    CloudTask unboundMapperTask = new CloudTask(taskId, commandLine);

                    //The set of files (exes, dlls and configuration files) required to run the mapper task.
                    IReadOnlyList<string> mapperTaskRequiredFiles = Constants.RequiredExecutableFiles;

                    List<ResourceFile> mapperTaskResourceFiles = Helpers.GetResourceFiles(containerSas, mapperTaskRequiredFiles);
                        
                    unboundMapperTask.ResourceFiles = mapperTaskResourceFiles; 

                    tasksToAdd.Add(unboundMapperTask);
                }

                //Submit the unbound task collection to the Batch Service.
                //Use the AddTask method which takes a collection of CloudTasks for the best performance.
                await batchClient.JobOperations.AddTaskAsync(this.jobId, tasksToAdd);

                //
                // Wait for the mapper tasks to complete.
                //
                Console.WriteLine("Waiting for the mapper tasks to complete...");

                //List all the mapper tasks using an id filter.
                DetailLevel mapperTaskIdFilter = new ODATADetailLevel()
                                                        {
                                                            FilterClause = string.Format("startswith(id, '{0}')", Constants.MapperTaskPrefix)
                                                        };

                IEnumerable<CloudTask> tasksToMonitor = batchClient.JobOperations.ListTasks(
                    this.jobId,
                    detailLevel: mapperTaskIdFilter);

                //Use the task state monitor to wait for the tasks to complete.
                TaskStateMonitor taskStateMonitor = batchClient.Utilities.CreateTaskStateMonitor();
                    
                bool timedOut = await taskStateMonitor.WaitAllAsync(tasksToMonitor, TaskState.Completed, TimeSpan.FromMinutes(5));

                //Get the list of mapper tasks in order to analyze their state and ensure they completed successfully.
                IPagedEnumerable<CloudTask> asyncEnumerable = batchClient.JobOperations.ListTasks(
                    this.jobId,
                    detailLevel: mapperTaskIdFilter);

                await asyncEnumerable.ForEachAsync(async cloudTask =>
                                             {
                                                 Console.WriteLine("Task {0} is in state: {1}", cloudTask.Id, cloudTask.State);

                                                 await Helpers.CheckForTaskSuccessAsync(cloudTask, dumpStandardOutOnTaskSuccess: false);

                                                 Console.WriteLine();
                                             });
                
                //If not all the tasks reached the desired state within the timeout then the job manager
                //cannot continue.
                if (timedOut)
                {
                    const string errorMessage = "Mapper tasks did not complete within expected timeout.";
                    Console.WriteLine(errorMessage);
                        
                    throw new TimeoutException(errorMessage);
                }
                    
                //
                // Create the reducer task.
                //
                string reducerTaskCommandLine = string.Format("{0} -ReducerTask", Constants.TextSearchExe);
                    
                Console.WriteLine("Adding the reducer task: {0}", Constants.ReducerTaskId);
                CloudTask unboundReducerTask = new CloudTask(Constants.ReducerTaskId, reducerTaskCommandLine);

                //The set of files (exes, dlls and configuration files) required to run the reducer task.
                List<ResourceFile> reducerTaskResourceFiles = Helpers.GetResourceFiles(containerSas, Constants.RequiredExecutableFiles);

                unboundReducerTask.ResourceFiles = reducerTaskResourceFiles;

                //Send the request to the Batch Service to add the reducer task.
                await batchClient.JobOperations.AddTaskAsync(this.jobId, unboundReducerTask);

                //
                //Wait for the reducer task to complete.
                //

                //Get the bound reducer task and monitor it for completion.
                CloudTask boundReducerTask = await batchClient.JobOperations.GetTaskAsync(this.jobId, Constants.ReducerTaskId);

                timedOut = await taskStateMonitor.WaitAllAsync(new List<CloudTask> {boundReducerTask}, TaskState.Completed, TimeSpan.FromMinutes(2));

                //Refresh the reducer task to get the most recent information about it from the Batch Service.
                await boundReducerTask.RefreshAsync();

                //Dump the reducer tasks exit code and scheduling error for debugging purposes.
                await Helpers.CheckForTaskSuccessAsync(boundReducerTask, dumpStandardOutOnTaskSuccess: true);

                //Handle the possibilty that the reducer task did not complete in the expected timeout.
                if (timedOut)
                {
                    const string errorMessage = "Reducer task did not complete within expected timeout.";

                    Console.WriteLine("Task {0} is in state: {1}", boundReducerTask.Id, boundReducerTask.State);

                    Console.WriteLine(errorMessage);
                    throw new TimeoutException(errorMessage);
                }
                    
                //The job manager has completed.
                Console.WriteLine("JobManager completed successfully.");
            }
        }
    }
}
