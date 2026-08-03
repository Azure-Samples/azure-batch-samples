## Azure Batch Python ffmpeg Article Sample

A Python application that uses Azure Batch to process media files in parallel
with the [ffmpeg](https://ffmpeg.org/) open-source tool. The sample converts
MP4 media files to MP3 format, in parallel, across a pool of Batch compute
nodes.

For details and explanation, see the accompanying article
[Tutorial: Run a parallel workload with Azure Batch using the Python API](https://learn.microsoft.com/azure/batch/tutorial-parallel-python).

### Prerequisites

- [Python 3.8 or later](https://www.python.org/downloads/) including `pip`
- An Azure Batch account and a linked general-purpose Azure Storage account
- The [Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli)
- [ffmpeg](https://ffmpeg.org/download.html) on your PATH (used locally to
  generate the sample input files)

### Authentication

This sample authenticates to Azure Batch and Azure Storage with Microsoft
Entra ID through
[`DefaultAzureCredential`](https://learn.microsoft.com/python/api/azure-identity/azure.identity.defaultazurecredential).
**No account keys are used.**

Sign in and make sure your identity has the required roles on both accounts:

```bash
az login
```

- On the **Batch account**, assign a role that allows data-plane operations,
  such as **Azure Batch Data Contributor** (required to create pools, jobs,
  and tasks).
- On the **Storage account**, assign **Storage Blob Data Contributor**
  (required to create containers, upload input files, and request the user
  delegation key used to sign SAS URLs).

```bash
# Batch account role
az role assignment create \
    --assignee "<your-user-principal-name>" \
    --role "Azure Batch Data Contributor" \
    --scope "/subscriptions/<subscription-id>/resourceGroups/<resource-group>/providers/Microsoft.Batch/batchAccounts/<batch-account-name>"

# Storage account role
az role assignment create \
    --assignee "<your-user-principal-name>" \
    --role "Storage Blob Data Contributor" \
    --scope "/subscriptions/<subscription-id>/resourceGroups/<resource-group>/providers/Microsoft.Storage/storageAccounts/<storage-account-name>"
```

> Role assignments can take a few minutes to propagate.

### Setting up the Python environment

Install the required packages using the
[requirements.txt](./requirements.txt) file:

```bash
pip install -r requirements.txt
```

The sample uses the current Azure SDK for Python libraries:
[`azure-batch`](https://pypi.org/project/azure-batch/) (`BatchClient`),
[`azure-identity`](https://pypi.org/project/azure-identity/), and
[`azure-storage-blob`](https://pypi.org/project/azure-storage-blob/).

### Running the sample

1. Open [config.py](./config.py) and set the values unique to your accounts
   (no keys required):

   ```python
   _BATCH_ACCOUNT_NAME = 'yourbatchaccount'
   _BATCH_ACCOUNT_URL = 'https://yourbatchaccount.yourregion.batch.azure.com'
   _STORAGE_ACCOUNT_NAME = 'mystorageaccount'
   ```

2. Generate the sample input files. The repository does not ship binary
   media, so create the local `InputFiles/*.mp4` clips with the included
   helper (requires ffmpeg on your PATH):

   ```bash
   python generate_input_files.py
   ```

   This writes five short sample clips (`LowPriVMs-1.mp4` …
   `LowPriVMs-5.mp4`) into the `InputFiles` directory. Alternatively, drop
   your own `.mp4` files into `InputFiles/`.

3. Run the app:

   ```bash
   python batch_python_tutorial_ffmpeg.py
   ```

By default the sample creates a pool of five Spot (low-priority) nodes of size
`STANDARD_A1_v2` running **Ubuntu Server 24.04 LTS**. You can change the pool
size, VM size, and node counts in `config.py`.

> Marketplace VM images and Batch node agents have support end dates. To list
> the image references and node agent SKUs that your Batch account currently
> supports, call the
> [`list_supported_images`](https://learn.microsoft.com/python/api/azure-batch/azure.batch.batchclient#azure-batch-batchclient-list-supported-images)
> method.
