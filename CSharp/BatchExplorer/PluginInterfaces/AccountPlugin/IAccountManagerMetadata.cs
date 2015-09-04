//Copyright (c) Microsoft Corporation

namespace Microsoft.Azure.BatchExplorer.PluginInterfaces.AccountPlugin
{
    /// <summary>
    /// Account manager metadata assoicated with an <see cref="IAccountManager"/>.
    /// </summary>
    public interface IAccountManagerMetadata
    {
        /// <summary>
        /// The name of the account manager.
        /// </summary>
        string Name { get; }
    }
}
