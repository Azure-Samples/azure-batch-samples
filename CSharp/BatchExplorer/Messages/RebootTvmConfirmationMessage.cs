
namespace Microsoft.Azure.BatchExplorer.Messages
{
    public enum TvmRebootConfimation
    {
        Confirmed,
        Cancelled
    }

    public class RebootTvmConfirmationMessage
    {
        public TvmRebootConfimation Confirmation { get; private set; }

        public RebootTvmConfirmationMessage(TvmRebootConfimation confirmation)
        {
            this.Confirmation = confirmation;
        }
    }
}
