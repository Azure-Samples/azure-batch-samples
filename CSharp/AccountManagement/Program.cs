//Copyright (c) Microsoft Corporation
namespace Microsoft.Azure.Batch.Samples.AccountManagement
{
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
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    public class BatchAccountManagementSample
    {
        private const string BatchNameSpace = "Microsoft.Batch";
        private const string ResourceGroupName = "AccountMgmtSampleGroup";
        
        private static readonly string[] Locations = new string[]
            {
                // Not all Azure locations are listed; please add the locations you want to use here
                "North Europe",
                "West Europe",
                "South Central US",
                "West US",
                "North Central US",
                "East US",
                "Southeast Asia",
                "East Asia"
            };

        public static void Main(string[] args)
        {
            try
            {
                // These methods involve user prompts to get some parameters and are therefore handled synchronously.
                AzureContext azureContext = GetAzureContext();
                string location = PromptUserForLocation();
                string accountName = PromptUserForAccountName();

                // Call the asynchronous version of the Main() method. This is done so that we can await various
                // calls to async methods within the "Main" method of this console application.
                MainAsync(azureContext, accountName, location).Wait();
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

        private static async Task MainAsync(AzureContext azureContext, string accountName, string location)
        {
            using (IResourceManagementClient resourceManagementClient =
                AzureSession.ClientFactory.CreateClient<ResourceManagementClient>(azureContext, AzureEnvironment.Endpoint.ResourceManager))
            {
                // Register with the Batch resource provider - only needs to be performed once per subscription.
                resourceManagementClient.Providers.Register(BatchNameSpace);

                await CreateResourceGroupAsync(resourceManagementClient, location);

                await PerformBatchAccountOperationsAsync(azureContext, accountName, location);

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

            IAccessToken accessToken = AzureSession.AuthenticationFactory.Authenticate(
                azureAccount, 
                environment, 
                AuthenticationFactory.CommonAdTenant, 
                null, 
                ShowDialog.Auto,  // Auto will use cached credentials. Set this parameter to ShowDialog.Always to always get a login prompt.
                TokenCache.DefaultShared);

            string subscriptionId = null;
            using (SubscriptionClient subscriptionClient = AzureSession.ClientFactory.CreateCustomClient<SubscriptionClient>(
                new TokenCloudCredentials(accessToken.AccessToken),
                environment.GetEndpointAsUri(AzureEnvironment.Endpoint.ResourceManager)))
            {
                subscriptionId = SelectSubscription(subscriptionClient);
            }

            AzureSubscription azureSubscription = new AzureSubscription();
            azureSubscription.Id = new Guid(subscriptionId);
            azureSubscription.Properties = new Dictionary<AzureSubscription.Property, string> ();
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

            // If there's more than 1 subscription under the Azure account, prompt the user for the subscription to use.
            if (subscriptions.Count > 1)
            {
                string[] subscriptionNames = subscriptions.Select(s => s.DisplayName).ToArray();
                string selectedSubscription = PromptForSelectionFromCollection(subscriptionNames, 
                    "Enter the number of the Azure subscription you would like to use:");
                selectedSub = subscriptions.First(s => s.DisplayName.Equals(selectedSubscription));
            }

            return selectedSub.SubscriptionId;
        }

        /// <summary>
        /// Prompts the user for the location where they want to create their Batch account.
        /// </summary>
        /// <returns>The location where the user's Batch account will be created.</returns>
        private static string PromptUserForLocation()
        {
            return PromptForSelectionFromCollection(Locations, "Enter the number of the location where you'd like to create your Batch account:");
        }

        /// <summary>
        /// Helper function that prompts the user to make a selection from a collection.
        /// </summary>
        /// <param name="choices">The set of options the user can choose from.</param>
        /// <param name="promptMessage">The message to display to the user.</param>
        /// <returns>The item the user selected from the collection.</returns>
        private static string PromptForSelectionFromCollection(string[] choices, string promptMessage)
        {
            Console.WriteLine(promptMessage);
            for (int i = 0; i < choices.Length; i++)
            {
                Console.WriteLine(" {0} - {1}", i + 1, choices[i]);
            }
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
            Console.WriteLine("The Batch account name must be 3 to 24 characters, and it must only contain lowercase letters and numbers.");
            Console.WriteLine("Please input the account name you want to create: ");
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
        private static async Task CreateResourceGroupAsync(IResourceManagementClient resourceManagementClient, string location)
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
        private static async Task DeleteResourceGroupAsync(IResourceManagementClient resourceManagementClient)
        {
            Console.WriteLine("Deleting resource group {0}", ResourceGroupName);
            await resourceManagementClient.ResourceGroups.DeleteAsync(ResourceGroupName);
            Console.WriteLine("Resource group deleted");
            Console.WriteLine();
        }

        /// <summary>
        /// Performs various Batch account operations using the Batch Management library.
        /// </summary>
        /// <param name="context">The <see cref="Microsoft.Azure.Common.Authentication.Models.AzureContext"/> containing information
        /// about the user's Azure account and subscription.</param>
        /// <param name="accountName">The name of the Batch account to create.</param>
        /// <param name="location">The location where the Batch account will be created.</param>
        /// <returns>A <see cref="System.Threading.Tasks.Task"/> object that represents the asynchronous operation.</returns>
        private static async Task PerformBatchAccountOperationsAsync(AzureContext context, string accountName, string location)
        {
            using (IBatchManagementClient batchManagementClient =
                AzureSession.ClientFactory.CreateClient<BatchManagementClient>(context, AzureEnvironment.Endpoint.ResourceManager))
            {
                // Get the account quota for the subscription
                SubscriptionQuotasGetResponse quotaResponse = await batchManagementClient.Subscriptions.GetSubscriptionQuotasAsync(location);
                Console.WriteLine("Your subscription can create {0} account(s) in the {1} region.", quotaResponse.AccountQuota, location);
                Console.WriteLine();

                // Create account
                Console.WriteLine("Creating account {0} ...", accountName);
                await batchManagementClient.Accounts.CreateAsync(ResourceGroupName, accountName, new BatchAccountCreateParameters() {Location = location});
                Console.WriteLine("Account {0} created", accountName);
                Console.WriteLine();

                // Get account
                Console.WriteLine("Getting account {0} ...", accountName);
                BatchAccountGetResponse getResponse = await batchManagementClient.Accounts.GetAsync(ResourceGroupName, accountName);
                AccountResource account = getResponse.Resource;
                Console.WriteLine("Got account {0}:", accountName);
                Console.WriteLine(" Account location: {0}", account.Location);
                Console.WriteLine(" Account resource type: {0}", account.Type);
                Console.WriteLine(" Account id: {0}", account.Id);
                Console.WriteLine(" Core quota: {0}", account.Properties.CoreQuota);
                Console.WriteLine(" Pool quota: {0}", account.Properties.PoolQuota);
                Console.WriteLine(" Active job and job schedule quota: {0}", account.Properties.ActiveJobAndJobScheduleQuota);
                Console.WriteLine();

                // Get account keys
                Console.WriteLine("Getting account keys of account {0} ....", accountName);
                BatchAccountListKeyResponse accountKeys = await batchManagementClient.Accounts.ListKeysAsync(ResourceGroupName, accountName);
                Console.WriteLine("Primary key of account {0}:", accountName);
                Console.WriteLine(accountKeys.PrimaryKey);
                Console.WriteLine("Secondary key of account {0}:", accountName);
                Console.WriteLine(accountKeys.SecondaryKey);
                Console.WriteLine();

                // Regenerate account key
                Console.WriteLine("Regenerating the primary key of account {0} ....", accountName);
                BatchAccountRegenerateKeyResponse newKeys = await batchManagementClient.Accounts.RegenerateKeyAsync(ResourceGroupName, accountName,
                    new BatchAccountRegenerateKeyParameters() {KeyName = AccountKeyType.Primary});
                Console.WriteLine("New primary key of account {0}:", accountName);
                Console.WriteLine(newKeys.PrimaryKey);
                Console.WriteLine("Secondary key of account {0}:", accountName);
                Console.WriteLine(newKeys.SecondaryKey);
                Console.WriteLine();

                // List accounts
                Console.WriteLine("Listing all Batch accounts under subscription id {0} ...", context.Subscription.Id);
                BatchAccountListResponse listResponse = await batchManagementClient.Accounts.ListAsync(new AccountListParameters());
                IList<AccountResource> accounts = listResponse.Accounts;
                Console.WriteLine("Total number of Batch accounts under subscription id {0}:  {1}", context.Subscription.Id, accounts.Count);
                for (int i = 0; i < accounts.Count; i++)
                {
                    Console.WriteLine(" {0} - {1}", i + 1, accounts[i].Name);
                }
                Console.WriteLine();

                // Delete account
                Console.WriteLine("Deleting account {0} ...", accountName);
                await batchManagementClient.Accounts.DeleteAsync(ResourceGroupName, accountName);
                Console.WriteLine("Account {0} deleted", accountName);
                Console.WriteLine();
            }
        }
    }
}
