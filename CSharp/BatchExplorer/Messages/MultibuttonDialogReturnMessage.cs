using System.Windows;

namespace Microsoft.Azure.BatchExplorer.Messages
{
    /// <summary>
    /// Message launched when a multibutton dialog closes
    /// </summary>
    public class MultibuttonDialogReturnMessage
    {
        /// <summary>
        /// The result of the closing dialog
        /// </summary>
        public MessageBoxResult MessageBoxResult { get; private set; }
        /// <summary>
        /// Construct the message
        /// </summary>
        /// <param name="messageBoxResult">the result gotten from the closing dialog box</param>
        public MultibuttonDialogReturnMessage(MessageBoxResult messageBoxResult)
        {
            MessageBoxResult = messageBoxResult;
        }
    }
}
