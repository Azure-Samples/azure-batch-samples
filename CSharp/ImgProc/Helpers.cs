// -----------------------------------------------------------------------------------------
// <copyright file="Helpers.cs" company="Microsoft">
//    Copyright Microsoft Corporation
// 
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//      http://www.apache.org/licenses/LICENSE-2.0
// 
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.
// </copyright>
// -----------------------------------------------------------------------------------------

using System;
using System.Configuration;
using Microsoft.Azure.Batch.Auth;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Microsoft.Azure.Batch.Samples.ImgProcSample
{
    public class Config
    {
        public string BatchServiceUrl { get; private set; }

        public string AccountName { get; private set; }

        public string Key { get; private set; }

        public string StorageKey { get; private set; }

        public string StorageAccount { get; private set; }

        public string StorageBlobEndpoint { get; private set; }

        public int NumTasks { get; private set; }

        public int NumTvms { get; private set; }

        public string ResourceContainerSAS { get; private set; }

        public string ImageMagickExeSAS { get; private set; }

        public string InputDataContainerSAS { get; private set; }

        public string InputBlobPrefix { get; private set; }

        public int NumInputBlobs { get; private set; }

        public string OutputContainerSAS { get; private set; }

        public string WorkitemName { get; private set; }

        public string PoolName { get; private set; }

        public bool WaitForCompletion { get; private set; }

        public bool WaitForPool { get; private set; }

        public bool CreatePool { get; private set; }

        public bool DeleteWorkitem { get; private set; }

        public bool DeletePool { get; private set; }

        public bool UploadResources { get; private set; }

        public bool InitializeStorageContainerSAS { get; private set; }

        public IBatchClient Client { get; private set; }

        /// <summary>
        /// Read the configuration object in from the App.Config
        /// </summary>
        /// <returns></returns>
        public static Config ParseConfig()
        {
            var config = new Config();

            config.BatchServiceUrl = GetConfigParam("BatchServiceUrl");
            config.AccountName = GetConfigParam("Account");
            config.Key = GetConfigParam("Key");
            config.Client = BatchClient.Connect(config.BatchServiceUrl, new BatchCredentials(config.AccountName, config.Key));

            config.StorageAccount = GetConfigParam("StorageAccount");
            config.StorageKey = GetConfigParam("StorageKey");
            config.StorageBlobEndpoint = "https://" + config.StorageAccount + ".blob.core.windows.net";

            config.NumTasks = Int32.Parse(GetConfigParam("NumTasks"));
            config.NumTvms = Int32.Parse(GetConfigParam("NumTvms"));
            config.InputDataContainerSAS = GetConfigParam("InputDataContainerSAS");
            config.InputBlobPrefix = GetConfigParam("InputBlobPrefix");
            config.NumInputBlobs = Int32.Parse(GetConfigParam("NumInputBlobs"));
            config.OutputContainerSAS = GetConfigParam("OutputContainerSAS");

            config.WorkitemName = "ImgProcWi" + DateTime.Now.ToString("_yyMMdd_HHmmss_") + Guid.NewGuid().ToString("N");

            config.ResourceContainerSAS = GetConfigParam("ResourcesSAS");
            config.ImageMagickExeSAS = GetConfigParam("ImageMagickExeSAS");

            config.WaitForCompletion = bool.Parse(GetConfigParam("WaitForCompletion"));
            config.WaitForPool = bool.Parse(GetConfigParam("WaitForPool"));

            config.DeleteWorkitem = bool.Parse(GetConfigParam("DeleteWorkitem"));

            config.CreatePool = bool.Parse(GetConfigParam("CreatePool"));
            config.PoolName = GetConfigParam("PoolName");

            if (config.CreatePool)
            {
                if(String.IsNullOrEmpty(config.PoolName))
                {
                    config.PoolName = "ImgProcPool" + Guid.NewGuid().ToString("N");
                }
            }
            else
            {
                if (String.IsNullOrEmpty(config.PoolName))
                {
                    throw new Exception("Provide pool name as CreatePool is false");
                }
            }

            config.DeletePool = bool.Parse(GetConfigParam("DeletePool"));

            config.InitializeStorageContainerSAS = bool.Parse(GetConfigParam("InitStorage"));
            if (config.InitializeStorageContainerSAS)
            {
                string account = GetConfigParam("StorageAccount");
                string key = GetConfigParam("StorageKey");
                config.InputDataContainerSAS = ImgProcUtils.CreateContainerWithPolicySASIfNotExist(
                    account, 
                    key, 
                    "watask-input", 
                    "readandlist", 
                    DateTime.Now, 
                    DateTime.Now.AddMonths(12), 
                    SharedAccessBlobPermissions.Read | SharedAccessBlobPermissions.List);
                
                config.OutputContainerSAS = ImgProcUtils.CreateContainerWithPolicySASIfNotExist(
                    account, 
                    key, 
                    "watask-output", 
                    "readandwrite", 
                    DateTime.Now, 
                    DateTime.Now.AddMonths(12), 
                    SharedAccessBlobPermissions.Read | SharedAccessBlobPermissions.Write);

                config.ResourceContainerSAS = ImgProcUtils.CreateContainerWithPolicySASIfNotExist(
                    account, 
                    key, 
                    "watask-resource", 
                    "readandwrite", 
                    DateTime.Now, 
                    DateTime.Now.AddMonths(12), 
                    SharedAccessBlobPermissions.Read | SharedAccessBlobPermissions.Write);
            }

            config.UploadResources = bool.Parse(GetConfigParam("UploadResources"));
            
            return config;
        }

        private static string GetConfigParam(String keyName)
        {
            return ConfigurationManager.AppSettings[keyName];
        }
    }

    public static class Constants
    {
        public const string ImgProcExeName = "ImgProc.exe";
        public const string StorageClientDllName = "Microsoft.WindowsAzure.Storage.dll";
        public const string BatchClientDllName = "Microsoft.Azure.Batch.dll";
        public const string CommonDllName = "Common.dll";
        public const string ImgProcTaskPrefix = "ImgProcTask";
    }

    
}
