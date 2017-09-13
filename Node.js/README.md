# Azure Batch Node.js sample
The set of scripts provided in the [GettingStarted](https://github.com/Azure/azure-batch-samples/tree/master/Node.js/GettingStarted) folder is a sample of creating Azure Batch jobs using the Node.js SDK.

Please refer to a step by step explanation of these scripts at the [Azure documentation link](https://docs.microsoft.com/en-us/azure/batch/batch-nodejs-get-started).

You will need to fill in Azure Batch Account details and the resource file SAS URIs

// Setting up variables specific to Batch & storage account
var accountName = '<azure-batch-account-name>';
var accountKey = '<account-key-downloaded>';
var accountUrl = '<account-url>';
var sh_url = '<Shell-script-SAS-URI>';
var scriptURI = '<Python-script-SAS-URI'>;

Also modify the [processcsv.py](https://github.com/Azure/azure-batch-samples/blob/master/Node.js/GettingStarted/processcsv.py) with your storage account credentials that contain the csv files for conversion.