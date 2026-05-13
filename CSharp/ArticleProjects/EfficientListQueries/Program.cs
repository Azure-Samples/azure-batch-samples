// Copyright (c) Microsoft Corporation
//
// Companion project to the following article:
// https://azure.microsoft.com/documentation/articles/batch-efficient-list-queries/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using Azure;
using Azure.Compute.Batch;
using Azure.ResourceManager.Batch;
using Microsoft.Azure.Batch.Samples.Common;

namespace Microsoft.Azure.Batch.Samples.Articles.EfficientListQueries
{
    public class Program
    {
        public static void Main(string[] args)
        {
            try
            {
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
            const string nodeSize     = "standard_d1_v2";
            const int nodeCount       = 1;
            const int taskSlotsPerNode = 4;

            const int taskCount = 5000;

            const string poolId = "EfficientListQueriesSamplePool";
            const string jobId  = "EfficientListQueriesSampleJob";

            var accountSettings = SampleHelpers.LoadAccountSettings();

            BatchClient batchClient = ClientFactory.CreateBatchClient(accountSettings);
            BatchAccountResource batchAccount = ClientFactory.CreateBatchAccountResource(accountSettings);

            // Create the pool via the ARM SDK (or get the existing one).
            BatchAccountPoolResource pool = await ArticleHelpers.CreatePoolIfNotExistAsync(
                batchAccount,
                poolId,
                nodeSize,
                nodeCount,
                taskSlotsPerNode);

            // Create a job (or get the existing one) via the data-plane SDK.
            BatchJob job = await ArticleHelpers.CreateJobIfNotExistAsync(batchClient, poolId, jobId);

            // Build a list of "large" tasks: each task carries an environment variable that
            // is 2048 bytes long, used to inflate response payloads in the queries below.
            var environmentSettings = new List<EnvironmentSetting>
            {
                new EnvironmentSetting("BIGENV") { Value = GetBigString(2048) }
            };

            var tasks = new List<BatchTaskCreateOptions>(taskCount);
            for (int i = 1; i < taskCount + 1; i++)
            {
                string taskId = "task" + i.ToString().PadLeft(5, '0');
                var task = new BatchTaskCreateOptions(taskId, "cmd /c echo %COMPUTERNAME%");
                foreach (var env in environmentSettings)
                {
                    task.EnvironmentSettings.Add(env);
                }
                tasks.Add(task);
            }

            Console.WriteLine();
            Console.WriteLine("Adding {0} tasks to job {1}...", taskCount, job.Id);

            Stopwatch stopwatch = Stopwatch.StartNew();

            // Bulk task submission via CreateTaskCollectionAsync (chunks of 100 are submitted by the SDK).
            const int batchSize = 100;
            for (int offset = 0; offset < tasks.Count; offset += batchSize)
            {
                int count = Math.Min(batchSize, tasks.Count - offset);
                var slice = tasks.GetRange(offset, count);
                await batchClient.CreateTaskCollectionAsync(job.Id, new BatchTaskGroup(slice));
            }

            stopwatch.Stop();
            Console.WriteLine("{0} tasks added in {1}, hit ENTER to query tasks...", taskCount, stopwatch.Elapsed);
            Console.ReadLine();
            Console.WriteLine();

            // Query the tasks back, varying $select / $filter / $expand to demonstrate the
            // size/time impact each option has.
            string[] idAndState = new[] { "id", "state" };
            string[] idStateEnv = new[] { "id", "state", "environmentSettings" };

            await QueryTasksAsync(batchClient, job.Id, select: idAndState, filter: "state eq 'active'");
            await QueryTasksAsync(batchClient, job.Id, select: idAndState, filter: "state eq 'running'");
            await QueryTasksAsync(batchClient, job.Id, select: idAndState, filter: "state eq 'completed'");

            await QueryTasksAsync(batchClient, job.Id, select: idAndState);
            await QueryTasksAsync(batchClient, job.Id, select: idStateEnv);
            await QueryTasksAsync(batchClient, job.Id, expand: new[] { "stats" });

            Console.WriteLine();
            Console.WriteLine("Done!");
            Console.WriteLine();

            Console.WriteLine("Delete job? [yes] no");
            string response = Console.ReadLine().ToLower();
            if (response != "n" && response != "no")
            {
                await batchClient.DeleteJobAsync(WaitUntil.Started, job.Id);
            }

            Console.WriteLine("Delete pool? [yes] no");
            response = Console.ReadLine().ToLower();
            if (response != "n" && response != "no")
            {
                await pool.DeleteAsync(WaitUntil.Started);
            }
        }

        private static string GetBigString(int size)
        {
            StringBuilder bigSB = new StringBuilder(size);
            for (int i = 0; i < size; i++)
            {
                bigSB.Append('a');
            }
            return bigSB.ToString();
        }

        private static async Task QueryTasksAsync(
            BatchClient batchClient,
            string jobId,
            IEnumerable<string> select = null,
            IEnumerable<string> expand = null,
            string filter = null)
        {
            int taskCount = 0;

            Stopwatch stopwatch = Stopwatch.StartNew();

            await foreach (BatchTask t in batchClient.GetTasksAsync(jobId, select: select, expand: expand, filter: filter))
            {
                taskCount++;
            }

            stopwatch.Stop();

            Console.WriteLine("{0} tasks retrieved in {1} (Expand: {2} | Filter: {3} | Select: {4})",
                taskCount,
                stopwatch.Elapsed,
                expand == null ? null : string.Join(",", expand),
                filter,
                select == null ? null : string.Join(",", select));
        }
    }
}
