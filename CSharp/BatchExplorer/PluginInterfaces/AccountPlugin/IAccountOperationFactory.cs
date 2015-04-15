using Microsoft.Azure.BatchExplorer.Models;

namespace Microsoft.Azure.BatchExplorer.PluginInterfaces.AccountPlugin
{
    /// <summary>
    /// Factory for account management operations and their corresponding UIs.
    /// </summary>
    public interface IAccountOperationFactory
    {
        AccountManagementAddOperation CreateAddAccountOperation();
        AccountManagementEditOperation CreateEditAccountOperation(Account account);
    }
}
