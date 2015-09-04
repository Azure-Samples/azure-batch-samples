//Copyright (c) Microsoft Corporation

using Microsoft.Azure.BatchExplorer.ViewModels;

namespace Microsoft.Azure.BatchExplorer.Messages
{
    /// <summary>
    /// Send this message when editing an account
    /// </summary>
    public class EditAccountMessage
    {
        public AccountDialogViewModel AccountDialogViewModel { get; set; }
    }
}
