// Copyright (c) Microsoft Corporation
//
// Companion project to the following article:
// https://azure.microsoft.com/documentation/articles/batch-management-dotnet/

namespace Microsoft.Azure.Batch.Samples.AccountManagement
{
    using global::Azure;
    using global::Azure.Core;
    using global::Azure.Identity;
    using global::Azure.ResourceManager;
    using global::Azure.ResourceManager.Batch;
    using global::Azure.ResourceManager.Batch.Models;
    using global::Azure.ResourceManager.Resources;
    using Microsoft.Azure.Batch.Samples.Common;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    public class BatchAccountManagementSample
    {
        // The name of the Resource Group that will be created and deleted.
        private const string ResourceGroupName = "AccountMgmtSampleGroup";

        public static void Main(string[] args)
        {
            try
            {
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
            // Authenticate with the user's default credentials (Azure CLI, Visual Studio,
            // managed identity, environment variables, etc.). Run `az login` before running
            // this sample if you have not already authenticated.
            ArmClient armClient = new ArmClient(new DefaultAzureCredential());

            SubscriptionResource subscription = await SelectSubscriptionAsync(armClient);
            string location = await PromptUserForLocationAsync(subscription);

            ResourceGroupResource resourceGroup = await CreateResourceGroupAsync(subscription, location);

            try
            {
                await PerformBatchAccountOperationsAsync(subscription, resourceGroup, location);
            }
            finally
            {
                await DeleteResourceGroupAsync(resourceGroup);
            }
        }

        /// <summary>
        /// Prompts the user to select a subscription from those accessible by the signed-in identity.
        /// </summary>
        private static async Task<SubscriptionResource> SelectSubscriptionAsync(ArmClient armClient)
        {
            var subscriptions = new List<SubscriptionResource>();
            await foreach (SubscriptionResource sub in armClient.GetSubscriptions().GetAllAsync())
            {
                subscriptions.Add(sub);
            }

            if (subscriptions.Count == 0)
            {
                throw new InvalidOperationException("No subscriptions found in account. Please create at least one subscription within your Azure account.");
            }

            if (subscriptions.Count == 1)
            {
                return subscriptions[0];
            }

            string[] subscriptionNames = subscriptions.Select(s => s.Data.DisplayName).ToArray();
            string selectedSubscription = PromptForSelectionFromCollection(subscriptionNames, "Enter the number of the Azure subscription to use: ");
            return subscriptions.First(s => s.Data.DisplayName.Equals(selectedSubscription));
        }

        /// <summary>
        /// Lists Azure regions where the Microsoft.Batch resource provider supports
        /// the batchAccounts resource type, and prompts the user to choose one.
        /// </summary>
        private static async Task<string> PromptUserForLocationAsync(SubscriptionResource subscription)
        {
            ResourceProviderResource batchProvider = await subscription.GetResourceProviderAsync("Microsoft.Batch");
            var batchAccountResourceType = batchProvider.Data.ResourceTypes.First(r => r.ResourceType == "batchAccounts");
            string[] locations = batchAccountResourceType.Locations.ToArray();

            return PromptForSelectionFromCollection(locations, "Enter the number of the location where you'd like to create your Batch account: ");
        }

        /// <summary>
        /// Helper function that prompts the user to make a selection from a collection.
        /// </summary>
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

            if (!int.TryParse(numberText, out int number) || number <= 0 || number > choices.Length)
            {
                throw new ArgumentException("Supplied value not a valid number from the list.");
            }

            return choices[number - 1];
        }

        /// <summary>
        /// Prompts the user for the name of the Batch account to create.
        /// </summary>
        private static string PromptUserForAccountName()
        {
            Console.WriteLine("Batch account names must be 3 to 24 characters and contain only lowercase letters and numbers.");
            Console.Write("Enter the name of the Batch account to create: ");
            string accountName = Console.ReadLine();
            Console.WriteLine();

            return accountName;
        }

        /// <summary>
        /// Creates the resource group that will host the Batch account.
        /// </summary>
        private static async Task<ResourceGroupResource> CreateResourceGroupAsync(SubscriptionResource subscription, string location)
        {
            ResourceGroupCollection resourceGroups = subscription.GetResourceGroups();

            Response<bool> exists = await resourceGroups.ExistsAsync(ResourceGroupName);
            if (exists.Value)
            {
                Response<ResourceGroupResource> existing = await resourceGroups.GetAsync(ResourceGroupName);
                return existing.Value;
            }

            Console.WriteLine("Creating resource group {0}", ResourceGroupName);
            ArmOperation<ResourceGroupResource> op = await resourceGroups.CreateOrUpdateAsync(
                WaitUntil.Completed,
                ResourceGroupName,
                new ResourceGroupData(new AzureLocation(location)));
            Console.WriteLine("Resource group created");
            Console.WriteLine();
            return op.Value;
        }

        /// <summary>
        /// Deletes the resource group used by this sample.
        /// </summary>
        private static async Task DeleteResourceGroupAsync(ResourceGroupResource resourceGroup)
        {
            Console.Write("Hit ENTER to delete resource group {0}: ", resourceGroup.Data.Name);
            Console.ReadLine();
            Console.WriteLine("Deleting resource group {0}...", resourceGroup.Data.Name);
            await resourceGroup.DeleteAsync(WaitUntil.Completed);
            Console.WriteLine("Resource group deleted");
            Console.WriteLine();
        }

        /// <summary>
        /// Performs various Batch account operations using the Azure.ResourceManager.Batch library.
        /// </summary>
        private static async Task PerformBatchAccountOperationsAsync(SubscriptionResource subscription, ResourceGroupResource resourceGroup, string location)
        {
            // Get the account quota for the subscription
            BatchLocationQuota quota = await subscription.GetBatchQuotasAsync(new AzureLocation(location));
            Console.WriteLine("Your subscription can create {0} account(s) in the {1} region.", quota.AccountQuota, location);
            Console.WriteLine();

            // Create account
            string accountName = PromptUserForAccountName();
            Console.WriteLine("Creating account {0}...", accountName);
            BatchAccountCollection accounts = resourceGroup.GetBatchAccounts();
            ArmOperation<BatchAccountResource> createOp = await accounts.CreateOrUpdateAsync(
                WaitUntil.Completed,
                accountName,
                new BatchAccountCreateOrUpdateContent(new AzureLocation(location)));
            BatchAccountResource account = createOp.Value;
            Console.WriteLine("Account {0} created", accountName);
            Console.WriteLine();

            // Get account
            Console.WriteLine("Getting account {0}...", accountName);
            account = await account.GetAsync();
            Console.WriteLine("Got account {0}:", account.Data.Name);
            Console.WriteLine("  Account location: {0}", account.Data.Location);
            Console.WriteLine("  Account resource type: {0}", account.Data.ResourceType);
            Console.WriteLine("  Account id: {0}", account.Data.Id);
            Console.WriteLine();

            // Print account quotas
            Console.WriteLine("Quotas for account {0}:", account.Data.Name);
            Console.WriteLine("  Dedicated core quota: {0}", account.Data.DedicatedCoreQuota);
            Console.WriteLine("  Low priority core quota: {0}", account.Data.LowPriorityCoreQuota);
            Console.WriteLine("  Pool quota: {0}", account.Data.PoolQuota);
            Console.WriteLine("  Active job and job schedule quota: {0}", account.Data.ActiveJobAndJobScheduleQuota);
            Console.WriteLine();

            // Get account keys
            Console.WriteLine("Getting account keys of account {0}...", account.Data.Name);
            BatchAccountKeys keys = await account.GetKeysAsync();
            Console.WriteLine("  Primary key of account {0}:   {1}", account.Data.Name, keys.Primary);
            Console.WriteLine("  Secondary key of account {0}: {1}", account.Data.Name, keys.Secondary);
            Console.WriteLine();

            // Regenerate primary account key
            Console.WriteLine("Regenerating the primary key of account {0}...", account.Data.Name);
            BatchAccountKeys newKeys = await account.RegenerateKeyAsync(
                new BatchAccountRegenerateKeyContent(BatchAccountKeyType.Primary));
            Console.WriteLine("  New primary key of account {0}: {1}", account.Data.Name, newKeys.Primary);
            Console.WriteLine("  Secondary key of account {0}:   {1}", account.Data.Name, newKeys.Secondary);
            Console.WriteLine();

            // List total number of accounts under the subscription id
            var allAccounts = new List<BatchAccountResource>();
            await foreach (BatchAccountResource acct in subscription.GetBatchAccountsAsync())
            {
                allAccounts.Add(acct);
            }

            Console.WriteLine("Total number of Batch accounts under subscription id {0}:  {1}", subscription.Data.SubscriptionId, allAccounts.Count);

            // Determine how many additional accounts can be created in the target region
            int numAccountsInRegion = allAccounts.Count(o => o.Data.Location == account.Data.Location);
            Console.WriteLine("Accounts in {0}: {1}", account.Data.Location, numAccountsInRegion);
            Console.WriteLine("You can create {0} more accounts in the {1} region.", quota.AccountQuota - numAccountsInRegion, account.Data.Location);
            Console.WriteLine();

            // List accounts in the subscription
            Console.WriteLine("Listing all Batch accounts under subscription id {0}...", subscription.Data.SubscriptionId);
            for (int i = 0; i < allAccounts.Count; i++)
            {
                Console.WriteLine("  {0} - {1} | Location: {2}", i + 1, allAccounts[i].Data.Name, allAccounts[i].Data.Location);
            }
            Console.WriteLine();

            // Delete account
            Console.Write("Hit ENTER to delete account {0}: ", account.Data.Name);
            Console.ReadLine();
            Console.WriteLine("Deleting account {0}...", account.Data.Name);
            await account.DeleteAsync(WaitUntil.Completed);
            Console.WriteLine("Account {0} deleted", account.Data.Name);
            Console.WriteLine();
        }
    }
}
