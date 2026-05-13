//Copyright (c) Microsoft Corporation

namespace Microsoft.Azure.Batch.Samples.HelloWorld
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using global::Azure;
    using global::Azure.Compute.Batch;
    using Microsoft.Azure.Batch.Samples.Common;
    using Microsoft.Extensions.Configuration;

    /// <summary>
    /// The main program of the HelloWorld sample
    /// </summary>
    public static class Program
    {
        public static void Main(string[] args)
        {
            try
            {
                AccountSettings accountSettings = SampleHelpers.LoadAccountSettings();
                Settings helloWorldSettings = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("settings.json")
                    .Build()
                    .Get<Settings>();

                HelloWorldAsync(accountSettings, helloWorldSettings).Wait();
            }
            catch (AggregateException aggregateException)
            {
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

        /// <summary>
        /// Submits a job to the Azure Batch service, and waits for it to complete.
        /// </summary>
        private static async Task HelloWorldAsync(
            AccountSettings accountSettings,
            Settings helloWorldConfigurationSettings)
        {
            Console.WriteLine("Running with the following settings: ");
            Console.WriteLine("-------------------------------------");
            Console.WriteLine(helloWorldConfigurationSettings.ToString());
            Console.WriteLine(accountSettings.ToString());

            BatchClient batchClient = ClientFactory.CreateBatchClient(accountSettings);

            string jobId = GettingStartedCommon.CreateJobId("HelloWorldJob");

            try
            {
                await SubmitJobAsync(batchClient, helloWorldConfigurationSettings, jobId);
                await GettingStartedCommon.WaitForTasksAndPrintOutputAsync(batchClient, jobId, new[] { "task1" }, TimeSpan.FromMinutes(10));
            }
            finally
            {
                if (!string.IsNullOrEmpty(jobId) && helloWorldConfigurationSettings.ShouldDeleteJob)
                {
                    Console.WriteLine("Deleting job: {0}", jobId);
                    await batchClient.DeleteJobAsync(WaitUntil.Started, jobId);
                }
            }
        }

        /// <summary>
        /// Creates a job with an auto-pool and adds a task to it.
        /// </summary>
        private static async Task SubmitJobAsync(
            BatchClient batchClient,
            Settings configurationSettings,
            string jobId)
        {
            var imageReference = new BatchVmImageReference
            {
                Publisher = configurationSettings.ImagePublisher,
                Offer = configurationSettings.ImageOffer,
                Sku = configurationSettings.ImageSku,
                Version = configurationSettings.ImageVersion,
            };

            var poolSpec = new BatchPoolSpecification(configurationSettings.PoolNodeVirtualMachineSize)
            {
                TargetDedicatedNodes = configurationSettings.PoolTargetNodeCount,
                VirtualMachineConfiguration = new VirtualMachineConfiguration(imageReference, configurationSettings.NodeAgentSkuId),
            };

            var autoPool = new BatchAutoPoolSpecification(BatchPoolLifetimeOption.JobOption)
            {
                AutoPoolIdPrefix = "HelloWorld",
                KeepAlive = false,
                Pool = poolSpec,
            };

            var jobOptions = new BatchJobCreateOptions(jobId, new BatchPoolInfo { AutoPoolSpecification = autoPool });
            await batchClient.CreateJobAsync(jobOptions);

            await batchClient.CreateTaskAsync(jobId, new BatchTaskCreateOptions("task1", "cmd /c echo Hello world from the Batch Hello world sample!"));
        }
    }
}
