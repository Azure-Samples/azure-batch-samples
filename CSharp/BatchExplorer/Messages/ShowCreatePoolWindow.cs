//Copyright (c) Microsoft Corporation

namespace Microsoft.Azure.BatchExplorer.Messages
{
    using System.Collections.Generic;
    using Batch;
    using Helpers;

    public class ShowCreatePoolWindow
    {
        public Cached<IList<NodeAgentSku>> NodeAgentSkus { get; private set; }

        public ShowCreatePoolWindow(Cached<IList<NodeAgentSku>> nodeAgentSkus)
        {
            this.NodeAgentSkus = nodeAgentSkus;
        }
    }
}
