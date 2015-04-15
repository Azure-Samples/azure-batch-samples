using Microsoft.Azure.Batch;

namespace Microsoft.Azure.BatchExplorer.Messages
{
    public class ShowHeatMapMessage
    {
        public ICloudPool Pool { get; private set; }

        public ShowHeatMapMessage(ICloudPool pool)
        {
            this.Pool = pool;
        }
    }
}
