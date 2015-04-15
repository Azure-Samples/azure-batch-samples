using Microsoft.Azure.BatchExplorer.Models;

namespace Microsoft.Azure.BatchExplorer.PluginInterfaces.AccountPlugin
{
    /// <summary>
    /// Interface exposing the functionality to create an account object for "Edit" purposes.
    /// </summary>
    public interface IAccountEditor
    {
        /// <summary>
        /// Creates an account object populated appropriately.
        /// </summary>
        /// <returns>An account object ready to be used.</returns>
        Account CreateAccountForEdit();
    }
}
