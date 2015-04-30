namespace Azure.Batch.SDK.Samples.JobScheduling
{
    class SampleConstants
    {
        public const string WorkItemNamePrefix = "JMScheduleTest";
        public const string JobManager = "JobManager.exe";

        // JobManager.exe is the second project in this solution, and the remaining files are its 
        // dependencies.  All of them need to be uploaded to blob storage in order for this demo to work.
        // A post build action copies these files to FilesToUpload
        public static string[] JobManagerFiles = new string[]{
                JobManager,
                "Microsoft.Data.Edm.dll",
                "Microsoft.Data.OData.dll",
                "Microsoft.Azure.Batch.dll",
                "System.Spatial.dll"
        };

        public const string JMTraceFile = "JMOutput.txt";
        public const string EnvWorkItemName = "WorkItemName";
        public const string EnvBatchAccountKeyName = "BatchAccountKey";
        public const string EnvWataskAccountName = "WATASK_ACCOUNT_NAME";
        public const string BatchSvcEndpoint = "https://batch.core.windows.net/";
    }
}
