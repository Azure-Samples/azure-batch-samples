using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Batch.Auth;
using Microsoft.Azure.Batch.Common;
using Microsoft.Azure.Batch.Samples.TextSearch.Properties;

namespace Microsoft.Azure.Batch.Samples.TextSearch
{
    /// <summary>
    /// The job manager task.  This task manages the other tasks in the job.  It first submits the 
    /// mapper tasks and once the mapper tasks are completed it submits the reducer task.
    /// </summary>
    public class JobManagerTask
    {
        private readonly Settings configurationSettings;
        private readonly string workItemName;
        private readonly string accountName;
        private readonly string jobName;

        /// <summary>
        /// Constructs a JobManagerTask object with default values.
        /// </summary>
        public JobManagerTask()
        {
            this.configurationSettings = Settings.Default;

            //Read some important data from preconfigured environment variables on the Batch VM.
            this.accountName = Environment.GetEnvironmentVariable("WATASK_ACCOUNT_NAME");
            this.workItemName = Environment.GetEnvironmentVariable("WATASK_WORKITEM_NAME");
            this.jobName = Environment.GetEnvironmentVariable("WATASK_JOB_NAME");
        }

        /// <summary>
        /// Runs the job manager task.
        /// </summary>
        public async Task RunAsync()
        {
            Console.WriteLine("JobManager for account: {0}, work item: {1}, job: {2} has started...",
                this.accountName,
                this.workItemName,
                this.jobName);
            Console.WriteLine();

            Console.WriteLine("JobManager running with the following settings: ");
            Console.WriteLine("----------------------------------------");
            Console.WriteLine(this.configurationSettings.ToString());

            //Set up the Batch Service credentials used to authenticate with the Batch Service.
            BatchCredentials batchCredentials = new BatchCredentials(
                this.configurationSettings.BatchAccountName,
                this.configurationSettings.BatchAccountKey);

            using (IBatchClient batchClient = BatchClient.Connect(this.configurationSettings.BatchServiceUrl, batchCredentials))
            {
                using (IWorkItemManager workItemManager = batchClient.OpenWorkItemManager())
                {
                    IToolbox toolbox = batchClient.OpenToolbox();
                    
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
                    List<ICloudTask> tasksToAdd = new List<ICloudTask>();

                    for (int i = 0; i < this.configurationSettings.NumberOfMapperTasks; i++)
                    {
                        string taskName = Helpers.GetMapperTaskName(i);
                        string fileBlobName = Helpers.GetSplitFileName(i);
                        string fileBlobPath = Helpers.ConstructBlobSource(containerSas, fileBlobName);

                        string commandLine = string.Format("{0} -MapperTask {1}", Constants.TextSearchExe, fileBlobPath);
                        ICloudTask unboundMapperTask = new CloudTask(taskName, commandLine);

                        //The set of files (exe's, dll's and configuration files) required to run the mapper task.
                        IReadOnlyList<string> mapperTaskRequiredFiles = Constants.RequiredExecutableFiles;

                        List<IResourceFile> mapperTaskResourceFiles = Helpers.GetResourceFiles(containerSas, mapperTaskRequiredFiles);
                        
                        unboundMapperTask.ResourceFiles = mapperTaskResourceFiles; 

                        tasksToAdd.Add(unboundMapperTask);
                    }

                    //Submit the unbound task collection to the Batch Service.
                    //Use the AddTask method which takes a collection of ICloudTasks for the best performance.
                    await workItemManager.AddTaskAsync(this.workItemName, this.jobName, tasksToAdd);

                    //
                    // Wait for the mapper tasks to complete.
                    //
                    Console.WriteLine("Waiting for the mapper tasks to complete...");

                    //List all the mapper tasks using a name filter.
                    DetailLevel mapperTaskNameFilter = new ODATADetailLevel()
                                                           {
                                                               FilterClause = string.Format("startswith(name, '{0}')", Constants.MapperTaskPrefix)
                                                           };

                    List<ICloudTask> tasksToMonitor = workItemManager.ListTasks(
                        this.workItemName, 
                        this.jobName,
                        detailLevel: mapperTaskNameFilter).ToList();

                    //Use the task state monitor to wait for the tasks to complete.
                    ITaskStateMonitor taskStateMonitor = toolbox.CreateTaskStateMonitor();
                    
                    bool timedOut = await taskStateMonitor.WaitAllAsync(tasksToMonitor, TaskState.Completed, TimeSpan.FromMinutes(5));

                    //Get the list of mapper tasks in order to analyze their state and ensure they completed successfully.
                    IEnumerableAsyncExtended<ICloudTask> asyncEnumerable = workItemManager.ListTasks(
                        this.workItemName,
                        this.jobName,
                        detailLevel: mapperTaskNameFilter);
                    IAsyncEnumerator<ICloudTask> asyncEnumerator = asyncEnumerable.GetAsyncEnumerator();
                    
                    //Dump the status of each mapper task.
                    while (await asyncEnumerator.MoveNextAsync())
                    {
                        ICloudTask cloudTask = asyncEnumerator.Current;

                        Console.WriteLine("Task {0} is in state: {1}", cloudTask.Name, cloudTask.State);

                        await Helpers.CheckForTaskSuccessAsync(cloudTask, dumpStandardOutOnTaskSuccess: false);

                        Console.WriteLine();
                    }

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
                    
                    Console.WriteLine("Adding the reducer task: {0}", Constants.ReducerTaskName);
                    ICloudTask unboundReducerTask = new CloudTask(Constants.ReducerTaskName, reducerTaskCommandLine);

                    //The set of files (exe's, dll's and configuration files) required to run the reducer task.
                    List<IResourceFile> reducerTaskResourceFiles = Helpers.GetResourceFiles(containerSas, Constants.RequiredExecutableFiles);

                    unboundReducerTask.ResourceFiles = reducerTaskResourceFiles;

                    //Send the request to the Batch Service to add the reducer task.
                    await workItemManager.AddTaskAsync(this.workItemName, this.jobName, unboundReducerTask);

                    //
                    //Wait for the reducer task to complete.
                    //

                    //Get the bound reducer task and monitor it for completion.
                    ICloudTask boundReducerTask = await workItemManager.GetTaskAsync(this.workItemName, this.jobName, Constants.ReducerTaskName);

                    timedOut = await taskStateMonitor.WaitAllAsync(new List<ICloudTask> {boundReducerTask}, TaskState.Completed, TimeSpan.FromMinutes(2));

                    //Refresh the reducer task to get the most recent information about it from the Batch Service.
                    await boundReducerTask.RefreshAsync();

                    //Dump the reducer tasks exit code and scheduling error for debugging purposes.
                    await Helpers.CheckForTaskSuccessAsync(boundReducerTask, dumpStandardOutOnTaskSuccess: true);

                    //Handle the possibilty that the reducer task did not complete in the expected timeout.
                    if (timedOut)
                    {
                        const string errorMessage = "Reducer task did not complete within expected timeout.";

                        Console.WriteLine("Task {0} is in state: {1}", boundReducerTask.Name, boundReducerTask.State);

                        Console.WriteLine(errorMessage);
                        throw new TimeoutException(errorMessage);
                    }
                    
                    //The job manager has completed.
                    Console.WriteLine("JobManager completed successfully.");
                }
            }
        }
    }
}
