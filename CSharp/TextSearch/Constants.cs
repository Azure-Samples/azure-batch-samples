using System.Collections.Generic;

namespace Microsoft.Azure.Batch.Samples.TextSearch
{
    /// <summary>
    /// A set of useful constants.
    /// </summary>
    public static class Constants
    {
        public const string TextSearchExe = "TextSearch.exe";
        public const string MapperTaskPrefix = "MapperTask";
        public const string ReducerTaskId = "ReducerTask";
        public const string TextFilePath = "Text.txt";

        /// <summary>
        /// The list of required files to run the TextSearch executable.  Since TextSearch is run as a job manager which issues Azure
        /// Batch API calls, it needs all the DLLs of the Batch client library.
        /// </summary>
        public readonly static IReadOnlyList<string> RequiredExecutableFiles = new List<string>
                                                                {
                                                                    TextSearchExe,
                                                                    "TextSearch.pdb",
                                                                    "TextSearch.exe.config",
                                                                    "Microsoft.WindowsAzure.Storage.dll",
                                                                    "Microsoft.Azure.Batch.dll",
                                                                    "Hyak.Common.dll",
                                                                    "Microsoft.Azure.Common.dll",
                                                                    "Microsoft.Azure.Common.NetFramework.dll",
                                                                    "Microsoft.Data.Services.Client.dll",
                                                                    "Microsoft.Threading.Tasks.dll",
                                                                    "Microsoft.Threading.Tasks.Extensions.Desktop.dll",
                                                                    "Microsoft.Threading.Tasks.Extensions.dll",
                                                                    "Newtonsoft.Json.dll",
                                                                    "System.Net.Http.Extensions.dll",
                                                                    "System.Net.Http.Primitives.dll",
                                                                    "Microsoft.Data.Edm.dll",
                                                                    "Microsoft.Data.OData.dll",
                                                                    "System.Spatial.dll",
                                                                };
    }
}
