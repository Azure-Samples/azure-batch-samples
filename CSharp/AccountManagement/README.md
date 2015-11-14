### AccountManagement

The AccountManagement sample project demonstrates usage of the [Batch Management .NET][net_mgmt_api] library.

NOTE: To run the sample application successfully, you must first register it with Azure Active Directory using the Azure Management Portal. See [Integrating Applications with Azure Active Directory][aad_integrate] for more information.

This sample application demonstrates the following operations:

1. Acquire security token from Azure Active Directory (AAD) using [ADAL][aad_adal]
2. Use acquired security token to create a [SubscriptionClient][net_subclient], query Azure for a list of subscriptions associated with the account, prompt user for subscription
3. Create a new credentials object associated with the selected subscription
4. Create a [ResourceManagementClient][net_resclient] using the new credentials
5. Use the [ResourceManagementClient][net_resclient] to create a new resource group
6. Use the [BatchManagementClient][net_batchclient] to perform a number of Batch account operations
7. Delete the resource group

For more information on using the [Batch Management .NET][net_mgmt_api] library, please see the following article:

[Manage Azure Batch accounts and quotas with Batch Management .NET][acom_article]

[aad_adal]: https://azure.microsoft.com/documentation/articles/active-directory-authentication-libraries/
[aad_integrate]: https://azure.microsoft.com/documentation/articles/active-directory-integrating-applications/
[acom_article]: https://azure.microsoft.com/documentation/articles/batch-management-dotnet/
[net_batchclient]: https://msdn.microsoft.com/library/azure/microsoft.azure.management.batch.batchmanagementclient.aspx
[net_mgmt_api]: https://msdn.microsoft.com/library/azure/mt463120.aspx
[net_mgmt_nuget]: https://www.nuget.org/packages/Microsoft.Azure.Management.Batch/
[net_resclient]: https://msdn.microsoft.com/library/azure/microsoft.azure.management.resources.resourcemanagementclient.aspx
[net_subclient]: https://msdn.microsoft.com/library/azure/microsoft.azure.subscriptions.subscriptionclient.aspx
