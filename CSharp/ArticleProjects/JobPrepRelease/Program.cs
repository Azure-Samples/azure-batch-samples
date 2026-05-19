// Copyright (c) Microsoft Corporation
//
// Companion project to the following article:
// https://azure.microsoft.com/documentation/articles/batch-job-prep-release/

namespace Microsoft.Azure.Batch.Samples.Articles.JobPrepRelease
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using global::Azure;
    using global::Azure.Compute.Batch;
    using global::Azure.ResourceManager.Batch;
    using Microsoft.Azure.Batch.Samples.Common;

    public class Program
    {
        public static async Task Main(string[] args)
        {
            try
            {
                await RunAsync(args);
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine("An exception occurred:");
                Console.WriteLine();
                Console.WriteLine(ex);
            }
            finally
            {
                Console.WriteLine();
                Console.WriteLine("Sample complete, hit ENTER to exit...");
                Console.ReadLine();
            }
        }

        private static async Task RunAsync(string[] args)
        {
            const string poolId = "JobPrepReleaseSamplePool";
            const string jobId  = "JobPrepReleaseSampleJob";

            const string taskOutputFile = "%AZ_BATCH_NODE_SHARED_DIR%\\job_prep_and_release.txt";
            const string jobPrepCmdLine = "cmd /c echo %AZ_BATCH_NODE_ID% tasks: >" + taskOutputFile;
            const string taskCmdLine = "cmd /c echo   %AZ_BATCH_TASK_ID% >> " + taskOutputFile;
            const string jobReleaseCmdLine = "cmd /c del " + taskOutputFile;

            var accountSettings = SampleHelpers.LoadAccountSettings();

            BatchClient batchClient = ClientFactory.CreateBatchClient(accountSettings);
            BatchAccountResource batchAccount = SampleHelpers.GetBatchAccountResource(accountSettings);

            // Create (or get) the pool via ARM.
            BatchAccountPoolResource pool = await ArticleHelpers.CreatePoolIfNotExistAsync(
                batchAccount,
                poolId,
                "standard_d2_v3",
                2,
                1);

            // Create the job (with prep + release tasks) if it does not yet exist.
            BatchJob job = await SampleHelpers.GetJobIfExistAsync(batchClient, jobId);
            if (job == null)
            {
                Console.WriteLine("Job {0} not found, creating...", jobId);

                var jobOptions = new BatchJobCreateOptions(jobId, new BatchPoolInfo { PoolId = poolId })
                {
                    JobPreparationTask = new BatchJobPreparationTask(jobPrepCmdLine),
                    JobReleaseTask = new BatchJobReleaseTask(jobReleaseCmdLine),
                };

                await batchClient.CreateJobAsync(jobOptions);
                job = await batchClient.GetJobAsync(jobId);
            }

            // Submit eight tasks; each appends its task ID to the shared text file.
            var tasks = new List<BatchTaskCreateOptions>();
            for (int i = 1; i <= 8; i++)
            {
                string taskId = "task" + i.ToString().PadLeft(3, '0');
                tasks.Add(new BatchTaskCreateOptions(taskId, taskCmdLine));
            }

            Console.WriteLine("Submitting tasks and awaiting completion...");
            await batchClient.CreateTaskCollectionAsync(job.Id, new BatchTaskGroup(tasks));

            // Wait for every task to reach the Completed state.
            await WaitForAllTasksCompletedAsync(batchClient, job.Id, TimeSpan.FromMinutes(30));

            Console.WriteLine("All tasks completed.");
            Console.WriteLine();

            // Print the shared text file from each idle node in the pool.
            await foreach (BatchNode node in batchClient.GetNodesAsync(poolId, select: new[] { "id", "state" }))
            {
                if (node.State == BatchNodeState.Idle)
                {
                    BinaryData fileData = await batchClient.GetNodeFileAsync(poolId, node.Id, "shared\\job_prep_and_release.txt");
                    Console.WriteLine("Contents of shared\\job_prep_and_release.txt on {0}:", node.Id);
                    Console.WriteLine("-------------------------------------------");
                    Console.WriteLine(fileData.ToString());
                }
            }

            // Terminate the job to mark it as Completed; this triggers the job release task on every
            // node that ran job tasks. Note that the job release task is also executed when a job is
            // deleted, so you do not need to call Terminate if you typically delete your jobs upon task
            // completion.
            await batchClient.TerminateJobAsync(WaitUntil.Started, job.Id);

            // Wait for the job to reach Completed. This wait is not typically necessary in production
            // code, but is done here to enable the checking of the release task exit codes below.
            await ArticleHelpers.WaitForJobToReachStateAsync(batchClient, job.Id, BatchJobState.Completed, TimeSpan.FromMinutes(2));

            // Print prep / release task exit codes per node.
            await foreach (BatchJobPreparationAndReleaseTaskStatus info in batchClient.GetJobPreparationAndReleaseTaskStatusesAsync(job.Id))
            {
                Console.WriteLine();
                Console.WriteLine("{0}: ", info.NodeId);

                if (info.JobPreparationTaskExecutionInfo != null)
                {
                    Console.WriteLine("  Prep task exit code:    {0}", info.JobPreparationTaskExecutionInfo.ExitCode);
                }

                if (info.JobReleaseTaskExecutionInfo != null)
                {
                    Console.WriteLine("  Release task exit code: {0}", info.JobReleaseTaskExecutionInfo.ExitCode);
                }
            }

            // Clean up.
            Console.WriteLine();
            Console.WriteLine("Delete job? [yes] no");
            string response = Console.ReadLine().ToLower();
            if (response != "n" && response != "no")
            {
                await batchClient.DeleteJobAsync(WaitUntil.Started, job.Id);
            }

            Console.WriteLine("Delete pool? [yes] no");
            response = Console.ReadLine();
            if (response != "n" && response != "no")
            {
                await pool.DeleteAsync(WaitUntil.Started);
            }
        }

        private static async Task WaitForAllTasksCompletedAsync(BatchClient batchClient, string jobId, TimeSpan timeout)
        {
            DateTime timeoutAt = DateTime.UtcNow.Add(timeout);
            string[] select = new[] { "id", "state" };

            while (true)
            {
                bool allCompleted = true;
                bool anyTasks = false;

                await foreach (BatchTask task in batchClient.GetTasksAsync(jobId, select: select))
                {
                    anyTasks = true;
                    if (task.State != BatchTaskState.Completed)
                    {
                        allCompleted = false;
                        break;
                    }
                }

                if (anyTasks && allCompleted)
                {
                    return;
                }

                if (DateTime.UtcNow > timeoutAt)
                {
                    throw new TimeoutException($"Timed out waiting for tasks in job {jobId} to complete.");
                }

                await Task.Delay(TimeSpan.FromSeconds(15));
            }
        }
    }
}
