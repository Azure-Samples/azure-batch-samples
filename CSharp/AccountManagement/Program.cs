// Copyright (c) Microsoft Corporation
//
// Companion project to the following article:
// https://azure.microsoft.com/documentation/articles/batch-management-dotnet/

namespace Microsoft.Azure.Batch.Samples.AccountManagement
{
    using Microsoft.Azure;
    using Microsoft.Azure.Batch.Samples.Common;
    using Microsoft.Azure.Management.Batch;
    using Microsoft.Azure.Management.Batch.Models;
    using Microsoft.Azure.Management.ResourceManager;
    using Microsoft.Azure.Management.ResourceManager.Models;
    using Microsoft.IdentityModel.Clients.ActiveDirectory;
    using Microsoft.Rest;
    using Microsoft.Rest.Azure;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;

    public class BatchAccountManagementSample
    {
        // This sample uses the Active Directory Authentication Library (ADAL) to discover
        // subscriptions in your account and obtain TokenCloudCredentials required by the
        // Batch Management and Resource Management clients. It then creates a Resource
        // Group, performs Batch account operations, and then deletes the Resource Group.

        // These endpoints are used during authentication and authorization with AAD.
        private const string AuthorityUri = "https://login.microsoftonline.com/common"; // Azure Active Directory "common" endpoint
        private const string ResourceUri  = "https://management.core.windows.net/";     // Azure service management resource

        // The URI to which Azure AD will redirect in response to an OAuth 2.0 request. This value is
        // specified by you when you register an application with AAD (see ClientId comment). It does not
        // need to be a real endpoint, but must be a valid URI (e.g. https://accountmgmtsampleapp).
        private const string RedirectUri = "[specify-your-redirect-uri-here]";

        // Specify the unique identifier (the "Client ID") for your application. This is required so that your
        // native client application (i.e. this sample) can access the Microsoft Azure AD Graph API. For information
        // about registering an application in Azure Active Directory, please see "Adding an Application" here:
        // https://azure.microsoft.com/documentation/articles/active-directory-integrating-applications/
        private const string ClientId = "[specify-your-client-id-here]";

        // These constants are used by the ResourceManagementClient when querying AAD and for resource group creation.
        // These values should not be modified.
        private const string BatchNameSpace = "Microsoft.Batch";
        private const string BatchAccountResourceType = "batchAccounts";

        // The name of the Resource Group that will be created and deleted.
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
            // Obtain an access token using the "common" AAD resource. This allows the application
            // to query AAD for information that lies outside the application's tenant (such as for
            // querying subscription information in your Azure account).
            AuthenticationContext authContext = new AuthenticationContext(AuthorityUri);
            AuthenticationResult authResult = await authContext.AcquireTokenAsync(
                ResourceUri,
                ClientId,
                new Uri(RedirectUri),
                new PlatformParameters(PromptBehavior.Auto));

            // The first credential object is used when querying for subscriptions, and is therefore
            // not associated with a specific subscription.
            ServiceClientCredentials subscriptionCreds = new TokenCredentials(authResult.AccessToken);

            string subscriptionId = String.Empty;
            using (SubscriptionClient subClient = new SubscriptionClient(subscriptionCreds))
            {
                // Ask the user to select a subscription. We'll use the selected subscription's
                // ID when constructing another credential object used in initializing the management
                // clients for the remainder of the sample.
                subscriptionId = await SelectSubscriptionAsync(subClient);
            }

            // These credentials are associated with a subscription, and can therefore be used when
            // creating Resource and Batch management clients for use in manipulating entities within
            // the subscription (e.g. resource groups and Batch accounts).
            ServiceClientCredentials creds = new TokenCredentials(authResult.AccessToken);

            // With the ResourceManagementClient, we create a resource group in which to create the Batch account.
            using (ResourceManagementClient resourceManagementClient = new ResourceManagementClient(creds))
            {
                resourceManagementClient.SubscriptionId = subscriptionId;

                // Register with the Batch resource provider; this only needs to be performed once per subscription.
                resourceManagementClient.Providers.Register(BatchNameSpace);
                
                string location = await PromptUserForLocationAsync(resourceManagementClient);
                
                await CreateResourceGroupAsync(resourceManagementClient, location);

                await PerformBatchAccountOperationsAsync(authResult.AccessToken, subscriptionId, location);

                await DeleteResourceGroupAsync(resourceManagementClient);
            }
        }

        /// <summary>
        /// Select the subscription id to use in the rest of the sample. 
        /// </summary>
        /// <param name="client">The <see cref="Microsoft.Azure.Management.ResourceManager.SubscriptionClient"/> to use to get all the subscriptions 
        /// under the user's Azure account.</param>
        /// <returns>A <see cref="System.Threading.Tasks.Task"/> object that represents the asynchronous operation.</returns>
        /// <remarks>If the user has 1 subscription under their Azure account, it is chosen automatically. If the user has more than
        /// one, they are prompted to make a selection.</remarks>
        private static async Task<string> SelectSubscriptionAsync(SubscriptionClient client)
        {
            IPage<Subscription> subs = await client.Subscriptions.ListAsync();

            if (subs.Any())
            {
                var subscriptionsList = new List<Subscription>();
                subscriptionsList.AddRange(subs);

                var nextLink = subs.NextPageLink;
                while (nextLink != null)
                {
                    subs = await client.Subscriptions.ListNextAsync(nextLink);
                    subscriptionsList.AddRange(subs);
                    nextLink = subs.NextPageLink;
                }

                if (subscriptionsList.Count > 1)
                {
                    // More than 1 subscription found under the Azure account, prompt the user for the subscription to use
                    string[] subscriptionNames = subscriptionsList.Select(s => s.DisplayName).ToArray();
                    string selectedSubscription = PromptForSelectionFromCollection(subscriptionNames, "Enter the number of the Azure subscription to use: ");
                    Subscription selectedSub = subscriptionsList.First(s => s.DisplayName.Equals(selectedSubscription));
                    return selectedSub.SubscriptionId;
                }
                else
                {
                    // Only one subscription found, use that one
                    return subscriptionsList.First().SubscriptionId;
                }
            }
            else
            {
                throw new InvalidOperationException("No subscriptions found in account. Please create at least one subscription within your Azure account.");
            }
        }

        /// <summary>
        /// Obtains a list of locations via the specified <see cref="Microsoft.Azure.Management.ResourceManager.IResourceManagementClient"/>
        /// and prompts the user to select a location from the list.
        /// </summary>
        /// <param name="resourceManagementClient">The <see cref="Microsoft.Azure.Management.ResourceManager.IResourceManagementClient"/> 
        /// to use when obtaining a list of datacenter locations.</param>
        /// <returns>A <see cref="System.Threading.Tasks.Task"/> object that represents the asynchronous operation.</returns>
        private static async Task<string> PromptUserForLocationAsync(IResourceManagementClient resourceManagementClient)
        {
            // Obtain the list of available datacenter locations for Batch accounts supported by this subscription
            Provider batchProvider = await resourceManagementClient.Providers.GetAsync(BatchNameSpace);
            ProviderResourceType batchResource = batchProvider.ResourceTypes.Where(p => p.ResourceType == BatchAccountResourceType).First();
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
            if (!int.TryParse(numberText, out number) || number <= 0 || number > choices.Length)
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
        /// <param name="resourceManagementClient">The <see cref="Microsoft.Azure.Management.ResourceManager.IResourceManagementClient"/>
        /// to use when creating the resource group.</param>
        /// <param name="location">The location where the resource group will be created.</param>
        /// <returns>A <see cref="System.Threading.Tasks.Task"/> object that represents the asynchronous operation.</returns>
        private static async Task CreateResourceGroupAsync(IResourceManagementClient resourceManagementClient, string location)
        {
            bool? existsResult = await resourceManagementClient.ResourceGroups.CheckExistenceAsync(ResourceGroupName);
            if (existsResult == null || !existsResult.Value)
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
        /// <param name="resourceManagementClient">The <see cref="Microsoft.Azure.Management.ResourceManager.IResourceManagementClient"/> 
        /// to use when deleting the resource group.</param>
        /// <returns>A <see cref="System.Threading.Tasks.Task"/> object that represents the asynchronous operation.</returns>
        private static async Task DeleteResourceGroupAsync(IResourceManagementClient resourceManagementClient)
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
        /// <param name="accessToken">The access token to use for authentication.</param>
        /// <param name="subscriptionId">The subscription id to use for creating the Batch management client</param>
        /// <param name="location">The location where the Batch account will be created.</param>
        /// <returns>A <see cref="System.Threading.Tasks.Task"/> object that represents the asynchronous operation.</returns>
        private static async Task PerformBatchAccountOperationsAsync(string accessToken, string subscriptionId, string location)
        {
            using (BatchManagementClient batchManagementClient = new BatchManagementClient(new TokenCredentials(accessToken)))
            {
                batchManagementClient.SubscriptionId = subscriptionId;

                // Get the account quota for the subscription
                BatchLocationQuota quotaResponse = await batchManagementClient.Location.GetQuotasAsync(location);
                Console.WriteLine("Your subscription can create {0} account(s) in the {1} region.", quotaResponse.AccountQuota, location);
                Console.WriteLine();

                // Create account
                string accountName = PromptUserForAccountName();
                Console.WriteLine("Creating account {0}...", accountName);
                await batchManagementClient.BatchAccount.CreateAsync(ResourceGroupName, accountName, new BatchAccountCreateParameters() { Location = location });
                Console.WriteLine("Account {0} created", accountName);
                Console.WriteLine();

                // Get account
                Console.WriteLine("Getting account {0}...", accountName);
                BatchAccount account = await batchManagementClient.BatchAccount.GetAsync(ResourceGroupName, accountName);
                Console.WriteLine("Got account {0}:", account.Name);
                Console.WriteLine("  Account location: {0}", account.Location);
                Console.WriteLine("  Account resource type: {0}", account.Type);
                Console.WriteLine("  Account id: {0}", account.Id);
                Console.WriteLine();

                // Print account quotas
                Console.WriteLine("Quotas for account {0}:", account.Name);
                Console.WriteLine("  Dedicated core quota: {0}", account.DedicatedCoreQuota);
                Console.WriteLine("  Low priority core quota: {0}", account.LowPriorityCoreQuota);
                Console.WriteLine("  Pool quota: {0}", account.PoolQuota);
                Console.WriteLine("  Active job and job schedule quota: {0}", account.ActiveJobAndJobScheduleQuota);
                Console.WriteLine();

                // Get account keys
                Console.WriteLine("Getting account keys of account {0}...", account.Name);
                BatchAccountKeys accountKeys = await batchManagementClient.BatchAccount.GetKeysAsync(ResourceGroupName, account.Name);
                Console.WriteLine("  Primary key of account {0}:   {1}", account.Name, accountKeys.Primary);
                Console.WriteLine("  Secondary key of account {0}: {1}", account.Name, accountKeys.Secondary);
                Console.WriteLine();

                // Regenerate primary account key
                Console.WriteLine("Regenerating the primary key of account {0}...", account.Name);
                BatchAccountKeys newKeys = await batchManagementClient.BatchAccount.RegenerateKeyAsync(
                    ResourceGroupName, account.Name, 
                    AccountKeyType.Primary);
                Console.WriteLine("  New primary key of account {0}: {1}", account.Name, newKeys.Primary);
                Console.WriteLine("  Secondary key of account {0}:   {1}", account.Name, newKeys.Secondary);
                Console.WriteLine();

                // List total number of accounts under the subscription id
                IPage<BatchAccount> listResponse = await batchManagementClient.BatchAccount.ListAsync();
                var accounts = new List<BatchAccount>();
                accounts.AddRange(listResponse);

                var nextLink = listResponse.NextPageLink;
                while (nextLink != null)
                {
                    listResponse = await batchManagementClient.BatchAccount.ListNextAsync(nextLink);
                    accounts.AddRange(listResponse);
                    nextLink = listResponse.NextPageLink;
                }

                Console.WriteLine("Total number of Batch accounts under subscription id {0}:  {1}", batchManagementClient.SubscriptionId, accounts.Count());

                // Determine how many additional accounts can be created in the target region
                int numAccountsInRegion = accounts.Count(o => o.Location == account.Location);
                Console.WriteLine("Accounts in {0}: {1}", account.Location, numAccountsInRegion);
                Console.WriteLine("You can create {0} more accounts in the {1} region.", quotaResponse.AccountQuota - numAccountsInRegion, account.Location);
                Console.WriteLine();

                // List accounts in the subscription
                Console.WriteLine("Listing all Batch accounts under subscription id {0}...", batchManagementClient.SubscriptionId);
                foreach (BatchAccount acct in accounts)
                {
                    Console.WriteLine("  {0} - {1} | Location: {2}", accounts.IndexOf(acct) + 1, acct.Name, acct.Location);
                }
                Console.WriteLine();

                // Delete account
                Console.Write("Hit ENTER to delete account {0}: ", account.Name);
                Console.ReadLine();
                Console.WriteLine("Deleting account {0}...", account.Name);

                try
                {
                    await batchManagementClient.BatchAccount.DeleteAsync(ResourceGroupName, account.Name);
                }
                catch (CloudException ex)
                {
                    /*  Account deletion is a long running operation. This .DeleteAsync() method will submit the account deletion request and
                     *  poll for the status of the long running operation until the account is deleted. Currently, querying for the operation
                     *  status after the account is deleted will return a 404 error, so we have to add this catch statement. This behavior will
                     *  be fixed in a future service release.
                     */
                    if (ex.Response.StatusCode != HttpStatusCode.NotFound)
                    {
                        throw;
                    }
                }

                Console.WriteLine("Account {0} deleted", account.Name);
                Console.WriteLine();
            }
        }
    }
}
