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
                Console.WriteLine("Within Program Main of SampleJobManagerTask");
                SampleJobManagerTask jobManagerTask = new SampleJobManagerTask();

                Console.WriteLine(jobManagerTask.configurationSettings.ToString());

                jobManagerTask.RunAsync().Wait();
            }
            catch (Exception e)
            {
                Console.WriteLine("Inside regular exception block");
                Console.WriteLine(e);
                throw;
            }
            //catch (AggregateException e)
            //{
            //    SampleHelpers.PrintAggregateException(e);

            //    throw;
            //}
        }
    }
}
