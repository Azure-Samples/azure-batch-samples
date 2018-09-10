//Copyright (c) Microsoft Corporation

namespace Microsoft.Azure.Batch.Samples.BatchMetricsUsageSample
{
    using Microsoft.Azure.Batch.Samples.BatchMetrics;
    using Microsoft.Azure.Batch;
    using Microsoft.Azure.Batch.Auth;
    using Microsoft.Azure.Batch.Common;
    using Microsoft.Azure.Batch.Samples.Common;
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    internal static class Program
    {
        private static void Main()
        {
            // Call the asynchronous version of the Main() method. This is done so that we can await various
            // calls to async methods within the "Main" method of this console application.

            try
            {
                AccountSettings accountSettings = SampleHelpers.LoadAccountSettings();
                MainAsync(accountSettings).Wait();
            }
            catch (AggregateException ex)
            {
                SampleHelpers.PrintAggregateException(ex);
                throw;
            }

            Console.WriteLine("Press return to exit...");
            Console.ReadLine();
        }

        private static async Task MainAsync(AccountSettings accountSettings)
        {
            // Use the AccountSettings from the Common project to initialize a BatchClient.
            var credentials = new BatchSharedKeyCredentials(
                accountSettings.BatchServiceUrl,
                accountSettings.BatchAccountName,
                accountSettings.BatchAccountKey);

            using (var batchClient = BatchClient.Open(credentials))
            {
                // Create a MetricMonitor.  Once started, this will periodically fetch job metrics
                // (specifically, the counts of tasks in different states) from Azure Batch.
                // The monitor will stop reporting once disposed, so often you would have the monitor
                // as a long-lived member variable, but for demo purposes we use a 'using' statement
                // to ensure disposal.
                using (var monitor = new MetricMonitor(batchClient))
                {
                    // For demo purposes, print the latest metrics every time the monitor updates them.
                    monitor.MetricsUpdated += (s, e) =>
                        {
                            Console.WriteLine();
                            Console.WriteLine(FormatMetrics(monitor.CurrentMetrics));
                        };
                    // Start monitoring.  The monitor will fetch metrics in the background.
                    monitor.Start();

                    // Give the monitor some jobs to report on.
                    var jobSubmitter = new JobSubmitter(batchClient);
                    await jobSubmitter.SubmitJobsAsync();  // Submit a series of jobs over a period of several minutes
                    await Task.Delay(TimeSpan.FromMinutes(2));  // Give the last submitted job time to get under way so we can see it progressing
                    await jobSubmitter.CleanUpJobsAsync();
                    await jobSubmitter.CleanUpPoolIfRequiredAsync();
                }
            }
        }

        // The next set of methods format a MetricEvent for compact display in the console.  The format
        // used in this sample is:
        //
        // Collected from 11:44:57 to 11:45:02
        //
        // sample-job-1:  Act= 14  Pre=  1  Run= 17  Com=155
        // sample-job-2:  Act=308  Pre=  0  Run=  8  Com= 43
        //
        // where Act, Pre, Run and Com represent the number of tasks in the Active, Preparing, Running
        // and Completed states.  In a real application you might represent these visually or
        // output them to a CSV file for display in a spreadsheet or data visualisation tool.

        private static string FormatMetrics(MetricEvent metrics)
        {
            return FormatMetricsCollectionRange(metrics) + Environment.NewLine + FormatMetricsBody(metrics);
        }

        private static string FormatMetricsCollectionRange(MetricEvent metrics)
        {
            return string.Format("Collected from {0:HH:mm:ss} to {1:HH:mm:ss}",
                metrics.CollectionStarted.ToLocalTime(),
                metrics.CollectionCompleted.ToLocalTime());
        }

        private static string FormatMetricsBody(MetricEvent metrics)
        {
            if (metrics.IsError)
            {
                var error = metrics.Error;
                return error.GetType().Name + ": " + error.Message;
            }

            if (!metrics.JobIds.Any())
            {
                return "No jobs in account";
            }

            var jobIdFormatLength = metrics.JobIds.Max(id => id.Length);
            var jobInfos = metrics.JobIds.Select(id => FormatJobMetrics(id, metrics.GetMetrics(id), jobIdFormatLength));
            return String.Join(Environment.NewLine, jobInfos);
        }

        private static readonly ReadOnlyCollection<TaskState> TaskStates =
            Enum.GetValues(typeof(TaskState))
                .Cast<TaskState>()
                .ToList().AsReadOnly();

        private static string FormatJobMetrics(string jobId, JobMetrics metrics, int jobIdFormatLength)
        {
            var taskStateCounts = metrics.TaskStateCounts;
            var taskStateInfos = TaskStates.Select(s => new { State = s, Count = metrics.TaskStateCounts[s] })
                                           .Select(c => String.Format("{0}={1,3:##0}", c.State.ToString().Substring(0, 3), c.Count));

            var paddedJobId = jobId + ":" + new string(' ', jobIdFormatLength - jobId.Length);

            return paddedJobId + "  " + String.Join("  ", taskStateInfos);
        }
    }
}
