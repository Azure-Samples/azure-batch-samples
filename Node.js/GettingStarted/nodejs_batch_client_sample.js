// Importing required node.js modules
var batch = require('azure-batch');

// Replace values below with Batch Account details 
var accountName = '<batch-account-name>';
var accountKey = '<batch-account-key>';
var accountUrl = '<batch-account-url>';

// Replace values with SAS URIs of the shell script file
var sh_url = "https://batchdevsgsa.blob.core.windows.net/downloads/startup_prereq.sh?st=2017-04-10T18%3A11%3A00Z&se=2020-03-11T18%3A11%3A00Z&sp=rl&sv=2018-03-28&sr=b&sig=xxxx";

// Replace values with SAS URIs of the Python script file
var scriptURI = "https://batchdevsgsa.blob.core.windows.net/downloads/processcsv.py?st=2017-04-10T18%3A11%3A00Z&se=2020-03-11T18%3A11%3A00Z&sp=rl&sv=2018-03-28&sr=b&sig=xxxx";

// Pool ID 
var now = new Date();
var poolId = "processcsv_"+ now.getFullYear() + now.getMonth() + now.getDay() + now.getHours() + now.getSeconds();

// Job ID 
var jobId = "processcsvjob"; 

// Create Batch credentials object using account name and account key
var credentials = new batch.SharedKeyCredentials(accountName,accountKey);    

// Create Batch service client
var batchClient = new batch.ServiceClient(credentials,accountUrl);

// Pool VM Image Reference
var imgRef = {publisher:"Canonical",
                offer:"UbuntuServer",
                sku:"16.04-LTS",
                version:"latest"}
// Pool VM configuraion object
var vmConfig = {imageReference:imgRef,
    nodeAgentSKUId:"batch.node.ubuntu 16.04"};
// Number of VMs to create in a pool
var numVms = 4;
var vmSize = "STANDARD_D1_V2";
// Pool configuration object
var poolConfig = {id:poolId, 
    displayName:"Processing csv files",
    vmSize:vmSize,
    virtualMachineConfiguration:vmConfig,
    targetDedicatedNodes:numVms,
    enableAutoScale:false };

// Creating Batch Pool
console.log("Creating pool with ID : " + poolId);
var pool = batchClient.pool.add(poolConfig,function(error,result,request,response){
    if(error !== null)
    {
        console.log("An error occured while creating the pool...");
        console.log(error.response);        

    }
    else
    {
        // If there is no error then create the Batch Job        
        createJob();
    }
 });

function createJob()
{
    console.log("Creating job with ID : " + jobId);
    // Preparation Task configuraion object    
    var jobPrepTaskConfig = {id:"installprereq",
        commandLine:"sudo sh startup_prereq.sh > startup.log",
        resourceFiles:[{'httpUrl':sh_url,'filePath':'startup_prereq.sh'}],
        waitForSuccess:true,runElevated:true};

    // Setting Batch Pool ID
    var poolInfo = {poolId:poolId};    
    // Batch job configuration object
    var jobConfig = {id:jobId,
        displayName:"process csv files",
        jobPreparationTask:jobPrepTaskConfig,
        poolInfo:poolInfo};
    
     // Submitting Batch Job

     var job = batchClient.job.add(jobConfig,function(error,result){
        if(error !== null)
        {
            console.log("An error occured while creating the job...");   
            console.log(error);
        }
        else
        {
            // Create tasks if the job submitted successfully                        
            createTasks();
        }
    });
}

function createTasks()
{
    console.log("Creating tasks....");
    var containerList = ["con1","con2","con3","con4"];    
    containerList.forEach(function(val,index){
        console.log("Submitting task for container : " + val);
        var containerName = val;
        var taskID = containerName + "_process";        
        // Task configuration object
        var taskConfig = {id:taskID,
            displayName:'process csv in ' + containerName,
            commandLine:'python processcsv.py --container ' + containerName,
            resourceFiles:[{'httpUrl':scriptURI,'filePath':'processcsv.py'}]};

        var task = batchClient.task.add(jobId,taskConfig,function(error,result){
            if(error !== null)
            {
                console.log("Error occured while creating task for container " + containerName + ". Details : "  + error.response);
            }
            else
            {
                console.log("Task for container : " + containerName + " submitted successfully");
            }
        });
    });      
}