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
            try
            {
                AccountSettings accountSettings = AccountSettings.Default;
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
            var credentials = new BatchSharedKeyCredentials(
                accountSettings.BatchServiceUrl,
                accountSettings.BatchAccountName,
                accountSettings.BatchAccountKey);

            using (var batchClient = await BatchClient.OpenAsync(credentials))
            {
                using (var monitor = new MetricMonitor(batchClient))
                {
                    monitor.MetricsUpdated += (s, e) =>
                        {
                            Console.WriteLine();
                            Console.WriteLine(FormatMetrics(monitor.CurrentMetrics));
                        };
                    monitor.Start();

                    var jobSubmitter = new JobSubmitter(batchClient);
                    await jobSubmitter.SubmitJobsAsync();
                    await jobSubmitter.CleanUpJobsAsync();
                }
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

            var jobIdFormatLength = metrics.JobIds.Max(id => id.Length);
            var jobInfos = metrics.JobIds.Select(id => FormatJobStatistics(id, metrics.GetMetrics(id), jobIdFormatLength));
            return String.Join(Environment.NewLine, jobInfos);
        }

        private static readonly ReadOnlyCollection<TaskState> TaskStates =
            Enum.GetValues(typeof(TaskState))
                .Cast<TaskState>()
                .ToList().AsReadOnly();

        private static string FormatJobStatistics(string jobId, BatchMetrics.JobStatistics statistics, int jobIdFormatLength)
        {
            var taskStateCounts = statistics.TaskStateCounts;
            var taskStateInfos = TaskStates.Select(s => new { State = s, Count = statistics.TaskStateCounts[s] })
                                           .Select(c => String.Format("{0}={1,3:##0}", c.State.ToString().Substring(0, 3), c.Count));

            var paddedJobId = jobId + ":" + new string(' ', jobIdFormatLength - jobId.Length);

            return paddedJobId + "  " + String.Join("  ", taskStateInfos);
        }
    }
}
