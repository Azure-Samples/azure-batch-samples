//Copyright (c) Microsoft Corporation

namespace Microsoft.Azure.BatchExplorer.Plugins.AccountPlugin
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.ComponentModel.Composition;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Xml.Serialization;
    using Microsoft.Azure.BatchExplorer.Helpers;
    using Microsoft.Azure.BatchExplorer.Models;
    using Microsoft.Azure.BatchExplorer.PluginInterfaces.AccountPlugin;
    using Microsoft.Azure.BatchExplorer.Plugins.LegacyAccountSupport;


    /// <summary>
    /// See <see cref="IAccountManager"/>.
    /// </summary>
    [Export(typeof(IAccountManager))]
    [ExportMetadata("Name", "Default Account Manager")]
    public class DefaultAccountManager : EntityBase, IAccountManager
    {
        private int editingIndex;
        
        /// <summary>
        /// Default constructor
        /// </summary>
        public DefaultAccountManager()
        {
            this.Accounts = new ObservableCollection<Account>();
            this.OperationFactory = new DefaultAccountOperationFactory(this);
            this.editingIndex = -1;
        }

        #region IAccountManager

        /// <summary>
        /// See <see cref="IAccountManager.OperationFactory"/>.
        /// </summary>
        public IAccountOperationFactory OperationFactory { get; private set; }

        /// <summary>
        /// See <see cref="IAccountManager.Accounts"/>.
        /// </summary>
        public ObservableCollection<Account> Accounts { get; private set; }

        /// <summary>
        /// See <see cref="IAccountManager.HasAccounts"/>.
        /// </summary>
        public bool HasAccounts
        {
            get { return this.Accounts.Any(); }
        }

        /// <summary>
        /// See <see cref="IAccountManager.CanDeleteAccount"/>.
        /// </summary>
        public bool CanDeleteAccount
        {
            get { return true; }
        }

        /// <summary>
        /// See <see cref="IAccountManager.CanEditAccount"/>.
        /// </summary>
        public bool CanEditAccount
        {
            get { return true; }
        }
        
        /// <summary>
        /// See <see cref="IAccountManager.InitalizeAsync"/>.
        /// </summary>
        public Task InitalizeAsync()
        {
            return this.LoadAccountsAsync();
        }

        /// <summary>
        /// See <see cref="IAccountManager.DeleteAccountAsync"/>.
        /// </summary>
        /// <param name="account">The account to be removed.</param>
        /// <returns>The asynchronous operation.</returns>
        public async Task DeleteAccountAsync(Account account)
        {
            if (!this.Accounts.Contains(account))
            {
                throw new Exception("Attept to remove unrecognized account");
            }
            Account tempAccount = null;
            //We are waiting on an account being edited, so we need to make sure our editing index is still accurate after the move
            if (this.editingIndex >= 0)
            {
                tempAccount = this.Accounts[this.editingIndex];
            }
            this.Accounts.Remove(account);

            //We were tracking an account before the remove, so update the edit index
            if (tempAccount != null)
            {
                this.editingIndex = this.Accounts.IndexOf(tempAccount);
            }
            
            this.FirePropertyChangedEvent("HasAccounts");
            await this.SaveAccountsAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// See <see cref="IAccountManager.AddAccountAsync"/>.
        /// </summary>
        /// <param name="account">The account to be added.</param>
        /// <returns>The asynchronous operation.</returns>
        public async Task AddAccountAsync(Account account)
        {
            if (this.Accounts.Contains(account))
            {
                //TODO: Should this be a custom exception type?
                throw new Exception(string.Format(CultureInfo.CurrentCulture, "Account with alias {0} has already been added", account.Alias));
            }

            if (!(account is DefaultAccount))
            {
                throw new Exception(string.Format("Account type: {0}, expected Type: {1}", account.GetType().Name, typeof(DefaultAccount).Name));
            }

            this.Accounts.Add(account);
            this.FirePropertyChangedEvent("HasAccounts");

            await this.SaveAccountsAsync().ConfigureAwait(false);
        }
        
        /// <summary>
        /// See <see cref="IAccountManager.CloneAccountForEditAsync"/>.
        /// </summary>
        /// <param name="account">The account to clone.</param>
        /// <returns>A deep clone of the account.</returns>
        public Task<Account> CloneAccountForEditAsync(Account account)
        {
            this.editingIndex = this.Accounts.IndexOf(account);
            if (this.editingIndex < 0)
            {
                throw new Exception("Attempt was made to edit unrecognized account");
            }

            var clone = new DefaultAccount(this)
            {
                AccountName = account.AccountName,
                Alias = account.Alias,
                Key = account.Key,
                BatchServiceUrl = account.BatchServiceUrl,
                UniqueIdentifier = account.UniqueIdentifier,
                ParentAccountManager = this
            };
            return Task.FromResult<Account>(clone);
        }

        /// <summary>
        /// See <see cref="IAccountManager.CommitEditAsync"/>.
        /// </summary>
        /// <param name="account">The edited clone.</param>
        /// <returns>The asynchronous operation.</returns>
        public async Task CommitEditAsync(Account account)
        {
            if (this.editingIndex < 0)
            {
                throw new Exception("Commit was called but no account was selected for edit");
            }
            bool accountAlreadyExists = this.Accounts.Any(acct => acct.Equals(account) && acct.UniqueIdentifier != account.UniqueIdentifier);
            if (accountAlreadyExists)
            {
                throw new Exception(string.Format(CultureInfo.CurrentCulture, "Account with alias {0} has already been added", account.Alias));
            }

            //Replace the selected account with the cloned account
            this.Accounts[this.editingIndex] = account;
            this.Accounts.RemoveAt(this.editingIndex);
            this.Accounts.Insert(this.editingIndex, account);
            this.editingIndex = -1;
            await this.SaveAccountsAsync().ConfigureAwait(false);
        }

        #endregion

        #region Private methods

        /// <summary>
        /// Load the accounts from the accounts file
        /// </summary>
        private async Task LoadAccountsAsync()
        {
            string accountFileName = GetAccountFileName();
            FileInfo fileInfo = new FileInfo(accountFileName);

            if (!fileInfo.Exists || fileInfo.Length == 0)
            {
                using (File.Create(accountFileName))
                {
                }
                await this.SaveAccountsAsync().ConfigureAwait(false);
            }

            //Try first to deserialize using the new account deserialization mechanism, if that fails, fall back to the old way
            bool writeBackToFile = false;
            try
            {
                XmlSerializer x = new XmlSerializer(typeof(DefaultAccountSerializationContainer));
                using (StreamReader reader = new StreamReader(accountFileName))
                {
                    object deserializedObject = x.Deserialize(reader); //TODO: This is not technically async...

                    DefaultAccountSerializationContainer accountContainer = (DefaultAccountSerializationContainer)deserializedObject;

                    List<Account> accounts = accountContainer.GetAccountCollection(this);
                    
                    //Update accounts to the new URI format if required
                    writeBackToFile = this.UpdateAccountsToNewUriFormat(accounts);

                    this.Accounts = new ObservableCollection<Account>(accounts);
                }
            }
            catch (InvalidOperationException)
            {
                writeBackToFile = true;
                XmlSerializer x = new XmlSerializer(typeof(LegacyAccountManager));
                using (StreamReader reader = new StreamReader(accountFileName))
                {
                    object deserializedObject = x.Deserialize(reader); //TODO: This is not technically async...

                    LegacyAccountManager legacyAccountManager = (LegacyAccountManager)deserializedObject;
                    this.Accounts = new ObservableCollection<Account>(legacyAccountManager.CreateAccounts(this));
                }
            }

            //One time write back to change format of the file
            if (writeBackToFile)
            {
                await this.SaveAccountsAsync().ConfigureAwait(false);
            }
        }


        private bool UpdateAccountsToNewUriFormat(IEnumerable<Account> accounts)
        {
            bool hasUpdatedAnAccount = false;

            foreach (Account account in accounts)
            {
                if (String.IsNullOrEmpty(account.BatchServiceUrl) || String.IsNullOrEmpty(account.AccountName))
                {
                    // Skip the bad setting of old accounts
                    continue;
                }

                //The new format requires the account name to be in the Url
                if (!account.BatchServiceUrl.Contains(account.AccountName))
                {
                    int doubleSlashIndex = account.BatchServiceUrl.IndexOf(@"//", System.StringComparison.Ordinal);
                    if (doubleSlashIndex != -1)
                    {
                        hasUpdatedAnAccount = true;

                        string prefix = account.BatchServiceUrl.Substring(0, doubleSlashIndex + 2);
                        string postfix = account.BatchServiceUrl.Substring(doubleSlashIndex + 2, account.BatchServiceUrl.Length - prefix.Length);

                        //Remove trailing slashes from postfix
                        postfix = postfix.Trim('/');

                        //Regex to match an IP endpoint
                        Regex ipMatchingRegex = new Regex(@"^(?:[0-9]{1,3}\.){3}[0-9]{1,3}(?::[0-9]+)?$");

                        //check if this is a path or a host style URL
                        if (!ipMatchingRegex.Match(postfix).Success)
                        {
                            //Host style
                            postfix = account.AccountName + "." + postfix;
                        }
                        else
                        {
                            //Path style
                            postfix = postfix + "/" + account.AccountName;
                        }

                        string updatedUrl = prefix + postfix;
                        account.BatchServiceUrl = updatedUrl;
                    }
                }
            }

            return hasUpdatedAnAccount;
        }
        
        /// <summary>
        /// Save the accounts to the account file
        /// </summary>
        private async Task SaveAccountsAsync()
        {
            string fileName = GetAccountFileName();

            IEnumerable<DefaultAccount> defaultAccountList = this.Accounts.Cast<DefaultAccount>();
            DefaultAccountSerializationContainer serializationContainer = new DefaultAccountSerializationContainer(defaultAccountList);

            XmlSerializer x = new XmlSerializer(typeof(DefaultAccountSerializationContainer));
            using (var writer = new StreamWriter(fileName, false))
            {
                x.Serialize(writer, serializationContainer);
                await writer.FlushAsync().ConfigureAwait(false);
            }
        }
        
        /// <summary>
        /// Gets the magic string representing the path to the account file
        /// </summary>
        /// <returns></returns>
        private static string GetAccountFileName()
        {
            const string fileName = "accounts.xml";

            if (String.IsNullOrEmpty(Microsoft.Azure.BatchExplorer.Helpers.Common.LocalAppDataDirectory))
            {
                MessageBox.Show("LocalAppData environment variable is not set", "Azure Batch Explorer", MessageBoxButton.OK);
                throw new InvalidProgramException("LocalAppData not defined in environment");
            }

            // create the directory if necessary
            string fullDirectoryPath = Path.Combine(
                Microsoft.Azure.BatchExplorer.Helpers.Common.LocalAppDataDirectory, 
                Microsoft.Azure.BatchExplorer.Helpers.Common.LocalAppDataSubfolder);
            Directory.CreateDirectory(fullDirectoryPath);

            return Path.Combine(fullDirectoryPath, fileName);
        }

        #endregion
    }
}
