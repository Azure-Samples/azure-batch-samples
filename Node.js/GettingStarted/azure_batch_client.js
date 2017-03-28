// Initializing node.js modules

var batch = require('azure-batch');
var https = require('https');

var fs = require('fs');
var uuid = require('node-uuid');

var accountName = '<azure-batch-account-name>';
var accountKey = '<account-key-downloaded>';
var accountUrl = '<account-url>'
// Setting up Job configuration along with preparation task
var jobId = "processcsvjob"; 


// Create Batch credentials object using account name and account key
 var credentials = new batch.SharedKeyCredentials(accountName,accountKey);    

 // Create Batch service client

var batch_client = new batch.ServiceClient(credentials,accountUrl);

var now = new Date();


var poolid = "processcsv_"+ now.getFullYear() + now.getMonth() + now.getDay() + now.getHours() + now.getSeconds()

console.log("Creating pool with ID" + poolid);

var imgRef = {publisher:"Canonical",offer:"UbuntuServer",sku:"14.04.2-LTS",version:"latest"}
var vmconfig = {imageReference:imgRef,nodeAgentSKUId:"batch.node.ubuntu 14.04"}
var numVms = 4;
var vmSize = "STANDARD_A1";

var clientPoolRequestID = uuid.v4();
var poolConfig = {id:poolid, displayName:poolid,vmSize:vmSize,virtualMachineConfiguration:vmconfig,targetDedicated:numVms,enableAutoScale:false }
var poolAddOptionsConfig = {clientRequestId:clientPoolRequestID,returnClientRequestId:true}
var poolOptions = {poolAddOptions:poolAddOptionsConfig}
var pool = batch_client.pool.add(poolConfig,poolOptions,function(error,result,request,response){            
    //        console.log("Pool add response stream" + response.body);
            if(error != null)
            {
                console.log(error.response);
            }
            
        });

console.log("Submitted request to create pool...");


var isPoolStateActive = false;
var poolCreationError = false;
var maxRetry = 1000;
var numRetries = 0;
var jobCreated = false;

while(isPoolStateActive == false && poolCreationError == false && numRetries < maxRetry)
{
    
    var cloudPool = batch_client.pool.get(poolid,function(error,result,request,response){
        if(error == null)
        {
            if(result.state == "active")
            {
                  isPoolStateActive = true;
                  if(jobCreated==true)
                  {
                      // do nothing
                  }
                  else
                  {
                     jobCreated = true;
                     console.log("Pool created successfully now submitting job");
                     createJob();
                  }
                  //// Create the Job 
            }
        }
        else
        {
            if(error.statusCode==404)
            {
                //console.log("Pool not found yet returned 404...");    
                           
            }
            else
            {
                poolCreationError = true;
            }
        }
        });
  
  numRetries++;
 
}

console.log("Pool Status : " + isPoolStateActive);






function createJob()
{
    console.log("Inside create job...");
    var sh_url = "Shell script SAS URI";
    var job_prep_task_config = {id:"installprereq",commandLine:"sudo sh startup_prereq.sh > startup.log",resourceFiles:[{'blobSource':sh_url,'filePath':'startup_prereq.sh'}],waitForSuccess:true,runElevated:true}
    // Setting up Batch pool configuration
    var poolConfigJob = {poolId:poolid}
    
    
    var job_config = {id:jobId,displayName:"process csv files",jobPreparationTask:job_prep_task_config,poolInfo:poolConfigJob}
    
     // Adding Azure batch job to the pool
     var job = batch_client.job.add(job_config,function(error,result){
        if(error !=null)
        {
            console.log(error);
        }        
        
    });       
    var maxRetryJob = 100;
    var isJobCreated = false
    var isJobCreatedError = false
    var numRetriesJob = 0;
    var taskSubmitted = false;
    while(numRetriesJob < maxRetryJob && isJobCreated == false && isJobCreatedError == false)
    {
        var jobStatus = batch_client.job.get(jobId,function(error,result,request,response)
        {
            if(error == null)
            {
               // console.log(result);
               // console.log("Result state" + result.state);
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
                    //console.log("Job not created yet, received 404...");
                }
                else
                {
                    console.log("Error occured while job creation" + error.response);
                    isJobCreatedError = true;
                }
            }
        }); 
        numRetriesJob++;
   }
    

}

function createTasks()
{
    var container_list = ["con1","con2","con3","con4"]
    var scriptURI = "Python scrip SAS URI";
    console.log("Entering container list");
    container_list.forEach(function(val,index){
           console.log("Submitting task for container : " + val);
           var date_param = Math.floor((new Date()).getUTCMilliseconds() / 1000)
           var exec_info_config = {startTime: date_param}
           var container_name = val;
           var taskID = container_name + "_process";
           var task_config = {id:taskID,displayName:'process csv in ' + container_name,commandLine:'python processcsv.py --container ' + container_name,resourceFiles:[{'blobSource':scriptURI,'filePath':'processcsv.py'}]}
           var task = batch_client.task.add(jobId,task_config,function(error,result){
                if(error != null)
                {
                    console.log(error.response);     
                }
                else
                {
                    console.log("Task for container : " + container_name + "submitted successfully");
                }
               
               

           });

    });
       
}
