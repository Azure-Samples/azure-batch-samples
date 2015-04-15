using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using Microsoft.Azure.Batch;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Microsoft.Azure.Batch.Samples.ImgProcSample
{
    public class ImgProcUtils
    {
        public static List<IResourceFile> GetResourceFiles(string resourceContainerPrefix)
        {
            string[] dependencies =
            {
                Constants.ImgProcExeName, 
                Constants.StorageClientDllName, 
                Constants.BatchClientDllName
            };

            var resources = new List<IResourceFile>();

            for (int i = 0; i < dependencies.Length; ++i)
            {
                ResourceFile res = new ResourceFile(ConstructBlobSource(resourceContainerPrefix, dependencies[i]), dependencies[i]);
                resources.Add(res);
            }

            return resources;
        }

        public static string ConstructBlobSource(string container, string blob)
        {
            int index = container.IndexOf("?");

            if (index != -1)
            {
                //SAS                
                string containerAbsoluteUrl = container.Substring(0, index);
                return containerAbsoluteUrl + "/" + blob + container.Substring(index);
            }
            else
            {
                return container + "/" + blob;
            }
        }

        public static void UploadFileToBlob(string fileName, string containerSAS)
        {
            Console.WriteLine("Uploading {0} to {1}", fileName, containerSAS);
            CloudBlobContainer container = new CloudBlobContainer(new Uri(containerSAS));
            CloudBlockBlob blob = container.GetBlockBlobReference(fileName);
            blob.UploadFromStream(new FileStream(fileName, FileMode.Open, FileAccess.Read));
        }

        public static string CreateContainerWithPolicySASIfNotExist(string account, string key, string container, string policy, DateTime start, DateTime end, SharedAccessBlobPermissions permissions)
        {
            // 1. form the credentail and initial client
            CloudStorageAccount storageaccount = new CloudStorageAccount(new Microsoft.WindowsAzure.Storage.Auth.StorageCredentials(account, key), false);
            CloudBlobClient client = storageaccount.CreateCloudBlobClient();

            // 2. create container if it doesn't exist
            CloudBlobContainer storagecontainer = client.GetContainerReference(container);
            storagecontainer.CreateIfNotExists();

            // 3. validate policy, create/overwrite if doesn't match
            bool policyFound = false;
            SharedAccessBlobPolicy accesspolicy = new SharedAccessBlobPolicy()
            {
                SharedAccessExpiryTime = end,
                SharedAccessStartTime = start,
                Permissions = permissions
            };

            BlobContainerPermissions blobPermissions = storagecontainer.GetPermissions();
            if (blobPermissions.SharedAccessPolicies.ContainsKey(policy))
            {
                SharedAccessBlobPolicy containerpolicy = blobPermissions.SharedAccessPolicies[policy];
                if (!(permissions == (containerpolicy.Permissions & permissions) && start <= containerpolicy.SharedAccessStartTime && end >= containerpolicy.SharedAccessExpiryTime))
                {
                    blobPermissions.SharedAccessPolicies[policy] = accesspolicy;
                }
                else
                {
                    policyFound = true;
                }
            }
            else
            {
                blobPermissions.SharedAccessPolicies.Add(policy, accesspolicy);
            }
            if (!policyFound)
            {
                storagecontainer.SetPermissions(blobPermissions);
            }

            // 4. genereate SAS and return
            string containerSAS = storagecontainer.GetSharedAccessSignature(new SharedAccessBlobPolicy(), policy);
            string containerUrl = storagecontainer.Uri.AbsoluteUri + containerSAS;

            return containerUrl;
        }

        public static string GetTaskName(int idx)
        {
            return Constants.ImgProcTaskPrefix + idx;
        }

        public static void GenerateImages(string destFile, string text)
        {
            Bitmap objBmpImage = new Bitmap(1, 1);

            int intWidth = 0;
            int intHeight = 0;

            // Create the Font object for the image text drawing.
            Font objFont = new Font("Arial", 20, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Pixel);

            // Create a graphics object to measure the text's width and height.
            Graphics objGraphics = Graphics.FromImage(objBmpImage);

            // This is where the bitmap size is determined.
            intWidth = (int)objGraphics.MeasureString(text, objFont).Width;
            intHeight = (int)objGraphics.MeasureString(text, objFont).Height;

            // Create the bmpImage again with the correct size for the text and font.
            objBmpImage = new Bitmap(objBmpImage, new Size(intWidth, intHeight));

            // Add the colors to the new bitmap.
            objGraphics = Graphics.FromImage(objBmpImage);

            // Set Background color
            objGraphics.Clear(Color.White);
            objGraphics.SmoothingMode = SmoothingMode.AntiAlias;
            objGraphics.TextRenderingHint = TextRenderingHint.AntiAlias;
            objGraphics.DrawString(text, objFont, new SolidBrush(Color.FromArgb(102, 102, 102)), 0, 0);
            objGraphics.Flush();
            objBmpImage.Save(destFile, ImageFormat.Jpeg);
        }
    }
}
