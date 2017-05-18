//Copyright (c) Microsoft Corporation

namespace Microsoft.Azure.BatchExplorer.Messages
{
    public class ShowResizePoolWindow
    {
        public string PoolId { get; private set; }
        public int? CurrentDedicated { get; private set; }
        public int? CurrentLowPriority { get; private set; }
        public string CurrentAutoScaleFormula { get; private set; }

        public ShowResizePoolWindow(string poolId, int? currentDedicated, int? currentLowPriority, string currentAutoScaleFormula)
        {
            this.PoolId = poolId;
            this.CurrentDedicated = currentDedicated;
            this.CurrentLowPriority = currentLowPriority;
            this.CurrentAutoScaleFormula = currentAutoScaleFormula;
        }
    }
}
