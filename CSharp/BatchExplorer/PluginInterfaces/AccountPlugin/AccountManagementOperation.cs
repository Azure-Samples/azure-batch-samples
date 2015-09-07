//Copyright (c) Microsoft Corporation

using System.Windows.Controls;
using Microsoft.Azure.BatchExplorer.Models;

namespace Microsoft.Azure.BatchExplorer.PluginInterfaces.AccountPlugin
{
    /// <summary>
    /// Represents an account management operation associated with a UI control.
    /// </summary>
    public abstract class AccountManagementOperation
    {
        /// <summary>
        /// The UI control associated with the account management operation.
        /// </summary>
        public Control Control { get; private set; }

        /// <summary>
        /// Initializes a new operation with the specified backing control.
        /// </summary>
        /// <param name="control">The control associated with this operation.</param>
        protected AccountManagementOperation(Control control)
        {
            this.Control = control;
        }

        /// <summary>
        /// Completes the operation and returns the associated account.
        /// </summary>
        /// <returns>The account which was a result of the operation.</returns>
        public abstract Account Complete();
    }
}
