
namespace Microsoft.Azure.BatchExplorer.Messages
{
    public enum TvmReimageConfimation
    {
        Confirmed,
        Cancelled
    }

    public class ReimageTvmConfirmationMessage
    {
        public TvmReimageConfimation Confirmation { get; private set; }

        public ReimageTvmConfirmationMessage(TvmReimageConfimation confirmation)
        {
            this.Confirmation = confirmation;
        }
    }
}
