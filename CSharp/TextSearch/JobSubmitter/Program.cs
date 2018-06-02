//Copyright (c) Microsoft Corporation

namespace Microsoft.Azure.Batch.Samples.TextSearch
{
    using System;
    using Common;

    /// <summary>
    /// The main program for the JobSubmitter
    /// </summary>
    public class Program
    {
        public static void Main(string[] args)
        {
            try
            {
                JobSubmitter jobSubmitter = new JobSubmitter();

                jobSubmitter.RunAsync().Wait();
            }
            catch (AggregateException e)
            {
                SampleHelpers.PrintAggregateException(e);

                throw;
            }
            
        }
    }
}
