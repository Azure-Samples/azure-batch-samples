//Copyright (c) Microsoft Corporation

namespace Microsoft.Azure.Batch.Samples.TextSearch
{
    using System;
    using System.IO;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Configuration;

    /// <summary>
    /// The mapper task - it processes a file by performing a regular expression match on each line.
    /// </summary>
    public class MapperTask
    {
        private readonly Settings configurationSettings;
        private readonly string fileName;

        /// <summary>
        /// Constructs a mapper task object with the specified file name.
        /// </summary>
        /// <param name="blobSas">The file name to process.</param>
        public MapperTask(string fileName)
        {
            this.fileName = fileName;
            this.configurationSettings = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("settings.json")
                .Build()
                .Get<Settings>();
        }

        /// <summary>
        /// Runs the mapper task.
        /// </summary>
        public async Task RunAsync()
        {
            using (FileStream fileStream = File.Open(this.fileName, FileMode.Open))
            using (StreamReader streamReader = new StreamReader(fileStream))
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
