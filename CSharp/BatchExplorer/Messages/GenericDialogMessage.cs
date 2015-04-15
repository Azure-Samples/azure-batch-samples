
namespace Microsoft.Azure.BatchExplorer.Messages
{
    /// <summary>
    /// Launch a dialog with a message
    /// </summary>
    public class GenericDialogMessage
    {
        /// <summary>
        /// Create a new GenericDialogMessage with the specified message
        /// </summary>
        /// <param name="messageString"></param>
        public GenericDialogMessage(string messageString)
        {
            MessageString = messageString;
        }
        /// <summary>
        /// The message in the dialog
        /// </summary>
        public string MessageString { get; set; }
    }
}
