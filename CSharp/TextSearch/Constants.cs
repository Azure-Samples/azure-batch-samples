using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Azure.Batch.Samples.TextSearch
{
    /// <summary>
    /// A set of useful constants.
    /// </summary>
    public static class Constants
    {
        public const string TextSearchExe = "TextSearch.exe";
        public const string TextSearchExeConfiguration = "TextSearch.exe.config";
        public const string StorageClientDll = "Microsoft.WindowsAzure.Storage.dll";
        public const string BatchClientDll = "Microsoft.Azure.Batch.dll";
        public const string EdmDll = "Microsoft.Data.Edm.dll";
        public const string ODataDll = "Microsoft.Data.OData.dll";
        public const string SpatialDll = "System.Spatial.dll";
        public const string MapperTaskPrefix = "MapperTask";
        public const string ReducerTaskName = "ReducerTask";
        public const string TextFilePath = "Text.txt";

        public readonly static IReadOnlyList<string> RequiredExecutableFiles = new List<string>
                                                                {
                                                                    Constants.TextSearchExe,
                                                                    Constants.TextSearchExeConfiguration,
                                                                    Constants.BatchClientDll,
                                                                    Constants.EdmDll,
                                                                    Constants.ODataDll,
                                                                    Constants.SpatialDll,
                                                                    Constants.StorageClientDll
                                                                };
    }
}
