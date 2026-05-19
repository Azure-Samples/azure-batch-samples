//Copyright (c) Microsoft Corporation

namespace Microsoft.Azure.Batch.Samples.BatchMetricsUsageSample
{
    using global::Azure.Compute.Batch;
    using Microsoft.Azure.Batch.Samples.BatchMetrics;
    using Microsoft.Azure.Batch.Samples.Common;
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Threading.Tasks;

    internal static class Program
    {
        private static void Main()
        {
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
            BatchClient batchClient = ClientFactory.CreateBatchClient(accountSettings);

            // Create a MetricMonitor.  Once started, this will periodically fetch job metrics
            // (specifically, the counts of tasks in different states) from Azure Batch.
            using (var monitor = new MetricMonitor(batchClient))
            {
                monitor.MetricsUpdated += (s, e) =>
                {
                    Console.WriteLine();
                    Console.WriteLine(FormatMetrics(monitor.CurrentMetrics));
                };
                monitor.Start();

                // Give the monitor some jobs to report on.
                var jobSubmitter = new JobSubmitter(accountSettings);
                await jobSubmitter.SubmitJobsAsync();
                await Task.Delay(TimeSpan.FromMinutes(2));
                await jobSubmitter.CleanUpAsync();
            }
        }

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
            return string.Join(Environment.NewLine, jobInfos);
        }

        private static readonly ReadOnlyCollection<BatchTaskState> TaskStates =
            new ReadOnlyCollection<BatchTaskState>(new[]
            {
                BatchTaskState.Active,
                BatchTaskState.Preparing,
                BatchTaskState.Running,
                BatchTaskState.Completed,
            });

        private static string FormatJobMetrics(string jobId, JobMetrics metrics, int jobIdFormatLength)
        {
            var taskStateInfos = TaskStates.Select(s => new { State = s, Count = metrics.TaskStateCounts[s] })
                                           .Select(c => string.Format("{0}={1,3:##0}", c.State.ToString().Substring(0, 3), c.Count));

            var paddedJobId = jobId + ":" + new string(' ', jobIdFormatLength - jobId.Length);

            return paddedJobId + "  " + string.Join("  ", taskStateInfos);
        }
    }
}
