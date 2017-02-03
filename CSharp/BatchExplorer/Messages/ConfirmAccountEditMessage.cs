//Copyright (c) Microsoft Corporation

using Microsoft.Azure.BatchExplorer.Models;
using Microsoft.Azure.BatchExplorer.PluginInterfaces.AccountPlugin;

namespace Microsoft.Azure.BatchExplorer.Messages
{
    /// <summary>
    /// Send this when an edit is confirmed
    /// </summary>
    public class ConfirmAccountEditMessage
    {
        public IAccountManager AccountManager { get; set; }
        public Account AccountToEdit { get; set; }
    }
}
