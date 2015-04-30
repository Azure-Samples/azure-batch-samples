namespace Microsoft.Azure.BatchExplorer.Models
{
    public class AddTaskOptions
    {
        public string WorkItemName { get; set; }
        public string JobName { get; set; }
        public string CommandLine { get; set; }
        public string TaskName { get; set; }
    }
}
