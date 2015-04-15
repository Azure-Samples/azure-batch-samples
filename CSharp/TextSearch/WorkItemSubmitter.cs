using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.Batch.Auth;
using Microsoft.Azure.Batch.Common;
using Microsoft.Azure.Batch.Samples.TextSearch.Properties;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Microsoft.Azure.Batch.Samples.TextSearch
{
    /// <summary>
    /// Submits the work item to the Batch Service and waits for it to complete.
    /// Once it has completed, it downloads the reducer task output
    /// and prints it to the console.
    /// </summary>
    public class WorkItemSubmitter
    {
        private readonly Settings configurationSettings;

        /// <summary>
        /// Constructs a WorkItemSubmitter with default values.
        /// </summary>
        public WorkItemSubmitter()
        {
            //Load the configuration settings.
            configurationSettings = Settings.Default;
        }

        /// <summary>
        /// Populates Azure Storage with the required files, and 
        /// submits the work item to the Azure Batch service.
        /// </summary>
        public async Task RunAsync()
        {
            Console.WriteLine("Running with the following settings: ");
            Console.WriteLine("----------------------------------------");
            Console.WriteLine(this.configurationSettings.ToString());
            
            //Upload resources if required.
            if (this.configurationSettings.ShouldUploadResources)
            {
                Console.WriteLine("Splitting file: {0} into {1} subfiles", 
                    Constants.TextFilePath, 
                    this.configurationSettings.NumberOfMapperTasks);

                //Split the text file into the correct number of files for consumption by the mapper tasks.
                FileSplitter splitter = new FileSplitter();
                List<string> mapperTaskFiles = await splitter.SplitAsync(
                    Constants.TextFilePath, 
                    this.configurationSettings.NumberOfMapperTasks);
                
                await this.UploadResourcesAsync(mapperTaskFiles);
            }

            //Generate a SAS for the container.
            string containerSasUrl = Helpers.ConstructContainerSas(
                this.configurationSettings.StorageAccountName,
                this.configurationSettings.StorageAccountKey,
                this.configurationSettings.StorageServiceUrl,
                this.configurationSettings.BlobContainer);

            //Set up the Batch Service credentials used to authenticate with the Batch Service.
            BatchCredentials batchCredentials = new BatchCredentials(
                this.configurationSettings.BatchAccountName,
                this.configurationSettings.BatchAccountKey);

            using (IBatchClient batchClient = BatchClient.Connect(this.configurationSettings.BatchServiceUrl, batchCredentials))
            {
                using (IWorkItemManager workItemManager = batchClient.OpenWorkItemManager())
                {
                    //Create the unbound work item in local memory.  An object which exists only in local memory (and not on the Batch Service) is "unbound".
                    string workItemName = Environment.GetEnvironmentVariable("USERNAME") + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
                    
                    ICloudWorkItem unboundWorkItem = workItemManager.CreateWorkItem(workItemName);

                    //
                    // Construct the work item properties in local memory before commiting them to the Batch Service.
                    //

                    //Allow enough VMs in the pool to run each mapper task, and 1 extra to run the job manager.
                    int numberOfPoolVMs = 1 + this.configurationSettings.NumberOfMapperTasks;

                    //Define the pool specification for the pool which the work item will run on.
                    IPoolSpecification poolSpecification = new PoolSpecification()
                                                               {
                                                                   TargetDedicated = numberOfPoolVMs,
                                                                   VMSize = "small",
                                                                   //You can learn more about os families and versions at: 
                                                                   //http://msdn.microsoft.com/en-us/library/azure/ee924680.aspx
                                                                   OSFamily = "4",
                                                                   TargetOSVersion = "*"
                                                               };

                    //Use the auto pool feature of the Batch Service to create a pool when the work item is created.
                    //This creates a new pool for each work item which is added.
                    IAutoPoolSpecification autoPoolSpecification = new AutoPoolSpecification()
                                                                       {
                                                                           AutoPoolNamePrefix = "TextSearchPool",
                                                                           KeepAlive = false,
                                                                           PoolLifeTimeOption = PoolLifeTimeOption.WorkItem,
                                                                           PoolSpecification = poolSpecification
                                                                       };

                    //Define the execution environment for this work item -- it will run on the pool defined by the auto pool specification above.
                    unboundWorkItem.JobExecutionEnvironment = new JobExecutionEnvironment()
                                                                   {
                                                                       AutoPoolSpecification = autoPoolSpecification
                                                                   };

                    //Define the job manager for this work item.  This job manager will run when any job is created and will submit the tasks for 
                    //the work item.  The job manager is the executable which manages the lifetime of the job
                    //and all tasks which should run for the job.  In this case, the job manager submits the mapper and reducer tasks.
                    string jobManagerCommandLine = string.Format("{0} -JobManagerTask", Constants.TextSearchExe);
                    List<IResourceFile> jobManagerResourceFiles = Helpers.GetResourceFiles(containerSasUrl, Constants.RequiredExecutableFiles);
                    const string jobManagerTaskName = "JobManager";

                    unboundWorkItem.JobSpecification = new JobSpecification()
                                                           {
                                                               JobManager = new JobManager()
                                                                                {
                                                                                    ResourceFiles = jobManagerResourceFiles,
                                                                                    CommandLine = jobManagerCommandLine,

                                                                                    //Determines if the job should terminate when the job manager process exits
                                                                                    KillJobOnCompletion = false, 
                                                                                    
                                                                                    Name = jobManagerTaskName
                                                                                }
                                                           };

                    try
                    {
                        //Commit the unbound work item to the Batch Service.
                        Console.WriteLine("Adding work item: {0} to the Batch Service.", unboundWorkItem.Name);
                        await unboundWorkItem.CommitAsync(); //Issues a request to the Batch Service to add the work item which was defined above.

                        //
                        // Wait for the job manager task to complete.
                        //
                        
                        //An object which is backed by a corresponding Batch Service object is "bound."
                        ICloudWorkItem boundWorkItem = await workItemManager.GetWorkItemAsync(workItemName);

                        //Wait for the job to be created automatically by the Batch Service.
                        string boundJobName = await Helpers.WaitForActiveJobAsync(boundWorkItem);
                        
                        ICloudTask boundJobManagerTask = await workItemManager.GetTaskAsync(
                            workItemName,
                            boundJobName,
                            jobManagerTaskName);

                        TimeSpan maxJobCompletionTimeout = TimeSpan.FromMinutes(30);

                        IToolbox toolbox = batchClient.OpenToolbox();
                        ITaskStateMonitor monitor = toolbox.CreateTaskStateMonitor();
                        bool timedOut = await monitor.WaitAllAsync(new List<ICloudTask> { boundJobManagerTask }, TaskState.Completed, maxJobCompletionTimeout);

                        Console.WriteLine("Done waiting for job manager task.");

                        await boundJobManagerTask.RefreshAsync();

                        //Check to ensure the job manager task exited successfully.
                        await Helpers.CheckForTaskSuccessAsync(boundJobManagerTask, dumpStandardOutOnTaskSuccess: true);

                        if (timedOut)
                        {
                            throw new TimeoutException(string.Format("Timed out waiting for job manager task to complete."));
                        }
                    }
                    catch (AggregateException e)
                    {
                        e.Handle(
                            (innerE) =>
                                {
                                    //We print all the inner exceptions for debugging purposes.
                                    Console.WriteLine(innerE.ToString());
                                    return false;
                                });
                        throw;
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Hit unexpected exception: {0}", e.ToString());
                        throw;
                    }
                    finally
                    {
                        //Delete the work item.
                        //This will delete the auto pool associated with the work item as long as the pool
                        //keep alive property is set to false.
                        if (this.configurationSettings.ShouldDeleteWorkItem)
                        {
                            Console.WriteLine("Deleting work item {0}", workItemName);
                            workItemManager.DeleteWorkItem(workItemName);
                        }

                        //Note that there were files uploaded to a container specified in the 
                        //configuration file.  This container will not be deleted or cleaned up by this sample.
                    }
                }
            }
        }

        /// <summary>
        /// Upload resources required for this work item to Azure Storage.
        /// </summary>
        /// <param name="additionalFilesToUpload">Additional files to upload.</param>
        private async Task UploadResourcesAsync(IEnumerable<string> additionalFilesToUpload)
        {
            string containerName = this.configurationSettings.BlobContainer;

            Console.WriteLine("Uploading resources to storage container: {0}", containerName);

            List<Task> asyncTasks = new List<Task>();

            //Upload the files which are required to run the executable.
            asyncTasks.Add(this.UploadFileToBlobAsync(Constants.StorageClientDll, containerName));
            asyncTasks.Add(this.UploadFileToBlobAsync(Constants.TextSearchExe, containerName));
            asyncTasks.Add(this.UploadFileToBlobAsync(Constants.TextSearchExeConfiguration, containerName));
            asyncTasks.Add(this.UploadFileToBlobAsync(Constants.BatchClientDll, containerName));
            asyncTasks.Add(this.UploadFileToBlobAsync(Constants.EdmDll, containerName));
            asyncTasks.Add(this.UploadFileToBlobAsync(Constants.ODataDll, containerName));
            asyncTasks.Add(this.UploadFileToBlobAsync(Constants.SpatialDll, containerName));
            
            //Upload any additional files specified.
            foreach (string fileName in additionalFilesToUpload)
            {
                asyncTasks.Add(this.UploadFileToBlobAsync(fileName, containerName));
            }

            await Task.WhenAll(asyncTasks); //Wait for all the uploads to finish.
        }

        /// <summary>
        /// Upload a file as a blob.
        /// </summary>
        /// <param name="fileName">The name of the file to upload.</param>
        /// <param name="containerName">The name of the container to upload the blob to.</param>
        private async Task UploadFileToBlobAsync(string fileName, string containerName)
        {
            containerName = containerName.ToLower(); //Force lower case because Azure Storage only allows lower case container names.
            
            try
            {
                CloudStorageAccount cloudStorageAccount = new CloudStorageAccount(
                new StorageCredentials(
                    this.configurationSettings.StorageAccountName,
                    this.configurationSettings.StorageAccountKey),
                this.configurationSettings.StorageServiceUrl,
                true);

                CloudBlobClient client = cloudStorageAccount.CreateCloudBlobClient();
                CloudBlobContainer container = client.GetContainerReference(containerName);
                CloudBlockBlob blob = container.GetBlockBlobReference(fileName);

                //Create the container if it doesn't exist.
                await container.CreateIfNotExistsAsync(BlobContainerPublicAccessType.Off, null, null); //Forbid public access

                Console.WriteLine("Uploading {0} to {1}", fileName, blob.Uri);
                using (FileStream fileStream = new FileStream(fileName, FileMode.Open, FileAccess.Read))
                {
                    //Upload the file to the specified container.
                    await blob.UploadFromStreamAsync(fileStream);
                }
                Console.WriteLine("Done uploading {0}", fileName);
            }
            catch (AggregateException aggregateException)
            {
                //If there was an AggregateException process it and dump the useful information.
                foreach (Exception e in aggregateException.InnerExceptions)
                {
                    StorageException storageException = e as StorageException;
                    if (storageException != null)
                    {
                        if (storageException.RequestInformation != null &&
                            storageException.RequestInformation.ExtendedErrorInformation != null)
                        {
                            StorageExtendedErrorInformation errorInfo = storageException.RequestInformation.ExtendedErrorInformation;
                            Console.WriteLine("Extended error information. Code: {0}, Message: {1}",
                                              errorInfo.ErrorMessage,
                                              errorInfo.ErrorCode);

                            if (errorInfo.AdditionalDetails != null)
                            {
                                foreach (KeyValuePair<string, string> keyValuePair in errorInfo.AdditionalDetails)
                                {
                                    Console.WriteLine("Key: {0}, Value: {1}", keyValuePair.Key, keyValuePair.Value);
                                }
                            }
                        }
                    }
                }
               
                throw; //Rethrow on blob upload failure.
            }

        }
    }
}
