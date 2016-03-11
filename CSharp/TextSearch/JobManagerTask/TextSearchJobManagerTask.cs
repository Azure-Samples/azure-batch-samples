//Copyright (c) Microsoft Corporation

namespace Microsoft.Azure.Batch.Samples.TextSearch
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Common;
    using Microsoft.Azure.Batch.Auth;
    using Microsoft.Azure.Batch.Common;
    using WindowsAzure.Storage;
    using WindowsAzure.Storage.Auth;

    /// <summary>
    /// The job manager task.  This task manages the other tasks in the job.  It first submits the 
    /// mapper tasks and once the mapper tasks are completed it submits the reducer task.
    /// </summary>
    public class TextSearchJobManagerTask
    {
        private readonly Settings textSearchSettings;
        private readonly AccountSettings accountSettings;
        private readonly string accountName;
        private readonly string jobId;

        /// <summary>
        /// Constructs a TextSearchJobManagerTask object with default values.
        /// </summary>
        public TextSearchJobManagerTask()
        {
            this.textSearchSettings = Settings.Default;
            this.accountSettings = AccountSettings.Default;

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
            Console.WriteLine(this.textSearchSettings.ToString());

            //Set up the Batch Service credentials used to authenticate with the Batch Service.
            BatchSharedKeyCredentials batchSharedKeyCredentials = new BatchSharedKeyCredentials(
                this.accountSettings.BatchServiceUrl,
                this.accountSettings.BatchAccountName,
                this.accountSettings.BatchAccountKey);

            CloudStorageAccount cloudStorageAccount = new CloudStorageAccount(
                new StorageCredentials(
                    this.accountSettings.StorageAccountName,
                    this.accountSettings.StorageAccountKey),
                this.accountSettings.StorageServiceUrl,
                useHttps: true);

            using (BatchClient batchClient = await BatchClient.OpenAsync(batchSharedKeyCredentials))
            {
                //Construct a container SAS to provide the Batch Service access to the files required to
                //run the mapper and reducer tasks.
                string containerSas = SampleHelpers.ConstructContainerSas(
                    cloudStorageAccount,
                    this.textSearchSettings.BlobContainer);

                //
                // Submit mapper tasks.
                //
                await this.SubmitMapperTasksAsync(batchClient, containerSas);

                //
                // Wait for the mapper tasks to complete.
                //
                await this.WaitForMapperTasksToCompleteAsync(batchClient);
                    
                //
                // Create the reducer task.
                //
                await this.SubmitReducerTaskAsync(batchClient, containerSas);

                //
                // Wait for the reducer task to complete.
                //
                string textToUpload = await this.WaitForReducerTaskToCompleteAsync(batchClient);

                //
                // Upload the results of the reducer task to Azure storage for consumption later
                //

                await SampleHelpers.UploadBlobTextAsync(cloudStorageAccount, this.textSearchSettings.BlobContainer, Constants.ReducerTaskResultBlobName, textToUpload);

                //The job manager has completed.
                Console.WriteLine("JobManager completed successfully.");
            }
        }

        private async Task SubmitMapperTasksAsync(BatchClient batchClient, string containerSas)
        {
            Console.WriteLine("Submitting {0} mapper tasks.", this.textSearchSettings.NumberOfMapperTasks);

            //The collection of tasks to add to the Batch Service.
            List<CloudTask> tasksToAdd = new List<CloudTask>();

            for (int i = 0; i < this.textSearchSettings.NumberOfMapperTasks; i++)
            {
                string taskId = Helpers.GetMapperTaskId(i);
                string fileBlobName = Helpers.GetSplitFileName(i);
                string fileBlobPath = SampleHelpers.ConstructBlobSource(containerSas, fileBlobName);

                string commandLine = string.Format("{0} {1}", Constants.MapperTaskExecutable, fileBlobPath);
                CloudTask unboundMapperTask = new CloudTask(taskId, commandLine);

                //The set of files (exes, dlls and configuration files) required to run the mapper task.
                IReadOnlyList<string> mapperTaskRequiredFiles = Constants.RequiredExecutableFiles;

                List<ResourceFile> mapperTaskResourceFiles = SampleHelpers.GetResourceFiles(containerSas, mapperTaskRequiredFiles);

                unboundMapperTask.ResourceFiles = mapperTaskResourceFiles;

                tasksToAdd.Add(unboundMapperTask);
            }

            //Submit the unbound task collection to the Batch Service.
            //Use the AddTask method which takes a collection of CloudTasks for the best performance.
            await batchClient.JobOperations.AddTaskAsync(this.jobId, tasksToAdd);
        }

        private async Task WaitForMapperTasksToCompleteAsync(BatchClient batchClient)
        {
            Console.WriteLine("Waiting for the mapper tasks to complete...");

            //List all the mapper tasks using an id filter.
            DetailLevel mapperTaskIdFilter = new ODATADetailLevel()
            {
                FilterClause = string.Format("startswith(id, '{0}')", Constants.MapperTaskPrefix)
            };

            IEnumerable<CloudTask> tasksToMonitor = batchClient.JobOperations.ListTasks(
                this.jobId,
                detailLevel: mapperTaskIdFilter);

            // Use the task state monitor to wait for the tasks to complete.  Monitoring the tasks
            // for completion is necessary if you are using KillJobOnCompletion = TRUE, as otherwise when the job manager
            // exits it will kill all of the tasks that are still running under the job.
            TaskStateMonitor taskStateMonitor = batchClient.Utilities.CreateTaskStateMonitor();

            bool timedOut = await taskStateMonitor.WhenAllAsync(tasksToMonitor, TaskState.Completed, TimeSpan.FromMinutes(5));

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
        }

        private async Task SubmitReducerTaskAsync(BatchClient batchClient, string containerSas)
        {
            Console.WriteLine("Adding the reducer task: {0}", Constants.ReducerTaskId);
            CloudTask unboundReducerTask = new CloudTask(Constants.ReducerTaskId, Constants.ReducerTaskExecutable);

            //The set of files (exes, dlls and configuration files) required to run the reducer task.
            List<ResourceFile> reducerTaskResourceFiles = SampleHelpers.GetResourceFiles(containerSas, Constants.RequiredExecutableFiles);

            unboundReducerTask.ResourceFiles = reducerTaskResourceFiles;

            //Send the request to the Batch Service to add the reducer task.
            await batchClient.JobOperations.AddTaskAsync(this.jobId, unboundReducerTask);
        }

        private async Task<string> WaitForReducerTaskToCompleteAsync(BatchClient batchClient)
        {
            //Get the bound reducer task and monitor it for completion.
            CloudTask boundReducerTask = await batchClient.JobOperations.GetTaskAsync(this.jobId, Constants.ReducerTaskId);
            TaskStateMonitor taskStateMonitor = batchClient.Utilities.CreateTaskStateMonitor();

            bool timedOut = await taskStateMonitor.WhenAllAsync(new List<CloudTask> { boundReducerTask }, TaskState.Completed, TimeSpan.FromMinutes(2));

            //Refresh the reducer task to get the most recent information about it from the Batch Service.
            await boundReducerTask.RefreshAsync();

            //Dump the reducer tasks exit code and scheduling error for debugging purposes.
            string stdOut = await Helpers.CheckForTaskSuccessAsync(boundReducerTask, dumpStandardOutOnTaskSuccess: true);

            //Handle the possibilty that the reducer task did not complete in the expected timeout.
            if (timedOut)
            {
                const string errorMessage = "Reducer task did not complete within expected timeout.";

                Console.WriteLine("Task {0} is in state: {1}", boundReducerTask.Id, boundReducerTask.State);

                Console.WriteLine(errorMessage);
                throw new TimeoutException(errorMessage);
            }

            return stdOut;
        }

    }
}
