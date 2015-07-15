
namespace Microsoft.Azure.BatchExplorer.Messages
{
    public class ShowAddTaskWindow
    {
        public string JobId { get; private set; }

        public ShowAddTaskWindow(string jobId)
        {
            this.JobId = jobId;
        }
    }
}
