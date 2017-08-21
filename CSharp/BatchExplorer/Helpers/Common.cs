//Copyright (c) Microsoft Corporation

namespace Microsoft.Azure.BatchExplorer.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.Composition;
    using System.ComponentModel.Composition.Hosting;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Reflection;
    using Microsoft.Azure.Batch.Common;
    using Microsoft.Azure.BatchExplorer.ViewModels;

    public static class Common
    {
        public readonly static string LocalAppDataDirectory = System.Environment.GetEnvironmentVariable("LOCALAPPDATA");
        public const string LocalAppDataSubfolder = @"Microsoft\Azure Batch Explorer";

        public const string PluginSubfolderName = "Plugins";

        public static readonly IReadOnlyDictionary<string, string> SupportedOSFamilyDictionary =
            new Dictionary<string, string>
        {
            {"Windows Server 2008 R2", "2"},
            {"Windows Server 2012", "3"},
            {"Windows Server 2012 R2", "4"},
        };

        public static readonly IReadOnlyList<string> SupportedVirtualMachineSizesList =
            new List<string>
            {
                "Small", 
                "Medium",
                "Large", 
                "ExtraLarge", 
                "A5",
                "A6",
                "A7",
                "A8",
                "A9",
                "A10",
                "A11",
                "STANDARD_D1",
                "STANDARD_D1_V2",
                "STANDARD_D2",
                "STANDARD_D2_V2",
                "STANDARD_D3",
                "STANDARD_D3_V2",
                "STANDARD_D4",
                "STANDARD_D4_V2",
                "STANDARD_D5_V2",
                "STANDARD_D11",
                "STANDARD_D11_V2",
                "STANDARD_D12",
                "STANDARD_D12_V2",
                "STANDARD_D13",
                "STANDARD_D13_V2",
                "STANDARD_D14",
                "STANDARD_D14_V2",
                "STANDARD_D15_V2",
            };  

        /// <summary>
        /// Determines if an exception is a "NotFound" exception from the Batch service
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public static bool IsExceptionNotFound(Exception e)
        {
            AggregateException aggEx = e as AggregateException;
            BatchException batchException = e as BatchException;

            if (aggEx != null)
            {
                aggEx = aggEx.Flatten(); //Flatten the aggregate exception

                batchException = aggEx.InnerExceptions.Cast<BatchException>().FirstOrDefault();
            }
            
            if (batchException != null)
            {
                if (batchException.RequestInformation != null &&
                    batchException.RequestInformation.BatchError != null)
                {
                    if(batchException.RequestInformation.HttpStatusCode == HttpStatusCode.NotFound)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Loads plugins for the specified plugin target from the plugin folder.
        /// </summary>
        /// <param name="pluginTarget">The target to load plugins for.</param>
        public static void LoadPlugins(object pluginTarget)
        {
            AggregateCatalog catalog = new AggregateCatalog(new AssemblyCatalog(typeof(MainViewModel).Assembly));

            if (!Directory.Exists(Common.PluginSubfolderName))
            {
                Directory.CreateDirectory(Common.PluginSubfolderName);
            }
            string[] subDirectories = Directory.GetDirectories(Common.PluginSubfolderName);

            foreach (string subDirectory in subDirectories)
            {
                catalog.Catalogs.Add(new DirectoryCatalog(subDirectory));
            }
            
            CompositionContainer container = new CompositionContainer(catalog);

            try
            {
                container.ComposeParts(pluginTarget);
            }
            catch (ReflectionTypeLoadException e)
            {
                //Catch any exceptions thrown by the loader and dump them before throwing
                foreach (Exception loaderException in e.LoaderExceptions)
                {
                    Console.WriteLine(loaderException);
                }
                throw;
            }
        }

        public static int? GetNullableIntValue(string content)
        {
            int value;
            int? output = null;
            if (Int32.TryParse(content, out value))
            {
                output = value;
            }

            return output;
        }
    }
}
