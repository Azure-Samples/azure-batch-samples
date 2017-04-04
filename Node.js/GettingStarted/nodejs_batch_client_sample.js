// Initializing node.js modules
const sleepInterval = 4000;
const batch = require('azure-batch');
const https = require('https');
const fs = require('fs');
const uuid = require('node-uuid');
const sleep = require('system-sleep');

// Setting up variables specific to Batch & storage account
var accountName = '<azure-batch-account-name>';
var accountKey = '<account-key-downloaded>';
var accountUrl = '<account-url>'
var sh_url = "Shell script SAS URI";
var scriptURI = "Python script SAS URI";

// Max retries
const maxRetriesVal = 100;
// Job ID 
var jobId = "processcsvjob"; 

// Create Batch credentials object using account name and account key
var credentials = new batch.SharedKeyCredentials(accountName,accountKey);    

// Create Batch service client
var batchClient = new batch.ServiceClient(credentials,accountUrl);

var now = new Date();
var poolId = "processcsv_"+ now.getFullYear() + now.getMonth() + now.getDay() + now.getHours() + now.getSeconds()

console.log("Creating pool with ID" + poolId);

var imgRef = {publisher:"Canonical",
                offer:"UbuntuServer",
                sku:"14.04.2-LTS",
                version:"latest"}
var vmConfig = {imageReference:imgRef,
    nodeAgentSKUId:"batch.node.ubuntu 14.04"}

var numVms = 4;
var vmSize = "STANDARD_A1";
var clientPoolRequestID = uuid.v4();

var poolConfig = {id:poolId, 
    displayName:"Processing csv files",
    vmSize:vmSize,
    virtualMachineConfiguration:vmConfig,
    targetDedicated:numVms,
    enableAutoScale:false }
var isPoolCreationError = false;
var pool = batchClient.pool.add(poolConfig,function(error,result,request,response){
    if(error !== null)
    {
        console.log(error.response);
        isPoolCreationError = true;
    }
 });
console.log("Submitted request to create pool...");

var isPoolStateActive = false;
var numRetries = 0;
var jobCreated = false;

while(isPoolStateActive == false && isPoolCreationError == false && numRetries < maxRetriesVal)
{
    var cloudPool = batchClient.pool.get(poolId,function(error,result,request,response)
    {
        if(error == null)
        {
            if(result.state == "active")
            {
                isPoolStateActive = true;
                if(jobCreated==false)
                {
                    console.log("Pool created successfully, now submitting the job");
                    // Create the Job 
                    createJob(); 
                    jobCreated = true;
                }
            }
        }
        else
        {
            if(error.statusCode==404)
            {
                /// Ignore 404 as the pool is still being created                                           
            }
            else
            {
                console.log("Error occured while pool creation : " + error.response);
                isPoolCreationError = true;
            }
        }
    });
    numRetries++;
    sleep(sleepInterval);
}

function createJob()
{
    console.log("Creating job...");    
    var jobPrepTaskConfig = {id:"installprereq",
                                commandLine:"sudo sh startup_prereq.sh > startup.log",
                                resourceFiles:[{'blobSource':sh_url,'filePath':'startup_prereq.sh'}],
                                waitForSuccess:true,
                                runElevated:true}
    // Setting up Batch pool configuration
    var poolConfigJob = {poolId:poolId}    
    
    var jobConfig = {id:jobId,
        displayName:"process csv files",
        jobPreparationTask:jobPrepTaskConfig,
        poolInfo:poolConfigJob}
    
     // Adding Azure Batch job to the pool
     var job = batchClient.job.add(jobConfig,function(error,result){
        if(error !== null)
        {
            console.log(error);
        }
    });

    
    var isJobCreated = false
    var isJobCreatedError = false
    var numRetriesJob = 0;
    var taskSubmitted = false;
    while(numRetriesJob < maxRetriesVal && isJobCreated == false && isJobCreatedError == false)
    {
        var job = batchClient.job.get(jobId,function(error,result,request,response)
        {
            if(error == null)
            {              
                if(result.state == 'active')
                {
                    isJobCreated = true;  
                    if (taskSubmitted==false)
                    {
                        console.log("Job found...");
                        console.log("Creating tasks...");
                        createTasks();
                        taskSubmitted = true;
                    }
                }
            }
            else
            {
                if(error.statusCode == 404)
                {
                    // Ignore 404 as the job is still being created                    
                }
                else
                {
                    console.log("Error occured while creating a job : " + error.response);
                    isJobCreatedError = true;
                }
            }
        }); 
        numRetriesJob++;
        sleep(sleepInterval);
        
   }
}

function createTasks()
{
    var containerList = ["con1","con2","con3","con4"]    
    console.log("Entering container list");
    containerList.forEach(function(val,index){
        console.log("Submitting task for container : " + val);
        var containerName = val;
        var taskID = containerName + "_process";
        var taskConfig = {id:taskID,
            displayName:'process csv in ' + containerName,
            commandLine:'python processcsv.py --container ' + containerName,
            resourceFiles:[{'blobSource':scriptURI,'filePath':'processcsv.py'}]}

        var task = batchClient.task.add(jobId,taskConfig,function(error,result){
            if(error != null)
            {
                console.log("Error occured while creating task for container " + containerName + ". Details : "  + error.response);
            }
            else
            {
                console.log("Task for container : " + containerName + "submitted successfully");
            }
        });
    });      
}
