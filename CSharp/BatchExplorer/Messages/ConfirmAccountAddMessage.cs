using Microsoft.Azure.BatchExplorer.Models;

namespace Microsoft.Azure.BatchExplorer.Messages
{
    /// <summary>
    /// Send this when confirming the addition of a new account
    /// </summary>
    public class ConfirmAccountAddMessage
    {
        public Account AccountToAdd { get; set; }
    }
}
