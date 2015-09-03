//Copyright (c) Microsoft Corporation

using GalaSoft.MvvmLight.Messaging;
using Microsoft.Azure.BatchExplorer.Helpers;
using Microsoft.Azure.BatchExplorer.Messages;

namespace Microsoft.Azure.BatchExplorer.ViewModels
{
    public class GenericMessageViewModel : EntityBase
    {
        #region Public UI Properties

        public string Message { get; private set; }

        public CommandBase Ok
        {
            get
            {
                return new CommandBase((item) => Messenger.Default.Send(new CloseGenericMessage()));
            }
        }

        #endregion

        public GenericMessageViewModel(string message)
        {
            this.Message = message;

        }
    }
}
