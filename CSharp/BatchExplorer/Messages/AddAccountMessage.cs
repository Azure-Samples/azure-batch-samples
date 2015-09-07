//Copyright (c) Microsoft Corporation

using Microsoft.Azure.BatchExplorer.ViewModels;

namespace Microsoft.Azure.BatchExplorer.Messages
{
    /// <summary>
    /// Send this message when adding a new account
    /// </summary>
    public class AddAccountMessage
    {
        public AccountDialogViewModel AccountDialogViewModel { get; set; }
    }
}
