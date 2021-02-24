import { BatchServiceClient, BatchSharedKeyCredentials } from "@azure/batch";

// Replace values below with Batch Account details 
const batchAccountName = '<batch-account-name>';
const batchAccountKey = '<batch-account-key>';
const batchEndpoint = '<batch-account-url>';

// Replace values with SAS URIs of the shell script file
const sh_url = "https://batchdevsgsa.blob.core.windows.net/downloads/startup_prereq.sh?st=2017-04-10T18%3A11%3A00Z&se=2020-03-11T18%3A11%3A00Z&sp=rl&sv=2018-03-28&sr=b&sig=xxxx";

// Replace values with SAS URIs of the Python script file
const scriptURI = "https://batchdevsgsa.blob.core.windows.net/downloads/processcsv.py?st=2017-04-10T18%3A11%3A00Z&se=2020-03-11T18%3A11%3A00Z&sp=rl&sv=2018-03-28&sr=b&sig=xxxx";


const credentials = new BatchSharedKeyCredentials(batchAccountName, batchAccountKey);
const batchClient = new BatchServiceClient(credentials, batchEndpoint);

// Pool ID 
const now = new Date();
const poolId = `processcsv_${now.getFullYear()}${now.getMonth()}${now.getDay()}${now.getHours()}${now.getSeconds()}`;

// Job ID 
const jobId = "processcsvjob";

// Pool VM Image Reference
const imgRef = {
    publisher: "Canonical",
    offer: "UbuntuServer",
    sku: "16.04-LTS",
    version: "latest"
}
// Pool VM configuraion object
const vmConfig = {
    imageReference: imgRef,
    nodeAgentSKUId: "batch.node.ubuntu 16.04"
};
// Number of VMs to create in a pool
const numVms = 4;
const vmSize = "STANDARD_D1_V2";
// Pool configuration object
const poolConfig = {
    id: poolId,
    displayName: "Processing csv files",
    vmSize: vmSize,
    virtualMachineConfiguration: vmConfig,
    targetDedicatedNodes: numVms,
    enableAutoScale: false
};

// Creating Batch Pool
console.log("Creating pool with ID : " + poolId);
const pool = batchClient.pool.add(poolConfig, function (error, result, request, response) {
    if (error !== null) {
        console.log("An error occured while creating the pool...");
        console.log(error.response);
    }
    else {
        // If there is no error then create the Batch Job        
        createJob();
    }
});

function createJob() {
    console.log("Creating job with ID : " + jobId);
    // Preparation Task configuraion object    
    const jobPrepTaskConfig = {
        id: "installprereq",
        commandLine: "sudo sh startup_prereq.sh > startup.log",
        resourceFiles: [{ 'httpUrl': sh_url, 'filePath': 'startup_prereq.sh' }],
        waitForSuccess: true, runElevated: true
    };

    // Setting Batch Pool ID
    const poolInfo = { poolId: poolId };
    // Batch job configuration object
    const jobConfig = {
        id: jobId,
        displayName: "process csv files",
        jobPreparationTask: jobPrepTaskConfig,
        poolInfo: poolInfo
    };

    // Submitting Batch Job
    const job = batchClient.job.add(jobConfig, function (error, result) {
        if (error !== null) {
            console.log("An error occurred while creating the job...");
            console.log(error);
        }
        else {
            // Create tasks if the job submitted successfully                        
            createTasks();
        }
    });
}

function createTasks() {
    console.log("Creating tasks....");
    const containerList = ["con1", "con2", "con3", "con4"];
    containerList.forEach(function (val, index) {
        console.log("Submitting task for container : " + val);
        const containerName = val;
        const taskID = containerName + "_process";
        // Task configuration object
        const taskConfig = {
            id: taskID,
            displayName: 'process csv in ' + containerName,
            commandLine: 'python processcsv.py --container ' + containerName,
            resourceFiles: [{ 'httpUrl': scriptURI, 'filePath': 'processcsv.py' }]
        };

        const task = batchClient.task.add(jobId, taskConfig, function (error, result) {
            if (error !== null) {
                console.log("Error occured while creating task for container " + containerName + ". Details : " + error.response);
            }
            else {
                console.log("Task for container : " + containerName + " submitted successfully");
            }
        });
    });
}