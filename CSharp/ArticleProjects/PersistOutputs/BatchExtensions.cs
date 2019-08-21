namespace Microsoft.Azure.Batch.Samples.Articles.PersistOutputs
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Batch.Common;
    using Conventions.Files;
    using Microsoft.WindowsAzure.Storage;

    public static class BatchExtensions
    {
        /// <summary>
        /// Monitors the specified job's tasks and returns each as they complete. When all
        /// of the tasks in the job have completed, the method returns.
        /// </summary>
        /// <param name="job">The <see cref="CloudJob"/> containing the tasks to monitor.</param>
        /// <returns>One or more completed <see cref="CloudTask"/>.</returns>
        public static IEnumerable<CloudTask> CompletedTasks(this CloudJob job)
        {
            HashSet<string> yieldedTasks = new HashSet<string>();

            ODATADetailLevel detailLevel = new ODATADetailLevel();
            detailLevel.SelectClause = "id,state,url,executionInfo";

            while (true)
            {
                List<CloudTask> tasks = job.ListTasks(detailLevel).ToList();

                IEnumerable<CloudTask> newlyCompleted = tasks.Where(t => t.State == TaskState.Completed)
                    .Where(t => !yieldedTasks.Contains(t.Id));

                foreach (CloudTask task in newlyCompleted)
                {
                    yield return task;
                    yieldedTasks.Add(task.Id);
                }

                if (yieldedTasks.Count == tasks.Count)
                {
                    yield break;
                }
            }
        }

        public static CloudTask WithOutputFile(
            this CloudTask task,
            CloudJob job,
            string pattern,
            string containerUrl,
            JobOutputKind outputKind,
            OutputFileUploadCondition uploadCondition)
        {
            var outputBase = job.GetOutputStoragePath(outputKind);
            bool patternContainsWildcard = pattern.Contains("*");
            var path = patternContainsWildcard ? outputBase : $"{outputBase}/{pattern}";

            return task.WithOutputFile(
                pattern,
                containerUrl,
                path,
                uploadCondition);
        }

        public static CloudTask WithOutputFile(
            this CloudTask task,
            string pattern,
            string containerUrl,
            TaskOutputKind outputKind,
            OutputFileUploadCondition uploadCondition)
        {
            var outputBase = task.GetOutputStoragePath(outputKind);
            bool patternContainsWildcard = pattern.Contains("*");
            var path = patternContainsWildcard ? outputBase : $"{outputBase}/{pattern}";

            return task.WithOutputFile(
                pattern,
                containerUrl,
                path,
                uploadCondition);
        }

        private static CloudTask WithOutputFile(
            this CloudTask task,
            string pattern,
            string containerUrl,
            string path,
            OutputFileUploadCondition uploadCondition)
        {
            if (task.OutputFiles == null)
            {
                task.OutputFiles = new List<OutputFile>();
            }

            task.OutputFiles.Add(
                new OutputFile(
                    filePattern: pattern,
                    destination: new OutputFileDestination(new OutputFileBlobContainerDestination(
                        containerUrl: containerUrl,
                        path: path)),
                    uploadOptions: new OutputFileUploadOptions(
                        uploadCondition: uploadCondition)));

            return task;
        }
    }
}
