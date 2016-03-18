##Python Samples

###Azure Storage samples

####[blobxfer.py](./Storage)
Code sample to perform AzCopy-like blob data transfer to/from Azure Blob
Storage.

###Azure Batch samples

#### Configuring the samples
In order the run the samples, they must be configured with Azure Batch and Azure Storage credentials. The credentials for each sample are gathered from the common configuration located [here](./Batch/configuration.cfg). Once you have configured your account credentials, you can run any of the samples and they will make use of the credentials provided in the common configuration file.

Each sample also has a configuration file specific to the individual sample (for example [sample1_helloworld.cfg](./Batch/sample1_helloworld.cfg))

#### Setting up the Python environment
In order to run the samples, you will need Python 3.5. You will also need to install the Azure Batch and Azure Storage python packages.  This can be done using the [requirements.txt](./Batch/requirements.txt) file using `pip install -r requirements.txt`

You can also optionally use the [Visual Studio project](./Batch/BatchSamples.pyproj) and the [Python Tools for Visual Studio plugin](https://github.com/Microsoft/PTVS/wiki/PTVS-Installation).

####[sample1\_helloworld.py](./Batch/sample1_helloworld.py)
The HelloWorld sample is an introduction to the framework required to communicate with the Batch service. It submits a job using an auto-pool and then submits a task which performs a simple echo command.  The task has no required files.  The focus of this sample is on the API calls required to add a job to the Batch service and monitor the status of that job from a client.

####[sample2\_pools\_and\_resourcefiles.py](./Batch/sample2_pools_and_resourcefiles.py)
This sample expands on the HelloWorld sample. It creates a fixed pool and then submits a simple python script as the only task of the job. This sample also showcases the use of a StartTask as a method to get files onto every node in the pool.