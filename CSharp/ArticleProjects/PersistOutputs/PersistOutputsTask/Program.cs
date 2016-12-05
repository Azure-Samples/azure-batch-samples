// Copyright (c) Microsoft Corporation
//
// Companion project to the following article:
// https://azure.microsoft.com/documentation/articles/batch-task-output/

namespace Microsoft.Azure.Batch.Samples.Articles.PersistOutputs.PersistOutputsTask
{
    using Microsoft.Azure.Batch.Conventions.Files;
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    // This sample task application creates a million random numbers and writes them to a file,
    // pretending they're the results of Monte Carlo trials. The detailed results file is saved
    // as a task OUTPUT, and creates a file containing just the mean and standard deviation which
    // is saved as a task PREVIEW.

    public class Program
    {
        private static readonly Random random = new Random();
        private static readonly TimeSpan stdoutFlushDelay = TimeSpan.FromSeconds(3);

        public static int Main(string[] args)
        {
            return RunTaskAsync().GetAwaiter().GetResult();
        }

        public static async Task<int> RunTaskAsync()
        {
            // Obtain service-defined environment variables
            string jobId = Environment.GetEnvironmentVariable("AZ_BATCH_JOB_ID");
            string taskId = Environment.GetEnvironmentVariable("AZ_BATCH_TASK_ID");

            // Obtain the custom environment variable we set in the client application
            string jobContainerUrl = Environment.GetEnvironmentVariable("JOB_CONTAINER_URL");

            // The task will use the TaskOutputStorage to store both its output and log updates
            TaskOutputStorage taskStorage = new TaskOutputStorage(new Uri(jobContainerUrl), taskId);

            // The primary task logic is wrapped in a using statement that sends updates to the
            // stdout.txt blob in Storage every 15 seconds while the task code runs.
            using (ITrackedSaveOperation stdout = await taskStorage.SaveTrackedAsync(
                TaskOutputKind.TaskLog, 
                RootDir("stdout.txt"), 
                "stdout.txt", 
                TimeSpan.FromSeconds(15)))
            {
                string outputFile = $"results_{taskId}.txt";
                string summaryFile = $"summary_{taskId}.txt";

                using (StreamWriter output = File.CreateText(WorkingDir(outputFile)))
                {
                    using (StreamWriter summary = File.CreateText(WorkingDir(summaryFile)))
                    {
                        output.WriteLine($"# Task {taskId}");

                        const int runCount = 1000000;
                        int[] results = new int[runCount];
                        double resultTotal = 0;

                        for (int i = 0; i < runCount; ++i)
                        {
                            int runResult = PerformSingleRunMonteCarloSimulation();
                            output.WriteLine($"{i}, {runResult}");
                            results[i] = runResult;
                            resultTotal += runResult;

                            if (i % 5000 == 0)
                            {
                                Console.WriteLine($"{DateTime.UtcNow}: Processing... done {i}");
                            }
                        }

                        double mean = resultTotal / runCount;
                        double stddev = Math.Sqrt((from r in results
                                                let d = r - mean
                                                select d * d).Average());

                        summary.WriteLine($"Task:      {taskId}");
                        summary.WriteLine($"Run count: {runCount}");
                        summary.WriteLine($"Mean:      {mean}");
                        summary.WriteLine($"Std dev:   {stddev}");
                    }
                }

                // Persist the task output to Azure Storage
                Task.WaitAll(
                    taskStorage.SaveAsync(TaskOutputKind.TaskOutput, outputFile),
                    taskStorage.SaveAsync(TaskOutputKind.TaskPreview, summaryFile)
                    );

                // We are tracking the disk file to save our standard output, but the node agent may take
                // up to 3 seconds to flush the stdout stream to disk. So give the file a moment to catch up.
                await Task.Delay(stdoutFlushDelay);

                return 0;
            }
        }

        private static string RootDir(string path)
        {
            return Path.Combine(Environment.GetEnvironmentVariable("AZ_BATCH_TASK_DIR"), path);
        }

        private static string WorkingDir(string path)
        {
            return Path.Combine(Environment.GetEnvironmentVariable("AZ_BATCH_TASK_WORKING_DIR"), path);
        }

        private static int PerformSingleRunMonteCarloSimulation()
        {
            Thread.Sleep(TimeSpan.FromMilliseconds(0.1));
            return random.Next(100);
        }
    }
}
