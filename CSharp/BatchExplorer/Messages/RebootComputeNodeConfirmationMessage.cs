
namespace Microsoft.Azure.BatchExplorer.Messages
{
    public enum ComputeNodeRebootConfimation
    {
        Confirmed,
        Cancelled
    }

    public class RebootComputeNodeConfirmationMessage
    {
        public ComputeNodeRebootConfimation Confirmation { get; private set; }

        public RebootComputeNodeConfirmationMessage(ComputeNodeRebootConfimation confirmation)
        {
            this.Confirmation = confirmation;
        }
    }
}
