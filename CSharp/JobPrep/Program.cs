#define VERBOSE

using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Azure.Batch;
using Microsoft.Azure.Batch.Auth;
using Microsoft.Azure.Batch.Common;


namespace Microsoft.Azure.Batch.Sample.JobPrep
{
    public class Program
    {
        private const string Url = "https://batch.core.windows.net";
        private const string CredentialFile = "..\\..\\batchcred.txt";

        // insert your batch account name and key along with the name of a pool to use. If the pool is new or has no VMs, 3
        // VMs will be added to the pool to perform work.
        private static string BatchAccount = "<batch_account>";
        private static string BatchKey = "<batch_key>";
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

            if (File.Exists(CredentialFile))
            {
                string[] lines = File.ReadAllLines(CredentialFile);
                if (lines.Length >= 2)
                {
                    BatchAccount = lines[0];
                    BatchKey = lines[1];
                }
            }

            // Get an instance of the BatchClient for a given Azure Batch account.
            BatchCredentials cred = new BatchCredentials(BatchAccount, BatchKey);
            using (IBatchClient client = BatchClient.Connect(Url, cred))
            {
                // if you want to put a retry policy in place, enable it here
                // the built-in policies are No Retry (default), Linear Retry, and Exponential Retry
                //client.CustomBehaviors.Add(new SetRetryPolicy(new Microsoft.Azure.Batch.Protocol.LinearRetry()));

                ListPools(client);
                ListWorkItems(client);

                CreatePoolIfNotExist(client, Program.PoolName);
                AddWork(client);

                ListPools(client);
                ListWorkItems(client);

                Console.WriteLine("Remember to delete the pool if you are done. Otherwise you'll still be charged for the running VM.");
                string line;
                while (true)
                {
                    Console.Write("Do you want to delete the pool? [y/N]:");
                    line = Console.ReadLine();
                    if (string.IsNullOrEmpty(line) || 0 == string.Compare(line, "n", true))
                    {
                        break;
                    }
                    else if (0 == string.Compare(line, "y", true))
                    {
                        DeletePool(client, Program.PoolName);
                        break;
                    }
                };
            }
        }

        private static void DeletePool(IBatchClient client, string poolName)
        {
            using (IPoolManager pm = client.OpenPoolManager())
            {
                try
                {
                    pm.DeletePool(PoolName);
                }
                catch (AggregateException ex)
                {
                    // Error happens when query job prep task info. Ignore JobPreparationTaskNotRunOnTVM
                    // sinc it means the task has not been run. Add the vm back to the query queue and continue.
                    // Other exception will be re-thrown.
                    ex.Handle((x) =>
                    {
                        if (x is BatchException)
                        {
                            BatchException be = x as BatchException;

                            if (null != be.RequestInformation
                            && null != be.RequestInformation.AzureError
                            && 0 == be.RequestInformation.AzureError.Code.CompareTo("PoolNotFound"))
                            {
                                return true;
                            }
                        }
                        return false;
                    });
                }
            }
        }

        private static void CreatePoolIfNotExist(IBatchClient client, string poolName)
        {
            // All Pool and VM operation starts from PoolManager
            using (IPoolManager pm = client.OpenPoolManager())
            {
                bool found = false;
                try
                {
                    ICloudPool p = pm.GetPool(poolName, new ODATADetailLevel(selectClause: "name"));
                    found = true;
                    if (!p.ListVMs(new ODATADetailLevel(selectClause:"name")).Any<IVM>())
                    {
                        Console.WriteLine("There are no VMs in this pool. No tasks will be run until at least one VM has been added via resizing.");
                        Console.WriteLine("Resizing pool to add 3 VMs. This might take a while...");
                        p.Resize(3);
                    }
                }
                catch (AggregateException ex)
                {
                    // Error happens when query job prep task info. Ignore JobPreparationTaskNotRunOnTVM
                    // sinc it means the task has not been run. Add the vm back to the query queue and continue.
                    // Other exception will be re-thrown.
                    ex.Handle((x) =>
                    {
                        if (x is BatchException)
                        {
                            BatchException be = x as BatchException;

                            if (null != be.RequestInformation
                            && null != be.RequestInformation.AzureError
                            && 0 == be.RequestInformation.AzureError.Code.CompareTo("PoolNotFound"))
                            {
                                return true;
                            }
                        }
                        return false;
                    });
                }

                if (!found)
                {
                    Console.WriteLine("Creating pool: {0}", poolName);
                    // if pool not found, call CreatePool
                    // You can learn more about os families and versions at:
                    // http://msdn.microsoft.com/en-us/library/azure/ee924680.aspx
                    ICloudPool pool = pm.CreatePool(poolName, targetDedicated: 5, vmSize: "small", osFamily: "3");
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
                IEnumerable<ICloudPool> pools = pm.ListPools(new ODATADetailLevel(selectClause: "name,state"));
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

        private static void Log(string msg, string vm = "")
        {
#if VERBOSE
            Console.WriteLine(DateTime.Now.ToString("HH:mm:ss.ffff ") + vm + " " + msg);
#endif
        }

        private static bool WaitAllJobPrepTask(
            IBatchClient client, 
            string pool, 
            string workitem, 
            string job, 
            Func<int, int, bool> stop, 
            Func<int, int, bool> succeed, 
            TimeSpan timeout,
            TimeSpan freq,
            int parallelism = 5)
        {
            using (IPoolManager pm = client.OpenPoolManager())
            {
                using (IWorkItemManager wm = client.OpenWorkItemManager())
                {

                    using (BlockingCollection<string> vms = new BlockingCollection<string>())
                    {
                        CancellationTokenSource cts = new CancellationTokenSource();
                        CancellationToken ct = cts.Token;

                        List<Task> tasks = new List<Task>();
                        int succeededJobPrepTasks = 0;
                        int failedJobPrepTasks = 0;

                        // find all VM in the Pool and put them in a queue
                        tasks.Add(Task.Run(async () =>
                        {
                            Log("Start adding vms to query queue.");
                            // list all vms and enqueue
                            ICloudPool p = pm.GetPool(PoolName);
                            var en = p.ListVMs(new ODATADetailLevel(selectClause: "name")).GetAsyncEnumerator();
                            while (await en.MoveNextAsync())
                            {
                                Log("Add vm to query queue.", en.Current.Name);
                                vms.Add(en.Current.Name);
                                if (ct.IsCancellationRequested)
                                    break;
                            }
                        }));

                        // launch N threads to get job prep task status of the nodes in the queue
                        for (int i = 0; i < parallelism; i++)
                        {
                            tasks.Add(Task.Run(() =>
                            {
                                while (!ct.IsCancellationRequested)
                                {
                                    string vm;
                                    try
                                    {
                                        vm = vms.Take(ct);
                                    }
                                    catch // if token cancelled, abort the thread
                                    {
                                        break;
                                    }
                                    Log("Start polling vm", vm);
                                    try
                                    {
                                        var state = pm.GetJobPreparationTaskStatus(pool, vm, workitem, job);
                                        Log("got vm state", vm);
                                        if (state.State == JobPreparationTaskState.Completed) // job prep task completed on the thread
                                        {
                                            if (state.ExitCode == 0)
                                                Interlocked.Increment(ref succeededJobPrepTasks);
                                            else
                                                Interlocked.Increment(ref failedJobPrepTasks);
                                            Log("==== job prep done ====", vm);
                                            if (stop(succeededJobPrepTasks, failedJobPrepTasks))
                                                cts.Cancel();
                                        }
                                        else // job prep still running
                                        {
                                            Log("State is not completed", vm);
                                            // Sleep for some time and put the VM back to the queue
                                            Task.Run(() =>
                                                {
                                                    Task.Delay(freq);
                                                    if (!ct.IsCancellationRequested)
                                                    {
                                                        Log("Add vm back to query queue", vm);
                                                        vms.Add(vm);
                                                    }
                                                });
                                        }
                                    }
                                    catch (AggregateException ex)
                                    {
                                        // Error happens when query job prep task info. Ignore JobPreparationTaskNotRunOnTVM
                                        // sinc it means the task has not been run. Add the vm back to the query queue and continue.
                                        // Other exception will be re-thrown.
                                        Log("Exception happend when querying prep task status.", vm);
                                        ex.Handle((x) =>
                                        {
                                            if (x is BatchException)
                                            {
                                                BatchException be = x as BatchException;

                                                if (null != be.RequestInformation
                                                && null != be.RequestInformation.AzureError
                                                && 0 == be.RequestInformation.AzureError.Code.CompareTo("JobPreparationTaskNotRunOnTVM"))
                                                {
                                                    Task.Run(() =>
                                                    {
                                                        Task.Delay(freq);
                                                        if (!ct.IsCancellationRequested)
                                                        {
                                                            Log("Add vm back to the query queue.", vm);
                                                            vms.Add(vm);
                                                        }
                                                    });
                                                    return true;
                                                }
                                            }

                                            return false;
                                        });
                                    }
                                }
                                Log("Thread cancel.");
                            }));
                        }
                        Task.WaitAll(tasks.ToArray(), timeout);
                        Log("All done.");
                        return succeed(succeededJobPrepTasks, failedJobPrepTasks);
                    }
                }
            }
        }

        private static void AddWork(IBatchClient client)
        {
            using (IPoolManager pm = client.OpenPoolManager())
            {
                using (IWorkItemManager wm = client.OpenWorkItemManager())
                {
                    //The toolbox contains some helper mechanisms to ease submission and monitoring of tasks.
                    IToolbox toolbox = client.OpenToolbox();

                    //Create a work item
                    string workItemName = Environment.GetEnvironmentVariable("USERNAME") + DateTime.Now.ToString("yyyyMMdd-HHmmss");
                    Console.WriteLine("Creating work item {0}", workItemName);
                    ICloudWorkItem cloudWorkItem = wm.CreateWorkItem(workItemName);
                    cloudWorkItem.JobExecutionEnvironment = new JobExecutionEnvironment() { PoolName = PoolName }; //Specify the pool to run on
                    cloudWorkItem.JobSpecification = new JobSpecification();
                    cloudWorkItem.JobSpecification.JobPreparationTask = new JobPreparationTask();
                    cloudWorkItem.JobSpecification.JobPreparationTask.Name = "jobprep";
                    cloudWorkItem.JobSpecification.JobPreparationTask.CommandLine = "cmd /c ping 127.0.0.1";
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
                            throw new TimeoutException("Timed out waiting for job.");
                        }
                        Thread.Sleep(TimeSpan.FromSeconds(5));
                    }

                    string jobName = cloudWorkItem.ExecutionInformation.RecentJob.Name;
                    Console.WriteLine("Found job {0}. Adding task objects...", jobName);
                    ICloudJob job = wm.GetJob(workItemName, jobName);

                    job.AddTask(new CloudTask("task1", "hostname"));
                    job.AddTask(new CloudTask("task2", "cmd /c dir /s ..\\.."));

                    bool haveEnoughSuccessJobPrepTask = WaitAllJobPrepTask(
                        client,
                        PoolName,
                        workItemName,
                        jobName,
                        (succ, fail) => { return succ >= 2 || fail > 0; }, // stop indicator
                        (succ, fail) => { return succ >= 2; }, // success indicator
                        timeout: TimeSpan.FromMinutes(5),
                        freq: TimeSpan.FromSeconds(5),
                        parallelism: 10
                        );

                    if (haveEnoughSuccessJobPrepTask) // got enough nodes, continue
                    {
                        //We use the task state monitor to monitor the state of our tasks -- in this case we will wait for them all to complete.
                        ITaskStateMonitor taskStateMonitor = toolbox.CreateTaskStateMonitor();

                        // blocking wait on the list of tasks until all tasks reach completed state
                        bool timedOut = taskStateMonitor.WaitAll(job.ListTasks(), TaskState.Completed, TimeSpan.FromMinutes(20));

                        if (timedOut)
                        {
                            throw new TimeoutException("Timed out waiting for tasks");
                        }

                        // dump task output
                        foreach (var t in job.ListTasks())
                        {
                            Console.WriteLine("Task " + t.Name + " output:\n" + t.GetTaskFile(Constants.StandardOutFileName).ReadAsString());
                        }
                    }
                    else // too much failure, abort
                    {
                        Console.WriteLine("too much failure, abort.");
                    }

                    // remember to delete the workitem before exiting
                    Console.WriteLine("Deleting work item: {0}", workItemName);
                    wm.DeleteWorkItem(workItemName);
                }
            }
        }
    }
}
