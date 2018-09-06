// Copyright (c) Microsoft Corporation
//
// Companion project to the following article:
// https://azure.microsoft.com/documentation/articles/batch-efficient-list-queries/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Batch;
using Microsoft.Azure.Batch.Auth;
using Microsoft.Azure.Batch.Common;
using Microsoft.Azure.Batch.Samples.Common;

namespace Microsoft.Azure.Batch.Samples.Articles.EfficientListQueries
{
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
            // You may adjust these values to experiment with different compute resource scenarios.
            const string nodeSize     = "standard_d1_v2";
            const int nodeCount       = 1;
            const int maxTasksPerNode = 4;

            // Adjust the task count to experiment with different list operation query durations
            const int taskCount = 5000;

            const string poolId = "EfficientListQueriesSamplePool";
            const string jobId  = "EfficientListQueriesSampleJob";

            var accountSettings = SampleHelpers.LoadAccountSettings();

            // Set up the credentials required by the BatchClient. Configure your AccountSettings in the
            // Microsoft.Azure.Batch.Samples.Common project within this solution.
            BatchSharedKeyCredentials cred = new BatchSharedKeyCredentials(
                accountSettings.BatchServiceUrl,
                accountSettings.BatchAccountName,
                accountSettings.BatchAccountKey);

            using (BatchClient batchClient = BatchClient.Open(cred))
            {
                // Create a CloudPool, or obtain an existing pool with the specified ID
                CloudPool pool = await ArticleHelpers.CreatePoolIfNotExistAsync(
                    batchClient,
                    poolId,
                    nodeSize,
                    nodeCount,
                    maxTasksPerNode);
                
                // Create a CloudJob, or obtain an existing job with the specified ID
                CloudJob job = await ArticleHelpers.CreateJobIfNotExistAsync(batchClient, poolId, jobId);

                // Configure the tasks we'll be querying. Each task simply echoes the node's
                // name and then exits. We create "large" tasks by setting an environment
                // variable for each that is 2048 bytes in size. This is done simply to
                // increase response time when querying the batch service to more clearly
                // demonstrate query durations.
                List<CloudTask> tasks = new List<CloudTask>();
                List<EnvironmentSetting> environmentSettings = new List<EnvironmentSetting>();
                environmentSettings.Add(new EnvironmentSetting("BIGENV", GetBigString(2048)));
                for (int i = 1; i < taskCount + 1; i++)
                {
                    string taskId = "task" + i.ToString().PadLeft(5, '0');
                    string taskCommandLine = "cmd /c echo %COMPUTERNAME%";
                    CloudTask task = new CloudTask(taskId, taskCommandLine);
                    task.EnvironmentSettings = environmentSettings;
                    tasks.Add(task);
                }

                Console.WriteLine();
                Console.WriteLine("Adding {0} tasks to job {1}...", taskCount, job.Id);

                Stopwatch stopwatch = Stopwatch.StartNew();

                // Add the tasks in one API call as opposed to a separate AddTask call for each. Bulk task submission
                // helps to ensure efficient underlying API calls to the Batch service.
                await batchClient.JobOperations.AddTaskAsync(job.Id, tasks);

                stopwatch.Stop();
                Console.WriteLine("{0} tasks added in {1}, hit ENTER to query tasks...", taskCount, stopwatch.Elapsed);
                Console.ReadLine();
                Console.WriteLine();
                stopwatch.Reset();

                // Obtain the tasks, specifying different detail levels to demonstrate limiting the number of tasks returned
                // and the amount of data returned for each. If your job tasks number in the thousands or have "large" properties
                // (such as our big environment variable), specifying a DetailLevel is important in reducing the amount of data
                // transferred, lowering your query response times (potentially greatly).

                // Get a subset of the tasks based on different task states
                ODATADetailLevel detail = new ODATADetailLevel();
                detail.FilterClause = "state eq 'active'";
                detail.SelectClause = "id,state";
                await QueryTasksAsync(batchClient, job.Id, detail);
                detail.FilterClause = "state eq 'running'";
                await QueryTasksAsync(batchClient, job.Id, detail);
                detail.FilterClause = "state eq 'completed'";
                await QueryTasksAsync(batchClient, job.Id, detail);

                // Get all tasks, but limit the properties returned to task id and state only
                detail.FilterClause = null;
                detail.SelectClause = "id,state";
                await QueryTasksAsync(batchClient, job.Id, detail);

                // Get all tasks, include id and state, also include the inflated environment settings property
                detail.SelectClause = "id,state,environmentSettings";
                await QueryTasksAsync(batchClient, job.Id, detail);

                // Get all tasks, include all standard properties, and expand the statistics
                detail.ExpandClause = "stats";
                detail.SelectClause = null;
                await QueryTasksAsync(batchClient, job.Id, detail);

                Console.WriteLine();
                Console.WriteLine("Done!");
                Console.WriteLine();

                // Clean up the resources we've created in the Batch account
                Console.WriteLine("Delete job? [yes] no");
                string response = Console.ReadLine().ToLower();
                if (response != "n" && response != "no")
                {
                    await batchClient.JobOperations.DeleteJobAsync(job.Id);
                }

                Console.WriteLine("Delete pool? [yes] no");
                response = Console.ReadLine().ToLower();
                if (response != "n" && response != "no")
                {
                    await batchClient.PoolOperations.DeletePoolAsync(pool.Id);
                }
            }
        }

        /// <summary>
        /// Returns a string of the specified number of bytes. The string is comprised solely of repeated instances of the 'a' character.
        /// </summary>
        /// <param name="size">The byte length of the string to obtain.</param>
        /// <returns>A string of the specified size.</returns>
        private static string GetBigString(int size)
        {
            StringBuilder bigSB = new StringBuilder(size);

            for (int i = 0; i < size; i++)
            {
                bigSB.Append('a');
            }

            return bigSB.ToString();
        }

        /// <summary>
        /// Queries and prints task information for the specified job.
        /// </summary>
        /// <param name="batchClient">A fully initialized <see cref="BatchClient"/>.</param>
        /// <param name="jobId">The ID of the job whose tasks should be queried.</param>
        /// <param name="detail">An <see cref="ODATADetailLevel"/> configured with one or more of expand, filter, select clauses.</param>
        /// <returns>A <see cref="System.Threading.Tasks.Task"/> object that represents the asynchronous operation.</returns>
        private static async Task QueryTasksAsync(BatchClient batchClient, string jobId, ODATADetailLevel detail)
        {
            List<CloudTask> taskList = new List<CloudTask>();

            Stopwatch stopwatch = Stopwatch.StartNew();

            taskList.AddRange(await batchClient.JobOperations.ListTasks(jobId, detail).ToListAsync());
            
            stopwatch.Stop();
            
            Console.WriteLine("{0} tasks retrieved in {1} (ExpandClause: {2} | FilterClause: {3} | SelectClause: {4})",
                taskList.Count,
                stopwatch.Elapsed,
                detail.ExpandClause,
                detail.FilterClause,
                detail.SelectClause);
        }
    }
}
