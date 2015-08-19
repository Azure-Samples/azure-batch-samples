namespace Microsoft.Azure.Batch.Samples.HelloWorld
{
    using System;

    /// <summary>
    /// The main program of the HelloWorld sample
    /// </summary>
    public static class Program
    {
        public static void Main(string[] args)
        {
            // This will boost parallel submission speed for REST APIs. If your use requires many simultaneous service calls set this number to something large, such as 100.  
            // See: http://msdn.microsoft.com/en-us/library/system.net.servicepointmanager.defaultconnectionlimit%28v=vs.110%29.aspx for more info.
            System.Net.ServicePointManager.DefaultConnectionLimit = 20;

            try
            {
                JobSubmitter jobSubmitter = new JobSubmitter();
                jobSubmitter.RunAsync().Wait();
            }
            catch (AggregateException aggregateException)
            {
                // Go through all exceptions and dump useful information
                foreach (Exception exception in aggregateException.InnerExceptions)
                {
                    Console.WriteLine(exception.ToString());
                    Console.WriteLine();
                }

                throw;
            }

            Console.WriteLine("Press return to exit...");
            Console.ReadLine();
        }
    }
}
