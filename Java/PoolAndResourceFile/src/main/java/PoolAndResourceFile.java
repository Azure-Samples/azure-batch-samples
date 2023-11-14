import com.azure.compute.batch.BatchClient;
import com.azure.compute.batch.BatchClientBuilder;
import com.azure.compute.batch.models.*;
import com.azure.core.credential.AzureNamedKeyCredential;
import com.azure.core.exception.HttpResponseException;
import com.azure.core.http.rest.PagedIterable;
import com.azure.storage.blob.BlobContainerClient;
import com.azure.storage.blob.BlobServiceClient;
import com.azure.storage.blob.BlobServiceClientBuilder;
import com.azure.storage.blob.sas.BlobSasPermission;
import com.azure.storage.blob.sas.BlobServiceSasSignatureValues;
import com.azure.storage.blob.specialized.BlockBlobClient;
import com.azure.storage.common.StorageSharedKeyCredential;

import java.io.File;
import java.io.IOException;
import java.nio.file.Files;
import java.time.Duration;
import java.time.OffsetDateTime;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.Date;
import java.util.List;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.TimeoutException;

public class PoolAndResourceFile {

    // Get Batch and storage account information from environment
    static String BATCH_ACCOUNT = System.getenv("AZURE_BATCH_ACCOUNT");
    static String BATCH_ACCESS_KEY = System.getenv("AZURE_BATCH_ACCESS_KEY");
    static String BATCH_URI = System.getenv("AZURE_BATCH_ENDPOINT");
    static String STORAGE_ACCOUNT_NAME = System.getenv("STORAGE_ACCOUNT_NAME");
    static String STORAGE_ACCOUNT_KEY = System.getenv("STORAGE_ACCOUNT_KEY");
    static String STORAGE_CONTAINER_NAME = "poolandresourcefile";

    // How many tasks to run across how many nodes
    static int TASK_COUNT = 5;
    static int NODE_COUNT = 1;

    // Modify these values to change which resources are deleted after the job finishes.
    // Skipping pool deletion will greatly speed up subsequent runs
    static boolean CLEANUP_STORAGE_CONTAINER = true;
    static boolean CLEANUP_JOB = true;
    static boolean CLEANUP_POOL = true;

    public static void main(String[] argv) {
        BatchClient client = new BatchClientBuilder()
                .endpoint(BATCH_URI)
                .credential(new AzureNamedKeyCredential(BATCH_ACCOUNT, BATCH_ACCESS_KEY))
                .buildClient();

        BlobContainerClient containerClient = createBlobContainerIfNotExists(
                STORAGE_ACCOUNT_NAME, STORAGE_ACCOUNT_KEY, STORAGE_CONTAINER_NAME);

        String userName = System.getProperty("user.name");
        String poolId = userName + "-pooltest";
        String jobId = "PoolAndResourceFileJob-" + userName + "-" +
                new Date().toString().replaceAll("(\\.|:|\\s)", "-");

        try {
            BatchPool sharedPool = createPoolIfNotExists(client, poolId);

            // Submit a job and wait for completion
            submitJob(client, containerClient, sharedPool.getId(), jobId, TASK_COUNT);
            waitForTasksToComplete(client, jobId, Duration.ofMinutes(5));

            System.out.println("\nTask Results");
            System.out.println("------------------------------------------------------");

            PagedIterable<BatchTask> tasks = client.listTasks(jobId);
            for (BatchTask task : tasks) {
                BatchTaskExecutionInfo execution = task.getExecutionInfo();

                if (execution.getFailureInfo() != null) {
                    System.out.println("Task " + task.getId() + " failed: " + execution.getFailureInfo().getMessage());
                }

                String outputFileName = execution.getExitCode() == 0 ? "stdout.txt" : "stderr.txt";
                String fileContent = client.getTaskFile(jobId, task.getId(), outputFileName).toString();

                System.out.println("\nTask " + task.getId() + " output (" + outputFileName + "):");
                System.out.println(fileContent);
            }

            System.out.println("------------------------------------------------------\n");
            // TODO: How do we replace BatchErrorException?
            // } catch (BatchErrorException err) {
            //     printBatchException(err);
        } catch (Exception ex) {
            ex.printStackTrace();
        } finally {
            // Clean up resources
            if (CLEANUP_JOB) {
                try {
                    System.out.println("Deleting job " + jobId);
                    client.deleteJob(jobId);
                } catch (HttpResponseException err) {
                    printBatchException(err);
                }
            }
            if (CLEANUP_POOL) {
                try {
                    System.out.println("Deleting pool " + poolId);
                    client.deletePool(poolId);
                } catch (HttpResponseException err) {
                    printBatchException(err);
                }
            }
            if (CLEANUP_STORAGE_CONTAINER) {
                System.out.println("Deleting storage container " + containerClient.getBlobContainerName());
                containerClient.deleteIfExists();
            }
        }

        System.out.println("\nFinished");
        System.exit(0);
    }

    /**
     * Create a pool if one doesn't already exist with the given ID
     *
     * @param client The Batch client
     * @param poolId The ID of the pool to create or look up
     * @return A newly created or existing pool
     */
    private static BatchPool createPoolIfNotExists(BatchClient client, String poolId)
            throws InterruptedException, TimeoutException {
        // Create a pool with a single node
        String osPublisher = "canonical";
        String osOffer = "0001-com-ubuntu-server-jammy";
        String vmSize = "Standard_A1_v2";
        int targetNodeCount = 1;
        Duration poolSteadyTimeout = Duration.ofMinutes(5);
        Duration nodeReadyTimeout = Duration.ofMinutes(20);

        // If the pool exists and is active (not being deleted), resize it
        if (client.poolExists(poolId) && client.getPool(poolId).getState().equals(BatchPoolState.ACTIVE)) {
            System.out.println("Pool " + poolId + " already exists: Resizing to " + targetNodeCount + " dedicated node(s)");
            client.resizePool(poolId, new BatchPoolResizeParameters().setTargetDedicatedNodes(NODE_COUNT));
        } else {
            System.out.println("Creating pool " + poolId + " with " + targetNodeCount + " dedicated node(s)");

            String nodeAgentSku = null;
            ImageReference image = null;
            for (ImageInfo sku : client.listSupportedImages()) {
                image = sku.getImageReference();
                nodeAgentSku = sku.getNodeAgentSkuId();
                if (sku.getOsType() == OSType.LINUX
                        && sku.getVerificationType().equals(ImageVerificationType.VERIFIED)
                        && image.getPublisher().equalsIgnoreCase(osPublisher)
                        && image.getOffer().equalsIgnoreCase(osOffer)) {
                    // Found a matching verified image
                    break;
                }
            }

            if (nodeAgentSku == null || image == null) {
                throw new IllegalArgumentException(
                        String.format("Unable to find a verified image with publisher '%s' and offer '%s'", osPublisher, osOffer));
            }

            client.createPool(new BatchPoolCreateParameters(poolId, vmSize)
                    .setVirtualMachineConfiguration(new VirtualMachineConfiguration(image, nodeAgentSku))
                    .setTargetDedicatedNodes(targetNodeCount));
        }

        long startTime = System.currentTimeMillis();
        long elapsedTime = 0L;
        boolean steady = false;

        // Wait for the VM to be allocated
        System.out.print("Waiting for pool to resize.");
        while (elapsedTime < poolSteadyTimeout.toMillis()) {
            BatchPool pool = client.getPool(poolId);
            if (pool.getAllocationState() == AllocationState.STEADY) {
                steady = true;
                break;
            }
            System.out.print(".");
            TimeUnit.SECONDS.sleep(10);
            elapsedTime = (new Date()).getTime() - startTime;
        }
        System.out.println();

        if (!steady) {
            throw new TimeoutException("The pool did not reach a steady state in the allotted time");
        }

        // The VMs in the pool don't need to be in and IDLE state in order to submit a
        // job.
        // The following code is just an example of how to poll for the VM state
        startTime = System.currentTimeMillis();
        elapsedTime = 0L;
        boolean hasIdleNode = false;

        // Wait for at least 1 node to reach the idle state
        System.out.print("Waiting for nodes to start.");
        while (elapsedTime < nodeReadyTimeout.toMillis()) {
            PagedIterable<BatchNode> nodes = client.listNodes(poolId, new ListBatchNodesOptions()
                    .setSelect(Arrays.asList("id", "state"))
                    .setFilter("state eq 'idle'")
                    .setMaxresults(1));

            for (BatchNode node : nodes) {
                if (node != null) {
                    hasIdleNode = true;
                    break;
                }
            }

            System.out.print(".");
            TimeUnit.SECONDS.sleep(10);
            elapsedTime = (new Date()).getTime() - startTime;
        }
        System.out.println();

        if (!hasIdleNode) {
            throw new TimeoutException("The node did not reach an IDLE state in the allotted time");
        }

        return client.getPool(poolId);
    }

    /**
     * Create blob container in order to upload file
     *
     * @param storageAccountName The name of the storage account to create or look up
     * @param storageAccountKey  An SAS key for accessing the storage account
     * @return A newly created or existing storage container
     */
    private static BlobContainerClient createBlobContainerIfNotExists(String storageAccountName, String storageAccountKey, String containerName) {
        System.out.println("Creating storage container " + containerName);

        BlobServiceClient blobClient = new BlobServiceClientBuilder()
                .endpoint(String.format("https://%s.blob.core.windows.net/", storageAccountName))
                .credential(new StorageSharedKeyCredential(storageAccountName, storageAccountKey))
                .buildClient();

        blobClient.createBlobContainerIfNotExists(containerName);

        return blobClient.getBlobContainerClient(containerName);
    }

    /**
     * Upload a file to a blob container and return an SAS key
     *
     * @param containerClient The blob container client to use
     * @param source          The local file to upload
     * @return An SAS key for the uploaded file
     */
    private static String uploadFileToStorage(BlobContainerClient containerClient, File source) throws IOException {
        BlockBlobClient blobClient = containerClient.getBlobClient(source.getName()).getBlockBlobClient();
        blobClient.upload(Files.newInputStream(source.toPath()), source.length());

        // Create SAS with expiry time of 1 day
        String sas = blobClient.generateSas(new BlobServiceSasSignatureValues(
                OffsetDateTime.now().plusDays(1),
                new BlobSasPermission().setReadPermission(true)
        ));

        return blobClient.getBlobUrl() + "?" + sas;
    }

    /**
     * Create a job and add some tasks
     *
     * @param client          The Batch client
     * @param containerClient A blob container to upload resource files
     * @param poolId          The ID of the pool to submit a job
     * @param jobId           A unique ID for the new job
     * @param taskCount       How many tasks to add
     */
    private static void submitJob(BatchClient client, BlobContainerClient containerClient, String poolId,
                                  String jobId, int taskCount) throws IOException, InterruptedException {
        System.out.println("Submitting job " + jobId + " with " + taskCount + " tasks");

        // Create job
        BatchPoolInfo poolInfo = new BatchPoolInfo();
        poolInfo.setPoolId(poolId);
        client.createJob(new BatchJobCreateParameters(jobId, poolInfo));

        // Upload a resource file and make it available in a "resources" subdirectory on nodes
        String fileName = "test.txt";
        String localPath = "./" + fileName;
        String remotePath = "resources/" + fileName;
        String signedUrl = uploadFileToStorage(containerClient, new File(localPath));
        List<ResourceFile> files = new ArrayList<>();
        files.add(new ResourceFile()
                .setHttpUrl(signedUrl)
                .setFilePath(remotePath));

        // Create tasks
        List<BatchTaskCreateParameters> tasks = new ArrayList<>();
        for (int i = 0; i < taskCount; i++) {
            tasks.add(new BatchTaskCreateParameters("mytask" + i, "cat " + remotePath)
                    .setResourceFiles(files));
        }

        // Add the tasks to the job
        client.createTasks(jobId, tasks);
    }

    /**
     * Wait for all tasks in a given job to be completed, or throw an exception on timeout
     *
     * @param client  The Batch client
     * @param jobId   The ID of the job to poll for completion.
     * @param timeout How long to wait for the job to complete before giving up
     */
    private static void waitForTasksToComplete(BatchClient client, String jobId, Duration timeout)
            throws InterruptedException, TimeoutException {
        long startTime = System.currentTimeMillis();
        long elapsedTime = 0L;

        System.out.print("Waiting for tasks to complete (Timeout: " + timeout.getSeconds() / 60 + "m)");

        while (elapsedTime < timeout.toMillis()) {
            PagedIterable<BatchTask> taskCollection = client.listTasks(jobId,
                    new ListBatchTasksOptions().setSelect(Arrays.asList("id", "state")));

            boolean allComplete = true;
            for (BatchTask task : taskCollection) {
                if (task.getState() != BatchTaskState.COMPLETED) {
                    allComplete = false;
                    break;
                }
            }

            if (allComplete) {
                System.out.println("\nAll tasks completed");
                // All tasks completed
                return;
            }

            System.out.print(".");

            TimeUnit.SECONDS.sleep(10);
            elapsedTime = (new Date()).getTime() - startTime;
        }

        System.out.println();

        throw new TimeoutException("Task did not complete within the specified timeout");
    }

    private static void printBatchException(HttpResponseException err) {
        // TODO: How do we get error details?
        System.out.printf("HTTP Response error %s%n", err.toString());
//         if (err.body() != null) {
//             System.out.printf("BatchError code = %s, message = %s%n", err.body().code(),
//                     err.body().message().value());
//             if (err.body().values() != null) {
//                 for (BatchErrorDetail detail : err.body().values()) {
//                     System.out.printf("Detail %s=%s%n", detail.key(), detail.value());
//                 }
//             }
//         }
    }

}
