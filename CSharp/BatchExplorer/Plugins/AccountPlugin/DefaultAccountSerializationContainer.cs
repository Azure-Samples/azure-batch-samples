//Copyright (c) Microsoft Corporation

using System.Collections.Generic;
using Microsoft.Azure.BatchExplorer.Models;
using Microsoft.Azure.BatchExplorer.PluginInterfaces.AccountPlugin;

namespace Microsoft.Azure.BatchExplorer.Plugins.AccountPlugin
{
    public class DefaultAccountSerializationContainer
    {
        public List<SerializableAccount> Accounts { get; private set; }

        public DefaultAccountSerializationContainer()
        {
            this.Accounts = new List<SerializableAccount>();
        }

        public DefaultAccountSerializationContainer(IEnumerable<DefaultAccount> accounts)
        {
            this.Accounts = new List<SerializableAccount>();

            foreach (DefaultAccount account in accounts)
            {
                this.Accounts.Add(new SerializableAccount(account));
            }
        }

        public List<Account> GetAccountCollection(IAccountManager parentAccountManager)
        {
            List<Account> results = new List<Account>();

            foreach (var serializableAccount in Accounts)
            {
                results.Add(serializableAccount.CreateAccountFromSerialization(parentAccountManager));
            }

            return results;
        }
    }
}
