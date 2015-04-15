using Microsoft.Azure.BatchExplorer.Models;
using Microsoft.Azure.BatchExplorer.PluginInterfaces.AccountPlugin;

namespace Microsoft.Azure.BatchExplorer.Plugins.AccountPlugin
{
    public class DefaultAccountOperationFactory : IAccountOperationFactory
    {
        private readonly DefaultAccountManager parentAccountManager;

        public DefaultAccountOperationFactory(DefaultAccountManager parentAccountManager)
        {
            this.parentAccountManager = parentAccountManager;
        }

        /// <summary>
        /// See <see cref="IAccountOperationFactory.CreateAddAccountOperation"/>.
        /// </summary>
        /// <returns></returns>
        public AccountManagementAddOperation CreateAddAccountOperation()
        {
            Account account = new DefaultAccount(this.parentAccountManager); //Create an empty account to be added.

            DefaultAccountDialogViewModel accountAdder = new DefaultAccountDialogViewModel(account);

            DefaultAccountManagementControl control = new DefaultAccountManagementControl(accountAdder);
            AccountManagementAddOperation operation = new AccountManagementAddOperation(accountAdder, control);

            return operation;
        }

        /// <summary>
        /// See <see cref="IAccountOperationFactory.CreateEditAccountOperation"/>.
        /// </summary>
        /// <param name="account">The account to edit.</param>
        /// <returns></returns>
        public AccountManagementEditOperation CreateEditAccountOperation(Account account)
        {
            DefaultAccountDialogViewModel accountEditor = new DefaultAccountDialogViewModel(account);

            DefaultAccountManagementControl control = new DefaultAccountManagementControl(accountEditor);
            AccountManagementEditOperation operation = new AccountManagementEditOperation(accountEditor, control);

            return operation;
        }
    }
}
