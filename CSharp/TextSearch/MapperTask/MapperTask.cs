//Copyright (c) Microsoft Corporation

namespace Microsoft.Azure.Batch.Samples.TextSearch
{
    using System;
    using System.IO;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using Microsoft.WindowsAzure.Storage.Blob;


    /// <summary>
    /// The mapper task - it downloads a file from Azure Storage and processes it searching for a regular expression match on 
    /// each file line.
    /// </summary>
    public class MapperTask
    {
        private readonly Settings configurationSettings;
        private readonly string blobSas;

        /// <summary>
        /// Constructs a mapper task object with the specified blob SAS.
        /// </summary>
        /// <param name="blobSas">The blob SAS to use.</param>
        public MapperTask(string blobSas)
        {
            this.blobSas = blobSas;
            this.configurationSettings = Settings.Default;
        }

        /// <summary>
        /// Runs the mapper task.
        /// </summary>
        public async Task RunAsync()
        {
            CloudBlockBlob blob = new CloudBlockBlob(new Uri(this.blobSas));
            Console.WriteLine("Matches in blob: {0}/{1}", blob.Container.Name, blob.Name);

            using (Stream memoryStream = new MemoryStream())
            {
                //Download the blob.
                await blob.DownloadToStreamAsync(memoryStream);
                memoryStream.Seek(0, SeekOrigin.Begin);

                using (StreamReader streamReader = new StreamReader(memoryStream))
                {
                    Regex regex = new Regex(this.configurationSettings.RegularExpression);

                    int lineCount = 0;

                    //Read the file content.
                    while (!streamReader.EndOfStream)
                    {
                        ++lineCount;
                        string textLine = await streamReader.ReadLineAsync();

                        //If the line matches the search parameters, then print it out.
                        if (textLine.Length > 0)
                        {
                            if (regex.Match(textLine).Success)
                            {
                                Console.WriteLine("Match: \"{0}\" -- line: {1}", textLine, lineCount);
                            }
                        }
                    }
                }
            }
        }
    }
}
