## TextSearch

### About this sample
This map-reduce style sample uses Azure Batch to perform parallel text processing on an input file by splitting it up into multiple sub-files and performing regular expression matching on each sub-file. The results are then rolled-up into a final report by a reduction phase where it is uploaded to Azure Storage.

#### JobSubmitter
Uploads the files required for the text processing to Azure Storage and submits a job to the Azure Batch service that utilizes the autopool functionality. It also specifies a job manager task which orchestrates the rest of the work performed by the Batch Service.

#### JobManager
The job manager task submits mapper and reducer tasks and also monitors the status of those tasks.

#### MapperTask
This executable performs a regular expression search on a specified file and writes the results to standard out.  

#### ReducerTask
This executable aggregates the output of the mapper tasks and writes them to standard out.

### Configuring this sample
Note: Arguments to this sample are controlled via the Common\Settings.settings, which are specific to this sample, and the AccountSettings.settings files where your Azure Batch and Storage credentials are specified.

### Running this sample
The TextSearch is comprised of 4 separate projects.  In order to open the sample, open the TextSearch.sln file.  In order to run the sample, first configure the sample as described in the previous section and then run the [JobSubmitter](./JobSubmitter) executable.
