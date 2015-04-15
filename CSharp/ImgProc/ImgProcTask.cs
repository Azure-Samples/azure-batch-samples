// -----------------------------------------------------------------------------------------
// <copyright file="ImgProcTask.cs" company="Microsoft">
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

using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Diagnostics;
using System.IO;

namespace Microsoft.Azure.Batch.Samples.ImgProcSample
{
    public class ImgProcTask
    {
        // This is the part that is executed by Azure Batch VMs in the cloud. 
        // From Azure Batch PoV, it's just an executable that runs for a while.
        // Step 1. Call "convert.exe" to process an image.
        // Step 2. Update result into blob.
        // Note there is no explicit download step since it's handled by Task scheduler

        public static void TaskMain(string[] args)
        {
            if (args == null || args.Length != 3)
            {
                throw new Exception("Usage: ImgProc.exe --Task <outputContainerSAS> <OutputFilenamePrefix>");
            }

            Console.WriteLine("Start ImgProcTask.");

            string outputContainerSAS = args[1];
            string prefix = args[2];

            //Convert all the jpg files to thumbnails by launching imagemick process
            GenerateThumbnailImages(prefix);            

            string[] outputFiles = Directory.GetFiles(Directory.GetCurrentDirectory(), prefix + "*");

            if (outputFiles == null ||
                outputFiles.Length == 0)
            {
                throw new Exception("Thumbnail files not generated");
            }

            //Store output to blob
            foreach (string outputFile in outputFiles)
            {
                int index = outputFile.LastIndexOf("thumb");
                string blobName = outputFile.Substring(index);
                ImgProcUtils.UploadFileToBlob(blobName, outputContainerSAS);
            }
            Console.WriteLine("Done.");
        }

        private static void GenerateThumbnailImages(string prefix)
        {
            string tvmRootDir = Environment.GetEnvironmentVariable("WATASK_TVM_ROOT_DIR");
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = tvmRootDir + @"\shared\imagemagick\convert.exe";
            startInfo.Arguments = "*.jpg -resize 120x120 " + prefix + "%03d.jpg";
            startInfo.CreateNoWindow = true;
            startInfo.RedirectStandardError = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.UseShellExecute = false;

            Process p = new Process();
            p.StartInfo = startInfo;

            Console.WriteLine("Start process {0} {1}", startInfo.FileName, startInfo.Arguments);

            p.Start();
            p.WaitForExit();
            string errorStream = p.StandardError.ReadToEnd();

            if (!String.IsNullOrEmpty(errorStream))
            {
                Console.WriteLine(errorStream);
                Environment.Exit(100);
            }
            string outputStream = p.StandardOutput.ReadToEnd();
            if (!String.IsNullOrEmpty(outputStream))
            {
                Console.WriteLine(outputStream);
            }
        }
    }
}
