// -----------------------------------------------------------------------------------------
// <copyright file="Job.cs" company="Microsoft">
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

namespace Microsoft.Azure.Batch.Samples.ImgProcSample
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Batch.Common;
    using Microsoft.Azure.Batch.FileStaging;

    /// <summary>
    /// In this sample, we have a set of input blobs and we use Task Service to process
    /// these blobs in parallel on multiple VMs. Each Task creates a thumbnail image
    /// of the corresponding image in the input blob
    /// 
    /// The sample creates a run-once workitem with no job manager. We then create 
    /// multiple tasks and assign multiple images to each task.
    /// </summary>
    public class MainProgram
    {
        private static Config config;
        private const string ImageMagickCmdLine = @"cmd /c ImageMagick.exe /VERYSILENT /LOG=install.log /DIR=%WATASK_TVM_ROOT_DIR%\shared\imagemagick";

        /// <summary>
        /// Creates or deletes a pool, as specified by the command line arguments.
        /// </summary>
        /// <param name="args">The command line arguments</param>
        public static void MainProgramPool(string[] args)
        {
            config = Config.ParseConfig();

            if (args != null && args.Length > 1 && args[1] == "--Create")
            {
                CreatePool();
            }
            else
            {
                if (args != null && args.Length > 1 && args[1] == "--Delete")
                {
                    DeletePool();
                }
            }
        }

        /// <summary>
        /// Creates a pool with a start task which installs ImageMagick onto each VM.
        /// </summary>
        private static void CreatePool()
        {
            using (IPoolManager pm = config.Client.OpenPoolManager())
            {
                Console.WriteLine("Creating pool: {0}", config.PoolName);
                //Create a pool -- note that OSFamily 3 is Windows Server 2012.  You can learn more about os families and versions at:
                //http://msdn.microsoft.com/en-us/library/azure/ee924680.aspx
                ICloudPool pool = pm.CreatePool(config.PoolName, targetDedicated: 3, vmSize: "small", osFamily: "3");
                
                //Create a start task for the pool (which installs ImageMagick).  The start task is run on each 
                //VM when they first join the pool.  In this case it is used as a setup step to place the ImageMagick exe onto the VM.
                IStartTask startTask = new StartTask();
                startTask.RunElevated = true;
                startTask.CommandLine = ImageMagickCmdLine;

                //Set up the resource files for the start task (requires the image magick exe)
                List<IResourceFile> resFiles = new List<IResourceFile>();

                ResourceFile file = new ResourceFile(config.ImageMagickExeSAS, "ImageMagick.exe");
                resFiles.Add(file);

                startTask.ResourceFiles = resFiles;
                startTask.WaitForSuccess = true;
                
                pool.StartTask = startTask;
                pool.Commit(); //Commit the pool -- this actually creates it on the Batch service.
            }
        }

        /// <summary>
        /// Deletes the pool.
        /// </summary>
        private static void DeletePool()
        {
            Console.WriteLine("Deleting Pool: {0}", config.PoolName);
            using (IPoolManager poolManager = config.Client.OpenPoolManager())
            {
                poolManager.DeletePool(config.PoolName);
            }
        }

        /// <summary>
        /// This is the client that creates workitem and submits tasks. 
        /// </summary>
        /// <param name="args"></param>
        public static void SubmitTasks(string[] args)
        {
            config = Config.ParseConfig();

            //Upload resources if specified
            if (config.UploadResources)
            {
                //Upload ImgProc.exe, Batch.dll and the Storage Client
                ImgProcUtils.UploadFileToBlob(Constants.StorageClientDllName, config.ResourceContainerSAS);
                ImgProcUtils.UploadFileToBlob(Constants.ImgProcExeName, config.ResourceContainerSAS);
                ImgProcUtils.UploadFileToBlob(Constants.BatchClientDllName, config.ResourceContainerSAS);
                Console.WriteLine("Done uploading files to blob");
            }

            try
            {
                using (IWorkItemManager wm = config.Client.OpenWorkItemManager())
                {
                    IToolbox toolbox = config.Client.OpenToolbox();
                    
                    //Use the task submission helper to ease creation of workitem and addition of tasks, as well as management of resource file staging.
                    ITaskSubmissionHelper taskSubmissionHelper = toolbox.CreateTaskSubmissionHelper(wm, config.PoolName);
                    taskSubmissionHelper.WorkItemName = config.WorkitemName;

                    //Compute the number of images each task should process
                    int numImgsPerTask = (int)Math.Round(config.NumInputBlobs / (decimal)config.NumTasks);

                    for (int i = 0; i < config.NumTasks; i++)
                    {
                        ICloudTask task = new CloudTask(
                            name: "task_no_" + i, 
                            commandline: string.Format("{0} --Task {1} thumb{2}", Constants.ImgProcExeName, config.OutputContainerSAS, i));
                        
                        Console.WriteLine("Generating task: {0}", task.Name);

                        task.FilesToStage = new List<IFileStagingProvider>();

                        int start = i * numImgsPerTask;
                        int end;
                        if (i < config.NumTasks - 1)
                        {
                            end = ((i + 1) * numImgsPerTask) - 1;
                        }
                        else
                        {
                            end = config.NumInputBlobs - 1;
                        }

                        //Generate and set up the list of files to be processed by this task
                        for (int j = start; j < end; j++)
                        {
                            string input = GetTempFilePath(j);
                            ImgProcUtils.GenerateImages(input, string.Format("{0}", j));
                            task.FilesToStage.Add(new FileToStage(input, new StagingStorageAccount(config.StorageAccount, config.StorageKey, config.StorageBlobEndpoint)));
                        }
                        task.ResourceFiles = ImgProcUtils.GetResourceFiles(config.ResourceContainerSAS);
                        taskSubmissionHelper.AddTask(task);
                    }

                    IJobCommitUnboundArtifacts artifacts = null;
                    try
                    {
                        Console.WriteLine("Submitting {0} tasks to the Batch Service", config.NumTasks);
                        
                        //Submit the tasks to the Batch Service
                        artifacts = taskSubmissionHelper.Commit() as IJobCommitUnboundArtifacts;
                    }
                    catch (AggregateException ae)
                    {
                        // Go through all exceptions and dump useful information
                        ae.Handle(x =>
                        {
                            if (x is BatchException)
                            {
                                BatchException be = x as BatchException;
                                if (null != be.RequestInformation && null != be.RequestInformation.AzureError)
                                {
                                    // Write the server side error information
                                    Console.Error.WriteLine(be.RequestInformation.AzureError.Code);
                                    Console.Error.WriteLine(be.RequestInformation.AzureError.Message.Value);
                                    if (null != be.RequestInformation.AzureError.Values)
                                    {
                                        foreach (var v in be.RequestInformation.AzureError.Values)
                                        {
                                            Console.Error.WriteLine(v.Key + " : " + v.Value);
                                        }
                                    }
                                }
                            }
                            // Indicate that the error has been handled
                            return true;
                        });
                    }

                    DateTime starttime = DateTime.Now;

                    //Wait for the job to complete
                    if (config.WaitForCompletion)
                    {
                        ICloudJob job = wm.GetJob(artifacts.WorkItemName, artifacts.JobName);

                        Console.WriteLine("Waiting for tasks to complete...");

                        // Wait up to 15 minutes for all tasks to reach the completed state
                        config.Client.OpenToolbox().CreateTaskStateMonitor().WaitAll(job.ListTasks(), TaskState.Completed, new TimeSpan(0, 15, 0));

                        DateTime endtime = DateTime.Now;

                        Console.WriteLine("Time taken for processing the images : {0} sec", endtime.Subtract(starttime).TotalSeconds);
                    }
                }
            }
            finally
            {
                //Delete the workitem that we created
                if (config.DeleteWorkitem &&
                    config.WaitForCompletion)
                {
                    Console.WriteLine("Press any key to delete the workitem . . .");
                    Console.ReadKey();
                    config.Client.OpenWorkItemManager().DeleteWorkItem(config.WorkitemName);
                }
            }
        }        

        private static string GetTempFilePath(int index) 
        {
            string path = Environment.GetEnvironmentVariable("TEMP");
            string file = System.IO.Path.Combine(path, string.Format("{0}.jpg", index));
            return file;
        }
    }
}
