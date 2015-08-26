namespace Microsoft.Azure.Batch.Samples.TextSearch
{
    using System;

    public class Program
    {
        public static void Main(string[] args)
        {
            try
            {
                ReducerTask reducerTask = new ReducerTask();
                reducerTask.RunAsync().Wait();
            }
            catch (AggregateException e)
            {
                Helpers.ProcessAggregateException(e);

                throw;
            }
        }
    }
}
