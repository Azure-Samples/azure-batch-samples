// Copyright (c) Microsoft Corporation

namespace Microsoft.Azure.Batch.Samples.TextSearch
{
    /// <summary>
    /// A set of useful constants.
    /// </summary>
    public static class Constants
    {
        public const string MapperTaskExecutable = "MapperTask.exe";
        public const string ReducerTaskExecutable = "ReducerTask.exe";
        public const string ReducerTaskResultBlobName = "ReducerTaskOutput";

        public const string MapperTaskPrefix = "MapperTask";
        public const string ReducerTaskId = "ReducerTask";
        public const string TextFilePath = "Text.txt";

        public const string StandardOutFileName = "stdout.txt";
        public const string StandardErrorFileName = "stderr.txt";
    }
}
