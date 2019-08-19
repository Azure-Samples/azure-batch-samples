//Copyright (c) Microsoft Corporation

using System.Collections.Generic;

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

        /// <summary>
        /// The list of required files to run the sample executables.  Since the JobManager.exe is run as a job manager in Batch 
        /// it needs all the DLLs of the Batch client library.
        /// </summary>
        public readonly static IReadOnlyList<string> RequiredExecutableFiles = new List<string>
            {
	            "JobSubmitter.pdb",
                MapperTaskExecutable,
                MapperTaskExecutable + ".config",
                "MapperTask.pdb",
                ReducerTaskExecutable,
                ReducerTaskExecutable + ".config",
                "ReducerTask.pdb",
                "settings.json",
                "accountsettings.json",
                "Microsoft.Azure.Batch.Samples.Common.dll",
                "Common.dll",
                "Microsoft.WindowsAzure.Storage.dll",
                "Microsoft.Azure.Batch.dll",
                "Microsoft.Rest.ClientRuntime.dll",
                "Microsoft.Rest.ClientRuntime.Azure.dll",
                "Newtonsoft.Json.dll",
                "Microsoft.Extensions.Configuration.dll",
                "Microsoft.Extensions.Configuration.Abstractions.dll",
                "Microsoft.Extensions.Configuration.Json.dll",
                "Microsoft.Extensions.Configuration.Binder.dll",
                "Microsoft.Extensions.Configuration.FileExtensions.dll",
                "Microsoft.Extensions.FileProviders.Physical.dll",
                "Microsoft.Extensions.FileProviders.Abstractions.dll",
                "netstandard.dll",
                "Microsoft.Extensions.Primitives.dll",
                "System.Net.Http.dll",
            };
    }
}
