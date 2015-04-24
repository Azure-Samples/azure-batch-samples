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


namespace JobPrep
{
    public class Program
    {
        private const string Url = "https://batch.core.windows.net";

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

            if (File.Exists("..\\..\\batchcred.txt"))
            {
                string[] lines = File.ReadAllLines("..\\..\\batchcred.txt");
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

                        if (!p.ListVMs().Any<IVM>())
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
                    ICloudPool pool = pm.CreatePool(poolName, targetDedicated: 20, vmSize: "small", osFamily: "3");
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

        private static async Task<bool> PollJobPrepTaskAsync(IPoolManager pm, string pool, string vm, string workitem, string job, int sleep)
        {
            while(true)
            {
                try
                {
                    Console.WriteLine("polling prep task on " + vm);
                    var preptask = await pm.GetJobPreparationTaskStatusAsync(pool, vm, workitem, job);
                    if (preptask.State == JobPreparationTaskState.Completed)
                        return preptask.ExitCode == 0;
                    Console.WriteLine("sleep a sec");
                    await Task.Delay(sleep);
                }
                catch (AggregateException) { // ignore not found exception
                    Console.WriteLine("caught exception in async method");
                }
            }
        }

        private static void Log(string msg, string vm = "")
        {
#if VERBOSE
            Console.WriteLine(DateTime.Now.ToString("HH:mm:ss.ffff ") + vm + " " + msg);
#endif
        }

        private static bool WaitAllJobPrepTask(IBatchClient client, string pool, string workitem, string job, Func<int, int, bool> stop, Func<int, int, bool> succeed, int timeout = 60*1000, int freq = 2000, int parallelism = 5)
        {
            using (IPoolManager pm = client.OpenPoolManager())
            {
                using (IWorkItemManager wm = client.OpenWorkItemManager())
                {

                    CancellationTokenSource cts = new CancellationTokenSource();
                    CancellationToken ct = cts.Token;

                    BlockingCollection<string> vms = new BlockingCollection<string>();
                    List<Task> tasks = new List<Task>();
                    int nb_succeeded = 0;
                    int nb_failed = 0;

                    // find all VM in the Pool and put them in a queue
                    Log("add vms");
                    tasks.Add(Task.Run(async () =>
                    {
                        Log("start adding vms");
                        // list all vms and enqueue
                        ICloudPool p = pm.GetPool(PoolName);
                        var en = p.ListVMs(new ODATADetailLevel(selectClause: "name")).GetAsyncEnumerator();
                        while(await en.MoveNextAsync())
                        {
                            Log("add vm", en.Current.Name);
                            vms.Add(en.Current.Name);
                            if (ct.IsCancellationRequested)
                                break;
                        }
                    }));

                    // launch N threads to get job prep task status of the nodes in the queue
                    for (int i = 0; i < parallelism; i++)
                        tasks.Add(Task.Run(() =>
                        {
                            while (!ct.IsCancellationRequested)
                            {
                                string vm;
                                try {
                                    vm = vms.Take(ct);
                                } catch // if token cancelled, abort the thread
                                {
                                    break;
                                }
                                Log("start polling vm", vm);
                                try
                                {
                                    var state = pm.GetJobPreparationTaskStatus(pool, vm, workitem, job);
                                    Log("got vm state", vm);
                                    if (state.State == JobPreparationTaskState.Completed) // job prep task completed on the thread
                                    {
                                        if (state.ExitCode == 0)
                                            Interlocked.Increment(ref nb_succeeded);
                                        else
                                            Interlocked.Increment(ref nb_failed);
                                        Log("==== job prep done ====", vm);
                                        if (stop(nb_succeeded, nb_failed))
                                            cts.Cancel();
                                    }
                                    else // job prep still running
                                    {
                                        Log("state not completed", vm);
                                        // Sleep for some time and put the VM back to the queue
                                        Task.Run(() => { Task.Delay(freq); if (!ct.IsCancellationRequested) { Log("add vm back", vm); vms.Add(vm); } });
                                    }
                                }
                                catch // job prep task status not found - not run on this VM
                                {
                                    Log("error query", vm);
                                    Task.Run(() => {Task.Delay(freq); if (!ct.IsCancellationRequested) { Log("add vm back", vm); vms.Add(vm); } });
                                }
                            }
                            Log("cancel");
                        }));
                    Task.WaitAll(tasks.ToArray(), timeout);
                    Log("all done");
                    return succeed(nb_succeeded, nb_failed);
                }
            }
        }

        private static void AddWork(IBatchClient client)
        {
            using (IPoolManager pm = client.OpenPoolManager()) {
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
                            throw new Exception("Timed out waiting for job.");
                        }
                        Thread.Sleep(TimeSpan.FromSeconds(5));
                    }

                    string jobName = cloudWorkItem.ExecutionInformation.RecentJob.Name;
                    Console.WriteLine("Found job {0}. Adding task objects...", jobName);
                    ICloudJob job = wm.GetJob(workItemName, jobName);

                    job.AddTask(new CloudTask("task1", "hostname"));
                    job.AddTask(new CloudTask("task2", "cmd /c dir /s ..\\.."));

                    bool cont = WaitAllJobPrepTask(
                        client,
                        PoolName,
                        workItemName,
                        jobName,
                        (succ, fail) => { return succ >= 2 || fail > 0; }, // stop indicator
                        (succ, fail) => { return succ >= 2; }, // succeess indicator
                        timeout: 60*1000,
                        freq: 5*1000,
                        parallelism: 10
                        );

                    if (cont) // got enough nodes, continue
                    {
                        //We use the task state monitor to monitor the state of our tasks -- in this case we will wait for them all to complete.
                        ITaskStateMonitor taskStateMonitor = toolbox.CreateTaskStateMonitor();

                        // blocking wait on the list of tasks until all tasks reach completed state
                        bool timedOut = taskStateMonitor.WaitAll(job.ListTasks(), TaskState.Completed, new TimeSpan(0, 20, 0));

                        if (timedOut)
                        {
                            throw new TimeoutException("Timed out waiting for tasks");
                        }

                        // dump task output
                        foreach (var t in job.ListTasks())
                        {
                            Console.WriteLine("Task " + t.Name + " says:\n" + t.GetTaskFile(Constants.StandardOutFileName).ReadAsString());
                        }
                    } else // too much failure, abort
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
