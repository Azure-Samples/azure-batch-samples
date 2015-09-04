//Copyright (c) Microsoft Corporation

namespace Microsoft.Azure.BatchExplorer.Messages
{
    public class ShowCreateComputeNodeUserWindow
    {
        public string PoolId { get; private set; }
        public string ComputeNodeId { get; private set; }

        public ShowCreateComputeNodeUserWindow(string poolId, string computeNodeId)
        {
            this.PoolId = poolId;
            this.ComputeNodeId = computeNodeId;
        }
    }
}
