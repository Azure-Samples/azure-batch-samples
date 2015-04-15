
namespace Microsoft.Azure.BatchExplorer.Messages
{
    public enum RefreshTarget
    {
        Pools,
        Jobs,
        WorkItems
    }

    public class RefreshMessage
    {
        public RefreshTarget ItemToRefresh { get; private set; }

        public RefreshMessage(RefreshTarget itemToRefresh)
        {
            this.ItemToRefresh = itemToRefresh;
        }
    }
}
