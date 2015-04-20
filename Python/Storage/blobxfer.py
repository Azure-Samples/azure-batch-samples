#!/usr/bin/env python

# blobxfer.py Code Sample
#
# Copyright (c) Microsoft Corporation
#
# All rights reserved.
#
# MIT License
#
# Permission is hereby granted, free of charge, to any person obtaining a
# copy of this software and associated documentation files (the "Software"),
# to deal in the Software without restriction, including without limitation
# the rights to use, copy, modify, merge, publish, distribute, sublicense,
# and/or sell copies of the Software, and to permit persons to whom the
# Software is furnished to do so, subject to the following conditions:
#
# The above copyright notice and this permission notice shall be included in
# all copies or substantial portions of the Software.
#
# THE SOFTWARE IS PROVIDED *AS IS*, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
# IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
# FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
# AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
# LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
# FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
# DEALINGS IN THE SOFTWARE.

"""
Code sample to show data transfer to/from Azure (block) blob storage

Notes:
- Script does not perform any validation regarding container and file
  naming and length restrictions
- Script will attempt to download from blob storage as-is. If the source
  filename is incompatible with the destination operating system, then
  failure may result
- When using SAS, the SAS key must be a container-level SAS if performing
  recursive directory upload or container download
- If uploading via SAS, the container must already be created in blob
  storage prior to upload. This is a limitation of SAS keys. The script
  will force disable container creation if a SAS key is specified.
- For non-SAS requests, timeouts may not be properly honored due to
  limitations of the Azure Python SDK.

TODO list:
- convert from synchronous multithreading to asyncio/trollius
- complete Python3 support (blocking on Azure Python SDK issues)
"""

# pylint: disable=C0301,R0913,R0914

# stdlib imports
from __future__ import print_function
import argparse
import base64
import errno
import hashlib
import os
# pylint: disable=F0401
try:
    import queue
except ImportError:
    import Queue as queue
# pylint: enable=F0401
import random
import socket
import sys
import threading
import time
# non-stdlib imports
import azure
import azure.servicemanagement
import azure.storage
import requests

# remap keywords for Python3
# pylint: disable=W0622,C0103
try:
    xrange
except NameError: # pragma: no cover
    xrange = range
try:
    long
except NameError: # pragma: no cover
    long = int
# pylint: enable=W0622,C0103

# global defines
_SCRIPT_VERSION = '0.8.2'
_DEFAULT_MAX_STORAGEACCOUNT_WORKERS = 64
_MAX_BLOB_CHUNK_SIZE_BYTES = 4194304
_MAX_LISTBLOBS_RESULTS = 1000
_DEFAULT_BLOB_ENDPOINT = 'blob.core.windows.net'
_DEFAULT_MANAGEMENT_ENDPOINT = 'management.core.windows.net'

# compute md5 for file for azure storage
def compute_md5_for_file_asbase64(filename, blocksize=16777216):
    """Compute MD5 hash for file and encode as Base64
    Parameters:
        filename - filename to compute md5
        blocksize - block size in bytes
    Returns:
        MD5 for file encoded as Base64
    Raises:
        Nothing
    """
    hasher = hashlib.md5()
    with open(filename, 'rb') as filedesc:
        while True:
            buf = filedesc.read(blocksize)
            if not buf:
                break
            hasher.update(buf)
        return base64.b64encode(hasher.digest())

# compute md5 for bit bucket
def compute_md5_for_data_asbase64(data):
    """Compute MD5 hash for bits and encode as Base64
    Parameters:
        data - data to compute MD5 hash over
    Returns:
        MD5 for data encoded as Base64
    Raises:
        Nothing
    """
    hasher = hashlib.md5()
    hasher.update(data)
    return base64.b64encode(hasher.digest())

# wrapper method to retry Azure Python SDK requests on timeouts/errors
def azure_request(req, timeout=None, wait=5, *args, **kwargs):
    """Issue a request to Azure
    Parameters:
        req - request to issue
        timeout - timeout in seconds
        wait - wait between retries in seconds
        args - positional args to req
        kwargs - keyworded args to req
    Returns:
        result of request
    Raises:
        Any uncaught exceptions
        IOError if timeout
    """
    start = time.clock()
    while True:
        try:
            return req(*args, **kwargs)
        except socket.error as exc:
            if exc.errno != errno.ETIMEDOUT and \
                    exc.errno != errno.ECONNRESET and \
                    exc.errno != errno.ECONNREFUSED and \
                    exc.errno != errno.ECONNABORTED and \
                    exc.errno != errno.ENETRESET:
                raise
        except Exception as exc:
            if 'TooManyRequests' not in exc.message and \
                    'InternalError' not in exc.message:
                raise
        if timeout is not None and time.clock() - start > timeout:
            raise IOError('waited for {} for request {}, exceeded timeout of {}'.format(
                time.clock() - start, req.__name__, timeout))
        time.sleep(random.randint(wait, wait * 2))

def http_request_wrapper(func, timeout=None, *args, **kwargs):
    """Wrap HTTP request to retry
    Parameters:
        func - function to call
        timeout - timeout
        args - regular args to pass to func
        kwargs - keyword args to pass to func
    Returns:
        Response object
    Raises:
        Re-raises uncaught errors/exceptions and status exceptions
        IOError if timeout
    """
    start = time.clock()
    while True:
        try:
            response = func(*args, timeout=timeout, **kwargs)
            response.raise_for_status()
            return response
        except socket.error as exc:
            if exc.errno != errno.ETIMEDOUT and \
                    exc.errno != errno.ECONNRESET and \
                    exc.errno != errno.ECONNREFUSED and \
                    exc.errno != errno.ECONNABORTED and \
                    exc.errno != errno.ENETRESET:
                raise
        except (requests.exceptions.ConnectTimeout,
                requests.exceptions.ReadTimeout):
            pass
        except requests.exceptions.HTTPError as exc:
            if exc.response.status_code < 500 or \
                    exc.response.status_code == 501 or \
                    exc.response.status_code == 505:
                raise
        if timeout is not None and time.clock() - start > timeout:
            raise IOError('waited for {}, exceeded timeout of {}'.format(
                time.clock() - start, timeout))
        time.sleep(random.randint(2, 5))

class SasBlobService(object):
    """BlobService supporting SAS for functions used in the Python SDK.
       create_container method does not exist because it is not a supported
       operation under SAS"""
    def __init__(self, blobep, saskey, timeout):
        """SAS Blob Service ctor
        Parameters:
            blobep - blob endpoint
            saskey - saskey
            timeout - timeout
        Returns:
            Nothing
        Raises:
            Nothing
        """
        self.blobep = blobep
        self.saskey = saskey
        self.timeout = timeout

    def list_blobs(self, container_name, marker=None,
            maxresults=_MAX_LISTBLOBS_RESULTS):
        """List blobs in container
        Parameters:
            container_name - container name
            marker - marker
            maxresults - max results
        Returns:
            List of blobs
        Raises:
            IOError if unexpected status code
        """
        url = '{blobep}{container_name}{saskey}'.format(
                blobep=self.blobep, container_name=container_name,
                saskey=self.saskey)
        reqparams = {'restype': 'container',
                'comp': 'list',
                'maxresults': str(maxresults)}
        if marker is not None:
            reqparams['marker'] = marker
        response = http_request_wrapper(requests.get, url=url,
                params=reqparams, timeout=self.timeout)
        if response.status_code != 200:
            raise IOError('incorrect status code returned for list_blobs: {}'.format(
                response.status_code))
        response.body = response.text
        # pylint: disable=W0212
        return azure.storage._parse_blob_enum_results_list(response)
        # pylint: enable=W0212

    def get_blob(self, container_name, blob_name, x_ms_range):
        """Get blob
        Parameters:
            container_name - container name
            blob_name - name of blob
            x_ms_range - byte range
        Returns:
            blob content
        Raises:
            IOError if unexpected status code
        """
        url = '{blobep}{container_name}/{blob_name}{saskey}'.format(
                blobep=self.blobep, container_name=container_name, blob_name=blob_name,
                saskey=self.saskey)
        reqheaders = {'x-ms-range': x_ms_range}
        response = http_request_wrapper(requests.get, url=url,
                headers=reqheaders, timeout=self.timeout)
        if response.status_code != 200 and response.status_code != 206:
            raise IOError('incorrect status code returned for get_blob: {}'.format(
                response.status_code))
        return response.content

    def get_blob_properties(self, container_name, blob_name):
        """Get blob properties
        Parameters:
            container_name - container name
            blob_name - name of blob
        Returns:
            blob properties (response header)
        Raises:
            IOError if unexpected status code
        """
        url = '{blobep}{container_name}/{blob_name}{saskey}'.format(
                blobep=self.blobep, container_name=container_name, blob_name=blob_name,
                saskey=self.saskey)
        response = http_request_wrapper(requests.head, url=url,
                timeout=self.timeout)
        if response.status_code != 200:
            raise IOError('incorrect status code returned for get_blob_properties: {}'.format(
                response.status_code))
        return response.headers

    def put_block(self, container_name, blob_name, block, blockid, content_md5):
        """Put block for blob
        Parameters:
            container_name - container name
            blob_name - name of blob
            block - block data
            blockid - block id
            content_md5 - md5 hash for block data
        Returns:
            Nothing
        Raises:
            IOError if unexpected status code
        """
        url = '{blobep}{container_name}/{blob_name}{saskey}'.format(
                blobep=self.blobep, container_name=container_name, blob_name=blob_name,
                saskey=self.saskey)
        reqheaders = {'Content-MD5': content_md5}
        reqparams = {'comp': 'block',
                'blockid': blockid}
        response = http_request_wrapper(requests.put, url=url, params=reqparams,
                headers=reqheaders, data=block, timeout=self.timeout)
        if response.status_code != 201:
            raise IOError('incorrect status code returned for put_block: {}'.format(
                response.status_code))

    def put_block_list(self, container_name, blob_name, block_list, x_ms_blob_content_md5):
        """Put block list for blob
        Parameters:
            container_name - container name
            blob_name - name of blob
            block_list - block list for blob
            x_ms_blob_content_md5 - md5 hash for blob
        Returns:
            Nothing
        Raises:
            IOError if unexpected status code
        """
        url = '{blobep}{container_name}/{blob_name}{saskey}'.format(
                blobep=self.blobep, container_name=container_name, blob_name=blob_name,
                saskey=self.saskey)
        reqheaders = {'x-ms-blob-content-md5': x_ms_blob_content_md5}
        reqparams = {'comp': 'blocklist'}
        body = ['<?xml version="1.0" encoding="utf-8"?><BlockList>']
        for block in block_list:
            body.append('<Latest>{}</Latest>'.format(block))
        body.append('</BlockList>')
        response = http_request_wrapper(requests.put, url=url, params=reqparams,
                headers=reqheaders, data=''.join(body), timeout=self.timeout)
        if response.status_code != 201:
            raise IOError('incorrect status code returned for put_block_list: {}'.format(
                response.status_code))

class BlobChunkWorker(threading.Thread):
    """Chunk worker for a Blob"""
    def __init__(self, exc, s_in_queue, s_out_queue, blob_service, timeout):
        """Blob Chunk worker Thread ctor
        Parameters:
            exc - exception list
            s_in_queue - storage in queue
            s_out_queue - storage out queue
            blob_service - blob service
            timeout - timeout
        Returns:
            Nothing
        Raises:
            Nothing
        """
        threading.Thread.__init__(self)
        self._exc = exc
        self._in_queue = s_in_queue
        self._out_queue = s_out_queue
        self.blob_service = blob_service
        self.timeout = timeout

    def run(self):
        """Thread code
        Parameters:
            Nothing
        Returns:
            Nothing
        Raises:
            Nothing
        """
        while True:
            xfertoazure, localresource, storageaccount, storageaccountkey, \
                    container, remoteresource, blockid, offset, bytestoxfer, \
                    flock, filedesc = self._in_queue.get()
            try:
                if xfertoazure:
                    # upload block
                    self.putblock(localresource, container, remoteresource,
                            blockid, offset, bytestoxfer, flock, filedesc)
                else:
                    # download range
                    self.getblobrange(localresource, container, remoteresource,
                            offset, bytestoxfer, flock, filedesc)
                # pylint: disable=W0703
            except Exception as exc:
                # pylint: enable=W0703
                self._exc.append(exc)
            self._out_queue.put([xfertoazure, localresource, storageaccount,
                storageaccountkey, container, remoteresource, blockid, offset,
                bytestoxfer, flock])
            if len(self._exc) > 0:
                break

    def putblock(self, localresource, container, remoteresource, blockid,
            offset, bytestoxfer, flock, filedesc):
        """Puts a block into Azure storage
        Parameters:
            localresource - name of local resource
            container - blob container
            remoteresource - name of remote resource
            blockid - block id
            offset - file offset
            bytestoxfer - number of bytes to xfer
            flock - file lock
            filedesc - file handle
        Returns:
            Nothing
        Raises:
            IOError if file cannot be read
        """
        # read the file at specified offset, must take lock
        blockdata = None
        with flock:
            closefd = False
            if not filedesc:
                filedesc = open(localresource, 'rb')
                closefd = True
            filedesc.seek(offset, 0)
            blockdata = filedesc.read(bytestoxfer)
            if closefd:
                filedesc.close()
        if not blockdata: # pragma: no cover
            raise IOError('could not read {}: {} -> {}'.format(
                localresource, offset, offset+bytestoxfer))
        # compute block md5
        blockmd5 = compute_md5_for_data_asbase64(blockdata)
        # issue REST put
        azure_request(self.blob_service.put_block, timeout=self.timeout,
                container_name=container, blob_name=remoteresource,
                block=blockdata, blockid=blockid, content_md5=blockmd5)

    def getblobrange(self, localresource, container, remoteresource, offset,
                     bytestoxfer, flock, filedesc):
        """Get a segment of a blob using range offset downloading
        Parameters:
            localresource - name of local resource
            container - blob container
            remoteresource - name of remote resource
            offset - file offset
            bytestoxfer - number of bytes to xfer
            flock - file lock
            filedesc - file handle
        Returns:
            Nothing
        Raises:
            Nothing
        """
        rangestr = 'bytes={}-{}'.format(offset, offset+bytestoxfer)
        blobdata = azure_request(self.blob_service.get_blob,
                timeout=self.timeout, container_name=container,
                blob_name=remoteresource, x_ms_range=rangestr)
        with flock:
            closefd = False
            if not filedesc:
                filedesc = open(localresource, 'r+b')
                closefd = True
            filedesc.seek(offset, 0)
            filedesc.write(blobdata)
            if closefd:
                filedesc.close()

def generate_xferspec_download(blob_service, args, storage_in_queue, localfile,
        remoteresource, contentlength, contentmd5, addfd):
    """Generate an xferspec for download
    Parameters:
        blob_service - blob service
        args - program arguments
        storage_in_queue - storage input queue
        localfile - name of local resource
        remoteresource - name of remote resource
        contentlength - content length
        contentmd5 - content md5
        addfd - create and add file handle
    Returns:
        xferspec containing instructions
    Raises:
        ValueError if get_blob_properties returns an invalid result or
            contentlength is invalid
    """
    # get the file metadata
    if contentlength is None or contentmd5 is None:
        result = azure_request(blob_service.get_blob_properties,
                timeout=args.timeout, container_name=args.container,
                blob_name=remoteresource)
        if not result:
            raise ValueError('unexpected result for get_blob_properties is None')
        if 'content-md5' in result:
            contentmd5 = result['content-md5']
        contentlength = long(result['content-length'])
    if contentlength < 0:
        raise ValueError('contentlength is invalid for {}'.format(remoteresource))
    print('remote file {} length: {} bytes, md5: {}'.format(remoteresource,
        contentlength, contentmd5))
    tmpfilename = localfile + '.blobtmp'
    nchunks = contentlength // args.chunksizebytes
    currfileoffset = 0
    nstorageops = 0
    flock = threading.Lock()
    filedesc = None
    # preallocate file
    with flock:
        filedesc = open(tmpfilename, 'wb')
        filedesc.seek(contentlength - 1)
        filedesc.write('\0')
        filedesc.close()
        if addfd:
            # reopen under r+b mode
            filedesc = open(tmpfilename, 'r+b')
        else:
            filedesc = None
    for _ in xrange(nchunks + 1):
        chunktoadd = min(args.chunksizebytes, contentlength)
        if chunktoadd + currfileoffset > contentlength:
            chunktoadd = contentlength - currfileoffset
        # on download, chunktoadd must be offset by 1 as the x-ms-range
        # header expects it that way. x -> y bytes means first bits of the
        # (x+1)th byte to the last bits of the (y+1)th byte. for example,
        # 0 -> 511 means byte 1 to byte 512
        xferspec = [False, tmpfilename, args.storageaccount,
                args.storageaccountkey, args.container, remoteresource,
                None, currfileoffset, chunktoadd - 1, flock, filedesc]
        currfileoffset = currfileoffset + chunktoadd
        nstorageops = nstorageops + 1
        storage_in_queue.put(xferspec)
        if currfileoffset >= contentlength:
            break
    return contentlength, nstorageops, contentmd5, filedesc

def generate_xferspec_upload(args, storage_in_queue, blockids, localfile,
        remoteresource, addfd):
    """Generate an xferspec for upload
    Parameters:
        args - program arguments
        storage_in_queue - storage input queue
        blockids - block id dictionary
        localfile - name of local resource
        remoteresource - name of remote resource
        addfd - create and add file handle
    Returns:
        xferspec containing instructions
    Raises:
        Nothing
    """
    # compute md5 hash
    md5digest = None
    if args.computefilemd5:
        md5digest = compute_md5_for_file_asbase64(localfile)
        print('{} md5: {}'.format(localfile, md5digest))
    # create blockids entry
    if localfile not in blockids:
        blockids[localfile] = []
    # partition local file into chunks
    filesize = os.path.getsize(localfile)
    nchunks = filesize // args.chunksizebytes
    currfileoffset = 0
    nstorageops = 0
    flock = threading.Lock()
    filedesc = None
    if addfd:
        with flock:
            filedesc = open(localfile, 'rb')
    for _ in xrange(nchunks + 1):
        chunktoadd = min(args.chunksizebytes, filesize)
        if chunktoadd + currfileoffset > filesize:
            chunktoadd = filesize - currfileoffset
        blockid = '{0:08d}'.format(currfileoffset // args.chunksizebytes)
        blockids[localfile].append(blockid)
        xferspec = [True, localfile, args.storageaccount,
                args.storageaccountkey, args.container, remoteresource,
                blockid, currfileoffset, chunktoadd, flock, filedesc]
        currfileoffset = currfileoffset + chunktoadd
        nstorageops = nstorageops + 1
        storage_in_queue.put(xferspec)
        if currfileoffset >= filesize:
            break
    return filesize, nstorageops, md5digest, filedesc

def create_dir_ifnotexists(dirname):
    """Create a directory if it doesn't exist
    Parameters:
        dirname - name of directory to create
    Returns:
        Nothing
    Raises:
        Unhandled exceptions
    """
    try:
        os.makedirs(dirname)
        print('created local directory: {}'.format(dirname))
    except OSError as exc:
        if exc.errno != errno.EEXIST:
            raise # pragma: no cover

def progress_bar(display, sprefix, rtext, value, qsize,
        start):
    """Display a progress bar
    Parameters:
        display - display bar
        sprefix - progress prefix
        rtext - rate text
        value - value input value
        qsize - queue size
        start - start time
    Returns:
        Nothing
    Raises:
        Nothing
    """
    if not display:
        return
    done = float(qsize) / value
    rate = float(qsize) / ((time.time() - start) / 60)
    sys.stdout.write('\r{0} progress: [{1:30s}] {2:.2f}% {3:10.2f} {4}/min    '.format(
        sprefix, '>' * int(done * 30), done * 100, rate, rtext))
    sys.stdout.flush()

def main():
    """Main function
    Parameters:
        None
    Returns:
        Nothing
    Raises:
        ValueError for invalid arguments
    """
    # get command-line args
    args = parseargs()

    # check some parameters
    if len(args.localresource) < 1 or len(args.storageaccount) < 1 or \
            len(args.container) < 1:
        raise ValueError('invalid positional arguments')
    if len(args.blobep) < 1:
        raise ValueError('blob endpoint is invalid')
    if args.forceupload and args.forcedownload:
        raise ValueError('cannot force download and upload in the same command')
    if args.storageaccountkey is not None and args.saskey is not None:
        raise ValueError('cannot use both a sas key and storage account key')
    if args.timeout is not None and args.timeout <= 0:
        args.timeout = None

    # get key if we don't have a handle on one
    sms = None
    if args.saskey is not None:
        if len(args.saskey) < 1:
            raise ValueError('invalid sas key specified')
    elif args.storageaccountkey is None:
        if args.managementcert is not None and \
                args.subscriptionid is not None:
            # check to ensure management cert is valid
            if len(args.managementcert) == 0 or \
                    args.managementcert.split('.')[-1].lower() != 'pem':
                raise ValueError('management cert appears to be invalid')
            if args.managementep is None or len(args.managementep) == 0:
                raise ValueError('management endpoint is invalid')
            # expand management cert path out if contains ~
            args.managementcert = os.path.abspath(
                    os.path.expanduser(args.managementcert))
            # get sms reference
            sms = azure.servicemanagement.ServiceManagementService(
                    args.subscriptionid, args.managementcert,
                    args.managementep)
            # get keys
            service_keys = azure_request(sms.get_storage_account_keys,
                    timeout=args.timeout, service_name=args.storageaccount)
            args.storageaccountkey = service_keys.storage_service_keys.primary
        else:
            raise ValueError('management cert/subscription id not specified without storage account key')

    # check storage account key validity
    if args.storageaccountkey is not None and \
            len(args.storageaccountkey) < 1: # pragma: no cover
        raise ValueError('storage account key is invalid')

    if args.storageaccountkey is None and \
            args.saskey is None: # pragma: no cover
        raise ValueError('could not get reference to storage account key or sas key')

    # expand any paths
    args.localresource = os.path.expanduser(args.localresource)

    # sanitize remote file name
    if args.remoteresource:
        args.remoteresource = args.remoteresource.strip(os.path.sep)

    # set chunk size
    if args.chunksizebytes is None or args.chunksizebytes < 64:
        args.chunksizebytes = _MAX_BLOB_CHUNK_SIZE_BYTES

    # set blob ep
    blobep = None
    if sms:
        storage_acct = azure_request(sms.get_storage_account_properties,
                timeout=args.timeout, service_name=args.storageaccount)
        blobep = storage_acct.storage_service_properties.endpoints[0]
    else:
        blobep = 'https://{}.{}/'.format(args.storageaccount, args.blobep)
    if blobep is None or len(blobep) < 1:
        raise ValueError('invalid blob endpoint')

    # create master blob service
    blob_service = None
    if args.storageaccountkey:
        blob_service = azure.storage.BlobService(account_name=args.storageaccount,
                account_key=args.storageaccountkey)
    elif args.saskey:
        blob_service = SasBlobService(blobep, args.saskey, args.timeout)
        # disable container creation (not possible with SAS)
        args.createcontainer = False
    if blob_service is None:
        raise ValueError('blob_service is invalid')

    # check which way we're transfering
    xfertoazure = False
    if args.forceupload or (not args.forcedownload and \
            os.path.exists(args.localresource)):
        xfertoazure = True
    else:
        if args.remoteresource is None:
            raise ValueError('cannot download remote file if not specified')

    # print all parameters
    print('=======================================')
    print('      azure blob xfer parameters')
    print('=======================================')
    print('     subscription id: {}'.format(args.subscriptionid))
    print('     management cert: {}'.format(args.managementcert))
    print('  transfer direction: {}'.format('local->Azure' if xfertoazure else 'Azure->local'))
    print('      local resource: {}'.format(args.localresource))
    print('     remote resource: {}'.format(args.remoteresource))
    print('  max num of workers: {}'.format(args.numworkers))
    print('             timeout: {}'.format(args.timeout))
    print('     storage account: {}'.format(args.storageaccount))
    print('             use SAS: {}'.format(True if args.saskey else False))
    print('           container: {}'.format(args.container))
    print('  blob container URI: {}'.format(blobep + args.container))
    print('    compute file MD5: {}'.format(args.computefilemd5))
    print('  chunk size (bytes): {}'.format(args.chunksizebytes))
    print('    create container: {}'.format(args.createcontainer))
    print(' keep mismatched MD5: {}'.format(args.keepmismatchedmd5files))
    print('    recursive if dir: {}'.format(args.recursive))
    print(' keep root dir on up: {}'.format(args.keeprootdir))
    print('=======================================\n')

    # mark start time after init
    print('script start time: {}'.format(time.strftime("%Y-%m-%d %H:%M:%S")))
    start = time.time()

    # populate instruction queues
    allfilesize = 0
    storage_in_queue = queue.Queue()
    nstorageops = 0
    blockids = {}
    completed_blockids = {}
    filemap = {}
    md5map = {}
    filedesc = None
    if xfertoazure:
        if os.path.isdir(args.localresource):
            # mirror directory
            if args.recursive:
                for root, _, files in os.walk(args.localresource):
                    for dirfile in files:
                        fname = os.path.join(root, dirfile)
                        remotefname = fname.strip(os.path.sep)
                        if not args.keeprootdir:
                            remotefname = os.path.sep.join(remotefname.split(os.path.sep)[1:])
                        filesize, ops, md5digest, filedesc = \
                                generate_xferspec_upload(args, storage_in_queue,
                                        blockids, fname, remotefname, False)
                        completed_blockids[fname] = 0
                        md5map[fname] = md5digest
                        filemap[fname] = remotefname
                        allfilesize = allfilesize + filesize
                        nstorageops = nstorageops + ops
            else:
                for lfile in os.listdir(args.localresource):
                    fname = os.path.join(args.localresource, lfile)
                    if os.path.isdir(fname):
                        continue
                    remotefname = lfile if not args.keeprootdir else fname
                    remotefname = remotefname.strip(os.path.sep)
                    filesize, ops, md5digest, filedesc = \
                            generate_xferspec_upload(args, storage_in_queue,
                                    blockids, fname, remotefname, False)
                    completed_blockids[fname] = 0
                    md5map[fname] = md5digest
                    filemap[fname] = remotefname
                    allfilesize = allfilesize + filesize
                    nstorageops = nstorageops + ops
        else:
            # upload single file
            if not args.remoteresource:
                args.remoteresource = args.localresource
            filesize, nstorageops, md5digest, filedesc = \
                    generate_xferspec_upload(args, storage_in_queue,
                            blockids, args.localresource,
                            args.remoteresource, True)
            completed_blockids[args.localresource] = 0
            md5map[args.localresource] = md5digest
            filemap[args.localresource] = args.remoteresource
            allfilesize = allfilesize + filesize
        # create container if needed
        if args.createcontainer:
            try:
                azure_request(blob_service.create_container,
                        timeout=args.timeout, container_name=args.container,
                        fail_on_exist=False)
            except azure.WindowsAzureConflictError:
                pass
    else:
        bloblist = []
        if args.remoteresource == '.':
            print('attempting to copy entire container: {} to {}'.format(
                args.container, args.localresource))
            marker = None
            while True:
                result = azure_request(blob_service.list_blobs,
                        timeout=args.timeout, container_name=args.container,
                        marker=marker, maxresults=_MAX_LISTBLOBS_RESULTS)
                if not result: # pragma: no cover
                    break
                for blob in result:
                    blobprop = [blob.name, blob.properties.content_length]
                    try:
                        blobprop.append(blob.properties.content_md5)
                    except AttributeError: # pragma: no cover
                        blobprop.append(None)
                    bloblist.append(blobprop)
                marker = result.next_marker
                if marker is None or len(marker) < 1:
                    break
        else:
            bloblist.append([args.remoteresource, None, None])
        print('generating local directory structure and pre-allocating space')
        # make the localresource directory
        created_dirs = set()
        create_dir_ifnotexists(args.localresource)
        # generate xferspec for all blobs
        for blob, contentlength, contentmd5 in bloblist:
            localfile = os.path.join(args.localresource, blob)
            # create any subdirectories if required
            prevdir = args.localresource
            subdirs = localfile.split(os.path.sep)[1:-1]
            for dirname in subdirs:
                prevdir = os.path.join(prevdir, dirname)
                if not prevdir in created_dirs:
                    create_dir_ifnotexists(prevdir)
            # add instructions
            filesize, ops, md5digest, filedesc = \
                    generate_xferspec_download(blob_service, args,
                            storage_in_queue, localfile, blob, contentlength,
                            contentmd5, False)
            md5map[localfile] = md5digest
            filemap[localfile] = localfile + '.blobtmp'
            allfilesize = allfilesize + filesize
            nstorageops = nstorageops + ops

    if nstorageops == 0: # pragma: no cover
        print('detected no actions needed to be taken, exiting...')
        sys.exit(1)

    if xfertoazure:
        print('performing {} put blocks and {} put block lists'.format(
            nstorageops, len(blockids)))
    else:
        print('performing {} range-gets'.format(nstorageops))
    storage_out_queue = queue.Queue(nstorageops)
    maxworkers = min([args.numworkers, nstorageops])
    exc_list = []
    for _ in xrange(maxworkers):
        thr = BlobChunkWorker(exc_list, storage_in_queue, storage_out_queue,
                blob_service, args.timeout)
        thr.setDaemon(True)
        thr.start()

    done_ops = 0
    storage_start = time.time()
    progress_bar(args.progressbar, 'xfer',
            'blocks' if xfertoazure else 'range-gets',
            nstorageops, done_ops, storage_start)
    while True:
        if len(exc_list) > 0:
            for exc in exc_list:
                print(exc)
            sys.exit(1)
        _, localresource, _, _, _, _, _, _, _, _ = storage_out_queue.get()
        if xfertoazure:
            completed_blockids[localresource] = completed_blockids[localresource] + 1
            if completed_blockids[localresource] == len(blockids[localresource]):
                azure_request(blob_service.put_block_list,
                        timeout=args.timeout,
                        container_name=args.container,
                        blob_name=filemap[localresource],
                        block_list=blockids[localresource],
                        x_ms_blob_content_md5=md5map[localresource])
        done_ops = done_ops + 1
        progress_bar(args.progressbar, 'xfer',
                'blocks' if xfertoazure else 'range-gets',
                nstorageops, done_ops, storage_start)
        if done_ops == nstorageops:
            break
        time.sleep(0.01) # pragma: no cover
    endtime = time.time()
    if filedesc:
        filedesc.close()
    progress_bar(args.progressbar, 'xfer',
            'blocks' if xfertoazure else 'range-gets',
            nstorageops, done_ops, storage_start)
    print('\n\n{} MiB transfered, elapsed {} sec. Throughput = {} Mbit/sec'.format(
        allfilesize / 1048576.0, endtime - storage_start,
        (8.0 * allfilesize / 1048576.0) / (endtime - storage_start)))

    if not xfertoazure:
        if args.computefilemd5:
            print('\nchecking md5 hashes and ', end='')
        print('finalizing files')
        for localfile in filemap:
            tmpfilename = filemap[localfile]
            finalizefile = True
            # compare md5 hash
            if args.computefilemd5:
                lmd5 = compute_md5_for_file_asbase64(tmpfilename)
                print('{}: local {} remote {} ->'.format(
                    localfile, lmd5, md5map[localfile]), end='')
                if lmd5 != md5map[localfile]:
                    print('MISMATCH')
                    if not args.keepmismatchedmd5files:
                        finalizefile = False
                else:
                    print('match')
            if finalizefile:
                # move tmp file to real file
                os.rename(tmpfilename, localfile)
            else:
                os.remove(localfile)

    print('\nscript elapsed time: {} sec'.format(time.time() - start))
    print('script end time: {}'.format(time.strftime("%Y-%m-%d %H:%M:%S")))

def parseargs(): # pragma: no cover
    """Sets up command-line arguments and parser
    Parameters:
        Nothing
    Returns:
        Parsed command line arguments
    Raises:
        Nothing
    """
    parser = argparse.ArgumentParser(description='Transfer block blobs to/from Azure storage')
    parser.set_defaults(blobep=_DEFAULT_BLOB_ENDPOINT,
            chunksizebytes=_MAX_BLOB_CHUNK_SIZE_BYTES, computefilemd5=True,
            createcontainer=True, managementep=_DEFAULT_MANAGEMENT_ENDPOINT,
            progressbar=True, recursive=True, timeout=None)
    parser.add_argument('storageaccount',
            help='name of storage account')
    parser.add_argument('container',
            help='name of blob container')
    parser.add_argument('localresource',
            help='name of the local file or directory if mirroring')
    parser.add_argument('--blobep',
            help='blob storage endpoint [{}]'.format(_DEFAULT_BLOB_ENDPOINT))
    parser.add_argument('--chunksizebytes', type=int,
            help='maximum chunk size to transfer in bytes [{}]'.format(
                _MAX_BLOB_CHUNK_SIZE_BYTES))
    parser.add_argument('--forcedownload', action='store_true',
            help='force download from Azure')
    parser.add_argument('--forceupload', action='store_true',
            help='force upload to Azure')
    parser.add_argument('--keepmismatchedmd5files', action='store_true',
            help='keep files with MD5 mismatches')
    parser.add_argument('--keeprootdir', action='store_true',
            help='keeps the root directory as a virtual directory in directory upload')
    parser.add_argument('--managementcert',
            help='path to management certificate .pem file')
    parser.add_argument('--managementep',
            help='management endpoint [{}]'.format(_DEFAULT_MANAGEMENT_ENDPOINT))
    parser.add_argument('--no-computefilemd5', dest='computefilemd5', action='store_false',
            help='do not compute file MD5 and either upload as metadata or validate on download')
    parser.add_argument('--no-createcontainer', dest='createcontainer', action='store_false',
            help='do not create container if it does not exist')
    parser.add_argument('--no-progressbar', dest='progressbar', action='store_false',
            help='disable progress bar')
    parser.add_argument('--no-recursive', dest='recursive', action='store_false',
            help='do not mirror local directory recursively')
    parser.add_argument('--numworkers', type=int,
            default=_DEFAULT_MAX_STORAGEACCOUNT_WORKERS,
            help='max number of workers [{}]'.format(
                _DEFAULT_MAX_STORAGEACCOUNT_WORKERS))
    parser.add_argument('--remoteresource',
            help='name of remote resource on Azure storage. "."=container copy recursive implied')
    parser.add_argument('--saskey',
            help='SAS key to use, if recursive upload or container download, this must be a container SAS')
    parser.add_argument('--storageaccountkey',
            help='storage account shared key')
    parser.add_argument('--subscriptionid',
            help='subscription id')
    parser.add_argument('--timeout', type=float,
            help='timeout in seconds for any operation to complete')
    parser.add_argument('--version', action='version', version=_SCRIPT_VERSION)
    return parser.parse_args()

if __name__ == '__main__':
    main()

