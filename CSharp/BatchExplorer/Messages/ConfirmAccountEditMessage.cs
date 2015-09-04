//Copyright (c) Microsoft Corporation

using Microsoft.Azure.BatchExplorer.Models;

namespace Microsoft.Azure.BatchExplorer.Messages
{
    /// <summary>
    /// Send this when an edit is confirmed
    /// </summary>
    public class ConfirmAccountEditMessage
    {
        public Account AccountToEdit { get; set; }
    }
}
