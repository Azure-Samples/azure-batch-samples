import java.io.*;
import java.net.URISyntaxException;
import java.security.InvalidKeyException;
import java.util.*;
import java.util.concurrent.TimeoutException;

import com.microsoft.azure.storage.*;
import com.microsoft.azure.storage.blob.*;
import org.apache.commons.io.IOUtils;

import com.microsoft.azure.batch.*;
import com.microsoft.azure.batch.auth.BatchSharedKeyCredentials;
import com.microsoft.azure.batch.protocol.models.*;

public class PoolAndResourceFile {

    // Create IaaS pool if pool isn't exist
    private static CloudPool CreatePool(BatchClient client, String poolId) throws BatchErrorException, IllegalArgumentException, IOException, InterruptedException, TimeoutException {
        // Pool only have 3 D1 VM
        String osPublisher = "OpenLogic";
        String osOffer = "CentOS";
        String poolVMSize = "STANDARD_D1";
        int poolVMCount = 1;
        long POOL_STEADY_TIMEOUT = 5 * 60 * 1000;
        long VM_READY_TIMEOUT = 20 * 60 * 1000;

        // Check pool existing
        if (!client.getPoolOperations().existsPool(poolId)) {

            // Get the sku image reference
            List<NodeAgentSku> skus = client.getAccountOperations().listNodeAgentSkus();
            String skuId = null;
            ImageReference imageRef = null;

            for (NodeAgentSku sku : skus) {
                if (sku.getOsType() == OSType.LINUX) {
                    for (ImageReference imgRef : sku.getVerifiedImageReferences()) {
                        if (imgRef.getPublisher().equalsIgnoreCase(osPublisher) && imgRef.getOffer().equalsIgnoreCase(osOffer)) {
                            imageRef = imgRef;
                            skuId = sku.getId();
                            break;
                        }
                    }
                }
            }

            // Use IaaS VM with Linux
            VirtualMachineConfiguration configuration = new VirtualMachineConfiguration();
            configuration.setNodeAgentSKUId(skuId);
            configuration.setImageReference(imageRef);

            client.getPoolOperations().createPool(poolId, poolVMSize, configuration, poolVMCount);
        }

        long startTime = System.currentTimeMillis();
        long elapsedTime = 0L;
        boolean steady = false;

        // Let's wait max 5 minute for VM to be allocated
        while (elapsedTime < POOL_STEADY_TIMEOUT) {
            CloudPool pool = client.getPoolOperations().getPool(poolId);
            if (pool.getAllocationState() == AllocationState.STEADY) {
                steady = true;
                break;
            }
            System.out.println("wait 30 seconds for pool steady...");
            Thread.sleep(30 * 1000);
            elapsedTime = (new Date()).getTime() - startTime;
        }

        if (!steady) {
            throw new TimeoutException("Pool wasn't steady at time");
        }

        // VMs of the pool don't need to be IDLE state in order to submit job
        // The following code just an example to check VM state
        startTime = System.currentTimeMillis();
        elapsedTime = 0L;
        boolean hasIdleVM = false;

        // Let's wait max 20 minutes for at least 1 VM to start up
        while (elapsedTime < VM_READY_TIMEOUT) {


            List<ComputeNode> nodeCollection = client.getComputeNodeOperations().listComputeNodes(poolId);
            for (ComputeNode node : nodeCollection) {
                if (node.getState() == ComputeNodeState.IDLE) {
                    hasIdleVM = true;
                    break;
                }
            }

            if (hasIdleVM) {
                break;
            }

            System.out.println("wait 30 seconds for vm start...");
            Thread.sleep(30 * 1000);
            elapsedTime = (new Date()).getTime() - startTime;
        }

        if (!hasIdleVM) {
            throw new TimeoutException("Vm wasn't ready at time");
        }

        return client.getPoolOperations().getPool(poolId);
    }

    // Create blob container in order to upload file
    private static CloudBlobContainer createBlobContainer(String storageAccountName, String storageAccountKey) throws URISyntaxException, StorageException {
        String CONTAINER_NAME = "poolsandresourcefiles";

        // Create storage credential from name and key
        StorageCredentials credentials = new StorageCredentialsAccountAndKey(storageAccountName, storageAccountKey);

        // Create storage account
        CloudStorageAccount storageAccount = new CloudStorageAccount(credentials);

        // Create the blob client
        CloudBlobClient blobClient =  storageAccount.createCloudBlobClient();

        // Get a reference to a container.
        // The container name must be lower case
        return blobClient.getContainerReference(CONTAINER_NAME);
    }

    // Upload file to blob container and return sas key
    private static String uploadFileToCloud(CloudBlobContainer container) throws URISyntaxException, IOException, InvalidKeyException, StorageException {
        String FILE_NAME = "test.txt";
        String FILE_PATH = "./" + FILE_NAME;

        // Create the container if it does not exist.
        container.createIfNotExists();

        // Upload file
        CloudBlockBlob blob = container.getBlockBlobReference(FILE_NAME);
        File source = new File(FILE_PATH);
        blob.upload(new FileInputStream(source), source.length());

        // Create policy with 1 day read permission
        SharedAccessBlobPolicy policy = new SharedAccessBlobPolicy();
        EnumSet<SharedAccessBlobPermissions> perEnumSet = EnumSet.of(SharedAccessBlobPermissions.READ);
        policy.setPermissions(perEnumSet);

        Calendar c = Calendar.getInstance();
        c.setTime(new Date());
        c.add(Calendar.DATE, 1);
        policy.setSharedAccessExpiryTime(c.getTime());

        // Create SAS key
        String sas = blob.generateSharedAccessSignature(policy, null);
        return blob.getUri() + "?" + sas;
    }

    // Create a job with a single task
    private static void submitJobAndAddTask(BatchClient client, CloudBlobContainer container, String poolId, String jobId) throws BatchErrorException, IOException, StorageException, InvalidKeyException, URISyntaxException {
        String FILE_NAME = "mytest.txt";

        // Create job run at the specified pool
        PoolInformation poolInfo = new PoolInformation();
        poolInfo.setPoolId(poolId);
        client.getJobOperations().createJob(jobId, poolInfo);

        // Create task
        TaskAddParameter taskToAdd = new TaskAddParameter();
        taskToAdd.setId("mytask");
        taskToAdd.setCommandLine(String.format("cat %s", FILE_NAME));

        String sas = uploadFileToCloud(container);

        // Associate resource file with task
        ResourceFile file = new ResourceFile();
        file.setFilePath(FILE_NAME);
        file.setBlobSource(sas);
        List<ResourceFile> files = new ArrayList<ResourceFile>();
        files.add(file);
        taskToAdd.setResourceFiles(files);

        // Add task to job
        client.getTaskOperations().createTask(jobId, taskToAdd);
    }

    // Wait all tasks under a specified job to be completed
    private static boolean waitForTasksToComplete(BatchClient client, String jobId, long expiryTime) throws BatchErrorException, IOException, InterruptedException {
        long startTime = System.currentTimeMillis();
        long elapsedTime = 0L;

        while (elapsedTime < expiryTime) {
            List<CloudTask> taskCollection = client.getTaskOperations().listTasks(jobId, new DetailLevel.Builder().selectClause("id, state").build());

            boolean allComplete = true;
            for (CloudTask task : taskCollection) {
                if (task.getState() != TaskState.COMPLETED) {
                    allComplete = false;
                    break;
                }
            }

            if (allComplete) {
                // All tasks completed
                return true;
            }

            System.out.println("wait 10 seconds for tasks complete...");

            // Check again after 10 seconds
            Thread.sleep(10 * 1000);
            elapsedTime = (new Date()).getTime() - startTime;
        }

        // Timeout, return false
        return false;
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

        long TASK_COMPLETE_TIMEOUT = 10 * 60 * 1000;
        String STANDARD_CONSOLE_OUTPUT_FILENAME = "stdout.txt";

        // Create batch client
        BatchSharedKeyCredentials cred = new BatchSharedKeyCredentials(batchUri, batchAccount, batchKey);
        BatchClient client = BatchClient.Open(cred);

        // Create storage container
        CloudBlobContainer container = createBlobContainer(storageAccountName, storageAccountKey);

        String userName = System.getProperty("user.name");
        String poolId = userName + "-pooltest";
        String jobId = "HelloWorldJob-" + userName + "-" + (new Date()).toString().replace(' ', '-').replace(':', '-').replace('.', '-');

        try
        {
            CloudPool sharedPool = CreatePool(client, poolId);
            submitJobAndAddTask(client, container, sharedPool.getId(), jobId);
            if (waitForTasksToComplete(client, jobId, TASK_COMPLETE_TIMEOUT)) {
                // Get the task command output file
                CloudTask task = client.getTaskOperations().getTask(jobId, "mytask");

                InputStream stream = client.getFileOperations().getFileFromTask(jobId, task.getId(), STANDARD_CONSOLE_OUTPUT_FILENAME);
                String fileContent = IOUtils.toString(stream, "UTF-8");
                System.out.println(fileContent);
            }
            else {
                throw new TimeoutException("Task wasn't completed at time");
            }
        }
        catch (BatchErrorException err) {
            System.out.println(String.format("BatchError %s", err.toString()));
            if (err.getBody() != null) {
                System.out.println(String.format("BatchError code = %s, message = %s", err.getBody().getCode(), err.getBody().getMessage().getValue()));
                if (err.getBody().getValues() != null) {
                    for (BatchErrorDetail detail : err.getBody().getValues()) {
                        System.out.println(String.format("Detail %s=%s", detail.getKey(), detail.getValue()));
                    }
                }
            }
            throw err;
        }
        catch (Exception ex) {
            System.out.println(ex);
        }
        finally {
            // Clean up the resource if necessary
            if (shouldDeleteJob) {
                try {
                    client.getJobOperations().deleteJob(jobId);
                } catch (BatchErrorException err) {
                    System.out.println(String.format("BatchError %s", err.getMessage()));
                }
            }

            if (shouldDeletePool) {
                try {
                    client.getJobOperations().deleteJob(poolId);
                } catch (BatchErrorException err) {
                    System.out.println(String.format("BatchError %s", err.getMessage()));
                }
            }

            if (shouldDeleteContainer) {
                container.deleteIfExists();
            }
        }
    }
}
