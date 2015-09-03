//Copyright (c) Microsoft Corporation

using System.Windows.Controls;
using Microsoft.Azure.BatchExplorer.Models;

namespace Microsoft.Azure.BatchExplorer.PluginInterfaces.AccountPlugin
{
    /// <summary>
    /// Account management edit operation.
    /// </summary>
    public class AccountManagementEditOperation : AccountManagementOperation
    {
        private readonly IAccountEditor accountEditor;

        public AccountManagementEditOperation(IAccountEditor accountEditor, Control control)
            : base(control)
        {
            this.accountEditor = accountEditor;
        }

        /// <summary>
        /// See <see cref="AccountManagementOperation"/>.
        /// </summary>
        /// <returns>The account associated with this operation.</returns>
        public override Account Complete()
        {
            return this.accountEditor.CreateAccountForEdit();
        }
    }
}
