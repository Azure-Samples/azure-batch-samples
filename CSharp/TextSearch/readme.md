##TextSearch

###About this sample
This map-reduce style sample uses Azure Batch to perform parallel text processing on an input file by splitting it up into multiple sub-files and performing regular expression matching on each sub-file. The results are then rolled-up into a final report by a reduction phase, and the final report is uploaded to Azure Storage.

####JobSubmitter
Uploads the files required for the text processing to Azure Storage and submits a job to the Azure Batch service that utilizes the autopool functionality. Also provides a job manager task. The job manager task will run on the autopool and drive the work done on the Batch Service.

####JobManager
The job manager task submits mapper and reducer tasks and also monitors the status of those tasks.

####MapperTask
This executable performs a regular expression search on a specified file and writes the results to standard out.  

####ReducerTask
This executable aggregates the output of the mapper tasks and writes them to standard out.

###Configuring this sample 
Note: Arguments to this sample are controlled via the .settings file/app.config.  Before running the sample there are fields in that file which must be populated.

###Running this sample
The TextSearch is comprised of 4 separate projects.  In order to open the sample, open the TextSearch.sln file.  In order to run the sample, first configure the sample with your Storage and Batch credentials, and then run the [JobSubmitter](./JobSubmitter) executable.