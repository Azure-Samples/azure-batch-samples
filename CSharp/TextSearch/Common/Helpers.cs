//Copyright (c) Microsoft Corporation

namespace Microsoft.Azure.Batch.Samples.TextSearch
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Batch.Common;

    /// <summary>
    /// Class containing helpers for the TextSearch sample.
    /// </summary>
    public static class Helpers
    {
        /// <summary>
        /// Gets the mapper task id corresponding to the specified task number.
        /// </summary>
        /// <param name="taskNumber">The mapper task number.</param>
        /// <returns>The mapper task id corresponding to the specified task number.</returns>
        public static string GetMapperTaskId(int taskNumber)
        {
            return $"{Constants.MapperTaskPrefix}_{taskNumber}";
        }

        /// <summary>
        /// Gets the file name corresponding to the specified file number.
        /// </summary>
        /// <param name="fileNumber">The file number.</param>
        /// <returns>The file name corresponding to the specified file number.</returns>
        public static string GetSplitFileName(int fileNumber)
        {
            return $"TextFile_{fileNumber}.txt";
        }

        /// <summary>
        /// Checks for a task's success or failure, and optionally dumps the output of the task.  In the case that the task hit a scheduler or execution error,
        /// dumps that information as well.
        /// </summary>
        /// <param name="boundTask">The task.</param>
        /// <param name="dumpStandardOutOnTaskSuccess">True to log the standard output file of the task even if it succeeded.  False to not log anything if the task succeeded.</param>
        public static async Task CheckForTaskSuccessAsync(CloudTask boundTask, bool dumpStandardOutOnTaskSuccess)
        {
            if (boundTask.State == TaskState.Completed)
            {
                //Dump the task failure info if there was one.
                if (boundTask.ExecutionInformation.FailureInformation != null)
                {
                    TaskFailureInformation failureInformation = boundTask.ExecutionInformation.FailureInformation;
                    Console.WriteLine($"Task {boundTask.Id} had a failure.");
                    Console.WriteLine($"Failure Code: {failureInformation.Code}");
                    Console.WriteLine($"Failure Message: {failureInformation.Message}");
                    Console.WriteLine($"Failure Category: {failureInformation.Category}");
                    Console.WriteLine("Failure Details:");

                    foreach (NameValuePair detail in failureInformation.Details)
                    {
                        Console.WriteLine("{0} : {1}", detail.Name, detail.Value);
                    }

                    if (boundTask.ExecutionInformation.ExitCode.HasValue)
                    {
                        Console.WriteLine($"Task {boundTask.Id} exit code: {boundTask.ExecutionInformation.ExitCode}");

                        if (boundTask.ExecutionInformation.ExitCode.Value != 0)
                        {
                            await GetFileAsync(boundTask, Batch.Constants.StandardOutFileName);
                            await GetFileAsync(boundTask, Batch.Constants.StandardErrorFileName);
                        }
                    }

                    throw new TextSearchException($"Task {boundTask.Id} failed");
                }
                else
                {
                    await GetFileAsync(boundTask, Batch.Constants.StandardOutFileName, dumpStandardOutOnTaskSuccess);
                }
            }
            else
            {
                throw new TextSearchException($"Task {boundTask.Id} is not completed yet.  Current state: {boundTask.State}");
            }
        }

        private static async Task<string> GetFileAsync(CloudTask boundTask, string fileName, bool dumpFile = true)
        {
            //Dump the standard out file of the task.
            NodeFile file = await boundTask.GetNodeFileAsync(Batch.Constants.StandardOutFileName);

            string fileContent = await file.ReadAsStringAsync();
            if (dumpFile)
            {
                Console.WriteLine($"Task {boundTask.Id} {fileName}:");
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
