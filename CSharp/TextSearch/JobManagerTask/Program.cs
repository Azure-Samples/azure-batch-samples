//Copyright (c) Microsoft Corporation

namespace Microsoft.Azure.Batch.Samples.TextSearch
{
    using System;
    using Common;

    public static class Program
    {
        public static void Main(string[] args)
        {
            try
            {
                TextSearchJobManagerTask jobManagerTask = new TextSearchJobManagerTask();

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
