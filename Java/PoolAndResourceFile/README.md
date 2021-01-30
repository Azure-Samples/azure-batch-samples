---
services: Batch
platforms: java
---

## Description
When run, this sample will:

- Create an Azure Batch pool with a single dedicated node
- Wait for the nodes to be ready
- Create a storage container and upload a resource file to it
- Submit a job with 5 tasks associated with the resource file
- Wait for all tasks to finish
- Delete the job, the pool and the storage container

## Running this Sample
To run this sample:

Set the following environment variables:
- `AZURE_BATCH_ACCOUNT` -- The Batch account name.
- `AZURE_BATCH_ACCESS_KEY` -- The Batch account key.
- `AZURE_BATCH_ENDPOINT` -- The Batch account endpoint.
- `STORAGE_ACCOUNT_NAME` -- The storage account to hold resource files.
- `STORAGE_ACCOUNT_KEY` -- The storage account key.

Clone repo and compile the code:

    git clone https://github.com/Azure/azure-batch-samples.git

    cd azure-batch-samples/Java/PoolAndResourceFile

    mvn clean compile exec:java