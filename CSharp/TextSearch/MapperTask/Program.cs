//Copyright (c) Microsoft Corporation

namespace Microsoft.Azure.Batch.Samples.TextSearch
{
    using System;
    using Common;

    public class Program
    {
        public static void Main(string[] args)
        {
            if (args != null && args.Length > 0)
            {
                if (args.Length != 1)
                {
                    DisplayUsage();
                    throw new ArgumentException("Incorrect number of arguments");
                }

                string blobSas = args[0];

                try
                {
                    MapperTask mapperTask = new MapperTask(blobSas);
                    mapperTask.RunAsync().Wait();
                }
                catch (AggregateException e)
                {
                    SampleHelpers.PrintAggregateException(e);

                    throw;
                }
            }
            else
            {
                DisplayUsage();
            }
        }

        /// <summary>
        /// Displays the usage of this executable.
        /// </summary>
        private static void DisplayUsage()
        {
            Console.WriteLine("{0} Usage:", Constants.MapperTaskExecutable);
            Console.WriteLine("{0} <blob SAS>       - Runs the mapper task, which downloads a file and performs a search on it", Constants.MapperTaskExecutable);
        }
    }
}
