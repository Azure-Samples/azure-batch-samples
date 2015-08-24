
namespace Microsoft.Azure.BatchExplorer.Messages
{
    public enum ComputeNodeReimageConfimation
    {
        Confirmed,
        Cancelled
    }

    public class ReimageComputeNodeConfirmationMessage
    {
        public ComputeNodeReimageConfimation Confirmation { get; private set; }

        public ReimageComputeNodeConfirmationMessage(ComputeNodeReimageConfimation confirmation)
        {
            this.Confirmation = confirmation;
        }
    }
}
