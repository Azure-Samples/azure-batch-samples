using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Azure.Batch.Samples.TelemetryInitializer;
using System;

namespace Microsfot.Azure.Batch.Samples.TelemetryStartTask
{
    class Program
    {
        static void Main(string[] args)
        {
            TelemetryConfiguration.Active.TelemetryInitializers.Add(new AzureBatchNodeTelemetryInitializer());
            while (true)
            {
                Console.WriteLine(string.Format("{0} Batch Application Insights process running.", DateTime.Now));
                Console.Out.Flush();
                System.Threading.Thread.Sleep(10000);
            }
        }
    }
}
