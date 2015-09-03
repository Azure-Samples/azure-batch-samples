//Copyright (c) Microsoft Corporation

namespace Microsoft.Azure.Batch.Samples.JobManager
{
    using System;
    using Common;

    public class Program
    {
        public static void Main(string[] args)
        {
            try
            {
                SampleJobManagerTask jobManagerTask = new SampleJobManagerTask();

                jobManagerTask.RunAsync().Wait();
            }
            catch (AggregateException e)
            {
                SampleHelpers.PrintAggregateException(e);

                throw;
            }
        }
    }
}
