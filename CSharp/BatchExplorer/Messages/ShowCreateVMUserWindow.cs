
namespace Microsoft.Azure.BatchExplorer.Messages
{
    public class ShowCreateVMUserWindow
    {
        public string PoolName { get; private set; }
        public string VMName { get; private set; }

        public ShowCreateVMUserWindow(string poolName, string vmName)
        {
            this.PoolName = poolName;
            this.VMName = vmName;
        }
    }
}
