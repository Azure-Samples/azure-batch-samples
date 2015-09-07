//Copyright (c) Microsoft Corporation

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using Microsoft.Azure.BatchExplorer.Models;

namespace Microsoft.Azure.BatchExplorer.PluginInterfaces.AccountPlugin
{
    /// <summary>
    /// Account manager interface which defines the control operations allowed on accounts.
    /// </summary>
    public interface IAccountManager : INotifyPropertyChanged
    {
        /// <summary>
        /// The <see cref="IAccountOperationFactory"/> object associated with this account manager.  The <see cref="IAccountOperationFactory"/> manages 
        /// account management operations and their UI (such as Add and Edit).
        /// </summary>
        IAccountOperationFactory OperationFactory { get; }

        /// <summary>
        /// Adds the associated account to the manager.
        /// </summary>
        /// <param name="account">The account for the manager to track.</param>
        /// <returns>The asynchronous operation.</returns>
        Task AddAccountAsync(Account account);

        /// <summary>
        /// True if the manager can edit accounts.  False if it cannot.
        /// </summary>
        bool CanEditAccount { get; }

        /// <summary>
        /// Creates a copy of the specified account for editing purposes.
        /// </summary>
        /// <param name="account">The account object to copy.</param>
        /// <returns>A copy of the specified account object.</returns>
        Task<Account> CloneAccountForEditAsync(Account account);

        /// <summary>
        /// Commits an edit to the manager, overriding the existing account and replacing it with the new account with modified properties.
        /// </summary>
        /// <param name="account">The edited account.</param>
        /// <returns>The asynchronous operation.</returns>
        Task CommitEditAsync(Account account);

        /// <summary>
        /// True if the manager can delete accounts.  False if it cannot.
        /// </summary>
        bool CanDeleteAccount { get; }

        /// <summary>
        /// Deletes the specified account from the manager.
        /// </summary>
        /// <param name="account">The account object to remove from tracking.</param>
        /// <returns>The asynchronous operation.</returns>
        Task DeleteAccountAsync(Account account);

        /// <summary>
        /// True if this manager has any accounts.  False otherwise.
        /// </summary>
        bool HasAccounts { get; }

        /// <summary>
        /// A collection of accounts currently being tracked by the <see cref="IAccountManager"/>.
        /// </summary>
        ObservableCollection<Account> Accounts { get; }
        
        /// <summary>
        /// Performs one time initialization of the account manager (such as reading from a file or database).
        /// </summary>
        /// <returns>The asynchronous operation.</returns>
        Task InitalizeAsync();
    }
}
