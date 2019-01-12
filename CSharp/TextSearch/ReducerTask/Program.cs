//Copyright (c) Microsoft Corporation

namespace Microsoft.Azure.Batch.Samples.TextSearch
{
    using System;
    using Common;

    public class Program
    {
        public static void Main(string[] args)
        {
            try
            {
                ReducerTask reducerTask = new ReducerTask();
                reducerTask.Run();
            }
            catch (AggregateException e)
            {
                SampleHelpers.PrintAggregateException(e);

                throw;
            }
        }
    }
}
