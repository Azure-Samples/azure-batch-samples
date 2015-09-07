//Copyright (c) Microsoft Corporation

using System.Windows;

namespace Microsoft.Azure.BatchExplorer.Messages
{
    /// <summary>
    /// A message that calls for a dialog box beyond the standard Ok-box (though you could use it to get one of those too)
    /// </summary>
    public class LaunchMultibuttonDialogMessage
    {
        private MessageBoxImage messageBoxImage = MessageBoxImage.None;

        /// <summary>
        /// The message at the top of the dialog box
        /// </summary>
        public string Caption { get; set; }
        /// <summary>
        /// The message in the dialog
        /// </summary>
        public string DialogMessage { get; set; }
        /// <summary>
        /// The type of buttons on the dialog
        /// </summary>
        public MessageBoxButton MessageBoxButton { get; set; }
        /// <summary>
        /// The type of icon on the dialog - OPTIONAL - defaults to .None if not used
        /// </summary>
        public MessageBoxImage MessageBoxImage
        {
            get { return this.messageBoxImage; }
            set { this.messageBoxImage = value; }
        }
    }
}
