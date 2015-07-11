using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Azure.Batch;
using Microsoft.Azure.Batch.Auth;
using Microsoft.Azure.Batch.Common;
using Microsoft.Azure.Batch.FileStaging;
using System.IO;
using System.Diagnostics;
using Constants = Microsoft.Azure.Batch.Constants;

namespace HelloWorld
{
    public class Program
    {
        // Specify your batch account name, region and key along with the name of a pool to use.
        // If the pool is new or has no VMs, 3 small VMs will be added to the pool to run the tasks.
        
        private const string BatchAccount = "<batch_account>";
        private const string BatchKey = "<batch_key>";
        private const string BatchRegion = "<batch_region>"; // e.g., westus
        
        private const string Url = "https://" + BatchAccount + "." + BatchRegion + ".batch.azure.com";

        private const string PoolId = "HelloWorld-Pool";

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
            BatchSharedKeyCredentials cred = new BatchSharedKeyCredentials(Url, BatchAccount, BatchKey);
            using (BatchClient client = BatchClient.Open(cred))
            {
                // add a retry policy. The built-in policies are No Retry (default), Linear Retry, and Exponential Retry
                client.CustomBehaviors.Add(RetryPolicyProvider.LinearRetryProvider(TimeSpan.FromSeconds(10), 3 ));

                ListPools(client);
                ListJobs(client);

                CloudPool pool = CreatePoolIfNeeded(client, PoolId);
                AddJobTwoTasks(client, pool.Id);

                ListPools(client);
                ListJobs(client);
                AddTasksWithFileStaging(client, pool.Id);

                ListPools(client);
                ListJobs(client);

                SubmitLargeNumberOfTasks(client, pool.Id);

                ListPools(client);
                ListJobs(client);
            }

            Console.WriteLine("Press return to exit...");
            Console.ReadLine();
        }

        private static CloudPool CreatePoolIfNeeded(BatchClient client, string poolId)
        {
            // go through all the pools and see if the named pool already exists
            bool found = false;
            CloudPool pool = null;
            foreach (CloudPool p in client.PoolOperations.ListPools())
            {
                // pools are uniquely identified by their name
                if (string.Equals(p.Id, poolId))
                {
                    Console.WriteLine("Using existing pool {0}", poolId);
                    found = true;

                    if (!p.ListComputeNodes().Any())
                    {
                        Console.WriteLine("There are no compute nodes in this pool. No tasks will be run until at least one node has been added via resizing.");
                        Console.WriteLine("Resizing pool to add 3 nodes. This might take a while...");
                        p.Resize(3);
                    }

                    pool = p;
                    break;
                }
            }

            if (!found)
            {
                Console.WriteLine("Creating pool: {0}", poolId);
                // if pool not found, call CreatePool
                // You can learn more about os families and versions at:
                // https://azure.microsoft.com/en-us/documentation/articles/cloud-services-guestos-update-matrix/
                pool = client.PoolOperations.CreatePool(poolId, targetDedicated: 3, virtualMachineSize: "small", osFamily: "3");
                //pool.Commit();
            }

            return pool;
        }

        private static void ListPools(BatchClient client)
        {
            Console.WriteLine("Listing Pools\n=============");
            // Using optional select clause to return only the ID and state. Makes query faster and reduces package size impact
            var pools = client.PoolOperations.ListPools(new ODATADetailLevel(selectClause: "id,state"));
            foreach (var p in pools)
            {
                Console.WriteLine("State of pool " + p.Id + " is " + p.State);
            }
            Console.WriteLine("=============\n");
        }

        private static void ListJobs(BatchClient client)
        {
            Console.WriteLine("Listing Jobs\n============");
                               
            var jobs = client.JobOperations.ListJobs(new ODATADetailLevel(selectClause: "id,state"));
            foreach (var j in jobs)
            {
                Console.WriteLine("State of job " + j.Id + " is " + j.State);
            }

            Console.WriteLine("============\n");
        }

        private static string CreateJobName(string prefix)
        {
            return String.Format("{0}-{1}-{2}", prefix, Environment.GetEnvironmentVariable("USERNAME"), DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        }

        /// <summary>
        /// Create a job associated with the specific pool, giving it the specified name
        /// </summary>
        private static CloudJob CreateBoundJob(JobOperations jobOps, string poolId, string jobName)
        {
            // get an empty unbound Job
            var quickJob = jobOps.CreateJob();
            quickJob.Id = jobName;
            quickJob.PoolInformation = new PoolInformation() { PoolId = poolId };

            // Commit Job to create it in the service
            quickJob.Commit();

            // Open the new Job as bound.
            CloudJob boundJob = jobOps.GetJob(jobName);

            return boundJob;
        }

        /// <summary>
        /// Create a job and add two simple tasks to it. Wait for completion using the Task state monitor
        /// </summary>
        private static void AddJobTwoTasks(BatchClient client, string sharedPoolId)
        {
            // a job is uniquely identified by its name so we will use a timestamp as suffix
            string jobName = CreateJobName("HelloWorldTwoTaskJob");

            Console.WriteLine("Creating job: " + jobName);
            CloudJob boundJob = CreateBoundJob(client.JobOperations, sharedPoolId, jobName);

            // add 2 quick tasks. Tasks within a job must have unique names
            List<CloudTask> tasksToRun = new List<CloudTask>(2);
            tasksToRun.Add(new CloudTask("task1", "hostname"));
            tasksToRun.Add(new CloudTask("task2", "cmd /c dir /s"));

            client.JobOperations.AddTask(boundJob.Id, tasksToRun);
            
            Console.WriteLine("Waiting for all tasks to complete on Job: {0} ...", boundJob.Id);

            //We use the task state monitor to monitor the state of our tasks -- in this case we will wait for them all to complete.
            TaskStateMonitor taskStateMonitor = client.Utilities.CreateTaskStateMonitor();

            // blocking wait on the list of tasks until all tasks reach completed state
            bool timedOut = taskStateMonitor.WaitAll(boundJob.ListTasks(), TaskState.Completed, new TimeSpan(0, 20, 0));

            if (timedOut)
            {
                throw new TimeoutException("Timed out waiting for tasks");
            }

            // dump task output
            foreach (CloudTask t in boundJob.ListTasks())
            {
                Console.WriteLine("Task " + t.Id + " says:\n" + t.GetNodeFile(Constants.StandardOutFileName).ReadAsString());
            }

            //Delete the job to ensure the tasks are cleaned up
            Console.WriteLine("Deleting job: {0}", boundJob.Id);
            client.JobOperations.DeleteJob(boundJob.Id);
        }

        /// <summary>
        /// Submit tasks which have dependant files.
        /// The files are automatically uploaded to Azure Storage using the FileStaging feature of the Azure.Batch client library.
        /// </summary>
        /// <param name="client"></param>
        private static void AddTasksWithFileStaging(BatchClient client, string sharedPoolId)
        {
            // create a uniquely named bound job
            string jobName = CreateJobName("HelloWorldFileStagingJob");

            Console.WriteLine("Creating job: " + jobName);
            CloudJob boundJob = CreateBoundJob(client.JobOperations, sharedPoolId, jobName);

            CloudTask taskToAdd1 = new CloudTask("task_with_file1", "cmd /c type *.txt");
            CloudTask taskToAdd2 = new CloudTask("task_with_file2", "cmd /c dir /s");

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

            // create a list of the tasks to add.
            List<CloudTask> tasksToRun = new List<CloudTask> {taskToAdd1, taskToAdd2};

            bool errors = false;

            try
            {
                //Stage the files to Azure Storage and add the tasks to Azure Batch.
                client.JobOperations.AddTask(boundJob.Id, tasksToRun);
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
                Console.WriteLine("Waiting for all tasks to complete on job: {0}...", boundJob.Id);

                List<CloudTask> tasksToMonitorForCompletion = boundJob.ListTasks().ToList();
                client.Utilities.CreateTaskStateMonitor().WaitAll(tasksToMonitorForCompletion, TaskState.Completed, TimeSpan.FromMinutes(30));

                foreach (CloudTask task in boundJob.ListTasks())
                {
                    Console.WriteLine("Task " + task.Id + " says:\n" + task.GetNodeFile(Constants.StandardOutFileName).ReadAsString());
                    Console.WriteLine(task.GetNodeFile(Constants.StandardErrorFileName).ReadAsString());
                }
            }

            //Delete the job to ensure the tasks are cleaned up
            Console.WriteLine("Deleting job: {0}", boundJob.Id);
            client.JobOperations.DeleteJob(boundJob.Id);
        }

        /// <summary>
        /// Submit a large number of tasks to the Batch Service.
        /// </summary>
        /// <param name="client">The batch client.</param>
        /// <param name="sharedPoolId">The ID of the pool to use for the job</param>
        private static void SubmitLargeNumberOfTasks(BatchClient client, string sharedPoolId)
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
            
            // create a uniquely named bound job
            string jobName = CreateJobName("HelloWorldLargeTaskCountJob");

            Console.WriteLine("Creating job: " + jobName);
            CloudJob boundJob = CreateBoundJob(client.JobOperations, sharedPoolId, jobName);

                //Generate a large number of tasks to submit
            List<CloudTask> tasksToSubmit = new List<CloudTask>(taskCountToCreate);
            for (int i = 0; i < taskCountToCreate; i++)
            {
                CloudTask task = new CloudTask("echo" + i.ToString("D5"), "echo");

                List<EnvironmentSetting> environmentSettings = new List<EnvironmentSetting>();
                environmentSettings.Add(new EnvironmentSetting("envone", envStr));

                task.EnvironmentSettings = environmentSettings;
                tasksToSubmit.Add(task);
            }

            BatchClientParallelOptions parallelOptions = new BatchClientParallelOptions()
                                                         {
                                                             //This will result in at most 10 simultaneous Bulk Add requests to the Batch Service.
                                                             MaxDegreeOfParallelism = 10
                                                         };

            Console.WriteLine("Submitting {0} tasks to job: {1}, on pool: {2}",
                taskCountToCreate,
                boundJob.Id,
                boundJob.ExecutionInformation.PoolId);

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            //Use the AddTask overload which supports a list of tasks for best AddTask performence - internally this method performs an
            //intelligent submission of tasks in batches in order to limit the number of REST API calls made to the Batch Service.
            client.JobOperations.AddTask(boundJob.Id, tasksToSubmit, parallelOptions);

            stopwatch.Stop();

            Console.WriteLine("Submitted {0} tasks in {1}", taskCountToCreate, stopwatch.Elapsed);

            //Delete the job to ensure the tasks are cleaned up
            Console.WriteLine("Deleting job: {0}", boundJob.Id);
            client.JobOperations.DeleteJob(boundJob.Id);
        }
    }
}
