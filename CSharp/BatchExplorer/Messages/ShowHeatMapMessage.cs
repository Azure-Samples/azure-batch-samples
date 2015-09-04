//Copyright (c) Microsoft Corporation

using Microsoft.Azure.Batch;

namespace Microsoft.Azure.BatchExplorer.Messages
{
    public class ShowHeatMapMessage
    {
        public CloudPool Pool { get; private set; }

        public ShowHeatMapMessage(CloudPool pool)
        {
            this.Pool = pool;
        }
    }
}
