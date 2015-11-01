.. image:: https://travis-ci.org/alfpark/azure-batch-samples.svg?branch=master
  :target: https://travis-ci.org/alfpark/azure-batch-samples
.. image:: https://coveralls.io/repos/alfpark/azure-batch-samples/badge.svg?branch=master&service=github
  :target: https://coveralls.io/github/alfpark/azure-batch-samples?branch=master
.. image:: https://img.shields.io/pypi/v/blobxfer.svg
  :target: https://pypi.python.org/pypi/blobxfer
.. image:: https://img.shields.io/pypi/dm/blobxfer.svg
  :target: https://pypi.python.org/pypi/blobxfer
.. image:: https://img.shields.io/pypi/pyversions/blobxfer.svg
  :target: https://pypi.python.org/pypi/blobxfer
.. image:: https://img.shields.io/pypi/l/blobxfer.svg
  :target: https://pypi.python.org/pypi/blobxfer

blobxfer
========
AzCopy-like OS independent Azure storage blob transfer tool

Installation
------------
blobxfer is on PyPI and can be installed via:

::

  pip install blobxfer

If you need more fine-grained control on installing dependencies, continue
reading this section. The blobxfer utility is a python script that can be used
on any platform where Python 2.7, 3.3, 3.4 or 3.5 is installable. Depending
upon the desired mode of authentication with Azure and options, the script
will require the following packages, some of which will automatically pull
required dependent packages:

- Base Requirements

  - ``azure-common`` >= 0.20.0

- Encryption Support

  - ``pycrypto`` >= 2.6.1

- Management Certificate

  - ``azure-servicemanagement-legacy`` >= 0.20.0
  - ``azure-storage`` >= 0.20.0

- Shared Account Key

  - ``azure-storage`` >= 0.20.0

- SAS Key

  - ``requests`` >= 2.7.0

If you want to utilize any/all of the connection methods to Azure Storage,
then install all three of ``azure-servicemanagement-legacy``,
``azure-storage``, and ``requests``. You can install these packages using pip,
easy_install or through standard setup.py procedures. As of this script
version 0.9.9.0, it no longer supports the legacy Azure Python SDK, i.e.,
``azure`` package with version < 1.0.0 due to breaking changes in the azure
packages.

Introduction
------------

The blobxfer.py script allows interacting with storage accounts using any of
the following methods: (1) management certificate, (2) shared account key,
(3) SAS key. The script can, in addition to working with single files, mirror
entire directories into and out of containers from Azure Storage, respectively.
Block- and file-level MD5 checksumming for data integrity is supported along
with various transfer optimizations, built-in retries, and user-specified
timeouts.

Program parameters and command-line options can be listed via the ``-h``
switch. At the minimum, three positional arguments are required: storage
account name, container name, local resource. Additionally, one of the
following authentication switches must be supplied: ``--subscriptionid`` with
``--managementcert``, ``--storageaccountkey``, or ``--saskey``. It is
recommended to use SAS keys wherever possible; only HTTPS transport is used in
the script.

Please remember when using SAS keys that only container-level SAS keys will
allow for entire directory uploading or container downloading. The container
must also have been created beforehand, as containers cannot be created
using SAS keys.

Please refer to the Microsoft HPC and Azure Batch Team `blog post`_ for code
sample explanations.

.. _blog post: http://blogs.technet.com/b/windowshpc/archive/2015/04/16/linux-blob-transfer-python-code-sample.aspx

Example Usage
-------------

The script will attempt to perform a smart transfer, by detecting if the local
resource exists. For example:

::

  blobxfer.py mystorageacct container0 mylocalfile.txt

Note: if you downloaded the script via PyPI, you should not append .py to
the invocation; just blobxfer should suffice.

If mylocalfile.txt exists locally, then the script will attempt to upload the
file to container0 on mystorageacct. If the file does not exist, then it will
attempt to download the resource. If the desired behavior is to download the
file from Azure even if the local file exists, one can override the detection
mechanism with ``--download``. ``--upload`` is available to force the transfer
to Azure storage. Note that specifying a particular direction does not force
the actual operation to occur as that depends on other options specified such
as skipping on MD5 matches. Note that you may use the ``--remoteresource`` flag
to rename the local file as the blob name on Azure storage if uploading.

If the local resource is a directory that exists, the script will attempt to
mirror (recursively copy) the entire directory to Azure storage while
maintaining subdirectories as virtual directories in Azure storage. You can
disable the recursive copy (i.e., upload only the files in the directory)
using the ``--no-recursive`` flag.

To download an entire container from your storage account, an example
commandline would be:

::

  blobxfer.py mystorageacct container0 mylocaldir --remoteresource .

Assuming mylocaldir directory does not exist, the script will attempt to
download all of the contents in container0 because “.” is set with
``--remoteresource`` flag. To download individual blobs, one would specify the
blob name instead of “.” with the ``--remoteresource`` flag. If mylocaldir
directory exists, the script will attempt to upload the directory instead of
downloading it. In this case, if you want to force the download direction,
indicate that with ``--download``. When downloading an entire container, the
script will attempt to pre-allocate file space and recreate the sub-directory
structure as needed.

To collate files into specified virtual directories or local paths, use
the ``--collate`` flag with the appropriate parameter. For example, the
following commandline:

::

  blobxfer.py mystorageacct container0 myvhds --upload --collate vhds --autovhd

If the directory ``myvhds`` had two vhd files a.vhd and subdir/b.vhd, these
files would be uploaded into ``container0`` under the virtual directory named
``vhds``, and b.vhd would not contain the virtual directory subdir; thus,
flattening the directory structure. The ``--autovhd`` flag would automatically
enable page blob uploads for these files. If you wish to collate all files
into the container directly, you would replace ``--collate vhds`` with
``--collate .``

To encrypt or decrypt files, the option ``--rsakey`` is available. This option
requires a file location for a PEM or DER/binary encoded RSA public or private
key. An RSA public key can be an X.509 subjectPublicKeyInfo, PKCS#1
RSAPublicKey, or OpenSSH textual public key. An RSA private key can be a PKCS#1
RSAPrivateKey or PKCS#8 PrivateKeyInfo. An optional parameter,
``--rsakeypassphrase`` is available for passphrase protected PEM keys.

To encrypt and upload, only the RSA public key is required, although if the
RSA private key is provided, the generated AES256 symmetric and signing keys
will be signed to ensure integrity and authentication. To download and decrypt
blobs which are encrypted, the RSA private key is required.

::

  blobxfer.py mystorageacct container0 myblobs --upload --rsakey myprivatekey.pem

The above example commandline would encrypt and upload files contained in
``myblobs`` using an RSA private key named ``myprivatekey.pem``. Although an
RSA private key is not required for uploading, by providing it during
encryption/upload, the generated AES256 symmetric and signing keys will be
signed for additional verification checks.

Currently only the ``FullBlob`` encryption mode is supported for the
parameter ``--encmode``. The ``FullBlob`` encryption mode either uploads or
downloads Azure Storage .NET/Java compatible client-side encrypted block blobs.

Please read the Encryption Notes below for more information.

General Notes
-------------

- blobxfer does not take any leases on blobs or containers. It is up to
  the user to ensure that blobs are not modified while download/uploads
  are being performed.
- No validation is performed regarding container and file naming and length
  restrictions.
- blobxfer will attempt to download from blob storage as-is. If the source
  filename is incompatible with the destination operating system, then
  failure may result.
- When using SAS, the SAS key must be a container-level SAS if performing
  recursive directory upload or container download.
- If uploading via SAS, the container must already be created in blob
  storage prior to upload. This is a limitation of SAS keys. The script
  will force disable container creation if a SAS key is specified.
- For non-SAS requests, timeouts may not be properly honored due to
  limitations of the Azure Python SDK.
- In order to skip download/upload matching files via MD5, the
  computefilemd5 flag must be enabled (it is enabled by default).
- When uploading files as page blobs, the content is page boundary
  byte-aligned. The MD5 for the blob is computed using the final aligned
  data if the source is not page boundary byte-aligned. This enables these
  page blobs or files to be skipped during subsequent download or upload,
  if the skiponmatch parameter is enabled.

Performance Notes
-----------------

- Most likely, you will need to tweak the ``--numworkers`` argument that best
  suits your environment. The default is the number of CPUs multiplied by 5.
  Increasing this number (or even using the default) may not provide the
  optimal balance between concurrency and your network conditions.
  Additionally, this number may not work properly if you are attempting to run
  multiple blobxfer sessions in parallel from one machine or IP address.
  Futhermore, this number may be defaulted to be set too high if encryption
  is enabled and the machine cannot handle processing multiple threads in
  parallel.
- As of requests 2.6.0 and Python versions < 2.7.9 (i.e., interpreter found
  on default Ubuntu 14.04 installations), if certain packages are installed,
  as those found in ``requests[security]`` then the underlying ``urllib3``
  package will utilize the ``ndg-httpsclient`` package which will use
  `pyOpenSSL`_.
  This will ensure the peers are `fully validated`_. However, this incurs a
  rather larger performance penalty. If you understand the potential security
  risks for disabling this behavior due to high performance requirements, you
  can either remove ``ndg-httpsclient`` or use the script in a ``virtualenv``
  environment without the ``ndg-httpsclient`` package. Python versions >=
  2.7.9 are not affected by this issue.

.. _pyOpenSSL: https://urllib3.readthedocs.org/en/latest/security.html#pyopenssl
.. _fully validated: https://urllib3.readthedocs.org/en/latest/security.html#insecureplatformwarning


Encryption Notes
----------------

- ENCRYPTION SUPPORT IS CONSIDERED ALPHA QUALITY. BREAKING CHANGES MAY BE
  APPLIED TO BLOBXFER DURING ALPHA TESTING RENDERING ENCRYPTED DATA
  UNRECOVERABLE. DO NOT USE FOR LIVE OR PRODUCTION DATA.
- Keys for AES256 block cipher are generated on a per-blob basis. These keys
  are encrypted using RSAES-OAEP and an optional signature for the keys are
  generated using RSASSA-PKCS1-v1_5.
- All required information regarding the encryption process is stored on
  each blob's ``encryptiondata`` metadata. This metadata is used on download
  to configure the proper download and decryption process. Encryption metadata
  set by blobxfer (or the Azure Storage .NET/Java client library) should not
  be modified or blobs may be unrecoverable.
- MD5 for both the pre-encrypted and encrypted version of the file is stored
  on the blob. Rsync-like synchronization is still supported transparently
  with encrypted blobs.
- Whole file MD5 checks are skipped if a message authentication code is found
  to validate the integrity of the encrypted data.
- Uploading the same file as an encrypted blob with a different encryption
  mode will not occur if the file content MD5 is the same. Additionally,
  if one wishes to apply encryption to a blob already uploaded to Storage
  that has not changed, the upload will not occur since the underlying
  Content MD5 has not changed; this behavior can be overriden by including
  the option ``--no-skiponmatch``.
- Encryption is only applied to block blobs. Encrypted page blobs appear to
  be of minimal value stored in Azure. Thus, if uploading encrypted VHDs for
  storage in Azure, do not enable either of the options: ``--pageblob`` or
  ``--autovhd`` as the script will fail.
- Downloading encrypted blobs may not fully preallocate each file due to
  padding. Script failure can result during transfer if there is insufficient
  disk space.
- Zero-byte (empty) files are not encrypted.

Change Log
----------

- 0.9.9.6: add encryption support, fix shared key upload with non-existent
  container, add file overwrite on download option, add auto-detection of file
  mimetype
- 0.9.9.5: add file collation support, fix page alignment bug, reduce memory
  usage
- 0.9.9.4: improve page blob upload algorithm to skip empty max size pages.
  fix zero length file uploads. fix single file upload that's skipped.
- 0.9.9.3: fix downloading of blobs with content length of zero
- 0.9.9.1: fix content length > 32bit for blob lists via SAS on Python2
- 0.9.9.0: update script for compatibility with new Azure Python packages
- 0.9.8: fix blob endpoint for non-SAS input, add retry on ServerBusy
- 0.9.7: normalize SAS keys (accept keys with or without ? char prefix)
- 0.9.6: revert local resource path expansion, PEP8 fixes
- 0.9.5: fix directory creation issue
- 0.9.4: fix Python3 compatibility issues
- 0.9.3: the script supports page blob uploading. To specify local files to
  upload as page blobs, specify the ``--pageblob`` parameter. The script also
  has a feature to detect files ending in the ``.vhd`` extension and will
  automatically upload just these files as page blobs while uploading other
  files as block blobs. Specify the ``--autovhd`` parameter (without the
  ``--pageblob`` parameter) to enable this behavior.
- 0.9.0: the script will automatically default to skipping files where if the
  MD5 checksum of either the local file or the stored MD5 of the remote
  resource respectively matches the remote resource or local file, then the
  upload or download for the file will be skipped. This capability will allow
  one to perform rsync-like operations where only files that have changed will
  be transferred. This behavior can be forcefully disabled by specifying
  ``--no-skiponmatch``.
- 0.8.2: performance regression fixes
