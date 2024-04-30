// Copyright (c) Microsoft Corporation. All rights reserved.

using System;
using System.Threading.Tasks;

namespace Azure.Compute.Batch.Samples.HelloWorld
{
    /// <summary>
    /// The main program of the HelloWorld sample
    /// </summary>
    public static class Program
    {
        private static string batchAccountResourceId = "your-batch-account-resource-id";

        public async static Task Main(string[] args)
        {
            await new HelloWorldSample().Run(batchAccountResourceId);

            Console.WriteLine("Press return to exit...");
            Console.ReadLine();
        }
    }
}
