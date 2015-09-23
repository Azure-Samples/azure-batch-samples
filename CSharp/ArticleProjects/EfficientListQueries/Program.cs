// Copyright (c) Microsoft Corporation
//
// Companion project backing the code snippets found in the following article:
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
            const int taskCount = 5000;

            const string poolId = "poolEffQuery";
            const string jobId  = "jobEffQuery";

            // Set up the credentials required by the BatchClient. Configure your AccountSettings in the
            // Microsoft.Azure.Batch.Samples.Common project within this solution.
            BatchSharedKeyCredentials cred = new BatchSharedKeyCredentials(AccountSettings.Default.BatchServiceUrl,
                                                                           AccountSettings.Default.BatchAccountName,
                                                                           AccountSettings.Default.BatchAccountKey);
            
            using (BatchClient batchClient = BatchClient.Open(cred))
            {
                // Create a CloudPool, or obtain an existing pool with the specified ID
                CreatePool(batchClient, poolId).Wait();
                CloudPool pool = batchClient.PoolOperations.GetPool(poolId);

                // Create a CloudJob, or obtain an existing job with the specified ID
                CloudJob job = CreateJob(batchClient, pool.Id, jobId);
                
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

                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();

                // To reduce the chances of hitting Batch service throttling limits, we add the tasks in
                // one API call as opposed to a separate AddTask call for each. This is crucial if you
                // are adding many tasks to your jobs.
                batchClient.JobOperations.AddTask(job.Id, tasks);

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
                QueryTasks(batchClient, job.Id, detail);
                detail.FilterClause = "state eq 'running'";
                QueryTasks(batchClient, job.Id, detail);
                detail.FilterClause = "state eq 'completed'";
                QueryTasks(batchClient, job.Id, detail);

                // Get all tasks, but limit the properties returned to task id and state only
                detail.FilterClause = null;
                detail.SelectClause = "id,state";
                QueryTasks(batchClient, job.Id, detail);

                // Get all tasks, include id and state, also include the inflated environment settings property
                detail.SelectClause = "id,state,environmentSettings";
                QueryTasks(batchClient, job.Id, detail);

                // Get all tasks, include all standard properties, and expand the statistics
                detail.ExpandClause = "stats";
                detail.SelectClause = null;
                QueryTasks(batchClient, job.Id, detail);

                Console.WriteLine();
                Console.WriteLine("Sample complete, hit ENTER to continue...");
                Console.ReadLine();

                // Clean up the resources we've created in the Batch account
                Console.WriteLine("Delete job? [yes] no");
                string response = Console.ReadLine().ToLower();
                if (response != "n" && response != "no")
                {
                    batchClient.JobOperations.DeleteJob(job.Id);
                }

                Console.WriteLine("Delete pool? [yes] no");
                response = Console.ReadLine().ToLower();
                if (response != "n" && response != "no")
                {
                    batchClient.PoolOperations.DeletePool(pool.Id);
                }
            }
        }

        /// <summary>
        /// Creates a CloudPool with a single compute node associated with the Batch account.
        /// </summary>
        /// <param name="batchClient">A fully initialized <see cref="BatchClient"/>.</param>
        /// <param name="poolId">The ID of the <see cref="CloudPool"/>.</param>
        private async static Task CreatePool(BatchClient batchClient, string poolId)
        {
            // Create and configure an unbound pool with the specified ID
            CloudPool pool = batchClient.PoolOperations.CreatePool(poolId: poolId,
                                                                   osFamily: "3",
                                                                   virtualMachineSize: "small",
                                                                   targetDedicated: 1);

            await GettingStartedCommon.CreatePoolIfNotExistAsync(batchClient, pool);
        }

        /// <summary>
        /// Creates a CloudJob in the specified pool if a job with the specified ID is not found
        /// in the pool, otherwise returns the existing job.
        /// </summary>
        /// <param name="batchClient">A fully initialized <see cref="BatchClient"/>.</param>
        /// <param name="poolId">The ID of the CloudPool in which the job should be created.</param>
        /// <param name="jobId">The ID of the CloudJob.</param>
        /// <returns>A bound CloudJob.</returns>
        private static CloudJob CreateJob(BatchClient batchClient, string poolId, string jobId)
        {
            CloudJob job = GetJobIfExist(batchClient, jobId);
            if (job == null)
            {
                Console.WriteLine("Job {0} not found, creating...", jobId);

                CloudJob unboundJob = batchClient.JobOperations.CreateJob(jobId, new PoolInformation() { PoolId = poolId });
                unboundJob.Commit();

                job = batchClient.JobOperations.GetJob(jobId);
            }           

            // Return the bound version of the job with all of its properties populated
            return job;
        }

        /// <summary>
        /// Returns an existing <see cref="CloudJob"/> if found in the Batch account.
        /// </summary>
        /// <param name="batchClient">A fully initialized <see cref="BatchClient"/>.</param>
        /// <param name="jobId">The <see cref="CloudJob.Id"/> of the desired pool.</param>
        /// <returns>A bound <see cref="CloudJob"/>, or <c>null</c> if the specified <see cref="CloudJob"/> does not exist.</returns>
        private static CloudJob GetJobIfExist(BatchClient batchClient, string jobId)
        {
            Console.WriteLine("Checking for existing job {0}...", jobId);

            // Construct a detail level with a filter clause that specifies the job
            ODATADetailLevel detail = new ODATADetailLevel(filterClause: string.Format("id eq '{0}'", jobId));

            foreach (CloudJob job in batchClient.JobOperations.ListJobs(detailLevel: detail))
            {
                Console.WriteLine("Existing job {0} found.", jobId);
                return job;
            }

            // No existing job found
            return null;
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
        private static void QueryTasks(BatchClient batchClient, string jobId, ODATADetailLevel detail)
        {
            List<CloudTask> taskList = new List<CloudTask>();

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            try
            {
                taskList.AddRange(batchClient.JobOperations.ListTasks(jobId, detail).ToList());
            }
            catch (AggregateException ex)
            {
                AggregateException ax = ex.Flatten();

                Console.WriteLine(ax.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            finally
            {
                stopwatch.Stop();
            }
            
            Console.WriteLine("{0} tasks retrieved in {1} (ExpandClause: {2} | FilterClause: {3} | SelectClause: {4})",
                        taskList.Count, 
                        stopwatch.Elapsed, 
                        detail.ExpandClause, 
                        detail.FilterClause, 
                        detail.SelectClause);
        }
    }
}
