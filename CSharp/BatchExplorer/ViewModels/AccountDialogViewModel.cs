//Copyright (c) Microsoft Corporation

using System.Windows.Controls;
using GalaSoft.MvvmLight.Messaging;
using Microsoft.Azure.BatchExplorer.Helpers;
using Microsoft.Azure.BatchExplorer.Messages;
using Microsoft.Azure.BatchExplorer.Models;
using Microsoft.Azure.BatchExplorer.PluginInterfaces.AccountPlugin;

namespace Microsoft.Azure.BatchExplorer.ViewModels
{
    public class AccountDialogViewModel : EntityBase
    {
        private readonly AccountManagementOperation accountOperation;

        /// <summary>
        /// Use this constructor when creating a dialog that will be used on a new account
        /// </summary>
        public AccountDialogViewModel(IAccountOperationFactory factory)
            : this(null, factory)
        { }

        /// <summary>
        /// Use this constructor when creating a dialog that will be used on an edited account
        /// </summary>
        /// <param name="account">the existing account to be edited</param>
        /// <param name="factory"></param>
        public AccountDialogViewModel(Account account, IAccountOperationFactory factory)
        {
            if (account != null)
            {
                this.accountOperation = factory.CreateEditAccountOperation(account);
                Confirm = new CommandBase(ConfirmEdit);
                Cancel = new CommandBase(CancelEdit);
            }
            else
            {
                this.accountOperation = factory.CreateAddAccountOperation();
                Confirm = new CommandBase(ConfirmNew);
                Cancel = new CommandBase(CancelNew);
            }
        }
        /// <summary>
        /// Invoke this command when the confirmation button (at this time it is the one labeled submit) is pressed
        /// </summary>
        public CommandBase Confirm { get; private set; }
        
        /// <summary>
        /// Invoke this command when the cancel button is pressed
        /// </summary>
        public CommandBase Cancel { get; private set; }

        public Control PluginControl
        {
            get { return this.accountOperation.Control; }
        }

        private void ConfirmNew(object o)
        {
            Messenger.Default.Send(new ConfirmAccountAddMessage() { AccountToAdd = this.accountOperation.Complete() });
        }

        private void ConfirmEdit(object o)
        {
            Messenger.Default.Send(new ConfirmAccountEditMessage() { AccountToEdit = this.accountOperation.Complete() });
        }

        private void CancelNew(object o)
        {
            Messenger.Default.Send(new CloseGenericPopup());
        }

        private void CancelEdit(object o)
        {
            Messenger.Default.Send(new CloseGenericPopup());
        }
    }
}
