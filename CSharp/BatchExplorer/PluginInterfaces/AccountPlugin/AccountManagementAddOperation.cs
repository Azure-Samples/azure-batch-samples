using System.Windows.Controls;
using Microsoft.Azure.BatchExplorer.Models;

namespace Microsoft.Azure.BatchExplorer.PluginInterfaces.AccountPlugin
{
    /// <summary>
    /// Account management add operation.
    /// </summary>
    public class AccountManagementAddOperation : AccountManagementOperation
    {
        private readonly IAccountAdder accountAdder;
        
        public AccountManagementAddOperation(IAccountAdder accountAdder, Control control)
            : base(control)
        {
            this.accountAdder = accountAdder;
        }

        /// <summary>
        /// See <see cref="AccountManagementOperation"/>.
        /// </summary>
        /// <returns>The account associated with this operation.</returns>
        public override Account Complete()
        {
            return this.accountAdder.CreateAccountForAdd();
        }
    }
}
