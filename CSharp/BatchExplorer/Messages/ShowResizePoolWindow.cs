namespace Microsoft.Azure.BatchExplorer.Messages
{
    public class ShowResizePoolWindow
    {
        public string PoolName { get; private set; }
        public int? CurrentDedicated { get; private set; }

        public ShowResizePoolWindow(string poolName, int? currentDedicated)
        {
            this.PoolName = poolName;
            this.CurrentDedicated = currentDedicated;
        }
    }
}
