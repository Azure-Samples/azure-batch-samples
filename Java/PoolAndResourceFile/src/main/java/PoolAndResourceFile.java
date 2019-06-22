import java.io.*;
import java.net.URISyntaxException;
import java.security.InvalidKeyException;
import java.time.Duration;
import java.util.*;
import java.util.concurrent.TimeoutException;

import com.microsoft.azure.storage.*;
import com.microsoft.azure.storage.blob.*;

import com.microsoft.azure.batch.*;
import com.microsoft.azure.batch.auth.*;
import com.microsoft.azure.batch.protocol.models.*;

public class PoolAndResourceFile {

    /**
     * Create IaaS pool if pool isn't exist
     * 
     * @param client
     *            batch client instance
     * @param poolId
     *            the pool id
     * @return the pool instance
     * @throws BatchErrorException
     * @throws IllegalArgumentException
     * @throws IOException
     * @throws InterruptedException
     * @throws TimeoutException
     */
    private static CloudPool createPoolIfNotExists(BatchClient client, String poolId)
            throws BatchErrorException, IllegalArgumentException, IOException, InterruptedException, TimeoutException {
        // Create a pool with 1 A1 VM
        String osPublisher = "OpenLogic";
        String osOffer = "CentOS";
        String poolVMSize = "STANDARD_A1";
        int poolVMCount = 1;
        Duration POOL_STEADY_TIMEOUT = Duration.ofMinutes(5);
        Duration VM_READY_TIMEOUT = Duration.ofMinutes(20);

        // Check if pool exists
        if (!client.poolOperations().existsPool(poolId)) {

            // See detail of creating IaaS pool at
            // https://blogs.technet.microsoft.com/windowshpc/2016/03/29/introducing-linux-support-on-azure-batch/
            // Get the sku image reference
            List<NodeAgentSku> skus = client.accountOperations().listNodeAgentSkus();
            String skuId = null;
            ImageReference imageRef = null;

            for (NodeAgentSku sku : skus) {
                if (sku.osType() == OSType.LINUX) {
                    for (ImageReference imgRef : sku.verifiedImageReferences()) {
                        if (imgRef.publisher().equalsIgnoreCase(osPublisher)
                                && imgRef.offer().equalsIgnoreCase(osOffer)) {
                            imageRef = imgRef;
                            skuId = sku.id();
                            break;
                        }
                    }
                }
            }

            // Use IaaS VM with Linux
            VirtualMachineConfiguration configuration = new VirtualMachineConfiguration();
            configuration.withNodeAgentSKUId(skuId).withImageReference(imageRef);

            client.poolOperations().createPool(poolId, poolVMSize, configuration, poolVMCount);
        }

        long startTime = System.currentTimeMillis();
        long elapsedTime = 0L;
        boolean steady = false;

        // Wait for the VM to be allocated
        while (elapsedTime < POOL_STEADY_TIMEOUT.toMillis()) {
            CloudPool pool = client.poolOperations().getPool(poolId);
            if (pool.allocationState() == AllocationState.STEADY) {
                steady = true;
                break;
            }
            System.out.println("wait 30 seconds for pool steady...");
            Thread.sleep(30 * 1000);
            elapsedTime = (new Date()).getTime() - startTime;
        }

        if (!steady) {
            throw new TimeoutException("The pool did not reach a steady state in the allotted time");
        }

        // The VMs in the pool don't need to be in and IDLE state in order to submit a
        // job.
        // The following code is just an example of how to poll for the VM state
        startTime = System.currentTimeMillis();
        elapsedTime = 0L;
        boolean hasIdleVM = false;

        // Wait for at least 1 VM to reach the IDLE state
        while (elapsedTime < VM_READY_TIMEOUT.toMillis()) {
            List<ComputeNode> nodeCollection = client.computeNodeOperations().listComputeNodes(poolId,
                    new DetailLevel.Builder().withSelectClause("id, state").withFilterClause("state eq 'idle'")
                            .build());
            if (!nodeCollection.isEmpty()) {
                hasIdleVM = true;
                break;
            }

            System.out.println("wait 30 seconds for VM start...");
            Thread.sleep(30 * 1000);
            elapsedTime = (new Date()).getTime() - startTime;
        }

        if (!hasIdleVM) {
            throw new TimeoutException("The node did not reach an IDLE state in the allotted time");
        }

        return client.poolOperations().getPool(poolId);
    }

    /**
     * Create blob container in order to upload file
     * 
     * @param storageAccountName
     *            storage account name
     * @param storageAccountKey
     *            storage account key
     * @param containerName
     *            container name
     * @return CloudBlobContainer instance
     * @throws URISyntaxException
     * @throws StorageException
     */
    private static CloudBlobContainer createBlobContainer(String storageAccountName, String storageAccountKey, String containerName)
            throws URISyntaxException, StorageException {

        // Create storage credential from name and key
        StorageCredentials credentials = new StorageCredentialsAccountAndKey(storageAccountName, storageAccountKey);

        // Create storage account. The 'true' sets the client to use HTTPS for
        // communication with the account
        CloudStorageAccount storageAccount = new CloudStorageAccount(credentials, true);

        // Create the blob client
        CloudBlobClient blobClient = storageAccount.createCloudBlobClient();

        // Get a reference to a container.
        // The container name must be lower case
        return blobClient.getContainerReference(containerName);
    }

    /**
     * @param sharedAccessBlobPermissions
     * @param expiry
     *          expiry in number of days
     * @return
     *          shared access blob policy with permission and expiry
     */
    private static SharedAccessBlobPolicy getSharedAccessBlobPolicy(SharedAccessBlobPermissions sharedAccessBlobPermissions, int expiry) {
        SharedAccessBlobPolicy policy = new SharedAccessBlobPolicy();
        EnumSet<SharedAccessBlobPermissions> perEnumSet = EnumSet.of(sharedAccessBlobPermissions);
        policy.setPermissions(perEnumSet);

        Calendar c = Calendar.getInstance();
        c.setTime(new Date());
        c.add(Calendar.DATE, expiry);
        policy.setSharedAccessExpiryTime(c.getTime());
        return policy;
    }

    /**
     * Upload file to blob container and return sas key
     * 
     * @param container
     *            blob container
     * @param fileName
     *            the file name of blob
     * @param filePath
     *            the local file path
     * @return SAS key for the uploaded file
     * @throws URISyntaxException
     * @throws IOException
     * @throws InvalidKeyException
     * @throws StorageException
     */
    private static String uploadFileToCloud(CloudBlobContainer container, String fileName, String filePath)
            throws URISyntaxException, IOException, InvalidKeyException, StorageException {
        // Create the container if it does not exist.
        container.createIfNotExists();

        // Upload file
        CloudBlockBlob blob = container.getBlockBlobReference(fileName);
        File source = new File(filePath);
        blob.upload(new FileInputStream(source), source.length());

        // Create policy with 1 day read permission
        SharedAccessBlobPolicy policy = getSharedAccessBlobPolicy(SharedAccessBlobPermissions.READ, 1);

        // Create SAS key
        String sas = blob.generateSharedAccessSignature(policy, null);
        return blob.getUri() + "?" + sas;
    }

    /**
     *
     * @param container
     *          container to upload the file
     * @param filePattern
     *          all the files matching the given pattern will be upload to container
     * @return  list of output files
     * @throws StorageException
     * @throws InvalidKeyException
     */
    private static List<OutputFile> getOutputFiles(CloudBlobContainer container, String filePattern) throws StorageException, InvalidKeyException {
        // Create policy with 1 day write permission
        SharedAccessBlobPolicy policy = getSharedAccessBlobPolicy(SharedAccessBlobPermissions.WRITE, 1);
        String sasToken = container.generateSharedAccessSignature(policy, null);
        String containerSasUrl = String.format("%s?%s", container.getUri().toString(), sasToken);
        OutputFileBlobContainerDestination outputFileBlobContainerDestination =
                new OutputFileBlobContainerDestination()
                        .withContainerUrl(containerSasUrl);

        List<OutputFile> outputFiles = new ArrayList<>();
        OutputFile outputFile = new OutputFile()
                .withFilePattern(filePattern)
                .withDestination(new OutputFileDestination()
                        .withContainer(outputFileBlobContainerDestination))
                .withUploadOptions(new OutputFileUploadOptions()
                        .withUploadCondition(OutputFileUploadCondition.TASK_COMPLETION));

        outputFiles.add(outputFile);
        return outputFiles;
    }

    /**
     * Create a job with a single task
     * 
     * @param client
     *            batch client instance
     * @param container
     *            blob container to upload the resource file
     * @param outputFileUploadContainer
     *            blob container to upload the output files generated during task execution
     * @param poolId
     *            pool id
     * @param jobId
     *            job id
     * @throws BatchErrorException
     * @throws IOException
     * @throws StorageException
     * @throws InvalidKeyException
     * @throws URISyntaxException
     */
    private static void submitJobAndAddTask(BatchClient client, CloudBlobContainer container, CloudBlobContainer outputFileUploadContainer, String poolId,
            String jobId)
            throws BatchErrorException, IOException, StorageException, InvalidKeyException, URISyntaxException {
        String BLOB_FILE_NAME = "test.txt";
        String LOCAL_FILE_PATH = "./" + BLOB_FILE_NAME;

        // Create job run at the specified pool
        PoolInformation poolInfo = new PoolInformation();
        poolInfo.withPoolId(poolId);
        client.jobOperations().createJob(jobId, poolInfo);

        // Create task
        TaskAddParameter taskToAdd = new TaskAddParameter();
        // Create files named test2.txt and test3.csv. later we can transfer these files from vm to storage container using a file pattern
        String command = String.format("cat %s && cp %s test2.txt && cp %s test3.csv", BLOB_FILE_NAME, BLOB_FILE_NAME, BLOB_FILE_NAME);
        taskToAdd.withId("mytask").withCommandLine(String.format("/bin/bash -c \"%s\"", command));

        String sas = uploadFileToCloud(container, BLOB_FILE_NAME, LOCAL_FILE_PATH);

        // Associate resource file with task
        ResourceFile file = new ResourceFile();
        file.withFilePath(BLOB_FILE_NAME).withHttpUrl(sas);
        List<ResourceFile> files = new ArrayList<ResourceFile>();
        files.add(file);
        taskToAdd.withResourceFiles(files);

        //
        taskToAdd.withOutputFiles(getOutputFiles(outputFileUploadContainer, "*.txt"));

        // Add task to job
        client.taskOperations().createTask(jobId, taskToAdd);
    }

    /**
     * Wait all tasks under a specified job to be completed
     * 
     * @param client
     *            batch client instance
     * @param jobId
     *            job id
     * @param expiryTime
     *            the waiting period
     * @return if task completed in time, return true, otherwise, return false
     * @throws BatchErrorException
     * @throws IOException
     * @throws InterruptedException
     */
    private static boolean waitForTasksToComplete(BatchClient client, String jobId, Duration expiryTime)
            throws BatchErrorException, IOException, InterruptedException {
        long startTime = System.currentTimeMillis();
        long elapsedTime = 0L;

        while (elapsedTime < expiryTime.toMillis()) {
            List<CloudTask> taskCollection = client.taskOperations().listTasks(jobId,
                    new DetailLevel.Builder().withSelectClause("id, state").build());

            boolean allComplete = true;
            for (CloudTask task : taskCollection) {
                if (task.state() != TaskState.COMPLETED) {
                    allComplete = false;
                    break;
                }
            }

            if (allComplete) {
                // All tasks completed
                return true;
            }

            System.out.println("wait 10 seconds for tasks to complete...");

            // Check again after 10 seconds
            Thread.sleep(10 * 1000);
            elapsedTime = (new Date()).getTime() - startTime;
        }

        // Timeout, return false
        return false;
    }

    /**
     * print BatchErrorException to console
     * 
     * @param err
     *            BatchErrorException instance
     */
    private static void printBatchException(BatchErrorException err) {
        System.out.println(String.format("BatchError %s", err.toString()));
        if (err.body() != null) {
            System.out.println(String.format("BatchError code = %s, message = %s", err.body().code(),
                    err.body().message().value()));
            if (err.body().values() != null) {
                for (BatchErrorDetail detail : err.body().values()) {
                    System.out.println(String.format("Detail %s=%s", detail.key(), detail.value()));
                }
            }
        }
    }

    public static void main(String argv[]) throws Exception {
        // Get batch and storage account information from environment
        String batchAccount = System.getenv("AZURE_BATCH_ACCOUNT");
        String batchKey = System.getenv("AZURE_BATCH_ACCESS_KEY");
        String batchUri = System.getenv("AZURE_BATCH_ENDPOINT");

        String storageAccountName = System.getenv("STORAGE_ACCOUNT_NAME");
        String storageAccountKey = System.getenv("STORAGE_ACCOUNT_KEY");

        Boolean shouldDeleteContainer = true;
        Boolean shouldDeleteJob = true;
        Boolean shouldDeletePool = false;

        Duration TASK_COMPLETE_TIMEOUT = Duration.ofMinutes(1);
        String STANDARD_CONSOLE_OUTPUT_FILENAME = "stdout.txt";

        // Create batch client
        BatchSharedKeyCredentials cred = new BatchSharedKeyCredentials(batchUri, batchAccount, batchKey);
        BatchClient client = BatchClient.open(cred);

        // Create storage container
        CloudBlobContainer container = createBlobContainer(storageAccountName, storageAccountKey, "poolsandresourcefiles");
        CloudBlobContainer taskOutputFileUploadContainer = createBlobContainer(storageAccountName, storageAccountKey, "taskoutputfiles");
        taskOutputFileUploadContainer.createIfNotExists();

        String userName = System.getProperty("user.name");
        String poolId = userName + "-pooltest";
        String jobId = "HelloWorldJob-" + userName + "-"
                + (new Date()).toString().replace(' ', '-').replace(':', '-').replace('.', '-');

        try {
            CloudPool sharedPool = createPoolIfNotExists(client, poolId);
            submitJobAndAddTask(client, container, taskOutputFileUploadContainer, sharedPool.id(), jobId);
            if (waitForTasksToComplete(client, jobId, TASK_COMPLETE_TIMEOUT)) {
                // Check of the task output files uploaded to container
                for (ListBlobItem listBlobItem : taskOutputFileUploadContainer.listBlobs()) {
                    System.out.println("Uri of uploaded files after task completion: " + listBlobItem.getUri().toString());
                }
                // Get the task command output file
                CloudTask task = client.taskOperations().getTask(jobId, "mytask");

                ByteArrayOutputStream stream = new ByteArrayOutputStream();
                client.fileOperations().getFileFromTask(jobId, task.id(), STANDARD_CONSOLE_OUTPUT_FILENAME, stream);
                String fileContent = stream.toString("UTF-8");
                System.out.println(fileContent);
            } else {
                throw new TimeoutException("Task did not complete within the specified timeout");
            }
        } catch (BatchErrorException err) {
            printBatchException(err);
        } catch (Exception ex) {
            ex.printStackTrace();
        } finally {
            // Clean up the resource if necessary
            if (shouldDeleteJob) {
                try {
                    client.jobOperations().deleteJob(jobId);
                } catch (BatchErrorException err) {
                    printBatchException(err);
                }
            }

            if (shouldDeletePool) {
                try {
                    client.jobOperations().deleteJob(poolId);
                } catch (BatchErrorException err) {
                    printBatchException(err);
                }
            }

            if (shouldDeleteContainer) {
                container.deleteIfExists();
                taskOutputFileUploadContainer.deleteIfExists();
            }
        }
    }
}
