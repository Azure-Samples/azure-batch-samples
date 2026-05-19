// Copyright (c) Microsoft Corporation

namespace Microsoft.Azure.Batch.Samples.TextSearch
{
    using System;
    using System.IO;
    using Microsoft.Extensions.Configuration;

    /// <summary>
    /// The reducer task. This task aggregates the results from mapper tasks and prints them.
    /// </summary>
    public class ReducerTask
    {
        private readonly Settings textSearchSettings;

        public ReducerTask()
        {
            this.textSearchSettings = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("settings.json")
                .Build()
                .Get<Settings>();
        }

        public void Run()
        {
            for (int i = 0; i < this.textSearchSettings.NumberOfMapperTasks; i++)
            {
                string mapperTaskId = Helpers.GetMapperTaskId(i);
                string mapperFileContent = File.ReadAllText(mapperTaskId);
                Console.WriteLine(mapperFileContent);
                Console.WriteLine();
            }
        }
    }
}
