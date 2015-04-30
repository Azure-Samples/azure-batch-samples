using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Azure.Batch;
using Microsoft.Azure.Batch.Auth;
using Microsoft.Azure.Batch.Common;
using Microsoft.Azure.Batch.FileStaging;
using System.IO;
using System.Diagnostics;
using System.Threading;
using Constants = Microsoft.Azure.Batch.Constants;

namespace HelloWorld
{
    public class Program
    {
        private const string Url = "https://batch.core.windows.net";

        // insert your batch account name and key along with the name of a pool to use. If the pool is new or has no VMs, 3
        // VMs will be added to the pool to perform work.
        private const string BatchAccount = "<batch_account>";
        private const string BatchKey = "<batch_key>";
        private const string PoolName = "HelloWorld-Pool";

        // similarly, you need a storage account for the file staging example
        private const string StorageAccount = "<storage_account>";
        private const string StorageKey = "<storage_key>";
        private const string StorageBlobEndpoint = "https://" + StorageAccount + ".blob.core.windows.net";

        public static void Main(string[] args)
        {
            // This will boost parallel submission speed for REST APIs. If your use requires many simultaneous service calls set this number to something large, such as 100.  
            // See: http://msdn.microsoft.com/en-us/library/system.net.servicepointmanager.defaultconnectionlimit%28v=vs.110%29.aspx for more info.
            System.Net.ServicePointManager.DefaultConnectionLimit = 20;

            // Get an instance of the BatchClient for a given Azure Batch account.
            BatchCredentials cred = new BatchCredentials(BatchAccount, BatchKey);
            using (IBatchClient client = BatchClient.Connect(Url, cred))
            {
                // if you want to put a retry policy in place, enable it here
                // the built-in policies are No Retry (default), Linear Retry, and Exponential Retry
                //client.CustomBehaviors.Add(new SetRetryPolicy(new Microsoft.Azure.Batch.Protocol.LinearRetry()));

                ListPools(client);
                ListWorkItems(client);

                CreatePoolIfNotExist(client, PoolName);
                AddWork(client);

                ListPools(client);
                ListWorkItems(client);

                AddWorkWithFileStaging(client);

                ListPools(client);
                ListWorkItems(client);

                SubmitLargeNumberOfTasks(client);                
            }

            Console.WriteLine("Press return to exit...");
            Console.ReadLine();
        }

        private static void CreatePoolIfNotExist(IBatchClient client, string poolName)
        {
            // All Pool and VM operation starts from PoolManager
            using (IPoolManager pm = client.OpenPoolManager())
            {
                // go through all the pools and see if it already exists
                bool found = false;
                foreach (ICloudPool p in pm.ListPools())
                {
                    // pools are uniquely identified by their name
                    if (string.Equals(p.Name, poolName))
                    {
                        Console.WriteLine("Using existing pool {0}", poolName);
                        found = true;

                        if (!p.ListVMs().Any())
                        {
                            Console.WriteLine("There are no VMs in this pool. No tasks will be run until at least one VM has been added via resizing.");
                            Console.WriteLine("Resizing pool to add 3 VMs. This might take a while...");
                            p.Resize(3);
                        }

                        break;
                    }
                }

                if (!found)
                {
                    Console.WriteLine("Creating pool: {0}", poolName);
                    // if pool not found, call CreatePool
                    //You can learn more about os families and versions at:
                    //http://msdn.microsoft.com/en-us/library/azure/ee924680.aspx
                    ICloudPool pool = pm.CreatePool(poolName, targetDedicated: 3, vmSize: "small", osFamily: "3");
                    pool.Commit();
                }
            }
        }

        private static void ListPools(IBatchClient client)
        {
            using (IPoolManager pm = client.OpenPoolManager())
            {
                Console.WriteLine("Listing Pools\n=============");
                // Using optional select clause to return only the name and state.
                IEnumerable<ICloudPool> pools = pm.ListPools(new ODATADetailLevel(selectClause:"name,state"));
                foreach (var p in pools)
                {
                    Console.WriteLine("pool " + p.Name + " is " + p.State);
                }
                Console.WriteLine();
            }
        }

        private static void ListWorkItems(IBatchClient client)
        {
            // All Workitem, Job, and Task related operation start from WorkItemManager
            using (IWorkItemManager wm = client.OpenWorkItemManager())
            {
                Console.WriteLine("Listing Workitems\n=================");
                IEnumerable<ICloudWorkItem> wis = wm.ListWorkItems();
                foreach (var w in wis)
                {
                    Console.WriteLine("Workitem: " + w.Name + " State:" + w.State);
                }
                Console.WriteLine();
            }
        }

        private static void AddWork(IBatchClient client)
        {
            using (IWorkItemManager wm = client.OpenWorkItemManager())
            {
                //The toolbox contains some helper mechanisms to ease submission and monitoring of tasks.
                IToolbox toolbox = client.OpenToolbox();

                // to submit a batch of tasks, the TaskSubmissionHelper is useful.
                ITaskSubmissionHelper taskSubmissionHelper = toolbox.CreateTaskSubmissionHelper(wm, PoolName);

                // workitem is uniquely identified by its name so we will use a timestamp as suffix
                taskSubmissionHelper.WorkItemName = Environment.GetEnvironmentVariable("USERNAME") + DateTime.Now.ToString("yyyyMMdd-HHmmss");

                Console.WriteLine("Creating work item: {0}", taskSubmissionHelper.WorkItemName);

                // add 2 quick tasks. Tasks within a job must have unique names
                taskSubmissionHelper.AddTask(new CloudTask("task1", "hostname"));
                taskSubmissionHelper.AddTask(new CloudTask("task2", "cmd /c dir /s"));

                //Commit the tasks to the Batch Service
                IJobCommitUnboundArtifacts artifacts = taskSubmissionHelper.Commit() as IJobCommitUnboundArtifacts; 

                // TaskSubmissionHelper commit artifacts returns the workitem and job name
                if (artifacts != null)
                {
                    ICloudJob job = wm.GetJob(artifacts.WorkItemName, artifacts.JobName);

                    Console.WriteLine("Waiting for all tasks to complete on work item: {0}, Job: {1} ...",
                        artifacts.WorkItemName, artifacts.JobName);

                    //We use the task state monitor to monitor the state of our tasks -- in this case we will wait for them all to complete.
                    ITaskStateMonitor taskStateMonitor = toolbox.CreateTaskStateMonitor();

                    // blocking wait on the list of tasks until all tasks reach completed state
                    bool timedOut = taskStateMonitor.WaitAll(job.ListTasks(), TaskState.Completed,
                        new TimeSpan(0, 20, 0));

                    if (timedOut)
                    {
                        throw new TimeoutException("Timed out waiting for tasks");
                    }

                    // dump task output
                    foreach (var t in job.ListTasks())
                    {
                        Console.WriteLine("Task " + t.Name + " says:\n" +
                                          t.GetTaskFile(Constants.StandardOutFileName).ReadAsString());
                    }

                    // remember to delete the workitem before exiting
                    Console.WriteLine("Deleting work item: {0}", artifacts.WorkItemName);
                    wm.DeleteWorkItem(artifacts.WorkItemName);
                }
            }
        }

        /// <summary>
        /// Submit a work item with tasks which have dependant files.
        /// The files are automatically uploaded to Azure Storage using the FileStaging feature of the Azure.Batch client library.
        /// </summary>
        /// <param name="client"></param>
        private static void AddWorkWithFileStaging(IBatchClient client)
        {
            using (IWorkItemManager wm = client.OpenWorkItemManager())
            {

                IToolbox toolbox = client.OpenToolbox();
                ITaskSubmissionHelper taskSubmissionHelper = toolbox.CreateTaskSubmissionHelper(wm, PoolName);

                taskSubmissionHelper.WorkItemName = Environment.GetEnvironmentVariable("USERNAME") + DateTime.Now.ToString("yyyyMMdd-HHmmss");

                Console.WriteLine("Creating work item: {0}", taskSubmissionHelper.WorkItemName);

                ICloudTask taskToAdd1 = new CloudTask("task_with_file1", "cmd /c type *.txt");
                ICloudTask taskToAdd2 = new CloudTask("task_with_file2", "cmd /c dir /s");

                //Set up a collection of files to be staged -- these files will be uploaded to Azure Storage
                //when the tasks are submitted to the Azure Batch service.
                taskToAdd1.FilesToStage = new List<IFileStagingProvider>();
                taskToAdd2.FilesToStage = new List<IFileStagingProvider>();

                // generate a local file in temp directory
                Process cur = Process.GetCurrentProcess();
                string path = Path.Combine(Environment.GetEnvironmentVariable("TEMP"), cur.Id + ".txt");
                File.WriteAllText(path, "hello from " + cur.Id);

                // add file as task dependency so it'll be uploaded to storage before task 
                // is submitted and download onto the VM before task starts execution
                FileToStage file = new FileToStage(path, new StagingStorageAccount(StorageAccount, StorageKey, StorageBlobEndpoint));
                taskToAdd1.FilesToStage.Add(file);
                taskToAdd2.FilesToStage.Add(file); // filetostage object can be reused

                taskSubmissionHelper.AddTask(taskToAdd1);
                taskSubmissionHelper.AddTask(taskToAdd2);

                IJobCommitUnboundArtifacts artifacts = null;
                bool errors = false;
                
                try
                {
                    //Stage the files to Azure Storage and add the tasks to Azure Batch.
                    artifacts = taskSubmissionHelper.Commit() as IJobCommitUnboundArtifacts;
                }
                catch (AggregateException ae)
                {
                    errors = true;
                    // Go through all exceptions and dump useful information
                    ae.Handle(x =>
                    {
                        if (x is BatchException)
                        {
                            BatchException be = x as BatchException;
                            if (null != be.RequestInformation && null != be.RequestInformation.AzureError)
                            {
                                // Write the server side error information
                                Console.Error.WriteLine(be.RequestInformation.AzureError.Code);
                                Console.Error.WriteLine(be.RequestInformation.AzureError.Message.Value);
                                if (null != be.RequestInformation.AzureError.Values)
                                {
                                    foreach (var v in be.RequestInformation.AzureError.Values)
                                    {
                                        Console.Error.WriteLine(v.Key + " : " + v.Value);
                                    }
                                }
                            }
                        }
                        else
                        {
                            Console.WriteLine(x);
                        }
                        // Indicate that the error has been handled
                        return true;
                    });
                }

                // if there is no exception, wait for job response
                if (!errors)
                {
                    List<ICloudTask> tasksToMonitorForCompletion = wm.ListTasks(artifacts.WorkItemName, artifacts.JobName).ToList();

                    Console.WriteLine("Waiting for all tasks to complete on work item: {0}, Job: {1} ...", artifacts.WorkItemName, artifacts.JobName);
                    client.OpenToolbox().CreateTaskStateMonitor().WaitAll(tasksToMonitorForCompletion, TaskState.Completed, TimeSpan.FromMinutes(30));

                    foreach (ICloudTask task in wm.ListTasks(artifacts.WorkItemName, artifacts.JobName))
                    {
                        Console.WriteLine("Task " + task.Name + " says:\n" + task.GetTaskFile(Constants.StandardOutFileName).ReadAsString());
                        Console.WriteLine(task.GetTaskFile(Constants.StandardErrorFileName).ReadAsString());
                    }
                }

                Console.WriteLine("Deleting work item: {0}", artifacts.WorkItemName);
                wm.DeleteWorkItem(artifacts.WorkItemName); //Don't forget to delete the work item before you exit
            }
        }

        /// <summary>
        /// Submit a large number of tasks to the Batch Service.
        /// </summary>
        /// <param name="client">The batch client.</param>
        private static void SubmitLargeNumberOfTasks(IBatchClient client)
        {
            const int taskCountToCreate = 5000;

            // In order to simulate a "large" task object which has many properties set (such as resource files, environment variables, etc) 
            // we create a big environment variable so we have a big task object.
            char[] env = new char[2048];
            for (int i = 0; i < env.Length; i++)
            {
                env[i] = 'a';
            }

            string envStr = new string(env);

            using (IWorkItemManager wm = client.OpenWorkItemManager())
            {
                //Create a work item
                string workItemName = Environment.GetEnvironmentVariable("USERNAME") + DateTime.Now.ToString("yyyyMMdd-HHmmss");
                Console.WriteLine("Creating work item {0}", workItemName);
                ICloudWorkItem cloudWorkItem = wm.CreateWorkItem(workItemName);
                cloudWorkItem.JobExecutionEnvironment = new JobExecutionEnvironment() {PoolName = PoolName}; //Specify the pool to run on

                cloudWorkItem.Commit();

                //Wait for an active job
                TimeSpan maxJobCreationTimeout = TimeSpan.FromSeconds(90);
                DateTime jobCreationStartTime = DateTime.Now;
                DateTime jobCreationTimeoutTime = jobCreationStartTime.Add(maxJobCreationTimeout);
                
                cloudWorkItem = wm.GetWorkItem(workItemName);

                Console.WriteLine("Waiting for a job to become active...");
                while (cloudWorkItem.ExecutionInformation == null || cloudWorkItem.ExecutionInformation.RecentJob == null)
                {
                    cloudWorkItem.Refresh();
                    if (DateTime.Now > jobCreationTimeoutTime)
                    {
                        throw new Exception("Timed out waiting for job.");
                    }
                    Thread.Sleep(TimeSpan.FromSeconds(5));
                }
                
                string jobName = cloudWorkItem.ExecutionInformation.RecentJob.Name;
                Console.WriteLine("Found job {0}. Adding task objects...", jobName);

                //Generate a large number of tasks to submit
                List<ICloudTask> tasksToSubmit = new List<ICloudTask>();
                for (int i = 0; i < taskCountToCreate; i++)
                {
                    ICloudTask task = new CloudTask("echo" + i.ToString("D5"), "echo");

                    List<IEnvironmentSetting> environmentSettings = new List<IEnvironmentSetting>();
                    environmentSettings.Add(new EnvironmentSetting("envone", envStr));

                    task.EnvironmentSettings = environmentSettings;
                    tasksToSubmit.Add(task);
                }

                BatchClientParallelOptions parallelOptions = new BatchClientParallelOptions()
                                                             {
                                                                 //This will result in at most 10 simultaneous Bulk Add requests to the Batch Service.
                                                                 MaxDegreeOfParallelism = 10
                                                             };

                Console.WriteLine("Submitting {0} tasks to work item: {1}, job: {2}, on pool: {3}",
                    taskCountToCreate,
                    cloudWorkItem.Name,
                    jobName,
                    cloudWorkItem.JobExecutionEnvironment.PoolName);

                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();

                //Use the AddTask overload which supports a list of tasks for best AddTask performence - internally this method performs an
                //intelligent submission of tasks in batches in order to limit the number of REST API calls made to the Batch Service.
                wm.AddTask(cloudWorkItem.Name, jobName, tasksToSubmit, parallelOptions);
                
                stopwatch.Stop();

                Console.WriteLine("Submitted {0} tasks in {1}", taskCountToCreate, stopwatch.Elapsed);

                //Delete the work item to ensure the tasks are cleaned up
                wm.DeleteWorkItem(workItemName);
            }
        }
    }
}
