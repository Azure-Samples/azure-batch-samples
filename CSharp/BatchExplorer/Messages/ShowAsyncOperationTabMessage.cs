
namespace Microsoft.Azure.BatchExplorer.Messages
{
    public class ShowAsyncOperationTabMessage
    {
        public bool Show { get; set; }

        public ShowAsyncOperationTabMessage(bool show)
        {
            this.Show = show;
        }
    }
}
