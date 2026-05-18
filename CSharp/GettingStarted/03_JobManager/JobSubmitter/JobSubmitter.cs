//Copyright (c) Microsoft Corporation

namespace Microsoft.Azure.Batch.Samples.JobManager
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
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
    /// </summary>
    public class JobSubmitter
    {
        private readonly Settings jobManagerSettings;
        private readonly AccountSettings accountSettings;

        private const string JobManagerTaskExe = "SampleJobManagerTask.exe";
        private const string JobManagerTaskId = "SampleJobManager";

        public JobSubmitter()
        {
            this.jobManagerSettings = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("settings.json")
                .Build()
                .Get<Settings>();
            this.accountSettings = SampleHelpers.LoadAccountSettings();
        }

        public JobSubmitter(AccountSettings accountSettings, Settings settings)
        {
            this.jobManagerSettings = settings;
            this.accountSettings = accountSettings;
        }

        public async Task RunAsync()
        {
            Console.WriteLine("Running with the following settings: ");
            Console.WriteLine("-------------------------------------");
            Console.WriteLine(this.jobManagerSettings.ToString());
            Console.WriteLine(this.accountSettings.ToString());

            BatchClient batchClient = ClientFactory.CreateBatchClient(this.accountSettings);
            BlobServiceClient blobServiceClient = ClientFactory.CreateBlobServiceClient(this.accountSettings);
            BatchAccountResource batchAccount = SampleHelpers.GetBatchAccountResource(this.accountSettings);

            string jobId = null;

            try
            {
                await this.CreatePoolIfNotExistAsync(batchAccount, blobServiceClient);

                jobId = GettingStartedCommon.CreateJobId("SimpleJob");
                await this.SubmitJobAsync(batchClient, blobServiceClient, jobId);

                await GettingStartedCommon.PrintJobsAsync(batchClient);
                await GettingStartedCommon.PrintPoolsAsync(batchAccount);

                await GettingStartedCommon.WaitForTasksAndPrintOutputAsync(batchClient, jobId, new[] { JobManagerTaskId }, TimeSpan.FromMinutes(10));
            }
            finally
            {
                List<string> jobIdsToDelete = new List<string>();
                List<string> poolIdsToDelete = new List<string>();

                if (this.jobManagerSettings.ShouldDeleteJob && !string.IsNullOrEmpty(jobId))
                {
                    jobIdsToDelete.Add(jobId);
                }

                if (this.jobManagerSettings.ShouldDeletePool)
                {
                    poolIdsToDelete.Add(this.jobManagerSettings.PoolId);
                }

                await SampleHelpers.DeleteBatchResourcesAsync(batchClient, batchAccount, jobIdsToDelete, poolIdsToDelete);
            }
        }

        private async Task CreatePoolIfNotExistAsync(BatchAccountResource batchAccount, BlobServiceClient blobServiceClient)
        {
            string localSampleFilePath = GettingStartedCommon.GenerateTemporaryFile("StartTask.txt", "hello from Batch JobManager sample!");
            await SampleHelpers.UploadResourcesAsync(blobServiceClient, this.jobManagerSettings.BlobContainer, new[] { localSampleFilePath });
            string containerSas = await SampleHelpers.ConstructContainerSasAsync(
                blobServiceClient,
                this.jobManagerSettings.BlobContainer,
                BlobContainerSasPermissions.Read | BlobContainerSasPermissions.List);

            var startTask = new BatchAccountPoolStartTask { CommandLine = "cmd /c dir" };
            startTask.ResourceFiles.Add(new BatchResourceFile { BlobContainerUri = new Uri(containerSas) });

            var poolData = new BatchAccountPoolData
            {
                VmSize = this.jobManagerSettings.PoolNodeVirtualMachineSize,
                DeploymentConfiguration = new BatchDeploymentConfiguration
                {
                    VmConfiguration = new BatchVmConfiguration(
                        new BatchImageReference
                        {
                            Publisher = this.jobManagerSettings.ImagePublisher,
                            Offer = this.jobManagerSettings.ImageOffer,
                            Sku = this.jobManagerSettings.ImageSku,
                            Version = this.jobManagerSettings.ImageVersion,
                        },
                        this.jobManagerSettings.NodeAgentSkuId),
                },
                ScaleSettings = new BatchAccountPoolScaleSettings
                {
                    FixedScale = new BatchAccountFixedScaleSettings
                    {
                        TargetDedicatedNodes = this.jobManagerSettings.PoolTargetNodeCount,
                    },
                },
                StartTask = startTask,
            };

            await GettingStartedCommon.CreatePoolIfNotExistAsync(batchAccount, this.jobManagerSettings.PoolId, poolData);
        }

        private async Task SubmitJobAsync(BatchClient batchClient, BlobServiceClient blobServiceClient, string jobId)
        {
            // Upload all files in the JobSubmitter output directory so that the job manager has all the
            // dependencies it needs (Common.dll, Azure.Compute.Batch.dll, Azure.Storage.Blobs.dll, etc.).
            string outputDir = AppDomain.CurrentDomain.BaseDirectory;
            var jobManagerFiles = Directory
                .EnumerateFiles(outputDir, "*", SearchOption.TopDirectoryOnly)
                .Where(f =>
                {
                    string ext = Path.GetExtension(f).ToLowerInvariant();
                    return ext == ".dll" || ext == ".exe" || ext == ".pdb" || ext == ".config" || ext == ".json";
                })
                .ToList();

            await SampleHelpers.UploadResourcesAsync(blobServiceClient, this.jobManagerSettings.BlobContainer, jobManagerFiles);

            List<ResourceFile> jobManagerResourceFiles = await SampleHelpers.UploadResourcesAndCreateResourceFileReferencesAsync(
                blobServiceClient,
                this.jobManagerSettings.BlobContainer,
                jobManagerFiles);

            var jobManagerTask = new BatchJobManagerTask(JobManagerTaskId, JobManagerTaskExe)
            {
                KillJobOnCompletion = true,
            };
            foreach (var rf in jobManagerResourceFiles)
            {
                jobManagerTask.ResourceFiles.Add(rf);
            }

            var envSettings = new[]
            {
                new EnvironmentSetting("SAMPLE_BATCH_URL") { Value = this.accountSettings.BatchServiceUrl },
                new EnvironmentSetting("SAMPLE_STORAGE_ACCOUNT") { Value = this.accountSettings.StorageAccountName },
                new EnvironmentSetting("SAMPLE_STORAGE_URL") { Value = this.accountSettings.StorageServiceUrl },
            };
            foreach (var es in envSettings)
            {
                jobManagerTask.EnvironmentSettings.Add(es);
            }

            var jobOptions = new BatchJobCreateOptions(jobId, new BatchPoolInfo { PoolId = this.jobManagerSettings.PoolId })
            {
                JobManagerTask = jobManagerTask,
            };

            await batchClient.CreateJobAsync(jobOptions);
        }
    }
}
