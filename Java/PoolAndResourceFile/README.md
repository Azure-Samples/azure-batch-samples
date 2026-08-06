---
services: Batch
platforms: java
---

## Description
When run, this sample will:

- Create an Azure Batch pool with a single dedicated node
- Wait for the node to be ready
- Create a storage container and upload a resource file to it
- Submit a job with 5 tasks associated with the resource file
- Wait for all tasks to finish
- Delete the job, the pool and the storage container

## Prerequisites

- Configure both an Azure Batch account and an Azure Storage account in the same region.

- Set the following environment variables:
  - `AZURE_RESOURCE_GROUP_NAME` -- The resource group of the Batch account.
  - `AZURE_BATCH_ACCOUNT_NAME` -- The name of the Batch account.
  - `AZURE_BLOB_SERVICE_URL` -- The blob service URL of the Storage account.

- Ensure you have the [Azure CLI](https://learn.microsoft.com/cli/azure/) installed and run the following commands, 
  replacing `<subscription_id>` with your Azure subscription ID. When running the `az login` command, make sure
  you are authenticating as a user with appropriate permissions to both the Batch account and Storage account.
    ```shell
    az login 
    az account set -s <subscription_id> 
    ```

**Note:** The sample code uses [DefaultAzureCredential](https://github.com/Azure/azure-sdk-for-java/wiki/Azure-Identity-Examples#authenticating-with-defaultazurecredential),
which supports a wide variety of authentication methods, not just the Azure CLI. Any of these other methods should work
as long as the user or service principal has the appropriate permissions.

## Running the sample
Run the following command from the same directory as this README to compile and run the sample:

```shell
mvn clean compile exec:java
```