//Copyright (c) Microsoft Corporation

using Microsoft.Azure.BatchExplorer.Models;
using Microsoft.Azure.BatchExplorer.PluginInterfaces.AccountPlugin;

namespace Microsoft.Azure.BatchExplorer.Messages
{
    /// <summary>
    /// Send this when confirming the addition of a new account
    /// </summary>
    public class ConfirmAccountAddMessage
    {
        public IAccountManager AccountManager { get; set; }
        public Account AccountToAdd { get; set; }
    }
}
