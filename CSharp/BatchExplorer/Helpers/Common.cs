using System;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using Microsoft.Azure.Batch.Common;
using Microsoft.Azure.BatchExplorer.ViewModels;

namespace Microsoft.Azure.BatchExplorer.Helpers
{
    public static class Common
    {
        public readonly static string LocalAppDataDirectory = System.Environment.GetEnvironmentVariable("LOCALAPPDATA");
        public const string LocalAppDataSubfolder = @"Microsoft\Azure Batch Explorer";

        public const string PluginSubfolderName = "Plugins";

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
                    batchException.RequestInformation.AzureError != null)
                {
                    if(batchException.RequestInformation.HttpStatusCode == (int)HttpStatusCode.NotFound)
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
    }
}
