//Copyright (c) Microsoft Corporation

using Microsoft.Azure.BatchExplorer.Helpers;
using Microsoft.Azure.BatchExplorer.Models;
using Microsoft.Azure.BatchExplorer.PluginInterfaces.AccountPlugin;

namespace Microsoft.Azure.BatchExplorer.Plugins.AccountPlugin
{
    public class DefaultAccountDialogViewModel : EntityBase, IAccountAdder, IAccountEditor
    {
        private readonly Account account;

        /// <summary>
        /// Use this constructor when creating a dialog that will be used on an edited account
        /// </summary>
        /// <param name="account">the existing account to be edited</param>
        public DefaultAccountDialogViewModel(Account account)
        {
            this.account = account;
        }

        /// <summary>
        /// The Alias of the account
        /// </summary>
        public string Alias
        {
            get { return this.account.Alias; }
            set
            {
                this.account.Alias = value;
                FirePropertyChangedEvent(nameof(Alias));
            }
        }
        /// <summary>
        /// The Name of the account
        /// </summary>
        public string AccountName
        {
            get { return this.account.AccountName; }
            set
            {
                this.account.AccountName = value;
                FirePropertyChangedEvent(nameof(AccountName));
            }
        }
        /// <summary>
        /// The Key of the account
        /// </summary>
        public string BatchServiceKey
        {
            get { return this.account.BatchServiceKey; }
            set
            {
                this.account.BatchServiceKey = value;
                FirePropertyChangedEvent(nameof(BatchServiceKey));
            }
        }
        /// <summary>
        /// The Batch Service Url of the account.
        /// </summary>
        public string BatchServiceUrl
        {
            get { return this.account.BatchServiceUrl; }
            set
            {
                this.account.BatchServiceUrl = value;
                FirePropertyChangedEvent(nameof(BatchServiceUrl));
            }
        }

        public string LinkedStorageAccountName
        {
            get { return this.account.LinkedStorageAccountName; }
            set
            {
                this.account.LinkedStorageAccountName = value;
                FirePropertyChangedEvent(nameof(LinkedStorageAccountName));
            }
        }

        public string LinkedStorageAccountKey
        {
            get { return this.account.LinkedStorageAccountKey; }
            set
            {
                this.account.LinkedStorageAccountKey = value;
                FirePropertyChangedEvent(nameof(LinkedStorageAccountKey));
            }
        }

        #region IAccountAdder

        /// <summary>
        /// See <see cref="IAccountAdder.CreateAccountForAdd"/>.
        /// </summary>
        /// <returns></returns>
        public Account CreateAccountForAdd()
        {
            return this.account;
        }

        #endregion

        #region IAccountEditor

        /// <summary>
        /// See <see cref="IAccountEditor.CreateAccountForEdit"/>.
        /// </summary>
        /// <returns></returns>
        public Account CreateAccountForEdit()
        {
            return this.account;
        }

        #endregion
    }

}
