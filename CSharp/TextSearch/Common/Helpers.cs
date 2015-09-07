//Copyright (c) Microsoft Corporation

namespace Microsoft.Azure.Batch.Samples.TextSearch
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Batch.Common;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Auth;
    using Microsoft.WindowsAzure.Storage.Blob;

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
            return String.Format("{0}_{1}", Constants.MapperTaskPrefix, taskNumber);
        }

        /// <summary>
        /// Gets the file name corresponding to the specified file number.
        /// </summary>
        /// <param name="fileNumber">The file number.</param>
        /// <returns>The file name corresponding to the specified file number.</returns>
        public static string GetSplitFileName(int fileNumber)
        {
            return String.Format("TextFile_{0}.txt", fileNumber);
        }

        /// <summary>
        /// Checks for a task's success or failure, and optionally dumps the output of the task.  In the case that the task hit a scheduler or execution error,
        /// dumps that information as well.
        /// </summary>
        /// <param name="boundTask">The task.</param>
        /// <param name="dumpStandardOutOnTaskSuccess">True to log the standard output file of the task even if it succeeded.  False to not log anything if the task succeeded.</param>
        /// <returns>The string containing the standard out of the file, or null if stdout could not be gathered.</returns>
        public static async Task<string> CheckForTaskSuccessAsync(CloudTask boundTask, bool dumpStandardOutOnTaskSuccess)
        {
            if (boundTask.State == TaskState.Completed)
            {
                string result = null;

                //Check to see if the task has execution information metadata.
                if (boundTask.ExecutionInformation != null)
                {
                    //Dump the task scheduling error if there was one.
                    if (boundTask.ExecutionInformation.SchedulingError != null)
                    {
                        TaskSchedulingError schedulingError = boundTask.ExecutionInformation.SchedulingError;
                        Console.WriteLine("Task {0} hit scheduling error.", boundTask.Id);
                        Console.WriteLine("SchedulingError Code: {0}", schedulingError.Code);
                        Console.WriteLine("SchedulingError Message: {0}", schedulingError.Message);
                        Console.WriteLine("SchedulingError Category: {0}", schedulingError.Category);
                        Console.WriteLine("SchedulingError Details:");

                        foreach (NameValuePair detail in schedulingError.Details)
                        {
                            Console.WriteLine("{0} : {1}", detail.Name, detail.Value);
                        }

                        throw new TextSearchException(String.Format("Task {0} failed with a scheduling error", boundTask.Id));
                    }
                    
                    //Read the content of the output files if the task exited.
                    if (boundTask.ExecutionInformation.ExitCode.HasValue)
                    {
                        Console.WriteLine("Task {0} exit code: {1}", boundTask.Id, boundTask.ExecutionInformation.ExitCode);

                        if (dumpStandardOutOnTaskSuccess && boundTask.ExecutionInformation.ExitCode.Value == 0 || boundTask.ExecutionInformation.ExitCode.Value != 0)
                        {
                            //Dump the standard out file of the task.
                            NodeFile taskStandardOut = await boundTask.GetNodeFileAsync(Batch.Constants.StandardOutFileName);

                            Console.WriteLine("Task {0} StdOut:", boundTask.Id);
                            Console.WriteLine("----------------------------------------");
                            string stdOutString = await taskStandardOut.ReadAsStringAsync();
                            result = stdOutString;
                            Console.WriteLine(stdOutString);
                        }

                        //Check for nonzero exit code and dump standard error if there was a nonzero exit code.
                        if (boundTask.ExecutionInformation.ExitCode.Value != 0)
                        {
                            NodeFile taskErrorFile = await boundTask.GetNodeFileAsync(Batch.Constants.StandardErrorFileName);

                            Console.WriteLine("Task {0} StdErr:", boundTask.Id);
                            Console.WriteLine("----------------------------------------");
                            string stdErrString = await taskErrorFile.ReadAsStringAsync();
                            Console.WriteLine(stdErrString);

                            throw new TextSearchException(String.Format("Task {0} failed with a nonzero exit code", boundTask.Id));
                        }
                    }
                }

                return result;
            }
            else
            {
                throw new TextSearchException(String.Format("Task {0} is not completed yet.  Current state: {1}", boundTask.Id, boundTask.State));
            }
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
