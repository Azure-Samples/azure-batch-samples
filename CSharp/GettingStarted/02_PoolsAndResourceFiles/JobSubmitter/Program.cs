//Copyright (c) Microsoft Corporation

namespace Microsoft.Azure.Batch.Samples.PoolsAndResourceFiles
{
    using System;
    using Common;

    /// <summary>
    /// The main program of the PoolsAndResourceFiles sample
    /// </summary>
    public static class Program
    {
        public static void Main(string[] args)
        {
            // This will boost parallel submission speed for REST APIs. If your use requires many simultaneous service calls set this number to something large, such as 100.
            // See: https://msdn.microsoft.com/en-us/library/system.net.servicepointmanager.defaultconnectionlimit.aspx for more info.
            System.Net.ServicePointManager.DefaultConnectionLimit = 20;

            try
            {
                JobSubmitter jobSubmitter = new JobSubmitter();
                jobSubmitter.RunAsync().Wait();
            }
            catch (AggregateException aggregateException)
            {
                // Go through all exceptions and dump useful information
                SampleHelpers.PrintAggregateException(aggregateException);

                throw;
            }

            Console.WriteLine("Press return to exit...");
            Console.ReadLine();
        }
    }
}
