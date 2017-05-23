---
services: Batch
platforms: java
---

#Getting Started with Batch - Create Pool - in Java


  Azure Batch sample for managing pool -
   - Create IaaS pool
   - Wait the VMs to be ready
   - Submit a simple job with task associated with resource file
     - Upload file to Azure storage
     - Generate the SAS url for the file
     - Associate the resource with task
   - Wait the task to finish
   - Delete the job and the pool
 

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

## More information

[http://azure.com/java](http://azure.com/java)

If you don't have a Microsoft Azure subscription you can get a FREE trial account [here](http://go.microsoft.com/fwlink/?LinkId=330212)
