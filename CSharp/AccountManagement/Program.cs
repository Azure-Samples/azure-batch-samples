//Copyright (c) Microsoft Corporation

namespace Microsoft.Azure.Batch.Samples.AccountManagement
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure;
    using Microsoft.Azure.Batch.Samples.Common;
    using Microsoft.Azure.Common.Authentication;
    using Microsoft.Azure.Common.Authentication.Factories;
    using Microsoft.Azure.Common.Authentication.Models;
    using Microsoft.Azure.Management.Batch;
    using Microsoft.Azure.Management.Batch.Models;
    using Microsoft.Azure.Management.Resources;
    using Microsoft.Azure.Management.Resources.Models;
    using Microsoft.Azure.Subscriptions;
    using Microsoft.Azure.Subscriptions.Models;
    using Microsoft.IdentityModel.Clients.ActiveDirectory;

    public class BatchAccountManagementSample
    {
        private const string BatchNameSpace = "Microsoft.Batch";
        private const string ResourceGroupName = "AccountMgmtSampleGroup";

        public static void Main(string[] args)
        {
            try
            {
                // Call the asynchronous version of the Main() method. This is done so that we can await various
                // calls to async methods within the "Main" method of this console application.
                MainAsync().Wait();
            }
            catch (AggregateException ae)
            {
                Console.WriteLine();
                Console.WriteLine("One or more exceptions occurred.");
                Console.WriteLine();

                SampleHelpers.PrintAggregateException(ae.Flatten());
            }
            finally
            {
                Console.WriteLine();
                Console.WriteLine("Sample complete, hit ENTER to exit...");
                Console.ReadLine();
            }
        }

        private static async Task MainAsync()
        {
            AzureContext azureContext = GetAzureContext();

            using (ResourceManagementClient resourceManagementClient =
                   AzureSession.ClientFactory.CreateClient<ResourceManagementClient>(azureContext, AzureEnvironment.Endpoint.ResourceManager))
            {
                // Register with the Batch resource provider; this only needs to be performed once per subscription.
                resourceManagementClient.Providers.Register(BatchNameSpace);

                string location = await PromptUserForLocationAsync(resourceManagementClient);
                
                await CreateResourceGroupAsync(resourceManagementClient, location);

                await PerformBatchAccountOperationsAsync(azureContext, location);

                await DeleteResourceGroupAsync(resourceManagementClient);
            }
        }

        /// <summary>
        /// Gets the user's Azure account and subscription information
        /// </summary>
        /// <returns>An <see cref="Microsoft.Azure.Common.Authentication.Models.AzureContext"/> instance containing 
        /// information about the user's Azure account and subscription.</returns>
        private static AzureContext GetAzureContext()
        {
            AzureAccount azureAccount = new AzureAccount() { Type = AzureAccount.AccountType.User };
            AzureEnvironment environment = AzureEnvironment.PublicEnvironments[EnvironmentName.AzureCloud];
            
            // Create an access token for use in initializing the SubscriptionClient
            IAccessToken accessToken = AzureSession.AuthenticationFactory.Authenticate(
                azureAccount,
                environment,
                AuthenticationFactory.CommonAdTenant,
                null,
                ShowDialog.Auto,  // Auto will use cached credentials if available - set this parameter to ShowDialog.Always to always get a login prompt.
                TokenCache.DefaultShared);

            // Use a SubscriptionClient to obtain a list of subscriptions in the Azure account, and
            // ask the user to select a subscription if the account owns more than one subscription.
            string subscriptionId = null;
            using (SubscriptionClient subscriptionClient = AzureSession.ClientFactory.CreateCustomClient<SubscriptionClient>(
                   new TokenCloudCredentials(accessToken.AccessToken),
                   environment.GetEndpointAsUri(AzureEnvironment.Endpoint.ResourceManager)))
            {
                
                subscriptionId = SelectSubscription(subscriptionClient);
            }
            
            // Now that we have the ID of the subscription to use, we can create an AzureSubscription
            // that is used in initializing the AzureContext..
            AzureSubscription azureSubscription = new AzureSubscription();
            azureSubscription.Id = new Guid(subscriptionId);
            azureSubscription.Properties = new Dictionary<AzureSubscription.Property, string>();
            azureSubscription.Properties.Add(AzureSubscription.Property.Tenants, accessToken.TenantId);
            
            azureAccount.Properties[AzureAccount.Property.Tenants] = accessToken.TenantId;

            AzureContext context = new AzureContext(azureSubscription, azureAccount, environment);

            return context;
        }

        /// <summary>
        /// Select the subscription id to use in the rest of the sample. 
        /// </summary>
        /// <param name="client">The <see cref="Microsoft.Azure.Subscriptions.SubscriptionClient"/> to use to get all the subscriptions 
        /// under the user's Azure account.</param>
        /// <returns>The subscription id to use in the rest of the sample.</returns>
        /// <remarks>If the user has 1 subscription under their Azure account, it is chosen automatically. If the user has more than
        /// one, they are prompted to make a selection.</remarks>
        private static string SelectSubscription(SubscriptionClient client)
        {
            IList<Subscription> subscriptions = client.Subscriptions.List().Subscriptions;
            
            Subscription selectedSub = subscriptions.First();

            // If there is more than 1 subscription under the Azure account, prompt the user for the subscription to use.
            if (subscriptions.Count > 1)
            {
                string[] subscriptionNames = subscriptions.Select(s => s.DisplayName).ToArray();
                string selectedSubscription = PromptForSelectionFromCollection(subscriptionNames, "Enter the number of the Azure subscription to use: ");
                selectedSub = subscriptions.First(s => s.DisplayName.Equals(selectedSubscription));
            }

            return selectedSub.SubscriptionId;
        }

        /// <summary>
        /// Obtains a list of locations via the specified <see cref="Microsoft.Azure.Management.Resources.ResourceManagementClient"/>
        /// and prompts the user to select a location from the list.
        /// </summary>
        /// <param name="resourceManagementClient"></param>
        /// <returns>A <see cref="System.Threading.Tasks.Task"/> object that represents the asynchronous operation.</returns>
        private static async Task<string> PromptUserForLocationAsync(ResourceManagementClient resourceManagementClient)
        {
            // Obtain the list of available datacenter locations for Batch accounts supported by this subscription
            ProviderGetResult batchProvider = await resourceManagementClient.Providers.GetAsync(BatchNameSpace);
            ProviderResourceType batchResource = batchProvider.Provider.ResourceTypes.Where(p => p.Name == "batchAccounts").First();
            string[] locations = batchResource.Locations.ToArray();

            // Ask the user where they would like to create the resource group and account
            return PromptForSelectionFromCollection(locations, "Enter the number of the location where you'd like to create your Batch account: ");
        }

        /// <summary>
        /// Helper function that prompts the user to make a selection from a collection.
        /// </summary>
        /// <param name="choices">The set of options the user can choose from.</param>
        /// <param name="promptMessage">The message to display to the user.</param>
        /// <returns>The item the user selected from the collection.</returns>
        private static string PromptForSelectionFromCollection(string[] choices, string promptMessage)
        {
            for (int i = 0; i < choices.Length; i++)
            {
                Console.WriteLine(" {0} - {1}", i + 1, choices[i]);
            }

            Console.WriteLine();
            Console.Write(promptMessage);
            string numberText = Console.ReadLine();
            Console.WriteLine();
            
            int number = 0;
            if (!int.TryParse(numberText, out number) || number < 0 || number > choices.Length)
            {
                throw new ArgumentException("Supplied value not a valid number from the list.");
            }

            return choices[number - 1];
        }

        /// <summary>
        /// Prompts the user for the name of the Batch account to create.
        /// </summary>
        /// <returns>The name of the Batch account to create.</returns>
        private static string PromptUserForAccountName()
        {
            Console.WriteLine("Batch account names must be 3 to 24 characters and contain only lowercase letters and numbers.");
            Console.Write("Enter the name of the Batch account to create: ");
            string accountName = Console.ReadLine();
            Console.WriteLine();

            return accountName;
        }

        /// <summary>
        /// Creates a resource group. The user's Batch account will be created under this resource group.
        /// </summary>
        /// <param name="resourceManagementClient">The <see cref="Microsoft.Azure.Management.Resources.IResourceManagementClient"/> 
        /// to use when creating the resource group.</param>
        /// <param name="location">The location where the resource group will be created.</param>
        /// <returns>A <see cref="System.Threading.Tasks.Task"/> object that represents the asynchronous operation.</returns>
        private static async Task CreateResourceGroupAsync(ResourceManagementClient resourceManagementClient, string location)
        {
            ResourceGroupExistsResult existsResult = await resourceManagementClient.ResourceGroups.CheckExistenceAsync(ResourceGroupName);
            if (!existsResult.Exists)
            {
                Console.WriteLine("Creating resource group {0}", ResourceGroupName);
                await resourceManagementClient.ResourceGroups.CreateOrUpdateAsync(ResourceGroupName, new ResourceGroup(location));
                Console.WriteLine("Resource group created");
                Console.WriteLine();
            }
        }

        /// <summary>
        /// Deletes the resource group.
        /// </summary>
        /// <param name="resourceManagementClient">The <see cref="Microsoft.Azure.Management.Resources.IResourceManagementClient"/> 
        /// to use when deleting the resource group.</param>
        /// <returns>A <see cref="System.Threading.Tasks.Task"/> object that represents the asynchronous operation.</returns>
        private static async Task DeleteResourceGroupAsync(ResourceManagementClient resourceManagementClient)
        {
            Console.Write("Hit ENTER to delete resource group {0}: ", ResourceGroupName);
            Console.ReadLine();
            Console.WriteLine("Deleting resource group {0}...", ResourceGroupName);
            await resourceManagementClient.ResourceGroups.DeleteAsync(ResourceGroupName);
            Console.WriteLine("Resource group deleted");
            Console.WriteLine();
        }

        /// <summary>
        /// Performs various Batch account operations using the Batch Management library.
        /// </summary>
        /// <param name="context">The <see cref="Microsoft.Azure.Common.Authentication.Models.AzureContext"/> containing information
        /// about the user's Azure account and subscription.</param>
        /// <param name="location">The location where the Batch account will be created.</param>
        /// <returns>A <see cref="System.Threading.Tasks.Task"/> object that represents the asynchronous operation.</returns>
        private static async Task PerformBatchAccountOperationsAsync(AzureContext context, string location)
        {
            using (BatchManagementClient batchManagementClient =
                AzureSession.ClientFactory.CreateClient<BatchManagementClient>(context, AzureEnvironment.Endpoint.ResourceManager))
            {
				// Get the account quota for the subscription
                SubscriptionQuotasGetResponse quotaResponse = await batchManagementClient.Subscriptions.GetSubscriptionQuotasAsync(location);
                Console.WriteLine("Your subscription can create {0} account(s) in the {1} region.", quotaResponse.AccountQuota, location);
                Console.WriteLine();

                // Create account
                string accountName = PromptUserForAccountName();
                Console.WriteLine("Creating account {0}...", accountName);
                await batchManagementClient.Accounts.CreateAsync(ResourceGroupName, accountName, new BatchAccountCreateParameters() { Location = location });
                Console.WriteLine("Account {0} created", accountName);
                Console.WriteLine();

                // Get account
                Console.WriteLine("Getting account {0}...", accountName);
                BatchAccountGetResponse getResponse = await batchManagementClient.Accounts.GetAsync(ResourceGroupName, accountName);
                AccountResource account = getResponse.Resource;
                Console.WriteLine("Got account {0}:", account.Name);
                Console.WriteLine("  Account location: {0}", account.Location);
                Console.WriteLine("  Account resource type: {0}", account.Type);
                Console.WriteLine("  Account id: {0}", account.Id);
                Console.WriteLine();

                // Print account quotas
                Console.WriteLine("Quotas for account {0}:", account.Name);
                Console.WriteLine("  Core quota: {0}", account.Properties.CoreQuota);
                Console.WriteLine("  Pool quota: {0}", account.Properties.PoolQuota);
                Console.WriteLine("  Active job and job schedule quota: {0}", account.Properties.ActiveJobAndJobScheduleQuota);
                Console.WriteLine();

                // Get account keys
                Console.WriteLine("Getting account keys of account {0}...", account.Name);
                BatchAccountListKeyResponse accountKeys = await batchManagementClient.Accounts.ListKeysAsync(ResourceGroupName, account.Name);
                Console.WriteLine("  Primary key of account {0}:   {1}", account.Name, accountKeys.PrimaryKey);
                Console.WriteLine("  Secondary key of account {0}: {1}", account.Name, accountKeys.SecondaryKey);
                Console.WriteLine();

                // Regenerate primary account key
                Console.WriteLine("Regenerating the primary key of account {0}...", account.Name);
                BatchAccountRegenerateKeyResponse newKeys = await batchManagementClient.Accounts.RegenerateKeyAsync(
                    ResourceGroupName, account.Name, 
                    new BatchAccountRegenerateKeyParameters() { KeyName = AccountKeyType.Primary });
                Console.WriteLine("  New primary key of account {0}: {1}", account.Name, newKeys.PrimaryKey);
                Console.WriteLine("  Secondary key of account {0}:   {1}", account.Name, newKeys.SecondaryKey);
                Console.WriteLine();

                // Print subscription quota information
                BatchAccountListResponse listResponse = await batchManagementClient.Accounts.ListAsync(new AccountListParameters());
                IList<AccountResource> accounts = listResponse.Accounts;
                Console.WriteLine("Total number of Batch accounts under subscription id {0}:  {1}", context.Subscription.Id, accounts.Count);

                // Determine how many additional accounts can be created in the target region
                int numAccountsInRegion = accounts.Count(o => o.Location == account.Location);
                Console.WriteLine("Accounts in {0}: {1}", account.Location, numAccountsInRegion);
                Console.WriteLine("You can create {0} more accounts in the {1} region.", quotaResponse.AccountQuota - numAccountsInRegion, account.Location);
                Console.WriteLine();

                // List accounts in the subscription
                Console.WriteLine("Listing all Batch accounts under subscription id {0}...", context.Subscription.Id);
                foreach (AccountResource acct in accounts)
                {
                    Console.WriteLine("  {0} - {1} | Location: {2}", accounts.IndexOf(acct) + 1, acct.Name, acct.Location);
                }
                Console.WriteLine();

                // Delete account
                Console.Write("Hit ENTER to delete account {0}: ", account.Name);
                Console.ReadLine();
                Console.WriteLine("Deleting account {0}...", account.Name);
                await batchManagementClient.Accounts.DeleteAsync(ResourceGroupName, account.Name);
                Console.WriteLine("Account {0} deleted", account.Name);
                Console.WriteLine();
            }
        }
    }
}
