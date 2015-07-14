using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Azure.Batch;
using Microsoft.Azure.Batch.Auth;
using Microsoft.Azure.Batch.Common;
using Microsoft.Azure.Batch.Protocol;

namespace Azure.Batch.SDK.Samples.JobScheduling.JobMgr
{
    class Program
    {
        static void Main(string[] args)
        {
            // open a file for tracing
            var traceFile = File.Open(SampleConstants.JMTraceFile, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.ReadWrite);
            var traceWriter = new StreamWriter(traceFile);
            traceWriter.AutoFlush = true;

            try
            {
                // pick up hints from the environment as populated by the program run on the client machine
                var workItemName = Environment.GetEnvironmentVariable(SampleConstants.EnvWorkItemName);
                if (workItemName == null)
                {
                    traceWriter.WriteLine("Failed to get work item name from environment");
                    return;
                }
                else
                {
                    traceWriter.WriteLine("Our WI Name: " + workItemName);
                }

                BatchCredentials credentials = new BatchCredentials(
                    Environment.GetEnvironmentVariable(SampleConstants.EnvWataskAccountName), // Some basic useful elements are preset as environment variables.
                    Environment.GetEnvironmentVariable(SampleConstants.EnvBatchAccountKeyName)
                    );

                var bClient = BatchClient.Connect(SampleConstants.BatchSvcEndpoint, credentials);
                bClient.CustomBehaviors.Add(new SetRetryPolicy(new ExponentialRetry(TimeSpan.FromSeconds(5), 5)));

                using (var wiMgr = bClient.OpenWorkItemManager())
                {
                    var ourWorkItem = wiMgr.GetWorkItem(workItemName);

                    // Since is this a job manager task, its current job will be the most recent job. This is not
                    // true for non-JM tasks since the most recent job could have run a while ago.
                    string jobName;
                    if (ourWorkItem.ExecutionInformation != null && ourWorkItem.ExecutionInformation.RecentJob != null)
                    {
                        jobName = ourWorkItem.ExecutionInformation.RecentJob.Name;
                        traceWriter.WriteLine("Our job name: " + jobName);
                    }
                    else
                    {
                        traceWriter.WriteLine("Failed to get jobname from workitem - exiting");
                        return;
                    }

                    // Schedule tasks on the job.  Ping is being used as the basic task
                    List<CloudTask> tasksToAdd = new List<CloudTask>();

                    for (int i = 0; i < 5; i++)
                    {
                        string taskName = "pingTask" + i;
                        var task = new CloudTask(taskName, "ping 127.0.0.1 -n 8");
                        traceWriter.WriteLine("Adding task: " + taskName);
                        tasksToAdd.Add(task);
                    }

                    wiMgr.AddTask(workItemName, jobName, tasksToAdd);

                    // Monitor the current jobs to see when they are done, and then exit the job manager.  Monitoring the tasks
                    // for completion is necessary if you are using KillJobOnCompletion = TRUE, as otherwise when the job manager
                    // exits it will kill all of the tasks that still running.
                    //
                    // Occasionally a task may get killed and requeued during an upgrade or hardware failure, including the job manager
                    // task.  The job manager will be re-run in this case.  Robustness against this was not added into the sample for 
                    // simplicity, but should be added into any production code.
                    while (true)
                    {
                        // Get list of current tasks in queue. Total count includes the job manager so when
                        // we get to one left then we can exit.
                        var taskList = wiMgr.ListTasks(workItemName, jobName);
                        var tasksStillRunning = taskList.Count(task => task.State != TaskState.Completed);
                        if (tasksStillRunning > 1)
                        {
                            traceWriter.WriteLine("{0} tasks still running", tasksStillRunning);
                            Thread.Sleep(TimeSpan.FromSeconds(5));
                        }
                        else
                        {
                            break;
                        }
                    }

                    traceWriter.WriteLine("All done!");
                }
            }
            catch (Exception e)
            {
                traceWriter.WriteLine("Exception:");
                traceWriter.WriteLine(e.ToString());
            }
            finally
            {
                traceWriter.Flush();
                traceFile.Close();
            }
        }
    }
}
