using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Common.Authentication;
using Microsoft.Azure.Common.Authentication.Models;
using Microsoft.Azure.Management.Batch;
using Microsoft.Azure.Management.Batch.Models;
using Microsoft.Azure.Management.Resources;
using Microsoft.Azure.Management.Resources.Models;

namespace AccountManagement
{
    public class BatchAccountManagementSample
    {
        private const string BatchNameSpace = "Microsoft.Batch";
        private const string ResourceGroupName = "SampleGroup";
        
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
            AzureProfile profile = GetAzureProfile();

            string location = PromptUserForLocation();

            using (IResourceManagementClient resourceManagementClient = 
                AzureSession.ClientFactory.CreateClient<ResourceManagementClient>(profile, AzureEnvironment.Endpoint.ResourceManager))
            {
                // Register with the Batch resource provider.
                resourceManagementClient.Providers.Register(BatchNameSpace);

                try
                {
                    CreateResourceGroupAsync(resourceManagementClient, location).Wait();

                    PerformBatchAccountOperationsAsync(profile, location).Wait();
                }
                catch (AggregateException aex)
                {
                    foreach (Exception inner in aex.InnerExceptions)
                    {
                        Console.WriteLine("Unexpected error encountered: {0}", inner.ToString());
                    }
                }
            }

            Console.WriteLine();
            Console.WriteLine("Press any key to exit");
            Console.ReadKey();
        }

        // Gets the customer's Azure account and subscription information
        private static AzureProfile GetAzureProfile()
        {
            AzureProfile profile = new AzureProfile();
            ProfileClient profileClient = new ProfileClient(profile);
            AzureAccount azureAccount = new AzureAccount() {Type = AzureAccount.AccountType.User};

            // Prompts the user for their credentials and retrieves their account/subscription info
            profileClient.AddAccountAndLoadSubscriptions(azureAccount, profile.Environments[EnvironmentName.AzureCloud], null);

            // By default, the first subscription is chosen
            if (profileClient.Profile.Subscriptions.Count > 1)
            {
                SelectSubscription(profileClient.Profile);
            }

            return profileClient.Profile;
        }

        // Prompt the user for the subscription to use
        private static void SelectSubscription(AzureProfile profile)
        {
            string[] subscriptionNames = profile.Subscriptions.Values.Select(s => s.Name).ToArray();
            string selectedSubscription = PromptForSelectionFromList(subscriptionNames, "Enter the number of the Azure subscription you would like to use:");
            profile.DefaultSubscription = profile.Subscriptions.Values.First(s => s.Name.Equals(selectedSubscription));
        }

        // Prompt the user for the location where the resource group and Batch account will be created
        private static string PromptUserForLocation()
        {
            return PromptForSelectionFromList(Locations, "Enter the number of the location where you'd like to create your Batch account:");
        }

        // Prompts the user to select an item from a list of options
        private static string PromptForSelectionFromList(string[] choices, string promptMessage)
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

        // Create a resource group to create Batch accounts under.
        private static async Task CreateResourceGroupAsync(IResourceManagementClient resourceManagementClient, string location)
        {
            ResourceGroupExistsResult existsResult = await resourceManagementClient.ResourceGroups.CheckExistenceAsync(ResourceGroupName);
            if (!existsResult.Exists)
            {
                await resourceManagementClient.ResourceGroups.CreateOrUpdateAsync(ResourceGroupName, new ResourceGroup(location));
            }
        }

        // Performs various Batch account operations
        private static async Task PerformBatchAccountOperationsAsync(AzureProfile profile, string location)
        {
            using (IBatchManagementClient batchManagementClient = 
                AzureSession.ClientFactory.CreateClient<BatchManagementClient>(profile, AzureEnvironment.Endpoint.ResourceManager))
            {
                Console.WriteLine("The Batch account name must be 3 to 24 characters, and it must only contain lowercase letters and numbers.");
                Console.WriteLine("Please input the account name you want to create: ");
                string accountName = Console.ReadLine();
                Console.WriteLine();

                // Create account
                Console.WriteLine("Creating account {0} ...", accountName);
                await batchManagementClient.Accounts.CreateAsync(ResourceGroupName, accountName, new BatchAccountCreateParameters() {Location = location});
                Console.WriteLine("Account {0} created", accountName);
                Console.WriteLine();

                // Get acount
                Console.WriteLine("Getting account {0} ...", accountName);
                BatchAccountGetResponse getRespone = await batchManagementClient.Accounts.GetAsync(ResourceGroupName, accountName);
                AccountResource account = getRespone.Resource;
                Console.WriteLine("Got account {0}:", accountName);
                Console.WriteLine(" Account location: {0}", account.Location);
                Console.WriteLine(" Account resource type: {0}", account.Type);
                Console.WriteLine(" Account id: {0}", account.Id);
                Console.WriteLine();

                // Get account keys
                Console.WriteLine("Getting Account keys of account {0} ....", accountName);
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

                // list accounts
                Console.WriteLine("Listing all Batch accounts under subscription id {0} ...", profile.DefaultSubscription.Id);
                BatchAccountListResponse listResponse = await batchManagementClient.Accounts.ListAsync(new AccountListParameters());
                IList<AccountResource> accounts = listResponse.Accounts;
                Console.WriteLine("Total number of Batch accounts under subscription id {0}:  {1}", profile.DefaultSubscription.Id, accounts.Count);
                for (int i = 0; i < accounts.Count; i++)
                {
                    Console.WriteLine(" {0} - {1}", i + 1, accounts[i].Name);
                }
                Console.WriteLine();

                // delete account
                Console.WriteLine("Deleting account {0} ...", accountName);
                await batchManagementClient.Accounts.DeleteAsync(ResourceGroupName, accountName);
                Console.WriteLine("Account {0} deleted", accountName);
            }
        }
    }
}
