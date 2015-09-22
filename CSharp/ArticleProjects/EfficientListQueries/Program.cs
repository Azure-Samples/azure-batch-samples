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
            // Each task that we create below is intentionally quite large (>2048 bytes) so
            // you may wish to adjust this value (up or down) based on your network connection.
            const int taskCount = 5000;

            const string poolId = "pool1";
            const string jobId  = "job1";

            // Setup the credentials required the BatchClient. Configure your AccountSettings in the
            // Microsoft.Azure.Batch.Samples.Common project within this solution.
            BatchSharedKeyCredentials cred = new BatchSharedKeyCredentials(AccountSettings.Default.BatchServiceUrl,
                                                                           AccountSettings.Default.BatchAccountName,
                                                                           AccountSettings.Default.BatchAccountKey);
            
            using (BatchClient batchClient = BatchClient.Open(cred))
            {
                // Obtain an existing pool from the account, or create one if that pool does not yet exist
                CloudPool pool = GetPoolIfExist(batchClient, poolId);
                if (pool == null)
                {
                    Console.WriteLine("Pool {0} not found, creating...", poolId);

                    pool = CreatePool(batchClient, poolId);
                }

                // Create the job if it doesn't exist, otherwise get the existing job
                CloudJob job = GetJobIfExist(batchClient, jobId);
                if (job == null)
                {
                    Console.WriteLine("Job {0} not found, creating...", jobId);

                    CloudJob unboundJob = batchClient.JobOperations.CreateJob(jobId, new PoolInformation() { PoolId = pool.Id });
                    unboundJob.Commit();

                    // Get a bound version of the job with all of its properties populated
                    job = batchClient.JobOperations.GetJob(jobId);
                }

                // Configure the tasks we'll be querying. Each task simply echoes the node's
                // name and then exits. We create "large" tasks by setting an environment
                // variable for each that is 2048 bytes in size.
                List<CloudTask> tasks = new List<CloudTask>();
                List<EnvironmentSetting> environmentSettings = new List<EnvironmentSetting>();
                environmentSettings.Add(new EnvironmentSetting("BIGENV", GetBigString(2048)));
                for (int i = 1; i < taskCount + 1; i++)
                {
                    string taskId = "task" + i.ToString().PadLeft(3, '0');
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
                // are adding thousands of tasks to your jobs.
                batchClient.JobOperations.AddTask(job.Id, tasks);

                stopwatch.Stop();
                Console.WriteLine("{0} tasks added in {1}, hit ENTER to query tasks...", taskCount, stopwatch.Elapsed);
                Console.ReadLine();
                Console.WriteLine();
                stopwatch.Reset();

                // Obtain the tasks, specifying a detail level to limit the number of tasks returned and the amount of data
                // returned for each. If your job tasks number in the thousands or have "large" properties (such as our big
                // environment variable), specifying a DetailLevel is important in reducing the amount of data transferred,
                // lowering your query response times (potentially greatly).

                ODATADetailLevel detail = new ODATADetailLevel();
                detail.FilterClause = "state eq 'active'";
                QueryTasks(batchClient, job.Id, detail);
                detail.FilterClause = "state eq 'running'";
                QueryTasks(batchClient, job.Id, detail);
                detail.FilterClause = "state eq 'completed'";
                QueryTasks(batchClient, job.Id, detail);

                // Now retrieve *all* of the tasks, but limit the properties return to just the task ID and state - this
                // should be a relatively quick query
                detail.FilterClause = null;
                detail.SelectClause = "id,state";
                QueryTasks(batchClient, job.Id, detail);

                // Grab all tasks again, but this time, include the huge environment property as well (this should take some time)
                detail.SelectClause = "id,state,environmentSettings";
                QueryTasks(batchClient, job.Id, detail);

                // And finally, pull all tasks, include all standard properties, and expand the statistics
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
        private static CloudPool CreatePool(BatchClient batchClient, string poolId)
        {
            // Create and configure a pool with the specified ID
            CloudPool pool = batchClient.PoolOperations.CreatePool(poolId: poolId,
                                                                   osFamily: "3",
                                                                   virtualMachineSize: "small",
                                                                   targetDedicated: 1);

            pool.Commit();

            // Return the bound pool that has all its properties populated
            return batchClient.PoolOperations.GetPool(poolId);
        }

        /// <summary>
        /// Returns an existing <see cref="CloudPool"/> if found in the Batch account.
        /// </summary>
        /// <param name="batchClient">A fully initialized <see cref="BatchClient"/>.</param>
        /// <param name="poolId">The <see cref="CloudPool.Id"/> of the desired pool.</param>
        /// <returns>A bound <see cref="CloudPool"/>, or <c>null</c> if the specified <see cref="CloudPool"/> does not exist.</returns>
        private static CloudPool GetPoolIfExist(BatchClient batchClient, string poolId)
        {
            Console.WriteLine("Checking for existing pool {0}...", poolId);

            // Specify a detail level, limiting the properties returned for each pool to
            // just the ID of the pool
            ODATADetailLevel detail = new ODATADetailLevel(selectClause: "id");

            foreach (CloudPool pool in batchClient.PoolOperations.ListPools(detailLevel: detail))
            {
                if (string.Equals(poolId, pool.Id))
                {
                    Console.WriteLine("Existing pool {0} found.", poolId);

                    // Get the bound pool with all its properties populated
                    return batchClient.PoolOperations.GetPool(poolId); ;
                }
            }

            // No existing pool found
            return null;
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

            // Specify a detail level, limiting the properties returned for each pool to
            // just the ID of the pool
            ODATADetailLevel detail = new ODATADetailLevel(selectClause: "id");

            foreach (CloudJob job in batchClient.JobOperations.ListJobs(detailLevel: detail))
            {
                if (string.Equals(jobId, job.Id))
                {
                    Console.WriteLine("Existing job {0} found.", jobId);
                    return job;
                }
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
