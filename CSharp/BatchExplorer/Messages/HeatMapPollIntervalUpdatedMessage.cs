using Microsoft.Azure.BatchExplorer.ViewModels;

namespace Microsoft.Azure.BatchExplorer.Messages
{
    public class HeatMapPollIntervalUpdatedMessage
    {
        public HeatMapViewModel UpdatedViewModel { get; private set; }

        public HeatMapPollIntervalUpdatedMessage(HeatMapViewModel updatedViewModel)
        {
            this.UpdatedViewModel = updatedViewModel;
        }
    }
}
