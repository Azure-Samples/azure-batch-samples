
namespace Microsoft.Azure.BatchExplorer.Messages
{
    public class ShowAddTaskWindow
    {
        public string WorkItemName { get; private set; }
        public string JobName { get; private set; }

        public ShowAddTaskWindow(string workItemName, string jobName)
        {
            this.WorkItemName = workItemName;
            this.JobName = jobName;
        }
    }
}
