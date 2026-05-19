//Copyright (c) Microsoft Corporation

namespace Microsoft.Azure.Batch.Samples.PoolsAndResourceFiles
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using global::Azure.Compute.Batch;
    using global::Azure.ResourceManager.Batch;
    using global::Azure.ResourceManager.Batch.Models;
    using global::Azure.Storage.Blobs;
    using global::Azure.Storage.Sas;
    using Microsoft.Azure.Batch.Samples.Common;
    using Microsoft.Extensions.Configuration;

    /// <summary>
    /// Manages submission and lifetime of the Azure Batch job and pool.
    /// Pool operations use the Azure.ResourceManager.Batch (ARM) SDK.
    /// </summary>
    public class JobSubmitter
    {
        private readonly Settings poolsAndResourceFileSettings;
        private readonly AccountSettings accountSettings;

        // The SimpleTask project is included via project-dependency, so the
        // executable produced by that project will be in the same working
        // directory as JobSubmitter at runtime.
        private const string SimpleTaskExe = "SimpleTask.exe";

        public JobSubmitter()
        {
            this.accountSettings = SampleHelpers.LoadAccountSettings();
            this.poolsAndResourceFileSettings = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("settings.json")
                .Build()
                .Get<Settings>();
        }

        public JobSubmitter(AccountSettings accountSettings, Settings settings)
        {
            this.poolsAndResourceFileSettings = settings;
            this.accountSettings = accountSettings;
        }

        public async Task RunAsync()
        {
            Console.WriteLine("Running with the following settings: ");
            Console.WriteLine("-------------------------------------");
            Console.WriteLine(this.poolsAndResourceFileSettings.ToString());
            Console.WriteLine(this.accountSettings.ToString());

            BatchClient batchClient = ClientFactory.CreateBatchClient(this.accountSettings);
            BlobServiceClient blobServiceClient = ClientFactory.CreateBlobServiceClient(this.accountSettings);
            BatchAccountResource batchAccount = SampleHelpers.GetBatchAccountResource(this.accountSettings);

            string jobId = null;
            HashSet<string> blobContainerNames = new HashSet<string>();

            try
            {
                await this.CreatePoolIfNotExistAsync(batchAccount, blobServiceClient);

                jobId = GettingStartedCommon.CreateJobId("SimpleJob");
                var taskIds = await this.SubmitJobAsync(batchClient, blobServiceClient, jobId, blobContainerNames);

                await GettingStartedCommon.PrintJobsAsync(batchClient);
                await GettingStartedCommon.PrintPoolsAsync(batchAccount);

                await GettingStartedCommon.WaitForTasksAndPrintOutputAsync(batchClient, jobId, taskIds, TimeSpan.FromMinutes(10));
            }
            finally
            {
                await SampleHelpers.DeleteContainersAsync(blobServiceClient, blobContainerNames);

                List<string> jobIdsToDelete = new List<string>();
                List<string> poolIdsToDelete = new List<string>();

                if (this.poolsAndResourceFileSettings.ShouldDeleteJob && !string.IsNullOrEmpty(jobId))
                {
                    jobIdsToDelete.Add(jobId);
                }

                if (this.poolsAndResourceFileSettings.ShouldDeletePool)
                {
                    poolIdsToDelete.Add(this.poolsAndResourceFileSettings.PoolId);
                }

                await SampleHelpers.DeleteBatchResourcesAsync(batchClient, batchAccount, jobIdsToDelete, poolIdsToDelete);
            }
        }

        /// <summary>
        /// Creates a pool via the ARM SDK if it doesn't already exist.
        /// </summary>
        private async Task CreatePoolIfNotExistAsync(BatchAccountResource batchAccount, BlobServiceClient blobServiceClient)
        {
            // Stage start-task resource files in storage and reference them via a container SAS.
            string localSampleFilePath = GettingStartedCommon.GenerateTemporaryFile("StartTask.txt", "hello from Batch PoolsAndResourceFiles sample!");
            await SampleHelpers.UploadResourcesAsync(blobServiceClient, this.poolsAndResourceFileSettings.BlobContainer, new[] { localSampleFilePath });
            string containerSas = await SampleHelpers.ConstructContainerSasAsync(
                blobServiceClient,
                this.poolsAndResourceFileSettings.BlobContainer,
                BlobContainerSasPermissions.Read | BlobContainerSasPermissions.List);

            var startTask = new BatchAccountPoolStartTask { CommandLine = "cmd /c dir" };
            startTask.ResourceFiles.Add(new BatchResourceFile { BlobContainerUri = new Uri(containerSas) });

            var poolData = new BatchAccountPoolData
            {
                VmSize = this.poolsAndResourceFileSettings.PoolNodeVirtualMachineSize,
                DeploymentConfiguration = new BatchDeploymentConfiguration
                {
                    VmConfiguration = new BatchVmConfiguration(
                        new BatchImageReference
                        {
                            Publisher = this.poolsAndResourceFileSettings.ImagePublisher,
                            Offer = this.poolsAndResourceFileSettings.ImageOffer,
                            Sku = this.poolsAndResourceFileSettings.ImageSku,
                            Version = this.poolsAndResourceFileSettings.ImageVersion,
                        },
                        this.poolsAndResourceFileSettings.NodeAgentSkuId),
                },
                ScaleSettings = new BatchAccountPoolScaleSettings
                {
                    FixedScale = new BatchAccountFixedScaleSettings
                    {
                        TargetDedicatedNodes = this.poolsAndResourceFileSettings.PoolTargetNodeCount,
                    },
                },
                StartTask = startTask,
            };

            await GettingStartedCommon.CreatePoolIfNotExistAsync(batchAccount, this.poolsAndResourceFileSettings.PoolId, poolData);
        }

        /// <summary>
        /// Creates a job and adds a task to it. The task is a custom executable which has resource
        /// files associated with it.
        /// </summary>
        private async Task<List<string>> SubmitJobAsync(BatchClient batchClient, BlobServiceClient blobServiceClient, string jobId, HashSet<string> blobContainerNames)
        {
            await batchClient.CreateJobAsync(new BatchJobCreateOptions(jobId, new BatchPoolInfo { PoolId = this.poolsAndResourceFileSettings.PoolId }));

            string localSampleFile = Path.Combine(Path.GetTempPath(), "HelloWorld.txt");
            File.WriteAllText(localSampleFile, "hello from Batch PoolsAndResourceFiles sample!");

            string jobInputContainerName = ("job-input-" + jobId).ToLowerInvariant();
            blobContainerNames.Add(jobInputContainerName);

            var filesToStage = new List<string>
            {
                localSampleFile,
                SimpleTaskExe,
            };

            // Stage the task input files to Azure Storage so they can be downloaded as resource files
            // when the task runs on a compute node. Note: the Batch service does not automatically
            // delete content from your storage account, so files added in this way must be removed
            // manually when they are no longer needed (the sample cleans them up at the end of the run).
            List<ResourceFile> resourceFiles = await FileStager.StageFilesAsBlobsAsync(blobServiceClient, jobInputContainerName, filesToStage);

            var taskOptions = new BatchTaskCreateOptions("task_with_file1", SimpleTaskExe);
            foreach (var rf in resourceFiles)
            {
                taskOptions.ResourceFiles.Add(rf);
            }

            await batchClient.CreateTaskAsync(jobId, taskOptions);

            return new List<string> { taskOptions.Id };
        }
    }
}
