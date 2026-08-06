import com.azure.compute.batch.BatchClient;
import com.azure.compute.batch.BatchClientBuilder;
import com.azure.compute.batch.models.ResourceFile;
import com.azure.compute.batch.models.*;
import com.azure.core.credential.TokenCredential;
import com.azure.core.http.rest.PagedIterable;
import com.azure.core.management.AzureEnvironment;
import com.azure.core.management.exception.ManagementException;
import com.azure.core.management.profile.AzureProfile;
import com.azure.core.util.Configuration;
import com.azure.identity.DefaultAzureCredentialBuilder;
import com.azure.resourcemanager.batch.BatchManager;
import com.azure.resourcemanager.batch.models.AllocationState;
import com.azure.resourcemanager.batch.models.VirtualMachineConfiguration;
import com.azure.resourcemanager.batch.models.*;
import com.azure.storage.blob.BlobClient;
import com.azure.storage.blob.BlobContainerClient;
import com.azure.storage.blob.BlobServiceClient;
import com.azure.storage.blob.BlobServiceClientBuilder;
import com.azure.storage.blob.sas.BlobSasPermission;
import com.azure.storage.blob.sas.BlobServiceSasSignatureValues;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import java.io.File;
import java.time.Duration;
import java.time.OffsetDateTime;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.Date;
import java.util.List;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.TimeoutException;

public class PoolAndResourceFile {

    // Get Batch and Storage account information from environment
    static final String AZURE_RESOURCE_GROUP_NAME = Configuration.getGlobalConfiguration().get("AZURE_RESOURCE_GROUP_NAME");
    static final String AZURE_BATCH_ACCOUNT_NAME = Configuration.getGlobalConfiguration().get("AZURE_BATCH_ACCOUNT_NAME");
    static final String AZURE_BLOB_SERVICE_URL = Configuration.getGlobalConfiguration().get("AZURE_BLOB_SERVICE_URL");

    // How many tasks to run across how many nodes
    static final int TASK_COUNT = 5;
    static final int NODE_COUNT = 1;

    // Modify these values to change which resources are deleted after the job finishes.
    // Skipping pool deletion will greatly speed up subsequent runs
    static final boolean CLEANUP_STORAGE_CONTAINER = true;
    static final boolean CLEANUP_JOB = true;
    static final boolean CLEANUP_POOL = true;

    final Logger logger = LoggerFactory.getLogger(PoolAndResourceFile.class);
    final BatchManager batchManager;
    final BatchClient batchClient;
    final BlobServiceClient blobServiceClient;
    final BlobContainerClient blobContainerClient;

    public static void main(String[] argv) {
        new PoolAndResourceFile().runSample();
        System.exit(0);
    }

    public PoolAndResourceFile() {
        AzureProfile profile = new AzureProfile(AzureEnvironment.AZURE);
        TokenCredential credential = new DefaultAzureCredentialBuilder()
                .authorityHost(profile.getEnvironment().getActiveDirectoryEndpoint())
                .build();

        batchManager = BatchManager
                .authenticate(credential, profile);

        BatchAccount batchAccount = batchManager.batchAccounts()
                .getByResourceGroup(AZURE_RESOURCE_GROUP_NAME, AZURE_BATCH_ACCOUNT_NAME);

        batchClient = new BatchClientBuilder()
                .endpoint(String.format("https://%s", batchAccount.accountEndpoint()))
                .credential(credential)
                .buildClient();

        blobServiceClient = new BlobServiceClientBuilder()
                .endpoint(AZURE_BLOB_SERVICE_URL)
                .credential(credential)
                .buildClient();

        blobContainerClient = blobServiceClient.getBlobContainerClient("poolandresourcefile");
    }

    /**
     * Runs a job which prints out the contents of a file in Azure Storage.
     * Will create a pool if one doesn't already exist. If a pool exists it will be resized.
     */
    public void runSample() {
        logger.info("Creating storage container {} if it does not exist", blobContainerClient.getBlobContainerName());
        blobContainerClient.createIfNotExists();

        String userName = System.getProperty("user.name");
        String poolName = userName + "-pooltest";
        String jobId = "PoolAndResourceFileJob-" + userName + "-" +
                new Date().toString().replaceAll("(\\.|:|\\s)", "-");

        try {
            Pool pool = createPoolIfNotExists(poolName);

            // Submit a job and wait for completion
            submitJob(pool.name(), jobId);
            waitForTasksToComplete(jobId, Duration.ofMinutes(5));

            PagedIterable<BatchTask> tasks = batchClient.listTasks(jobId);
            for (BatchTask task : tasks) {
                BatchTaskExecutionInfo execution = task.getExecutionInfo();

                if (execution.getFailureInfo() != null) {
                    logger.error("Task {} failed: {}", task.getId(), execution.getFailureInfo().getMessage());
                }

                String outputFileName = execution.getExitCode() == 0 ? "stdout.txt" : "stderr.txt";
                String fileContent = batchClient.getTaskFile(jobId, task.getId(), outputFileName).toString();

                logger.info("Task {} output ({}):\n{}", task.getId(), outputFileName, fileContent);
            }
        } catch (BatchErrorException e) {
            logBatchException(e);
        } catch (Exception e) {
            logger.error("Unexpected error", e);
        } finally {
            // Clean up resources
            if (CLEANUP_JOB) {
                try {
                    logger.info("Deleting job {}", jobId);
                    batchClient.beginDeleteJob(jobId);
                } catch (BatchErrorException e) {
                    logBatchException(e);
                }
            }
            if (CLEANUP_POOL) {
                try {
                    logger.info("Deleting pool {}", poolName);
                    batchClient.beginDeletePool(poolName);
                } catch (BatchErrorException e) {
                    logBatchException(e);
                }
            }
            if (CLEANUP_STORAGE_CONTAINER) {
                logger.info("Deleting storage container {}", blobContainerClient.getBlobContainerName());
                blobContainerClient.deleteIfExists();
            }
        }

        logger.info("Finished");
    }

    /**
     * Create a pool if one doesn't already exist and waits until it reaches the steady state.
     *
     * @param poolName The ID of the pool to create or look up
     * @return A newly created or existing pool
     */
    protected Pool createPoolIfNotExists(String poolName) throws InterruptedException, TimeoutException {
        // Create a pool with a single node
        Duration poolSteadyTimeout = Duration.ofMinutes(5);
        Duration nodeReadyTimeout = Duration.ofMinutes(20);

        Pool pool = null;
        try {
            pool = batchManager.pools().get(AZURE_RESOURCE_GROUP_NAME, AZURE_BATCH_ACCOUNT_NAME, poolName);
        } catch (ManagementException e) {
            if (e.getResponse().getStatusCode() != 404) {
                throw new RuntimeException(e);
            }
        }

        if (pool != null && pool.provisioningState().equals(PoolProvisioningState.SUCCEEDED)) {
            logger.info("Pool {} already exists: Resizing to {} dedicated node(s)", poolName, NODE_COUNT);
            pool.update()
                    .withScaleSettings(new ScaleSettings().withFixedScale(
                            new FixedScaleSettings().withTargetDedicatedNodes(NODE_COUNT)))
                    .apply();
        } else {
            logger.info("Creating pool {} with {} dedicated node(s)", poolName, NODE_COUNT);

            pool = batchManager.pools()
                    .define(poolName)
                    .withExistingBatchAccount(AZURE_RESOURCE_GROUP_NAME, AZURE_BATCH_ACCOUNT_NAME)
                    .withVmSize("Standard_DS1_v2")
                    .withDeploymentConfiguration(new DeploymentConfiguration().withVirtualMachineConfiguration(
                            new VirtualMachineConfiguration()
                                    .withImageReference(
                                            new ImageReference()
                                                    .withPublisher("canonical")
                                                    .withOffer("0001-com-ubuntu-server-jammy")
                                                    .withSku("22_04-lts")
                                                    .withVersion("latest"))
                                    .withNodeAgentSkuId("batch.node.ubuntu 22.04")))
                    .withScaleSettings(new ScaleSettings().withFixedScale(new FixedScaleSettings()
                            .withTargetDedicatedNodes(NODE_COUNT)))
                    .create();
        }

        long startTime = System.currentTimeMillis();
        long elapsedTime = 0L;
        boolean steady = false;

        // Wait for the VM to be allocated
        logger.info("Waiting for pool to resize.");
        while (elapsedTime < poolSteadyTimeout.toMillis()) {
            pool.refresh();
            if (pool.allocationState().equals(AllocationState.STEADY)) {
                steady = true;
                break;
            }
            TimeUnit.SECONDS.sleep(10);
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
        boolean hasIdleNode = false;

        // Wait for at least 1 node to reach the idle state
        logger.info("Waiting for nodes to start.");
        while (elapsedTime < nodeReadyTimeout.toMillis()) {
            PagedIterable<BatchNode> nodes = batchClient.listNodes(poolName, new BatchNodesListOptions()
                    .setSelect(Arrays.asList("id", "state"))
                    .setFilter("state eq 'idle'"));
            if (nodes.stream().findAny().isPresent()) {
                hasIdleNode = true;
                break;
            }
            TimeUnit.SECONDS.sleep(10);
            elapsedTime = (new Date()).getTime() - startTime;
        }

        if (!hasIdleNode) {
            throw new TimeoutException("The node did not reach an IDLE state in the allotted time");
        }

        return pool.refresh();
    }

    /**
     * Create a job and add some tasks
     *
     * @param poolId            The ID of the pool to submit a job
     * @param jobId             A unique ID for the new job
     */
    protected void submitJob(String poolId, String jobId) {
        logger.info("Submitting job {} with {} tasks", jobId, TASK_COUNT);

        // Create job
        BatchPoolInfo poolInfo = new BatchPoolInfo();
        poolInfo.setPoolId(poolId);
        batchClient.createJob(new BatchJobCreateParameters(jobId, poolInfo));

        // Upload a resource file and make it available in a "resources" subdirectory on nodes
        String fileName = "test.txt";
        String localPath = "./" + fileName;
        String remotePath = "resources/" + fileName;
        String signedUrl = uploadFileToStorage(new File(localPath));
        List<ResourceFile> files = new ArrayList<>();
        files.add(new ResourceFile()
                .setHttpUrl(signedUrl)
                .setFilePath(remotePath));

        // Create tasks
        List<BatchTaskCreateParameters> tasks = new ArrayList<>();
        for (int i = 0; i < TASK_COUNT; i++) {
            tasks.add(new BatchTaskCreateParameters("mytask" + i, "cat " + remotePath).setResourceFiles(files));
        }

        // Add the tasks to the job
        batchClient.createTasks(jobId, tasks);
    }

    /**
     * Upload a file to a blob container and return an SAS key
     *
     * @param source            The local file to upload
     * @return An SAS key for the uploaded file
     */
    protected String uploadFileToStorage(File source) {
        BlobClient blobClient = blobContainerClient.getBlobClient(source.getName());
        if (!blobClient.exists()) {
            blobClient.uploadFromFile(source.getPath());
        }

        OffsetDateTime start = OffsetDateTime.now().minusMinutes(5);
        OffsetDateTime expiry = OffsetDateTime.now().plusHours(1);
        BlobSasPermission permissions = new BlobSasPermission().setReadPermission(true);

        String sas = blobClient.generateUserDelegationSas(
                new BlobServiceSasSignatureValues(expiry, permissions),
                blobServiceClient.getUserDelegationKey(start, expiry));

        return blobClient.getBlobUrl() + "?" + sas;
    }

    /**
     * Wait for all tasks in a given job to be completed, or throw an exception on timeout
     *
     * @param jobId   The ID of the job to poll for completion.
     * @param timeout How long to wait for the job to complete before giving up
     */
    private void waitForTasksToComplete(String jobId, Duration timeout) throws InterruptedException, TimeoutException {
        long startTime = System.currentTimeMillis();
        long elapsedTime = 0L;

        logger.info("Waiting for tasks to complete (Timeout: {}m)", timeout.getSeconds() / 60);

        while (elapsedTime < timeout.toMillis()) {
            PagedIterable<BatchTask> taskCollection = batchClient.listTasks(jobId,
                    new BatchTasksListOptions().setSelect(Arrays.asList("id", "state")));
            boolean allComplete = true;
            for (BatchTask task : taskCollection) {
                if (task.getState() != BatchTaskState.COMPLETED) {
                    allComplete = false;
                    break;
                }
            }
            if (allComplete) {
                logger.info("All tasks completed");
                // All tasks completed
                return;
            }
            TimeUnit.SECONDS.sleep(10);
            elapsedTime = (new Date()).getTime() - startTime;
        }

        throw new TimeoutException("Task did not complete within the specified timeout");
    }

    private void logBatchException(BatchErrorException err) {
        logger.error("BatchErrorException occurred", err);

        BatchError error = err.getValue();
        if (error != null) {
            logger.error("BatchError code = {}, message = {}", error.getCode(),
                error.getMessage() != null ? error.getMessage().getValue() : "(no message)");
            if (error.getValues() != null) {
                for (BatchErrorDetail detail : error.getValues()) {
                    logger.error("Detail {} = {}", detail.getKey(), detail.getValue());
                }
            }
        } else {
            logger.warn("No BatchError information found in exception.");
        }
    }
}