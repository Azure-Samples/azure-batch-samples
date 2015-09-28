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
Code sample to show data transfer to/from Azure blob storage

Notes:
- Script does not take any leases on blobs or containers. It is up to
  the user to ensure that blobs are not modified while download/uploads
  are being performed.
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
- In order to skip download/upload matching files via MD5, the
  computefilemd5 flag must be enabled (it is enabled by default).
- When uploading files as page blobs, the content is page boundary
  byte-aligned. The MD5 for the blob is computed using the final aligned
  data if the source is not page boundary byte-aligned. This enables these
  page blobs or files to be skipped during subsequent download or upload,
  if the skiponmatch parameter is enabled.

TODO list:
- remove dependency on azure python packages
- convert from synchronous multithreading to asyncio/trollius
"""

# pylint: disable=R0913,R0914

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
except ImportError:  # pragma: no cover
    import Queue as queue
# pylint: enable=F0401
import random
import socket
import sys
import threading
import time
import xml.etree.ElementTree as ET
# non-stdlib imports
try:
    import azure
    import azure.common
except ImportError:  # pragma: no cover
    pass
try:
    import azure.servicemanagement
except ImportError:  # pragma: no cover
    pass
try:
    import azure.storage.blob
except ImportError:  # pragma: no cover
    pass
try:
    import requests
except ImportError:  # pragma: no cover
    pass

# remap keywords for Python3
# pylint: disable=W0622,C0103
try:
    xrange
except NameError:  # pragma: no cover
    xrange = range
try:
    long
except NameError:  # pragma: no cover
    long = int
# pylint: enable=W0622,C0103

# global defines
_SCRIPT_VERSION = '0.9.9.5'
_DEFAULT_MAX_STORAGEACCOUNT_WORKERS = 64
_MAX_BLOB_CHUNK_SIZE_BYTES = 4194304
_EMPTY_MAX_PAGE_SIZE_MD5 = 'tc+p1sj+vWGPkawoQ9UKHA=='
_MAX_LISTBLOBS_RESULTS = 1000
_PAGEBLOB_BOUNDARY = 512
_DEFAULT_BLOB_ENDPOINT = 'blob.core.windows.net'
_DEFAULT_MANAGEMENT_ENDPOINT = 'management.core.windows.net'
_PY2 = sys.version_info.major == 2


class SasBlobList(object):
    """Sas Blob listing object"""
    def __init__(self):
        """Ctor for SasBlobList"""
        self.blobs = []
        self.next_marker = None

    def __iter__(self):
        """Iterator"""
        return iter(self.blobs)

    def __len__(self):
        """Length"""
        return len(self.blobs)

    def __getitem__(self, index):
        """Accessor"""
        return self.blobs[index]

    def add_blob(self, name, content_length, content_md5, blobtype):
        """Adds a blob to the list
        Parameters:
            name - blob name
            content_length - content length
            content_md5 - content md5
            blobtype - blob type
        Returns:
            Nothing
        Raises:
            Nothing
        """
        obj = type('bloblistobject', (object,), {})
        obj.name = name
        obj.properties = type('properties', (object,), {})
        obj.properties.content_length = content_length
        if content_md5 is not None and len(content_md5) > 0:
            obj.properties.content_md5 = content_md5
        else:
            obj.properties.content_md5 = None
        obj.properties.blobtype = blobtype
        self.blobs.append(obj)

    def set_next_marker(self, marker):
        """Set the continuation token
        Parameters:
            marker - next marker
        Returns:
            Nothing
        Raises:
            Nothing
        """
        if marker is not None and len(marker) > 0:
            self.next_marker = marker


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
        # normalize sas key
        if saskey[0] != '?':
            self.saskey = '?' + saskey
        else:
            self.saskey = saskey
        self.timeout = timeout

    def _parse_blob_list_xml(self, content):
        """Parse blob list in xml format to an attribute-based object
        Parameters:
            content - http response content in xml
        Returns:
            attribute-based object
        Raises:
            No special exception handling
        """
        result = SasBlobList()
        root = ET.fromstring(content)
        blobs = root.find('Blobs')
        for blob in blobs.iter('Blob'):
            name = blob.find('Name').text
            props = blob.find('Properties')
            cl = long(props.find('Content-Length').text)
            md5 = props.find('Content-MD5').text
            bt = props.find('BlobType').text
            result.add_blob(name, cl, md5, bt)
        try:
            result.set_next_marker(root.find('NextMarker').text)
        except Exception:
            pass
        return result

    def list_blobs(
            self, container_name, marker=None,
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
        reqparams = {
            'restype': 'container',
            'comp': 'list',
            'maxresults': str(maxresults)}
        if marker is not None:
            reqparams['marker'] = marker
        response = azure_request(
            requests.get, url=url, params=reqparams, timeout=self.timeout)
        response.raise_for_status()
        if response.status_code != 200:
            raise IOError(
                'incorrect status code returned for list_blobs: {}'.format(
                    response.status_code))
        return self._parse_blob_list_xml(response.content)

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
            blobep=self.blobep, container_name=container_name,
            blob_name=blob_name, saskey=self.saskey)
        reqheaders = {'x-ms-range': x_ms_range}
        response = azure_request(
            requests.get, url=url, headers=reqheaders, timeout=self.timeout)
        response.raise_for_status()
        if response.status_code != 200 and response.status_code != 206:
            raise IOError(
                'incorrect status code returned for get_blob: {}'.format(
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
            blobep=self.blobep, container_name=container_name,
            blob_name=blob_name, saskey=self.saskey)
        response = azure_request(
            requests.head, url=url, timeout=self.timeout)
        response.raise_for_status()
        if response.status_code != 200:
            raise IOError('incorrect status code returned for '
                          'get_blob_properties: {}'.format(
                              response.status_code))
        return response.headers

    def put_blob(
            self, container_name, blob_name, blob, x_ms_blob_type,
            x_ms_blob_content_md5, x_ms_blob_content_length):
        """Put blob for initializing page blobs
        Parameters:
            container_name - container name
            blob_name - name of blob
            blob - should be None for PageBlob (unused)
            x_ms_blob_type - should be 'PageBlob' or 'BlockBlob'
            x_ms_blob_content_md5 - blob MD5 hash
            x_ms_blob_content_length - content length aligned to
                512-byte boundary if PageBlob
        Returns:
            blob content
        Raises:
            IOError if unexpected status code
        """
        url = '{blobep}{container_name}/{blob_name}{saskey}'.format(
            blobep=self.blobep, container_name=container_name,
            blob_name=blob_name, saskey=self.saskey)
        reqheaders = {
            'x-ms-blob-type': x_ms_blob_type}
        if x_ms_blob_type == 'PageBlob':
            reqheaders['x-ms-blob-content-length'] = str(
                x_ms_blob_content_length)
        if x_ms_blob_content_md5 is not None:
            reqheaders['x-ms-blob-content-md5'] = x_ms_blob_content_md5
        response = azure_request(
            requests.put, url=url, headers=reqheaders, timeout=self.timeout)
        response.raise_for_status()
        if response.status_code != 201:
            raise IOError(
                'incorrect status code returned for put_blob: {}'.format(
                    response.status_code))
        return response.content

    def put_page(
            self, container_name, blob_name, page, x_ms_range,
            x_ms_page_write, content_md5):
        """Put page for page blob
        Parameters:
            container_name - container name
            blob_name - name of blob
            page - page data
            x_ms_range - byte range
            x_ms_page_write - page write option
            content_md5 - md5 hash for page data
        Returns:
            Nothing
        Raises:
            IOError if unexpected status code
        """
        url = '{blobep}{container_name}/{blob_name}{saskey}'.format(
            blobep=self.blobep, container_name=container_name,
            blob_name=blob_name, saskey=self.saskey)
        reqheaders = {
            'x-ms-range': x_ms_range,
            'x-ms-page-write': x_ms_page_write,
            'Content-MD5': content_md5}
        reqparams = {'comp': 'page'}
        response = azure_request(
            requests.put, url=url, params=reqparams, headers=reqheaders,
            data=page, timeout=self.timeout)
        response.raise_for_status()
        if response.status_code != 201:
            raise IOError(
                'incorrect status code returned for put_page: {}'.format(
                    response.status_code))

    def put_block(
            self, container_name, blob_name, block, blockid, content_md5):
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
            blobep=self.blobep, container_name=container_name,
            blob_name=blob_name, saskey=self.saskey)
        reqheaders = {'Content-MD5': content_md5}
        reqparams = {'comp': 'block', 'blockid': blockid}
        response = azure_request(
            requests.put, url=url, params=reqparams, headers=reqheaders,
            data=block, timeout=self.timeout)
        response.raise_for_status()
        if response.status_code != 201:
            raise IOError(
                'incorrect status code returned for put_block: {}'.format(
                    response.status_code))

    def put_block_list(
            self, container_name, blob_name, block_list,
            x_ms_blob_content_md5):
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
            blobep=self.blobep, container_name=container_name,
            blob_name=blob_name, saskey=self.saskey)
        reqheaders = {'x-ms-blob-content-md5': x_ms_blob_content_md5}
        reqparams = {'comp': 'blocklist'}
        body = ['<?xml version="1.0" encoding="utf-8"?><BlockList>']
        for block in block_list:
            body.append('<Latest>{}</Latest>'.format(block))
        body.append('</BlockList>')
        response = azure_request(
            requests.put, url=url, params=reqparams, headers=reqheaders,
            data=''.join(body), timeout=self.timeout)
        response.raise_for_status()
        if response.status_code != 201:
            raise IOError(
                'incorrect status code returned for put_block_list: {}'.format(
                    response.status_code))

    def set_blob_properties(
            self, container_name, blob_name, x_ms_blob_content_md5):
        """Sets blob properties (MD5 only)
        Parameters:
            container_name - container name
            blob_name - name of blob
            x_ms_blob_content_md5 - md5 hash for blob
        Returns:
            Nothing
        Raises:
            IOError if unexpected status code
        """
        url = '{blobep}{container_name}/{blob_name}{saskey}'.format(
            blobep=self.blobep, container_name=container_name,
            blob_name=blob_name, saskey=self.saskey)
        reqheaders = {'x-ms-blob-content-md5': x_ms_blob_content_md5}
        reqparams = {'comp': 'properties'}
        response = azure_request(
            requests.put, url=url, params=reqparams, headers=reqheaders,
            timeout=self.timeout)
        response.raise_for_status()
        if response.status_code != 200:
            raise IOError('incorrect status code returned for '
                          'set_blob_properties: {}'.format(
                              response.status_code))


class BlobChunkWorker(threading.Thread):
    """Chunk worker for a Blob"""
    def __init__(
            self, exc, s_in_queue, s_out_queue, args, blob_service,
            xfertoazure):
        """Blob Chunk worker Thread ctor
        Parameters:
            exc - exception list
            s_in_queue - storage in queue
            s_out_queue - storage out queue
            args - program arguments
            blob_service - blob service
            xfertoazure - xfer to azure (direction)
        Returns:
            Nothing
        Raises:
            Nothing
        """
        threading.Thread.__init__(self)
        self._exc = exc
        self._in_queue = s_in_queue
        self._out_queue = s_out_queue
        self._pageblob = args.pageblob
        self._autovhd = args.autovhd
        self.blob_service = blob_service
        self.timeout = args.timeout
        self.xfertoazure = xfertoazure

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
            localresource, container, remoteresource, blockid, \
                offset, bytestoxfer, flock, filedesc = self._in_queue.get()
            try:
                if self.xfertoazure:
                    # upload block/page
                    self.putblobdata(
                        localresource, container, remoteresource, blockid,
                        offset, bytestoxfer, flock, filedesc)
                else:
                    # download range
                    self.getblobrange(
                        localresource, container, remoteresource, offset,
                        bytestoxfer, flock, filedesc)
                # pylint: disable=W0703
            except Exception as exc:
                # pylint: enable=W0703
                self._exc.append(exc)
            self._out_queue.put(localresource)
            if len(self._exc) > 0:
                break

    def putblobdata(
            self, localresource, container, remoteresource, blockid, offset,
            bytestoxfer, flock, filedesc):
        """Puts data (blob or page) into Azure storage
        Parameters:
            localresource - name of local resource
            container - blob container
            remoteresource - name of remote resource
            blockid - block id (ignored for page blobs)
            offset - file offset
            bytestoxfer - number of bytes to xfer
            flock - file lock
            filedesc - file handle
        Returns:
            Nothing
        Raises:
            IOError if file cannot be read
        """
        # if bytestoxfer is zero, then we're transferring a zero-byte
        # file, use put blob instead of page/block ops
        if bytestoxfer == 0:
            contentmd5 = compute_md5_for_data_asbase64(b'')
            if as_page_blob(self._pageblob, self._autovhd, localresource):
                blob_type = 'PageBlob'
            else:
                blob_type = 'BlockBlob'
            azure_request(
                self.blob_service.put_blob, container_name=container,
                blob_name=remoteresource, blob=None, x_ms_blob_type=blob_type,
                x_ms_blob_content_md5=contentmd5,
                x_ms_blob_content_length=bytestoxfer)
            return
        # read the file at specified offset, must take lock
        data = None
        with flock:
            closefd = False
            if not filedesc:
                filedesc = open(localresource, 'rb')
                closefd = True
            filedesc.seek(offset, 0)
            data = filedesc.read(bytestoxfer)
            if closefd:
                filedesc.close()
        if not data:
            raise IOError('could not read {}: {} -> {}'.format(
                localresource, offset, offset + bytestoxfer))
        # issue REST put
        if as_page_blob(self._pageblob, self._autovhd, localresource):
            aligned = page_align_content_length(bytestoxfer)
            # fill data to boundary
            if aligned != bytestoxfer:
                data = data.ljust(aligned, b'\0')
            # compute page md5
            contentmd5 = compute_md5_for_data_asbase64(data)
            # check if this page is empty
            if contentmd5 == _EMPTY_MAX_PAGE_SIZE_MD5:
                return
            elif len(data) != _MAX_BLOB_CHUNK_SIZE_BYTES:
                data_chk = b'\0' * len(data)
                data_chk_md5 = compute_md5_for_data_asbase64(data_chk)
                del data_chk
                if data_chk_md5 == contentmd5:
                    return
                del data_chk_md5
            # upload page range
            rangestr = 'bytes={}-{}'.format(offset, offset + aligned - 1)
            azure_request(
                self.blob_service.put_page, container_name=container,
                blob_name=remoteresource, page=data, x_ms_range=rangestr,
                x_ms_page_write='update', content_md5=contentmd5,
                timeout=self.timeout)
        else:
            # compute block md5
            contentmd5 = compute_md5_for_data_asbase64(data)
            azure_request(
                self.blob_service.put_block, container_name=container,
                blob_name=remoteresource, block=data, blockid=blockid,
                content_md5=contentmd5, timeout=self.timeout)
        del data

    def getblobrange(
            self, localresource, container, remoteresource, offset,
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
        rangestr = 'bytes={}-{}'.format(offset, offset + bytestoxfer)
        blobdata = azure_request(
            self.blob_service.get_blob, timeout=self.timeout,
            container_name=container, blob_name=remoteresource,
            x_ms_range=rangestr)
        with flock:
            closefd = False
            if not filedesc:
                filedesc = open(localresource, 'r+b')
                closefd = True
            filedesc.seek(offset, 0)
            filedesc.write(blobdata)
            if closefd:
                filedesc.close()
        del blobdata


def azure_request(req, timeout=None, *args, **kwargs):
    """Wrapper method to issue/retry requests to Azure, works with both
    the Azure Python SDK and Requests
    Parameters:
        req - request to issue
        timeout - timeout in seconds
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
        except requests.Timeout as exc:
            pass
        except requests.HTTPError as exc:
            if exc.response.status_code < 500 or \
                    exc.response.status_code == 501 or \
                    exc.response.status_code == 505:
                raise
        except socket.error as exc:
            if exc.errno != errno.ETIMEDOUT and \
                    exc.errno != errno.ECONNRESET and \
                    exc.errno != errno.ECONNREFUSED and \
                    exc.errno != errno.ECONNABORTED and \
                    exc.errno != errno.ENETRESET:
                raise
        except Exception as exc:
            try:
                if not ('TooManyRequests' in exc.message or
                        'InternalError' in exc.message or
                        'ServerBusy' in exc.message or
                        'OperationTimedOut' in exc.message):
                    raise
            except AttributeError:
                raise exc
        if timeout is not None and time.clock() - start > timeout:
            raise IOError(
                'waited for {} for request {}, exceeded timeout of {}'.format(
                    time.clock() - start, req.__name__, timeout))
        time.sleep(random.randint(2, 5))


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
            raise  # pragma: no cover


def compute_md5_for_file_asbase64(filename, pagealign=False, blocksize=65536):
    """Compute MD5 hash for file and encode as Base64
    Parameters:
        filename - filename to compute md5
        pagealign - align bytes for page boundary
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
            buflen = len(buf)
            if pagealign and buflen < blocksize:
                aligned = page_align_content_length(buflen)
                if aligned != buflen:
                    buf = buf.ljust(aligned, b'\0')
            hasher.update(buf)
        if _PY2:
            return base64.b64encode(hasher.digest())
        else:
            return str(base64.b64encode(hasher.digest()), 'ascii')


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
    if _PY2:
        return base64.b64encode(hasher.digest())
    else:
        return str(base64.b64encode(hasher.digest()), 'ascii')


def page_align_content_length(length):
    """Compute page boundary alignment
    Parameters:
        length - content length
    Returns:
        aligned byte boundary
    Raises:
        Nothing
    """
    mod = length % _PAGEBLOB_BOUNDARY
    if mod != 0:
        return length + (_PAGEBLOB_BOUNDARY - mod)
    return length


def as_page_blob(pageblob, autovhd, name):
    """Determines if the file should be a pageblob
    Parameters:
        pageblob - pageblob arg
        autovhd - autovhd arg
        name - file name
    Returns:
        True if file should be a pageblob
    Raises:
        Nothing
    """
    if pageblob or (autovhd and name.lower().endswith('.vhd')):
        return True
    return False


def get_blob_listing(blob_service, args):
    """Convenience method for generating a blob listing of a container
    Parameters:
        blob_service - blob service
        args - program arguments
    Returns:
        dictionary of blob -> list [content length, content md5]
    Raises:
        Nothing
    """
    marker = None
    blobdict = {}
    while True:
        result = azure_request(
            blob_service.list_blobs, timeout=args.timeout,
            container_name=args.container, marker=marker,
            maxresults=_MAX_LISTBLOBS_RESULTS)
        for blob in result:
            blobdict[blob.name] = [
                blob.properties.content_length, blob.properties.content_md5]
        marker = result.next_marker
        if marker is None or len(marker) < 1:
            break
    return blobdict


def generate_xferspec_download(
        blob_service, args, storage_in_queue, localfile, remoteresource,
        contentlength, contentmd5, addfd):
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
        result = azure_request(
            blob_service.get_blob_properties, timeout=args.timeout,
            container_name=args.container, blob_name=remoteresource)
        if not result:
            raise ValueError(
                'unexpected result for get_blob_properties is None')
        if 'content-md5' in result:
            contentmd5 = result['content-md5']
        contentlength = long(result['content-length'])
    if contentlength < 0:
        raise ValueError(
            'contentlength is invalid for {}'.format(remoteresource))
    print('remote file {} length: {} bytes, md5: {}'.format(
        remoteresource, contentlength, contentmd5))
    # check if download is needed
    if args.skiponmatch and contentmd5 is not None and \
            os.path.exists(localfile):
        lmd5 = compute_md5_for_file_asbase64(localfile)
        print('{}: local {} remote {} ->'.format(
            localfile, lmd5, contentmd5), end='')
        if lmd5 != contentmd5:
            print('MISMATCH, re-downloading')
        else:
            print('match, skipping download')
            return None, None, None, None
    tmpfilename = localfile + '.blobtmp'
    nchunks = contentlength // args.chunksizebytes
    currfileoffset = 0
    nstorageops = 0
    flock = threading.Lock()
    filedesc = None
    # preallocate file
    with flock:
        filedesc = open(tmpfilename, 'wb')
        if contentlength > 0:
            filedesc.seek(contentlength - 1)
            filedesc.write(b'\0')
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
        xferspec = [tmpfilename, args.container, remoteresource, None,
                    currfileoffset, chunktoadd - 1, flock, filedesc]
        currfileoffset = currfileoffset + chunktoadd
        nstorageops = nstorageops + 1
        storage_in_queue.put(xferspec)
        if currfileoffset >= contentlength:
            break
    return contentlength, nstorageops, contentmd5, filedesc


def generate_xferspec_upload(
        args, storage_in_queue, blobskipdict, blockids, localfile,
        remoteresource, addfd):
    """Generate an xferspec for upload
    Parameters:
        args - program arguments
        storage_in_queue - storage input queue
        blobskipdict - blob skip dictionary
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
        md5digest = compute_md5_for_file_asbase64(
            localfile, as_page_blob(args.pageblob, args.autovhd, localfile))
        print('{} md5: {}'.format(localfile, md5digest))
        # check if upload is needed
        if args.skiponmatch and remoteresource in blobskipdict:
            print('{}->{}: local {} remote {} ->'.format(
                localfile, remoteresource, md5digest,
                blobskipdict[remoteresource][1]), end='')
            if md5digest != blobskipdict[remoteresource][1]:
                print('MISMATCH, re-uploading')
            else:
                print('match, skipping upload')
                return None, 0, None, None
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
        xferspec = [localfile, args.container, remoteresource, blockid,
                    currfileoffset, chunktoadd, flock, filedesc]
        currfileoffset = currfileoffset + chunktoadd
        nstorageops = nstorageops + 1
        storage_in_queue.put(xferspec)
        if currfileoffset >= filesize:
            break
    return filesize, nstorageops, md5digest, filedesc


def apply_file_collation(args, fname, apply_keeproot=False):
    """Apply collation path to a remote filename
    Parameters:
        args - arguments
        fname - file name
        apply_keeproot - apply keep rootdir transformation
    Returns:
        remote filename
    Raises:
        No special exception handling
    """
    remotefname = fname.strip(os.path.sep)
    if apply_keeproot and not args.keeprootdir:
        rtmp = remotefname.split(os.path.sep)
        if len(rtmp) > 1:
            remotefname = os.path.sep.join(rtmp[1:])
    if args.collate is not None:
        remotefname = remotefname.split(
            os.path.sep)[-1]
        if args.collate != '.':
            remotefname = os.path.sep.join(
                (args.collate, remotefname))
    return remotefname


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
    if args.upload and args.download:
        raise ValueError('cannot force transfer direction of download '
                         'and upload in the same command')
    if args.storageaccountkey is not None and args.saskey is not None:
        raise ValueError('cannot use both a sas key and storage account key')
    if args.pageblob and args.autovhd:
        raise ValueError('cannot specify both pageblob and autovhd parameters')
    if args.keeprootdir and args.collate is not None:
        raise ValueError('cannot specify both keeprootdir and collate path')
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
            args.managementcert = os.path.abspath(args.managementcert)
            # get sms reference
            sms = azure.servicemanagement.ServiceManagementService(
                args.subscriptionid, args.managementcert, args.managementep)
            # get keys
            service_keys = azure_request(
                sms.get_storage_account_keys, timeout=args.timeout,
                service_name=args.storageaccount)
            args.storageaccountkey = service_keys.storage_service_keys.primary
        else:
            raise ValueError('management cert/subscription id not '
                             'specified without storage account key')

    # check storage account key validity
    if args.storageaccountkey is not None and \
            len(args.storageaccountkey) < 1:
        raise ValueError('storage account key is invalid')

    # set valid num workers
    if args.numworkers < 1:
        args.numworkers = 1

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
        storage_acct = azure_request(
            sms.get_storage_account_properties, timeout=args.timeout,
            service_name=args.storageaccount)
        blobep = storage_acct.storage_service_properties.endpoints[0]
    else:
        blobep = 'https://{}.{}/'.format(args.storageaccount, args.blobep)
    if blobep is None or len(blobep) < 1:
        raise ValueError('invalid blob endpoint')

    # create master blob service
    blob_service = None
    if args.storageaccountkey:
        if args.blobep[0] == '.':
            host_base = args.blobep
        else:
            host_base = '.' + args.blobep
        if args.timeout is None:
            blob_service = azure.storage.blob.BlobService(
                account_name=args.storageaccount,
                account_key=args.storageaccountkey,
                host_base=host_base)
        else:
            blob_service = azure.storage.blob.BlobService(
                account_name=args.storageaccount,
                account_key=args.storageaccountkey,
                host_base=host_base, timeout=args.timeout)
    elif args.saskey:
        blob_service = SasBlobService(blobep, args.saskey, args.timeout)
        # disable container creation (not possible with SAS)
        args.createcontainer = False
    if blob_service is None:
        raise ValueError('blob_service is invalid')

    # check which way we're transfering
    xfertoazure = False
    if args.upload or (not args.download and
                       os.path.exists(args.localresource)):
        xfertoazure = True
    else:
        if args.remoteresource is None:
            raise ValueError('cannot download remote file if not specified')

    # print all parameters
    print('======================================')
    print(' azure blobxfer parameters [v{}]'.format(_SCRIPT_VERSION))
    print('======================================')
    print('     subscription id: {}'.format(args.subscriptionid))
    print('     management cert: {}'.format(args.managementcert))
    print('  transfer direction: {}'.format(
        'local->Azure' if xfertoazure else 'Azure->local'))
    print('      local resource: {}'.format(args.localresource))
    print('     remote resource: {}'.format(args.remoteresource))
    print('  max num of workers: {}'.format(args.numworkers))
    print('             timeout: {}'.format(args.timeout))
    print('     storage account: {}'.format(args.storageaccount))
    print('             use SAS: {}'.format(True if args.saskey else False))
    print(' upload as page blob: {}'.format(args.pageblob))
    print(' auto vhd->page blob: {}'.format(args.autovhd))
    print('           container: {}'.format(args.container))
    print('  blob container URI: {}'.format(blobep + args.container))
    print('    compute file MD5: {}'.format(args.computefilemd5))
    print('   skip on MD5 match: {}'.format(args.skiponmatch))
    print('  chunk size (bytes): {}'.format(args.chunksizebytes))
    print('    create container: {}'.format(args.createcontainer))
    print(' keep mismatched MD5: {}'.format(args.keepmismatchedmd5files))
    print('    recursive if dir: {}'.format(args.recursive))
    print(' keep root dir on up: {}'.format(args.keeprootdir))
    print('          collate to: {}'.format(
        args.collate if args.collate is not None else 'disabled'))
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
    filesizes = {}
    md5map = {}
    filedesc = None
    if xfertoazure:
        # if skiponmatch is enabled, list blobs first and check
        if args.skiponmatch:
            blobskipdict = get_blob_listing(blob_service, args)
        else:
            blobskipdict = {}
        if os.path.isdir(args.localresource):
            # mirror directory
            if args.recursive:
                for root, _, files in os.walk(args.localresource):
                    for dirfile in files:
                        fname = os.path.join(root, dirfile)
                        remotefname = apply_file_collation(
                            args, fname, apply_keeproot=True)
                        filesize, ops, md5digest, filedesc = \
                            generate_xferspec_upload(
                                args, storage_in_queue, blobskipdict,
                                blockids, fname, remotefname, False)
                        if filesize is not None:
                            completed_blockids[fname] = 0
                            md5map[fname] = md5digest
                            filemap[fname] = remotefname
                            filesizes[fname] = filesize
                            allfilesize = allfilesize + filesize
                            nstorageops = nstorageops + ops
            else:
                # copy just directory contents, non-recursively
                for lfile in os.listdir(args.localresource):
                    fname = os.path.join(args.localresource, lfile)
                    if os.path.isdir(fname):
                        continue
                    remotefname = apply_file_collation(
                        args, lfile if not args.keeprootdir else fname,
                        apply_keeproot=False)
                    filesize, ops, md5digest, filedesc = \
                        generate_xferspec_upload(
                            args, storage_in_queue, blobskipdict,
                            blockids, fname, remotefname, False)
                    if filesize is not None:
                        completed_blockids[fname] = 0
                        md5map[fname] = md5digest
                        filemap[fname] = remotefname
                        filesizes[fname] = filesize
                        allfilesize = allfilesize + filesize
                        nstorageops = nstorageops + ops
        else:
            # upload single file
            if not args.remoteresource:
                args.remoteresource = args.localresource
            args.remoteresource = apply_file_collation(
                args, args.remoteresource, apply_keeproot=False)
            filesize, nstorageops, md5digest, filedesc = \
                generate_xferspec_upload(
                    args, storage_in_queue, blobskipdict, blockids,
                    args.localresource, args.remoteresource, True)
            if filesize is not None:
                completed_blockids[args.localresource] = 0
                md5map[args.localresource] = md5digest
                filemap[args.localresource] = args.remoteresource
                filesizes[args.localresource] = filesize
                allfilesize = allfilesize + filesize
        # create container if needed
        if args.createcontainer:
            try:
                azure_request(
                    blob_service.create_container, timeout=args.timeout,
                    container_name=args.container, fail_on_exist=False)
            except azure.common.AzureConflictHttpError:
                pass
        # initialize page blobs
        if args.pageblob or args.autovhd:
            print('initializing page blobs')
            for key in filemap:
                if as_page_blob(args.pageblob, args.autovhd, key):
                    blob_service.put_blob(
                        container_name=args.container, blob_name=filemap[key],
                        blob=None, x_ms_blob_type='PageBlob',
                        x_ms_blob_content_md5=None,
                        x_ms_blob_content_length=page_align_content_length(
                            filesizes[key]))
    else:
        if args.remoteresource == '.':
            print('attempting to copy entire container: {} to {}'.format(
                args.container, args.localresource))
            blobdict = get_blob_listing(blob_service, args)
        else:
            blobdict = {args.remoteresource: [None, None]}
        print('generating local directory structure and pre-allocating space')
        # make the localresource directory
        created_dirs = set()
        create_dir_ifnotexists(args.localresource)
        created_dirs.add(args.localresource)
        # generate xferspec for all blobs
        for blob in blobdict:
            if args.collate is not None:
                localfile = os.path.join(
                    args.localresource, args.collate, blob)
            else:
                localfile = os.path.join(args.localresource, blob)
            # create any subdirectories if required
            localdir = os.path.dirname(localfile)
            if localdir not in created_dirs:
                create_dir_ifnotexists(localdir)
                created_dirs.add(localdir)
            # add instructions
            filesize, ops, md5digest, filedesc = \
                generate_xferspec_download(
                    blob_service, args, storage_in_queue, localfile,
                    blob, blobdict[blob][0], blobdict[blob][1], False)
            if filesize is not None:
                md5map[localfile] = md5digest
                filemap[localfile] = localfile + '.blobtmp'
                allfilesize = allfilesize + filesize
                nstorageops = nstorageops + ops
        del created_dirs

    if nstorageops == 0:
        print('detected no actions needed to be taken, exiting...')
        sys.exit(0)

    if xfertoazure:
        # count number of empty files
        emptyfiles = 0
        for fsize in filesizes.items():
            if fsize[1] == 0:
                emptyfiles += 1
        print('detected {} empty files to upload'.format(emptyfiles))
        if args.pageblob:
            print('performing {} put pages/blobs and {} set blob '
                  'properties'.format(
                      nstorageops, len(blockids) - emptyfiles))
            progress_text = 'pages'
        elif args.autovhd:
            print('performing {} mixed page/block operations with {} '
                  'finalizing ops'.format(
                      nstorageops, len(blockids) - emptyfiles))
            progress_text = 'chunks'
        else:
            print('performing {} put blocks/blobs and {} put block '
                  'lists'.format(
                      nstorageops, len(blockids) - emptyfiles))
            progress_text = 'blocks'
    else:
        print('performing {} range-gets'.format(nstorageops))
        progress_text = 'range-gets'
    storage_out_queue = queue.Queue(nstorageops)
    maxworkers = min((args.numworkers, nstorageops))
    exc_list = []
    for _ in xrange(maxworkers):
        thr = BlobChunkWorker(
            exc_list, storage_in_queue, storage_out_queue, args, blob_service,
            xfertoazure)
        thr.setDaemon(True)
        thr.start()

    done_ops = 0
    storage_start = time.time()
    progress_bar(
        args.progressbar, 'xfer', progress_text, nstorageops,
        done_ops, storage_start)
    while True:
        localresource = storage_out_queue.get()
        if len(exc_list) > 0:
            for exc in exc_list:
                print(exc)
            sys.exit(1)
        if xfertoazure:
            completed_blockids[localresource] = completed_blockids[
                localresource] + 1
            if completed_blockids[localresource] == len(
                    blockids[localresource]):
                if as_page_blob(args.pageblob, args.autovhd, localresource):
                    if args.computefilemd5:
                        azure_request(
                            blob_service.set_blob_properties,
                            timeout=args.timeout,
                            container_name=args.container,
                            blob_name=filemap[localresource],
                            x_ms_blob_content_md5=md5map[localresource])
                else:
                    # only perform put block list on non-zero byte files
                    if filesizes[localresource] > 0:
                        azure_request(
                            blob_service.put_block_list,
                            timeout=args.timeout,
                            container_name=args.container,
                            blob_name=filemap[localresource],
                            block_list=blockids[localresource],
                            x_ms_blob_content_md5=md5map[localresource])
        done_ops += 1
        progress_bar(
            args.progressbar, 'xfer', progress_text, nstorageops,
            done_ops, storage_start)
        if done_ops == nstorageops:
            break
    endtime = time.time()
    if filedesc is not None:
        filedesc.close()
    progress_bar(
        args.progressbar, 'xfer', progress_text, nstorageops,
        done_ops, storage_start)
    print('\n\n{} MiB transfered, elapsed {} sec. '
          'Throughput = {} Mbit/sec'.format(
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
                os.remove(tmpfilename)

    print('\nscript elapsed time: {} sec'.format(time.time() - start))
    print('script end time: {}'.format(time.strftime("%Y-%m-%d %H:%M:%S")))


def progress_bar(display, sprefix, rtext, value, qsize, start):
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
    sys.stdout.write(
        '\r{0} progress: [{1:30s}] {2:.2f}% {3:10.2f} {4}/min    '.format(
            sprefix, '>' * int(done * 30), done * 100, rate, rtext))
    sys.stdout.flush()


def parseargs():  # pragma: no cover
    """Sets up command-line arguments and parser
    Parameters:
        Nothing
    Returns:
        Parsed command line arguments
    Raises:
        Nothing
    """
    parser = argparse.ArgumentParser(
        description='Transfer block blobs to/from Azure storage')
    parser.set_defaults(
        autovhd=False, blobep=_DEFAULT_BLOB_ENDPOINT,
        chunksizebytes=_MAX_BLOB_CHUNK_SIZE_BYTES, collate=None,
        computefilemd5=True, createcontainer=True, keeprootdir=False,
        managementep=_DEFAULT_MANAGEMENT_ENDPOINT,
        numworkers=_DEFAULT_MAX_STORAGEACCOUNT_WORKERS, pageblob=False,
        progressbar=True, recursive=True, skiponmatch=True, timeout=None)
    parser.add_argument('storageaccount', help='name of storage account')
    parser.add_argument('container', help='name of blob container')
    parser.add_argument(
        'localresource',
        help='name of the local file or directory, if mirroring. "."=use '
        'current directory')
    parser.add_argument(
        '--autovhd', action='store_true',
        help='automatically upload files ending in .vhd as page blobs')
    parser.add_argument(
        '--blobep',
        help='blob storage endpoint [{}]'.format(_DEFAULT_BLOB_ENDPOINT))
    parser.add_argument(
        '--collate', nargs='?',
        help='collate all files into a specified path')
    parser.add_argument(
        '--chunksizebytes', type=int,
        help='maximum chunk size to transfer in bytes [{}]'.format(
            _MAX_BLOB_CHUNK_SIZE_BYTES))
    parser.add_argument(
        '--download', action='store_true',
        help='force transfer direction to download from Azure')
    parser.add_argument(
        '--keepmismatchedmd5files', action='store_true',
        help='keep files with MD5 mismatches')
    parser.add_argument(
        '--keeprootdir', action='store_true',
        help='keeps the root directory as a virtual directory in '
        'directory upload')
    parser.add_argument(
        '--managementcert',
        help='path to management certificate .pem file')
    parser.add_argument(
        '--managementep',
        help='management endpoint [{}]'.format(_DEFAULT_MANAGEMENT_ENDPOINT))
    parser.add_argument(
        '--no-computefilemd5', dest='computefilemd5', action='store_false',
        help='do not compute file MD5 and either upload as metadata '
        'or validate on download')
    parser.add_argument(
        '--no-createcontainer', dest='createcontainer', action='store_false',
        help='do not create container if it does not exist')
    parser.add_argument(
        '--no-progressbar', dest='progressbar', action='store_false',
        help='disable progress bar')
    parser.add_argument(
        '--no-recursive', dest='recursive', action='store_false',
        help='do not mirror local directory recursively')
    parser.add_argument(
        '--no-skiponmatch', dest='skiponmatch', action='store_false',
        help='do not skip upload/download on MD5 match')
    parser.add_argument(
        '--numworkers', type=int,
        help='max number of workers [{}]'.format(
            _DEFAULT_MAX_STORAGEACCOUNT_WORKERS))
    parser.add_argument(
        '--pageblob', action='store_true',
        help='upload as page blob rather than block blob, blobs will '
        'be page-aligned in Azure storage')
    parser.add_argument(
        '--remoteresource',
        help='name of remote resource on Azure storage. "."=container '
        'copy recursive implied')
    parser.add_argument(
        '--saskey',
        help='SAS key to use, if recursive upload or container download, '
        'this must be a container SAS')
    parser.add_argument(
        '--storageaccountkey',
        help='storage account shared key')
    parser.add_argument('--subscriptionid', help='subscription id')
    parser.add_argument(
        '--timeout', type=float,
        help='timeout in seconds for any operation to complete')
    parser.add_argument(
        '--upload', action='store_true',
        help='force transfer direction to upload to Azure')
    parser.add_argument('--version', action='version', version=_SCRIPT_VERSION)
    return parser.parse_args()

if __name__ == '__main__':
    main()
