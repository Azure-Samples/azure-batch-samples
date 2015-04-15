using GalaSoft.MvvmLight.Messaging;
using Microsoft.Azure.BatchExplorer.Helpers;
using Microsoft.Azure.BatchExplorer.Messages;
using Microsoft.Azure.BatchExplorer.Models;

namespace Microsoft.Azure.BatchExplorer.ViewModels
{
    public class OptionsViewModel : EntityBase
    {
        #region Public UI Properties

        public int MaxTrackedOperations { get; set; }
        public bool DisplayOperationHistory { get; set; }
        public bool UseStatsDuringList { get; set; }

        public string UseStatsDuringListDescriptionString
        {
            get
            {
                return "Expand statistics during all list operations.  This will return statistics information for all objects, at the cost of performance.";
            }
        }

        public string DisplayOperationHistoryDescriptionString
        {
            get
            {
                return "Display the operation history view at the bottom of the explorer.";
            }
        }

        public string MaxTrackedOperationsDescriptionString
        {
            get
            {
                return "The maximum number of operations which the operation history view will track.";
            }
        }

        #endregion

        public OptionsViewModel()
        {
            //Set the properties to their current values
            this.MaxTrackedOperations = OptionsModel.Instance.MaxTrackedOperations;
            this.DisplayOperationHistory = OptionsModel.Instance.DisplayOperationHistory;
            this.UseStatsDuringList = OptionsModel.Instance.UseStatsDuringList;
        }

        /// <summary>
        /// Invoke this command when the confirmation button (at this time it is the one labeled submit) is pressed
        /// </summary>
        public CommandBase Confirm 
        { 
            get
            {
                return new CommandBase(o =>
                {
                    OptionsModel options = OptionsModel.Instance;
                    options.MaxTrackedOperations = this.MaxTrackedOperations;
                    options.DisplayOperationHistory = this.DisplayOperationHistory;
                    options.UseStatsDuringList = this.UseStatsDuringList;

                    options.WriteOptions(); //Write the options file

                    Messenger.Default.Send(new CloseGenericPopup());
                });
            }
        }
        /// <summary>
        /// Invoke this command when the cancel button is pressed
        /// </summary>
        public CommandBase Cancel 
        { 
            get
            {
                return new CommandBase(o =>
                {
                    Messenger.Default.Send(new CloseGenericPopup());
                });
            }
        }
    }
}
