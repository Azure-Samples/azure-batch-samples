//Copyright (c) Microsoft Corporation

namespace Microsoft.Azure.Batch.Samples.TelemetryStartTask
{
    using ApplicationInsights.Extensibility;
    using System;
    using TelemetryInitializer;

    public static class Program
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
