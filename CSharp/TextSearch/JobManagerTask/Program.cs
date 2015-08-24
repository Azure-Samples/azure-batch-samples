namespace Microsoft.Azure.Batch.Samples.TextSearch
{
    using System;

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
                Helpers.ProcessAggregateException(e);

                throw;
            }
        }
    }
}
