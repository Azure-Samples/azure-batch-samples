//Copyright (c) Microsoft Corporation

using System.Collections.Generic;

namespace Microsoft.Azure.Batch.Samples.TextSearch
{
    /// <summary>
    /// A set of useful constants.
    /// </summary>
    public static class Constants
    {
        public const string JobManagerExecutable = "JobManagerTask.exe";
        public const string MapperTaskExecutable = "MapperTask.exe";
        public const string ReducerTaskExecutable = "ReducerTask.exe";
        public const string ReducerTaskResultBlobName = "ReducerTaskOutput";

        public const string MapperTaskPrefix = "MapperTask";
        public const string ReducerTaskId = "ReducerTask";
        public const string TextFilePath = "Text.txt";

        /// <summary>
        /// The list of required files to run the sample executables.  Since the JobManager.exe is run as a job manager in Batch 
        /// it needs all the DLLs of the Batch client library.
        /// </summary>
        public readonly static IReadOnlyList<string> RequiredExecutableFiles = new List<string>
            {
                JobManagerExecutable,
                "JobManagerTask.pdb",
                MapperTaskExecutable,
                "MapperTask.pdb",
                ReducerTaskExecutable,
                "ReducerTask.pdb",
                "Microsoft.Azure.Batch.Samples.Common.dll",
                "Microsoft.Azure.Batch.Samples.Common.dll.config",
                "Common.dll",
                "app.config",
                "Microsoft.WindowsAzure.Storage.dll",
                "Microsoft.Azure.Batch.dll",
                "Microsoft.Rest.ClientRuntime.dll",
                "Microsoft.Rest.ClientRuntime.Azure.dll",
                "Microsoft.Data.Services.Client.dll",
                "Newtonsoft.Json.dll",
                "Microsoft.Data.Edm.dll",
                "Microsoft.Data.OData.dll",
                "System.Spatial.dll",
            };
    }
}
