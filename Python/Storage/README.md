##blobxfer.py
Please refer to the Microsoft HPC and Azure Batch Team [blog post](http://blogs.technet.com/b/windowshpc/archive/2015/04/16/linux-blob-transfer-python-code-sample.aspx)
for code sample explanations.

###Introduction
The blobxfer.py script allows interacting with storage accounts using any of
the following methods: (1) management certificate, (2) shared account key,
(3) SAS key. The script can, in addition to working with single files, mirror
entire directories into and out of containers from Azure Storage, respectively.
Block- and file-level MD5 checksumming for data integrity is supported along
with various transfer optimizations, built-in retries, and user-specified
timeouts.

The blobxfer script is a python script that can be used on any platform where
Python 2.7, 3.3 or 3.4 can be installed. The script requires two
prerequisite packages to be installed: (1) azure and (2) requests. The azure
package is required for the script to utilize the
[Azure Python SDK](http://azure.microsoft.com/en-us/documentation/articles/python-how-to-install/)
to interact with Azure using a management certificate or a shared key. The
requests package is required for SAS support. If SAS is not needed, one can
remove all of the requests references from the script to reduce the
prerequisite footprint. You can install these packages using pip, easy_install
or through standard setup.py procedures.

Program parameters and command-line options can be listed via the -h switch. At
the minimum, three positional arguments are required: storage account name,
container name, local resource. Additionally, one of the following
authentication switches must be supplied: `--subscriptionid` with
`--managementcert`, `--storageaccountkey`, or `--saskey`. It is recommended
to use SAS keys wherever possible; only HTTPS transport is used in the script.

Please remember when using SAS keys that only container-level SAS keys will
allow for entire directory uploading or container downloading. The container
must also have been created beforehand, as containers cannot be created
using SAS keys.

###Example Usage
The script will attempt to perform a smart transfer, by detecting if the local
resource exists. For example:

```
blobxfer.py mystorageacct container0 mylocalfile.txt
```

If mylocalfile.txt exists locally, then the script will attempt to upload the
file to container0 on mystorageacct. If the file does not exist, then it will
attempt to download the resource. If the desired behavior is to download the
file from Azure even if the local file exists, one can override the detection
mechanism with `--download`. `--upload` is available to force the transfer to
Azure storage. Note that specifying a particular direction does not force the
actual operation to occur as that depends on other options specified such as
skipping on MD5 matches. Note that you may use the `--remoteresource` flag to
rename the local file as the blob name on Azure storage if uploading.

If the local resource is a directory that exists, the script will attempt to
mirror (recursively copy) the entire directory to Azure storage while
maintaining subdirectories as virtual directories in Azure storage. You can
disable the recursive copy (i.e., upload only the files in the directory)
using the `--no-recursive` flag.

To download an entire container from your storage account, an example
commandline would be:

```
blobxfer.py mystorageacct container0 mylocaldir --remoteresource .
```

Assuming mylocaldir directory does not exist, the script will attempt to
download all of the contents in container0 because “.” is set with
`--remoteresource` flag. To download individual blobs, one would specify the
blob name instead of “.” with the `--remoteresource` flag. If mylocaldir
directory exists, the script will attempt to upload the directory instead of
downloading it. In this case, if you want to force the download direction,
indicate that with `--download`. When downloading an entire container, the
script will attempt to pre-allocate file space and recreate the sub-directory
structure as needed.

###Notes
A note on performance with Python versions < 2.7.9 (i.e., interpreter found
on default Ubuntu 14.04 installations) -- as of requests 2.6.0, if certain
packages are installed, as those found in `requests[security]` then the
underlying urllib3 package will utilize the ndg-httpsclient package which
will use [pyOpenSSL](https://urllib3.readthedocs.org/en/latest/security.html#pyopenssl).
This will ensure the peers are [fully validated](https://urllib3.readthedocs.org/en/latest/security.html#insecureplatformwarning).
However, this incurs a rather larger performance penalty. If you understand
the potential security risks for disabling this behavior due to high
performance requirements, you can either remove ndg-httpsclient or use the
script in a virtualenv environment without the ndg-httpsclient package.
Python versions >= 2.7.9 are not affected by this issue.

###Change Log
* 0.9.8: fix blob endpoint for non-SAS input, add retry on ServerBusy
* 0.9.7: normalize SAS keys (accept keys with or without ? char prefix)
* 0.9.6: revert local resource path expansion, PEP8 fixes
* 0.9.5: fix directory creation issue
* 0.9.4: fix Python3 compatibility issues
* 0.9.3: the script supports page blob uploading. To specify local files to
upload as page blobs, specify the `--pageblob` parameter. The script also has
a feature to detect files ending in the `.vhd` extension and will
automatically upload just these files as page blobs while uploading other
files as block blobs. Specify the `--autovhd` parameter (without the
`--pageblob` parameter) to enable this behavior.
* 0.9.0: the script will automatically default to skipping files where if the
MD5 checksum of either the local file or the stored MD5 of the remote resource
respectively matches the remote resource or local file, then the upload or
download for the file will be skipped. This capability will allow one to
perform rsync-like operations where only files that have changed will be
transferred. This behavior can be forcefully disabled by specifying
`--no-skiponmatch`.
* 0.8.2: performance regression fixes
