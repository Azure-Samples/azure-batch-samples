//Copyright (c) Microsoft Corporation

namespace Microsoft.Azure.BatchExplorer.Messages
{
    public class ShowResizePoolWindow
    {
        public string PoolId { get; private set; }
        public int? CurrentDedicated { get; private set; }

        public ShowResizePoolWindow(string poolId, int? currentDedicated)
        {
            this.PoolId = poolId;
            this.CurrentDedicated = currentDedicated;
        }
    }
}
