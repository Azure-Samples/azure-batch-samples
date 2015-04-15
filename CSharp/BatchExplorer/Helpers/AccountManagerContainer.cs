using Microsoft.Azure.BatchExplorer.PluginInterfaces.AccountPlugin;

namespace Microsoft.Azure.BatchExplorer.Helpers
{
    public sealed class AccountManagerContainer
    {
        public IAccountManager AccountManager { get; private set; }
        public IAccountManagerMetadata Metadata { get; private set; }

        public AccountManagerContainer(IAccountManager accountManager, IAccountManagerMetadata metadata)
        {
            this.AccountManager = accountManager;
            this.Metadata = metadata;
        }
    }
}
