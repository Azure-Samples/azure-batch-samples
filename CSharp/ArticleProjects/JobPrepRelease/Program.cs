// Copyright (c) Microsoft Corporation
//
// Companion project to the following article:
// https://azure.microsoft.com/documentation/articles/batch-job-prep-release/

namespace Microsoft.Azure.Batch.Samples.Articles.JobPrepRelease
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Batch;
    using Microsoft.Azure.Batch.Auth;
    using Microsoft.Azure.Batch.Samples.Common;
    using Microsoft.Azure.Batch.Common;

    public class Program
    {
        public static void Main(string[] args)
        {
            try
            {
                // Call the asynchronous version of the Main() method. This is done so that we can await various
                // calls to async methods within the "Main" method of this console application.
                MainAsync(args).Wait();
            }
            catch (AggregateException ae)
            {
                Console.WriteLine();
                Console.WriteLine("One or more exceptions occurred.");
                Console.WriteLine();

                SampleHelpers.PrintAggregateException(ae.Flatten());
            }
            finally
            {
                Console.WriteLine();
                Console.WriteLine("Sample complete, hit ENTER to exit...");
                Console.ReadLine();
            }
        }

        private static async Task MainAsync(string[] args)
        {
            const string poolId = "JobPrepReleaseSamplePool";
            const string jobId  = "JobPrepReleaseSampleJob";

            // Location of the file that the job tasks will work with, a text file in the
            // node's "shared" directory.
            const string taskOutputFile = "%AZ_BATCH_NODE_SHARED_DIR%\\job_prep_and_release.txt";

            // The job prep task will write the node ID to the text file in the shared directory
            const string jobPrepCmdLine = "cmd /c echo %AZ_BATCH_NODE_ID% tasks: >" + taskOutputFile;

            // Each task then echoes its ID to the same text file
            const string taskCmdLine = "cmd /c echo   %AZ_BATCH_TASK_ID% >> " + taskOutputFile;

            // The job release task will then delete the text file from the shared directory
            const string jobReleaseCmdLine = "cmd /c del " + taskOutputFile;

            var accountSettings = SampleHelpers.LoadAccountSettings();

            // Configure your AccountSettings in the Microsoft.Azure.Batch.Samples.Common project within this solution
            BatchSharedKeyCredentials cred = new BatchSharedKeyCredentials(
                accountSettings.BatchServiceUrl,
                accountSettings.BatchAccountName,
                accountSettings.BatchAccountKey);

            // Initialize the BatchClient for access to your Batch account
            using (BatchClient batchClient = BatchClient.Open(cred))
            {
                // Create a CloudPool (or obtain an existing pool with the specified ID)
                CloudPool pool = await ArticleHelpers.CreatePoolIfNotExistAsync(
                    batchClient,
                    poolId,
                    "standard_d1_v2",
                    2,
                    1);
                
                // Create a CloudJob (or obtain an existing job with the specified ID)
                CloudJob job = await SampleHelpers.GetJobIfExistAsync(batchClient, jobId);
                if (job == null)
                {
                    Console.WriteLine("Job {0} not found, creating...", jobId);

                    CloudJob unboundJob = batchClient.JobOperations.CreateJob(jobId, new PoolInformation() { PoolId = poolId });

                    // Configure and assign the job preparation task
                    unboundJob.JobPreparationTask = new JobPreparationTask { CommandLine = jobPrepCmdLine };

                    // Configure and assign the job release task
                    unboundJob.JobReleaseTask = new JobReleaseTask { CommandLine = jobReleaseCmdLine };

                    await unboundJob.CommitAsync();

                    // Get the bound version of the job with all of its properties populated
                    job = await batchClient.JobOperations.GetJobAsync(jobId);
                }
                
                // Create the tasks that the job will execute
                List<CloudTask> tasks = new List<CloudTask>();
                for (int i = 1; i <= 8; i++)
                {
                    string taskId = "task" + i.ToString().PadLeft(3, '0');
                    string taskCommandLine = taskCmdLine;
                    CloudTask task = new CloudTask(taskId, taskCommandLine);
                    tasks.Add(task);
                }

                // Add the tasks in one API call as opposed to a separate AddTask call for each. Bulk task
                // submission helps to ensure efficient underlying API calls to the Batch service.
                Console.WriteLine("Submitting tasks and awaiting completion...");
                await batchClient.JobOperations.AddTaskAsync(job.Id, tasks);

                // Wait for the tasks to complete before proceeding. The long timeout here is to allow time
                // for the nodes within the pool to be created and started if the pool had not yet been created.
                await batchClient.Utilities.CreateTaskStateMonitor().WhenAll(
                    job.ListTasks(),
                    TaskState.Completed,
                    TimeSpan.FromMinutes(30));

                Console.WriteLine("All tasks completed.");
                Console.WriteLine();

                // Print the contents of the shared text file modified by the job preparation and other tasks.
                ODATADetailLevel nodeDetail = new ODATADetailLevel(selectClause: "id, state");
                IPagedEnumerable<ComputeNode> nodes = batchClient.PoolOperations.ListComputeNodes(pool.Id, nodeDetail);
                await nodes.ForEachAsync(async (node) =>
                {
                    // Check to ensure that the node is Idle before attempting to pull the text file.
                    // If the pool was just created, there is a chance that another node completed all
                    // of the tasks prior to the other node(s) completing their startup procedure.
                    if (node.State == ComputeNodeState.Idle)
                    {
                        NodeFile sharedTextFile = await node.GetNodeFileAsync("shared\\job_prep_and_release.txt");
                        Console.WriteLine("Contents of {0} on {1}:", sharedTextFile.Path, node.Id);
                        Console.WriteLine("-------------------------------------------");
                        Console.WriteLine(await sharedTextFile.ReadAsStringAsync());
                    }
                });

                // Terminate the job to mark it as Completed; this will initiate the Job Release Task on any node
                // that executed job tasks. Note that the Job Release Task is also executed when a job is deleted,
                // thus you need not call Terminate if you typically delete your jobs upon task completion.
                await batchClient.JobOperations.TerminateJobAsync(job.Id);

                // Wait for the job to reach state "Completed." Note that this wait is not typically necessary in
                // production code, but is done here to enable the checking of the release tasks exit code below.
                await ArticleHelpers.WaitForJobToReachStateAsync(batchClient, job.Id, JobState.Completed, TimeSpan.FromMinutes(2));

                // Print the exit codes of the prep and release tasks by obtaining their execution info
                List<JobPreparationAndReleaseTaskExecutionInformation> prepReleaseInfo = await batchClient.JobOperations.ListJobPreparationAndReleaseTaskStatus(job.Id).ToListAsync();
                foreach (JobPreparationAndReleaseTaskExecutionInformation info in prepReleaseInfo)
                {
                    Console.WriteLine();
                    Console.WriteLine("{0}: ", info.ComputeNodeId);

                    // If no tasks were scheduled to run on the node, the JobPreparationTaskExecutionInformation will be null
                    if (info.JobPreparationTaskExecutionInformation != null)
                    {
                        Console.WriteLine("  Prep task exit code:    {0}", info.JobPreparationTaskExecutionInformation.ExitCode);
                    }

                    // If no tasks were scheduled to run on the node, the JobReleaseTaskExecutionInformation will be null
                    if (info.JobReleaseTaskExecutionInformation != null)
                    {
                        Console.WriteLine("  Release task exit code: {0}", info.JobReleaseTaskExecutionInformation.ExitCode);
                    }
                }

                // Clean up the resources we've created in the Batch account
                Console.WriteLine();
                Console.WriteLine("Delete job? [yes] no");
                string response = Console.ReadLine().ToLower();
                if (response != "n" && response != "no")
                {
                    // Note that deleting the job will execute the job release task if the job was not previously terminated
                    await batchClient.JobOperations.DeleteJobAsync(job.Id);
                }

                Console.WriteLine("Delete pool? [yes] no");
                response = Console.ReadLine();
                if (response != "n" && response != "no")
                {
                    await batchClient.PoolOperations.DeletePoolAsync(pool.Id);
                }
            }
        }
    }
}
