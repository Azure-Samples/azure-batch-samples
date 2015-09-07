//Copyright (c) Microsoft Corporation

using Microsoft.Azure.BatchExplorer.Models;

namespace Microsoft.Azure.BatchExplorer.PluginInterfaces.AccountPlugin
{
    /// <summary>
    /// Interface exposing the functionality to Create an account object for the purposes of "adding" it.
    /// </summary>
    public interface IAccountAdder
    {
        /// <summary>
        /// Creates an account object populated appropriately.
        /// </summary>
        /// <returns>An account object ready to be used.</returns>
        Account CreateAccountForAdd();
    }
}
