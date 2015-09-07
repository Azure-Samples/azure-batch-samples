//Copyright (c) Microsoft Corporation

using Microsoft.Azure.BatchExplorer.Helpers;

namespace Microsoft.Azure.BatchExplorer.Messages
{
    /// <summary>
    /// Update the visibility of a panel containing a wait-spinner
    /// </summary>
    public class UpdateWaitSpinnerMessage
    {
        /// <summary>
        /// The wait spinner panel whose visibility is to be updated
        /// </summary>
        public WaitSpinnerPanel PanelToChange { get; private set; }
        /// <summary>
        /// True if the panel is to be made visible, otherwise false
        /// </summary>
        public bool MakeSpinnerVisible { get; private set; }
        /// <summary>
        /// Construct a new message to change the visibility of a wait-spinner-panel
        /// </summary>
        /// <param name="panelToChange">the panel to update</param>
        /// <param name="makePanelVisible">true if we want to make it visible, otherwise false</param>
        public UpdateWaitSpinnerMessage(WaitSpinnerPanel panelToChange, bool makePanelVisible=false)
        {
            PanelToChange = panelToChange;
            MakeSpinnerVisible = makePanelVisible;
        }
    }
}
