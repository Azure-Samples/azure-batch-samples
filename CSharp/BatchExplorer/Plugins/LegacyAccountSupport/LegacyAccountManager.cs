using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using Microsoft.Azure.BatchExplorer.Models;
using Microsoft.Azure.BatchExplorer.PluginInterfaces.AccountPlugin;

namespace Microsoft.Azure.BatchExplorer.Plugins.LegacyAccountSupport
{
    //TODO: Remove this in a future release
    [XmlRoot("AccountManager")]
    public class LegacyAccountManager
    {
        public bool IsBusy { get; set; }
        public List<LegacyAccount> Accounts { get; set; }

        public List<Account> CreateAccounts(IAccountManager manager)
        {
            List<Account> results = this.Accounts.Select(a => a.ToAccount(manager)).ToList();
            return results;
        }
    }
}
