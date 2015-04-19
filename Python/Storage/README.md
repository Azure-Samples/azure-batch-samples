###blobxfer.py
Please refer to this blog post for more information:
http://blogs.technet.com/b/windowshpc/archive/2015/04/16/linux-blob-transfer-python-code-sample.aspx

The blobxfer.py script allows interacting with storage accounts using any of
the following methods: (1) management certificate, (2) shared account key,
(3) SAS key. The script can, in addition to working with single files, mirror
entire directories into and out of containers from Azure Storage, respectively.
Block- and file-level MD5 checksumming for data integrity is supported along
with various transfer optimizations, built-in retries, and user-specified
timeouts.

The blobxfer script is a python script that can be used on any platform where
a modern Python interpreter can be installed. The script requires two
prerequisite packages to be installed: (1) azure and (2) requests. The azure
package is required for the script to utilize the Azure Python SDK to interact
with Azure using a management certificate or a shared key. The requests package
is required for SAS support. If SAS is not needed, one can remove all of the
requests references from the script to reduce the prerequisite footprint. You
can install these packages using pip, easy_install or through standard
setup.py procedures.

Program parameters and command-line options can be listed via the -h switch. At
the minimum, three positional arguments are required: storage account name,
container name, local resource. Additionally, one of the following
authentication switches must be supplied: --subscriptionid with
--managementcert, --storageaccountkey, or --saskey. It is recommended to use
SAS keys wherever possible; only HTTPS transport is used in the script.

The script will attempt to perform a smart transfer, by detecting if the local
resource exists. For example:

```
blobxfer.py mystorageacct container0 mylocalfile.txt
```

If mylocalfile.txt exists locally, then the script will attempt to upload the
file to container0 on mystorageacct. If the file does not exist, then it will
attempt to download the resource. If the desired behavior is to download the
file from Azure even if the local file exists, once can override the detection
mechanism with --forcedownload. --forceupload is available to force the
transfer to Azure storage. Note that you may use the --remoteresource flag to
rename the local file as the blob name on Azure storage if uploading.

If the local resource is a directory that exists, the script will attempt to
mirror (recursively copy) the entire directory to Azure storage while
maintaining subdirectories as virtual directories in Azure storage. You can
disable the recursive copy (i.e., upload only the files in the directory)
using the --no-recursive flag.

To download an entire container from your storage account, an example
commandline would be:

```
blobxfer.py mystorageacct container0 mylocaldir --remoteresource .
```

Assuming mylocaldir directory does not exist, the script will attempt to
download all of the contents in container0 because “.” is set with
--remoteresource flag. To download individual blobs, one would specify the
blob name instead of “.” with the --remoteresource flag. If mylocaldir
directory exists, the script will attempt to upload the directory instead of
downloading it. In this case, if you want to force the download, indicate that
with --forcedownload. When downloading an entire container, the script will
attempt to pre-allocate file space and recreate the sub-directory structure
as needed.

Please remember when using SAS keys that only container-level SAS keys will
allow for entire directory uploading or container downloading. The container
must also have been created beforehand, as containers cannot be created
using SAS keys.

