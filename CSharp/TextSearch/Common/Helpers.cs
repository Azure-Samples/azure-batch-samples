// Copyright (c) Microsoft Corporation

namespace Microsoft.Azure.Batch.Samples.TextSearch
{
    using System;
    using System.Threading.Tasks;
    using global::Azure;
    using global::Azure.Compute.Batch;

    /// <summary>
    /// Class containing helpers for the TextSearch sample.
    /// </summary>
    public static class Helpers
    {
        public static string GetMapperTaskId(int taskNumber)
        {
            return $"{Constants.MapperTaskPrefix}_{taskNumber}";
        }

        public static string GetSplitFileName(int fileNumber)
        {
            return $"TextFile_{fileNumber}.txt";
        }

        /// <summary>
        /// Checks for a task's success or failure, optionally dumping the standard out file.
        /// </summary>
        public static async Task CheckForTaskSuccessAsync(BatchClient batchClient, string jobId, BatchTask boundTask, bool dumpStandardOutOnTaskSuccess)
        {
            if (boundTask.State == BatchTaskState.Completed)
            {
                if (boundTask.ExecutionInfo?.FailureInfo != null)
                {
                    BatchTaskFailureInfo failureInformation = boundTask.ExecutionInfo.FailureInfo;
                    Console.WriteLine($"Task {boundTask.Id} had a failure.");
                    Console.WriteLine($"Failure Code: {failureInformation.Code}");
                    Console.WriteLine($"Failure Message: {failureInformation.Message}");
                    Console.WriteLine($"Failure Category: {failureInformation.Category}");
                    Console.WriteLine("Failure Details:");

                    foreach (BatchNameValuePair detail in failureInformation.Details)
                    {
                        Console.WriteLine("{0} : {1}", detail.Name, detail.Value);
                    }

                    if (boundTask.ExecutionInfo.ExitCode.HasValue)
                    {
                        Console.WriteLine($"Task {boundTask.Id} exit code: {boundTask.ExecutionInfo.ExitCode}");

                        if (boundTask.ExecutionInfo.ExitCode.Value != 0)
                        {
                            await GetFileAsync(batchClient, jobId, boundTask.Id, Constants.StandardOutFileName);
                            await GetFileAsync(batchClient, jobId, boundTask.Id, Constants.StandardErrorFileName);
                        }
                    }

                    throw new TextSearchException($"Task {boundTask.Id} failed");
                }
                else
                {
                    await GetFileAsync(batchClient, jobId, boundTask.Id, Constants.StandardOutFileName, dumpStandardOutOnTaskSuccess);
                }
            }
            else
            {
                throw new TextSearchException($"Task {boundTask.Id} is not completed yet.  Current state: {boundTask.State}");
            }
        }

        private static async Task<string> GetFileAsync(BatchClient batchClient, string jobId, string taskId, string fileName, bool dumpFile = true)
        {
            BinaryData data = await batchClient.GetTaskFileAsync(jobId, taskId, fileName);
            string fileContent = data.ToString();
            if (dumpFile)
            {
                Console.WriteLine($"Task {taskId} {fileName}:");
                Console.WriteLine("----------------------------------------");
                Console.WriteLine(fileContent);
            }
            return fileContent;
        }
    }

    /// <summary>
    /// Custom exception type for the Text Search sample.
    /// </summary>
    public class TextSearchException : Exception
    {
        public TextSearchException(string message) : base(message)
        {
        }
    }
}
