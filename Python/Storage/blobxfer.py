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

See notes in the README.rst file.

TODO list:
- convert from threading to multiprocessing
- move instruction queue data to class
- migrate connections with sas to azure-storage
"""

# pylint: disable=R0913,R0914

# stdlib imports
from __future__ import print_function
import argparse
import base64
import errno
import fnmatch
import hashlib
import hmac
import json
import mimetypes
import multiprocessing
import os
import platform
# pylint: disable=F0401
try:
    import queue
except ImportError:  # pragma: no cover
    import Queue as queue
# pylint: enable=F0401
import socket
import sys
import threading
import time
import traceback
try:
    from urllib.parse import quote as urlquote
except ImportError:  # pramga: no cover
    from urllib import quote as urlquote
import xml.etree.ElementTree as ET
# non-stdlib imports
import azure.common
try:
    import azure.servicemanagement
except ImportError:  # pragma: no cover
    pass
import azure.storage.blob
try:
    import cryptography.hazmat.backends
    import cryptography.hazmat.primitives.asymmetric.padding
    import cryptography.hazmat.primitives.asymmetric.rsa
    import cryptography.hazmat.primitives.ciphers
    import cryptography.hazmat.primitives.ciphers.algorithms
    import cryptography.hazmat.primitives.ciphers.modes
    import cryptography.hazmat.primitives.constant_time
    import cryptography.hazmat.primitives.hashes
    import cryptography.hazmat.primitives.padding
    import cryptography.hazmat.primitives.serialization
except ImportError:  # pragma: no cover
    pass
import requests

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
_SCRIPT_VERSION = '0.10.1'
_PY2 = sys.version_info.major == 2
_DEFAULT_MAX_STORAGEACCOUNT_WORKERS = multiprocessing.cpu_count() * 3
_MAX_BLOB_CHUNK_SIZE_BYTES = 4194304
_EMPTY_MAX_PAGE_SIZE_MD5 = 'tc+p1sj+vWGPkawoQ9UKHA=='
_MAX_LISTBLOBS_RESULTS = 1000
_PAGEBLOB_BOUNDARY = 512
_DEFAULT_BLOB_ENDPOINT = 'core.windows.net'
_DEFAULT_MANAGEMENT_ENDPOINT = 'management.core.windows.net'
# encryption defines
_AES256_KEYLENGTH_BYTES = 32
_AES256_BLOCKSIZE_BYTES = 16
_HMACSHA256_DIGESTSIZE_BYTES = 32
_AES256CBC_HMACSHA256_OVERHEAD_BYTES = _AES256_BLOCKSIZE_BYTES + \
    _HMACSHA256_DIGESTSIZE_BYTES
_ENCRYPTION_MODE_FULLBLOB = 'FullBlob'
_ENCRYPTION_MODE_CHUNKEDBLOB = 'ChunkedBlob'
_DEFAULT_ENCRYPTION_MODE = _ENCRYPTION_MODE_FULLBLOB
_ENCRYPTION_PROTOCOL_VERSION = '1.0'
_ENCRYPTION_ALGORITHM = 'AES_CBC_256'
_ENCRYPTION_AUTH_ALGORITHM = 'HMAC-SHA256'
_ENCRYPTION_CHUNKSTRUCTURE = 'IV || EncryptedData || Signature'
_ENCRYPTION_ENCRYPTED_KEY_SCHEME = 'RSA-OAEP'
_ENCRYPTION_METADATA_NAME = 'encryptiondata'
_ENCRYPTION_METADATA_MODE = 'EncryptionMode'
_ENCRYPTION_METADATA_ALGORITHM = 'Algorithm'
_ENCRYPTION_METADATA_MAC = 'MessageAuthenticationCode'
_ENCRYPTION_METADATA_LAYOUT = 'EncryptedDataLayout'
_ENCRYPTION_METADATA_CHUNKOFFSETS = 'ChunkByteOffsets'
_ENCRYPTION_METADATA_CHUNKSTRUCTURE = 'ChunkStructure'
_ENCRYPTION_METADATA_AGENT = 'EncryptionAgent'
_ENCRYPTION_METADATA_PROTOCOL = 'Protocol'
_ENCRYPTION_METADATA_ENCRYPTION_ALGORITHM = 'EncryptionAlgorithm'
_ENCRYPTION_METADATA_INTEGRITY_AUTH = 'EncryptionAuthentication'
_ENCRYPTION_METADATA_WRAPPEDCONTENTKEY = 'WrappedContentKey'
_ENCRYPTION_METADATA_ENCRYPTEDKEY = 'EncryptedKey'
_ENCRYPTION_METADATA_ENCRYPTEDAUTHKEY = 'EncryptedAuthenticationKey'
_ENCRYPTION_METADATA_CONTENT_IV = 'ContentEncryptionIV'
_ENCRYPTION_METADATA_KEYID = 'KeyId'
_ENCRYPTION_METADATA_BLOBXFER_EXTENSIONS = 'BlobxferExtensions'
_ENCRYPTION_METADATA_PREENCRYPTED_MD5 = 'PreEncryptedContentMD5'
_ENCRYPTION_METADATA_AUTH_NAME = 'encryptiondata_authentication'
_ENCRYPTION_METADATA_AUTH_METAAUTH = 'EncryptionMetadataAuthentication'
_ENCRYPTION_METADATA_AUTH_ENCODING = 'Encoding'
_ENCRYPTION_METADATA_AUTH_ENCODING_TYPE = 'UTF-8'


class EncryptionMetadataJson(object):
    """Class for handling encryption metadata json"""
    def __init__(
            self, args, symkey, signkey, iv, encdata_signature,
            preencrypted_md5, rsakeyid=None):
        """Ctor for EncryptionMetadataJson
        Parameters:
            args - program arguments
            symkey - symmetric key
            signkey - signing key
            iv - initialization vector
            encdata_signature - encrypted data signature (MAC)
            preencrypted_md5 - pre-encrypted md5 hash
            rsakeyid - symmetric key id
        Returns:
            Nothing
        Raises:
            Nothing
        """
        self.encmode = args.encmode
        self.rsaprivatekey = args.rsaprivatekey
        self.rsapublickey = args.rsapublickey
        self.chunksizebytes = args.chunksizebytes
        self.symkey = symkey
        self.signkey = signkey
        if rsakeyid is None:
            self.rsakeyid = 'private:key1'
        else:
            self.rsakeyid = rsakeyid
        self.iv = iv
        self.hmac = encdata_signature
        self.md5 = preencrypted_md5

    def construct_metadata_json(self):
        """Constructs encryptiondata metadata
        Paramters:
            None
        Returns:
            dict of encryptiondata and encryptiondata_authentiation json
        Raises:
            Nothing
        """
        encsymkey, _ = rsa_encrypt_key(
            self.rsaprivatekey, self.rsapublickey, self.symkey)
        encsignkey, _ = rsa_encrypt_key(
            self.rsaprivatekey, self.rsapublickey, self.signkey)
        encjson = {
            _ENCRYPTION_METADATA_MODE: self.encmode,
            _ENCRYPTION_METADATA_WRAPPEDCONTENTKEY: {
                _ENCRYPTION_METADATA_KEYID: self.rsakeyid,
                _ENCRYPTION_METADATA_ENCRYPTEDKEY: encsymkey,
                _ENCRYPTION_METADATA_ENCRYPTEDAUTHKEY: encsignkey,
                _ENCRYPTION_METADATA_ALGORITHM:
                _ENCRYPTION_ENCRYPTED_KEY_SCHEME,
            },
            _ENCRYPTION_METADATA_AGENT: {
                _ENCRYPTION_METADATA_PROTOCOL: _ENCRYPTION_PROTOCOL_VERSION,
                _ENCRYPTION_METADATA_ENCRYPTION_ALGORITHM:
                _ENCRYPTION_ALGORITHM
            },
            _ENCRYPTION_METADATA_INTEGRITY_AUTH: {
                _ENCRYPTION_METADATA_ALGORITHM:
                _ENCRYPTION_AUTH_ALGORITHM,
            },
            'KeyWrappingMetadata': {},
        }
        if self.md5 is not None:
            encjson[_ENCRYPTION_METADATA_BLOBXFER_EXTENSIONS] = {
                _ENCRYPTION_METADATA_PREENCRYPTED_MD5: self.md5
            }
        if self.encmode == _ENCRYPTION_MODE_FULLBLOB:
            encjson[_ENCRYPTION_METADATA_CONTENT_IV] = base64encode(self.iv)
            encjson[_ENCRYPTION_METADATA_INTEGRITY_AUTH][
                _ENCRYPTION_METADATA_MAC] = base64encode(self.hmac)
        elif self.encmode == _ENCRYPTION_MODE_CHUNKEDBLOB:
            encjson[_ENCRYPTION_METADATA_LAYOUT] = {}
            encjson[_ENCRYPTION_METADATA_LAYOUT][
                _ENCRYPTION_METADATA_CHUNKOFFSETS] = \
                self.chunksizebytes + _AES256CBC_HMACSHA256_OVERHEAD_BYTES + 1
            encjson[_ENCRYPTION_METADATA_LAYOUT][
                _ENCRYPTION_METADATA_CHUNKSTRUCTURE] = \
                _ENCRYPTION_CHUNKSTRUCTURE
        else:
            raise RuntimeError(
                'Unknown encryption mode: {}'.format(self.encmode))
        bencjson = json.dumps(
            encjson, sort_keys=True, ensure_ascii=False).encode(
                _ENCRYPTION_METADATA_AUTH_ENCODING_TYPE)
        encjson = {_ENCRYPTION_METADATA_NAME:
                   json.dumps(encjson, sort_keys=True)}
        # compute MAC over encjson
        hmacsha256 = hmac.new(self.signkey, digestmod=hashlib.sha256)
        hmacsha256.update(bencjson)
        authjson = {
            _ENCRYPTION_METADATA_AUTH_METAAUTH: {
                _ENCRYPTION_METADATA_ALGORITHM: _ENCRYPTION_AUTH_ALGORITHM,
                _ENCRYPTION_METADATA_AUTH_ENCODING:
                _ENCRYPTION_METADATA_AUTH_ENCODING_TYPE,
                _ENCRYPTION_METADATA_MAC: base64encode(hmacsha256.digest()),
            }
        }
        encjson[_ENCRYPTION_METADATA_AUTH_NAME] = json.dumps(
            authjson, sort_keys=True)
        return encjson

    def parse_metadata_json(
            self, blobname, rsaprivatekey, rsapublickey, mddict):
        """Parses a meta data dictionary containing the encryptiondata
        metadata
        Parameters:
            blobname - name of blob
            rsaprivatekey - RSA private key
            rsapublickey - RSA public key
            mddict - metadata dictionary
        Returns:
            Nothing
        Raises:
            RuntimeError if encryptiondata metadata contains invalid or
                unknown fields
        """
        if _ENCRYPTION_METADATA_NAME not in mddict:
            return
        # json parse internal dict
        meta = json.loads(mddict[_ENCRYPTION_METADATA_NAME])
        # populate preencryption md5
        if (_ENCRYPTION_METADATA_BLOBXFER_EXTENSIONS in meta and
                _ENCRYPTION_METADATA_PREENCRYPTED_MD5 in meta[
                    _ENCRYPTION_METADATA_BLOBXFER_EXTENSIONS]):
            self.md5 = meta[_ENCRYPTION_METADATA_BLOBXFER_EXTENSIONS][
                _ENCRYPTION_METADATA_PREENCRYPTED_MD5]
        else:
            self.md5 = None
        # if RSA key is not present return
        if rsaprivatekey is None and rsapublickey is None:
            return
        # check for required metadata fields
        if (_ENCRYPTION_METADATA_MODE not in meta or
                _ENCRYPTION_METADATA_AGENT not in meta):
            return
        # populate encryption mode
        self.encmode = meta[_ENCRYPTION_METADATA_MODE]
        # validate known encryption metadata is set to proper values
        if self.encmode == _ENCRYPTION_MODE_CHUNKEDBLOB:
            chunkstructure = meta[_ENCRYPTION_METADATA_LAYOUT][
                _ENCRYPTION_METADATA_CHUNKSTRUCTURE]
            if chunkstructure != _ENCRYPTION_CHUNKSTRUCTURE:
                raise RuntimeError(
                    '{}: unknown encrypted chunk structure {}'.format(
                        blobname, chunkstructure))
        protocol = meta[_ENCRYPTION_METADATA_AGENT][
            _ENCRYPTION_METADATA_PROTOCOL]
        if protocol != _ENCRYPTION_PROTOCOL_VERSION:
            raise RuntimeError('{}: unknown encryption protocol: {}'.format(
                blobname, protocol))
        blockcipher = meta[_ENCRYPTION_METADATA_AGENT][
            _ENCRYPTION_METADATA_ENCRYPTION_ALGORITHM]
        if blockcipher != _ENCRYPTION_ALGORITHM:
            raise RuntimeError('{}: unknown block cipher: {}'.format(
                blobname, blockcipher))
        if _ENCRYPTION_METADATA_INTEGRITY_AUTH in meta:
            intauth = meta[_ENCRYPTION_METADATA_INTEGRITY_AUTH][
                _ENCRYPTION_METADATA_ALGORITHM]
            if intauth != _ENCRYPTION_AUTH_ALGORITHM:
                raise RuntimeError(
                    '{}: unknown integrity/auth method: {}'.format(
                        blobname, intauth))
        symkeyalg = meta[_ENCRYPTION_METADATA_WRAPPEDCONTENTKEY][
            _ENCRYPTION_METADATA_ALGORITHM]
        if symkeyalg != _ENCRYPTION_ENCRYPTED_KEY_SCHEME:
            raise RuntimeError('{}: unknown key encryption scheme: {}'.format(
                blobname, symkeyalg))
        # populate iv and hmac
        if self.encmode == _ENCRYPTION_MODE_FULLBLOB:
            self.iv = base64.b64decode(meta[_ENCRYPTION_METADATA_CONTENT_IV])
            # don't base64 decode hmac
            if _ENCRYPTION_METADATA_INTEGRITY_AUTH in meta:
                self.hmac = meta[_ENCRYPTION_METADATA_INTEGRITY_AUTH][
                    _ENCRYPTION_METADATA_MAC]
            else:
                self.hmac = None
        # populate chunksize
        if self.encmode == _ENCRYPTION_MODE_CHUNKEDBLOB:
            self.chunksizebytes = long(
                meta[_ENCRYPTION_METADATA_LAYOUT][
                    _ENCRYPTION_METADATA_CHUNKOFFSETS])
        # if RSA key is a public key, stop here as keys cannot be decrypted
        if rsaprivatekey is None:
            return
        # decrypt symmetric key
        self.symkey = rsa_decrypt_key(
            rsaprivatekey,
            meta[_ENCRYPTION_METADATA_WRAPPEDCONTENTKEY][
                _ENCRYPTION_METADATA_ENCRYPTEDKEY], None)
        # decrypt signing key, if it exists
        if _ENCRYPTION_METADATA_ENCRYPTEDAUTHKEY in meta[
                _ENCRYPTION_METADATA_WRAPPEDCONTENTKEY]:
            self.signkey = rsa_decrypt_key(
                rsaprivatekey,
                meta[_ENCRYPTION_METADATA_WRAPPEDCONTENTKEY][
                    _ENCRYPTION_METADATA_ENCRYPTEDAUTHKEY], None)
        else:
            self.signkey = None
        # validate encryptiondata metadata using the signing key
        if (self.signkey is not None and
                _ENCRYPTION_METADATA_AUTH_NAME in mddict):
            authmeta = json.loads(mddict[_ENCRYPTION_METADATA_AUTH_NAME])
            if _ENCRYPTION_METADATA_AUTH_METAAUTH not in authmeta:
                raise RuntimeError(
                    '{}: encryption metadata auth block not found'.format(
                        blobname))
            if _ENCRYPTION_METADATA_AUTH_ENCODING not in authmeta[
                    _ENCRYPTION_METADATA_AUTH_METAAUTH]:
                raise RuntimeError(
                    '{}: encryption metadata auth encoding not found'.format(
                        blobname))
            intauth = authmeta[_ENCRYPTION_METADATA_AUTH_METAAUTH][
                _ENCRYPTION_METADATA_ALGORITHM]
            if intauth != _ENCRYPTION_AUTH_ALGORITHM:
                raise RuntimeError(
                    '{}: unknown integrity/auth method: {}'.format(
                        blobname, intauth))
            authhmac = base64.b64decode(
                authmeta[_ENCRYPTION_METADATA_AUTH_METAAUTH][
                    _ENCRYPTION_METADATA_MAC])
            bmeta = mddict[_ENCRYPTION_METADATA_NAME].encode(
                authmeta[_ENCRYPTION_METADATA_AUTH_METAAUTH][
                    _ENCRYPTION_METADATA_AUTH_ENCODING])
            hmacsha256 = hmac.new(self.signkey, digestmod=hashlib.sha256)
            hmacsha256.update(bmeta)
            if hmacsha256.digest() != authhmac:
                raise RuntimeError(
                    '{}: encryption metadata authentication failed'.format(
                        blobname))


class PqTupleSort(tuple):
    """Priority Queue tuple sorter: handles priority collisions.
    0th item in the tuple is the priority number."""
    def __lt__(self, rhs):
        return self[0] < rhs[0]

    def __gt__(self, rhs):
        return self[0] > rhs[0]

    def __le__(self, rhs):
        return self[0] <= rhs[0]

    def __ge__(self, rhs):
        return self[0] >= rhs[0]


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

    def add_blob(self, name, content_length, content_md5, blobtype, mddict):
        """Adds a blob to the list
        Parameters:
            name - blob name
            content_length - content length
            content_md5 - content md5
            blobtype - blob type
            mddict - metadata dictionary
        Returns:
            Nothing
        Raises:
            Nothing
        """
        obj = type('bloblistobject', (object,), {})
        obj.name = name
        obj.metadata = mddict
        obj.properties = type('properties', (object,), {})
        obj.properties.content_length = content_length
        obj.properties.content_settings = azure.storage.blob.ContentSettings()
        if content_md5 is not None and len(content_md5) > 0:
            obj.properties.content_settings.content_md5 = content_md5
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
            metadata = blob.find('Metadata')
            mddict = {}
            for md in metadata:
                mddict[md.tag] = md.text
            result.add_blob(name, cl, md5, bt, mddict)
        try:
            result.set_next_marker(root.find('NextMarker').text)
        except Exception:
            pass
        return result

    def list_blobs(
            self, container_name, marker=None,
            max_results=_MAX_LISTBLOBS_RESULTS, include=None):
        """List blobs in container
        Parameters:
            container_name - container name
            marker - marker
            max_results - max results
            include - `azure.storage.models.Include` include object
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
            'maxresults': str(max_results)}
        if marker is not None:
            reqparams['marker'] = marker
        if include is not None and include.metadata:
            reqparams['include'] = 'metadata'
        response = azure_request(
            requests.get, url=url, params=reqparams, timeout=self.timeout)
        response.raise_for_status()
        if response.status_code != 200:
            raise IOError(
                'incorrect status code returned for list_blobs: {}'.format(
                    response.status_code))
        return self._parse_blob_list_xml(response.content)

    def _get_blob(self, container_name, blob_name, start_range, end_range):
        """Get blob
        Parameters:
            container_name - container name
            blob_name - name of blob
            start_range - start range of bytes
            end_range - end range of bytes
        Returns:
            `azure.storage.blob.Blob` object
        Raises:
            IOError if unexpected status code
        """
        url = '{blobep}{container_name}/{blob_name}{saskey}'.format(
            blobep=self.blobep, container_name=container_name,
            blob_name=blob_name, saskey=self.saskey)
        reqheaders = {
            'x-ms-range': 'bytes={}-{}'.format(start_range, end_range)
        }
        response = azure_request(
            requests.get, url=url, headers=reqheaders, timeout=self.timeout)
        response.raise_for_status()
        if response.status_code != 200 and response.status_code != 206:
            raise IOError(
                'incorrect status code returned for get_blob: {}'.format(
                    response.status_code))
        return azure.storage.blob.Blob(content=response.content)

    def get_blob_properties(self, container_name, blob_name):
        """Get blob properties
        Parameters:
            container_name - container name
            blob_name - name of blob
        Returns:
            `azure.storage.blob.Blob` object
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
        # parse response headers into blob object
        blob = azure.storage.blob.Blob()
        blob.propertes = azure.storage.blob.BlobProperties()
        blob.properties.content_length = \
            long(response.headers['content-length'])
        blob.properties.content_settings = azure.storage.blob.ContentSettings()
        if 'content-md5' in response.headers:
            blob.properties.content_settings.content_md5 = \
                response.headers['content-md5']
        # read meta values, all meta values are lowercased
        mddict = {}
        for res in response.headers:
            if res.startswith('x-ms-meta-'):
                mddict[res[10:]] = response.headers[res]
        blob.metadata = mddict
        return blob

    def set_blob_metadata(
            self, container_name, blob_name, metadata):
        """Set blob metadata. Clearing is not supported.
        Parameters:
            container_name - container name
            blob_name - name of blob
            metadata - blob metadata dictionary
        Returns:
            Nothing
        Raises:
            IOError if unexpected status code
        """
        if metadata is None or len(metadata) == 0:
            return
        url = '{blobep}{container_name}/{blob_name}{saskey}'.format(
            blobep=self.blobep, container_name=container_name,
            blob_name=blob_name, saskey=self.saskey)
        reqparams = {'comp': 'metadata'}
        reqheaders = {}
        for key in metadata:
            reqheaders['x-ms-meta-' + key] = metadata[key]
        response = azure_request(
            requests.put, url=url, params=reqparams, headers=reqheaders,
            timeout=self.timeout)
        response.raise_for_status()
        if response.status_code != 200:
            raise IOError(
                'incorrect status code returned for '
                'set_blob_metadata: {}'.format(response.status_code))

    def create_blob(
            self, container_name, blob_name, content_length, content_settings):
        """Create blob for initializing page blobs
        Parameters:
            container_name - container name
            blob_name - name of blob
            content_length - content length aligned to 512-byte boundary
            content_settings - `azure.storage.blob.ContentSettings` object
        Returns:
            response content
        Raises:
            IOError if unexpected status code
        """
        url = '{blobep}{container_name}/{blob_name}{saskey}'.format(
            blobep=self.blobep, container_name=container_name,
            blob_name=blob_name, saskey=self.saskey)
        reqheaders = {
            'x-ms-blob-type': 'PageBlob',
            'x-ms-blob-content-length': str(content_length),
        }
        if content_settings is not None:
            if content_settings.content_md5 is not None:
                reqheaders['x-ms-blob-content-md5'] = \
                    content_settings.content_md5
            if content_settings.content_type is not None:
                reqheaders['x-ms-blob-content-type'] = \
                    content_settings.content_type
        response = azure_request(
            requests.put, url=url, headers=reqheaders, timeout=self.timeout)
        response.raise_for_status()
        if response.status_code != 201:
            raise IOError(
                'incorrect status code returned for create_blob: {}'.format(
                    response.status_code))
        return response.content

    def _put_blob(
            self, container_name, blob_name, blob, content_settings):
        """Put blob for creating/updated block blobs
        Parameters:
            container_name - container name
            blob_name - name of blob
            blob - blob content
            content_settings - `azure.storage.blob.ContentSettings` object
        Returns:
            response content
        Raises:
            IOError if unexpected status code
        """
        url = '{blobep}{container_name}/{blob_name}{saskey}'.format(
            blobep=self.blobep, container_name=container_name,
            blob_name=blob_name, saskey=self.saskey)
        reqheaders = {'x-ms-blob-type': 'BlockBlob'}
        if content_settings is not None:
            if content_settings.content_md5 is not None:
                reqheaders['x-ms-blob-content-md5'] = \
                    content_settings.content_md5
            if content_settings.content_type is not None:
                reqheaders['x-ms-blob-content-type'] = \
                    content_settings.content_type
        response = azure_request(
            requests.put, url=url, headers=reqheaders, timeout=self.timeout)
        response.raise_for_status()
        if response.status_code != 201:
            raise IOError(
                'incorrect status code returned for put_blob: {}'.format(
                    response.status_code))
        return response.content

    def update_page(
            self, container_name, blob_name, page, start_range, end_range,
            validate_content=False, content_md5=None):
        """Put page for page blob. This API differs from the Python storage
        sdk to maintain efficiency for block md5 computation.
        Parameters:
            container_name - container name
            blob_name - name of blob
            page - page data
            start_range - start range of bytes
            end_range - end range of bytes
            validate_content - validate content
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
            'x-ms-range': 'bytes={}-{}'.format(start_range, end_range),
            'x-ms-page-write': 'update'}
        if validate_content and content_md5 is not None:
            reqheaders['Content-MD5'] = content_md5
        reqparams = {'comp': 'page'}
        response = azure_request(
            requests.put, url=url, params=reqparams, headers=reqheaders,
            data=page, timeout=self.timeout)
        response.raise_for_status()
        if response.status_code != 201:
            raise IOError(
                'incorrect status code returned for update_page: {}'.format(
                    response.status_code))

    def put_block(
            self, container_name, blob_name, block, block_id,
            validate_content=False):
        """Put block for blob
        Parameters:
            container_name - container name
            blob_name - name of blob
            block - block data
            block_id - block id
            validate_content - validate content
        Returns:
            Nothing
        Raises:
            IOError if unexpected status code
        """
        url = '{blobep}{container_name}/{blob_name}{saskey}'.format(
            blobep=self.blobep, container_name=container_name,
            blob_name=blob_name, saskey=self.saskey)
        # compute block md5
        if validate_content:
            reqheaders = {'Content-MD5': compute_md5_for_data_asbase64(block)}
        else:
            reqheaders = None
        reqparams = {'comp': 'block', 'blockid': block_id}
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
            content_settings):
        """Put block list for blob
        Parameters:
            container_name - container name
            blob_name - name of blob
            block_list - list of `azure.storage.blob.BlobBlock`
            content_settings - `azure.storage.blob.ContentSettings` object
        Returns:
            Nothing
        Raises:
            IOError if unexpected status code
        """
        url = '{blobep}{container_name}/{blob_name}{saskey}'.format(
            blobep=self.blobep, container_name=container_name,
            blob_name=blob_name, saskey=self.saskey)
        reqheaders = {}
        if content_settings is not None:
            if content_settings.content_md5 is not None:
                reqheaders['x-ms-blob-content-md5'] = \
                    content_settings.content_md5
            if content_settings.content_type is not None:
                reqheaders['x-ms-blob-content-type'] = \
                    content_settings.content_type
        reqparams = {'comp': 'blocklist'}
        body = ['<?xml version="1.0" encoding="utf-8"?><BlockList>']
        for block in block_list:
            body.append('<Latest>{}</Latest>'.format(block.id))
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
            self, container_name, blob_name, content_settings):
        """Sets blob properties (MD5 only)
        Parameters:
            container_name - container name
            blob_name - name of blob
            content_settings - `azure.storage.blob.ContentSettings` object
        Returns:
            Nothing
        Raises:
            IOError if unexpected status code
        """
        url = '{blobep}{container_name}/{blob_name}{saskey}'.format(
            blobep=self.blobep, container_name=container_name,
            blob_name=blob_name, saskey=self.saskey)
        reqheaders = {}
        if content_settings is not None:
            if content_settings.content_md5 is not None:
                reqheaders['x-ms-blob-content-md5'] = \
                    content_settings.content_md5
        reqparams = {'comp': 'properties'}
        response = azure_request(
            requests.put, url=url, params=reqparams, headers=reqheaders,
            timeout=self.timeout)
        response.raise_for_status()
        if response.status_code != 200:
            raise IOError('incorrect status code returned for '
                          'set_blob_properties: {}'.format(
                              response.status_code))

    def delete_blob(
            self, container_name, blob_name):
        """Deletes a blob
        Parameters:
            container_name - container name
            blob_name - name of blob
        Returns:
            Nothing
        Raises:
            IOError if unexpected status code
        """
        url = '{blobep}{container_name}/{blob_name}{saskey}'.format(
            blobep=self.blobep, container_name=container_name,
            blob_name=blob_name, saskey=self.saskey)
        response = azure_request(
            requests.delete, url=url, timeout=self.timeout)
        response.raise_for_status()
        if response.status_code != 202:
            raise IOError(
                'incorrect status code returned for delete_blob: {}'.format(
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
        self.terminate = False
        self._exc = exc
        self._in_queue = s_in_queue
        self._out_queue = s_out_queue
        self._pageblob = args.pageblob
        self._autovhd = args.autovhd
        self.blob_service = blob_service
        self.timeout = args.timeout
        self.xfertoazure = xfertoazure
        self.rsaprivatekey = args.rsaprivatekey
        self.rsapublickey = args.rsapublickey
        self.encmode = args.encmode
        self.computeblockmd5 = args.computeblockmd5
        self.saskey = args.saskey

    def run(self):
        """Thread code
        Parameters:
            Nothing
        Returns:
            Nothing
        Raises:
            Nothing
        """
        while not self.terminate:
            try:
                pri, (localresource, container, remoteresource, blockid,
                      offset, bytestoxfer, encparam, flock, filedesc) = \
                    self._in_queue.get_nowait()
            except queue.Empty:
                break
            # detect termination early and break if necessary
            if self.terminate:
                break
            try:
                if self.xfertoazure:
                    # if iv is not ready for this chunk, re-add back to queue
                    if (not as_page_blob(
                            self._pageblob, self._autovhd, localresource) and
                            ((self.rsaprivatekey is not None or
                              self.rsapublickey is not None) and
                             self.encmode == _ENCRYPTION_MODE_FULLBLOB)):
                        _iblockid = int(blockid)
                        if _iblockid not in encparam[2]:
                            self._in_queue.put(
                                PqTupleSort((
                                    pri,
                                    (localresource, container, remoteresource,
                                     blockid, offset, bytestoxfer, encparam,
                                     flock, filedesc))))
                            continue
                    # upload block/page
                    self.putblobdata(
                        localresource, container, remoteresource, blockid,
                        offset, bytestoxfer, encparam, flock, filedesc)
                else:
                    # download range
                    self.getblobrange(
                        localresource, container, remoteresource, blockid,
                        offset, bytestoxfer, encparam, flock, filedesc)
                # pylint: disable=W0703
            except Exception:
                # pylint: enable=W0703
                self._exc.append(traceback.format_exc())
            self._out_queue.put((localresource, encparam))
            if len(self._exc) > 0:
                break

    def putblobdata(
            self, localresource, container, remoteresource, blockid, offset,
            bytestoxfer, encparam, flock, filedesc):
        """Puts data (blob or page) into Azure storage
        Parameters:
            localresource - name of local resource
            container - blob container
            remoteresource - name of remote resource
            blockid - block id (ignored for page blobs)
            offset - file offset
            bytestoxfer - number of bytes to xfer
            encparam - encryption metadata: (symkey, signkey, ivmap, pad)
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
                contentlength = bytestoxfer
                azure_request(
                    self.blob_service[1].create_blob, container_name=container,
                    blob_name=remoteresource, content_length=contentlength,
                    content_settings=azure.storage.blob.ContentSettings(
                        content_type=get_mime_type(localresource),
                        content_md5=contentmd5))
            else:
                contentlength = None
                azure_request(
                    self.blob_service[0]._put_blob, container_name=container,
                    blob_name=remoteresource, blob=None,
                    content_settings=azure.storage.blob.ContentSettings(
                        content_type=get_mime_type(localresource),
                        content_md5=contentmd5))
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
            if self.saskey:
                azure_request(
                    self.blob_service[1].update_page, container_name=container,
                    blob_name=remoteresource, page=data, start_range=offset,
                    end_range=offset + aligned - 1,
                    validate_content=self.computeblockmd5,
                    content_md5=contentmd5, timeout=self.timeout)
            else:
                azure_request(
                    self.blob_service[1].update_page, container_name=container,
                    blob_name=remoteresource, page=data, start_range=offset,
                    end_range=offset + aligned - 1,
                    validate_content=self.computeblockmd5,
                    timeout=self.timeout)
        else:
            # encrypt block if required
            if (encparam is not None and
                    (self.rsaprivatekey is not None or
                     self.rsapublickey is not None)):
                symkey = encparam[0]
                signkey = encparam[1]
                if self.encmode == _ENCRYPTION_MODE_FULLBLOB:
                    _blkid = int(blockid)
                    iv = encparam[2][_blkid]
                    pad = encparam[3]
                else:
                    iv = None
                    pad = True
                data = encrypt_chunk(
                    symkey, signkey, data, self.encmode, iv=iv, pad=pad)
                with flock:
                    if self.encmode == _ENCRYPTION_MODE_FULLBLOB:
                        # compute hmac for chunk
                        if _blkid == 0:
                            encparam[2]['hmac'].update(iv + data)
                        else:
                            encparam[2]['hmac'].update(data)
                        # store iv for next chunk
                        encparam[2][_blkid + 1] = data[
                            len(data) - _AES256_BLOCKSIZE_BYTES:]
                    # compute md5 for encrypted data chunk
                    encparam[2]['md5'].update(data)
            azure_request(
                self.blob_service[0].put_block, container_name=container,
                blob_name=remoteresource, block=data, block_id=blockid,
                validate_content=self.computeblockmd5, timeout=self.timeout)
        del data

    def getblobrange(
            self, localresource, container, remoteresource, blockid, offset,
            bytestoxfer, encparam, flock, filedesc):
        """Get a segment of a blob using range offset downloading
        Parameters:
            localresource - name of local resource
            container - blob container
            remoteresource - name of remote resource
            blockid - block id (integral)
            offset - file offset
            bytestoxfer - number of bytes to xfer
            encparam - decryption metadata:
                (symkey, signkey, offset_mod, encmode, ivmap, unpad)
            flock - file lock
            filedesc - file handle
        Returns:
            Nothing
        Raises:
            Nothing
        """
        if (encparam[0] is not None and
                encparam[3] == _ENCRYPTION_MODE_FULLBLOB):
            if offset == 0:
                start_range = offset
                end_range = offset + bytestoxfer
            else:
                # retrieve block size data prior for IV
                start_range = offset - _AES256_BLOCKSIZE_BYTES
                end_range = offset + bytestoxfer
        else:
            start_range = offset
            end_range = offset + bytestoxfer
        if as_page_blob(self._pageblob, self._autovhd, localresource):
            blob_service = self.blob_service[1]
        else:
            blob_service = self.blob_service[0]
        _blob = azure_request(
            blob_service._get_blob, timeout=self.timeout,
            container_name=container, blob_name=remoteresource,
            start_range=start_range, end_range=end_range)
        blobdata = _blob.content
        # decrypt block if required
        if encparam[0] is not None:
            if encparam[3] == _ENCRYPTION_MODE_FULLBLOB:
                if offset == 0:
                    iv = encparam[4][0]
                else:
                    iv = blobdata[:_AES256_BLOCKSIZE_BYTES]
                    blobdata = blobdata[_AES256_BLOCKSIZE_BYTES:]
                unpad = encparam[5]
                # update any buffered data to hmac
                hmacdict = encparam[4]['hmac']
                if hmacdict['hmac'] is not None:
                    # grab file lock to manipulate hmac
                    with flock:
                        # include iv in first hmac calculation
                        if offset == 0:
                            hmacdict['buffered'][blockid] = iv + blobdata
                        else:
                            hmacdict['buffered'][blockid] = blobdata
                        # try to process hmac data
                        while True:
                            curr = hmacdict['curr']
                            if curr in hmacdict['buffered']:
                                hmacdict['hmac'].update(
                                    hmacdict['buffered'][curr])
                                hmacdict['buffered'].pop(curr)
                                hmacdict['curr'] = curr + 1
                            else:
                                break
            else:
                iv = None
                unpad = True
            blobdata = decrypt_chunk(
                encparam[0], encparam[1], blobdata, encparam[3], iv=iv,
                unpad=unpad)
        if blobdata is not None:
            with flock:
                closefd = False
                if not filedesc:
                    filedesc = open(localresource, 'r+b')
                    closefd = True
                filedesc.seek(offset - (encparam[2] or 0), 0)
                filedesc.write(blobdata)
                if closefd:
                    filedesc.close()
        del blobdata
        del _blob


def pad_pkcs7(buf):
    """Appends PKCS7 padding to an input buffer.
    Parameters:
        buf - buffer to add padding
    Returns:
        buffer with PKCS7_PADDING
    Raises:
        No special exception handling
    """
    padder = cryptography.hazmat.primitives.padding.PKCS7(
        cryptography.hazmat.primitives.ciphers.
        algorithms.AES.block_size).padder()
    return padder.update(buf) + padder.finalize()


def unpad_pkcs7(buf):
    """Removes PKCS7 padding a decrypted object.
    Parameters:
        buf - buffer to remove padding
    Returns:
        buffer without PKCS7_PADDING
    Raises:
        No special exception handling
    """
    unpadder = cryptography.hazmat.primitives.padding.PKCS7(
        cryptography.hazmat.primitives.ciphers.
        algorithms.AES.block_size).unpadder()
    return unpadder.update(buf) + unpadder.finalize()


def generate_aes256_keys():
    """Generate AES256 symmetric key and signing key
    Parameters:
        None
    Returns:
        Tuple of symmetric key and signing key
    Raises:
        Nothing
    """
    symkey = os.urandom(_AES256_KEYLENGTH_BYTES)
    signkey = os.urandom(_AES256_KEYLENGTH_BYTES)
    return symkey, signkey


def rsa_encrypt_key(rsaprivatekey, rsapublickey, plainkey, asbase64=True):
    """Encrypt a plaintext key using RSA and PKCS1_OAEP padding
    Parameters:
        rsaprivatekey - rsa private key for encryption
        rsapublickey - rsa public key for encryption
        plainkey - plaintext key
        asbase64 - encode as base64
    Returns:
        Tuple of encrypted key and signature (if RSA private key is given)
    Raises:
        Nothing
    """
    if rsapublickey is None:
        rsapublickey = rsaprivatekey.public_key()
    if rsaprivatekey is None:
        signature = None
    else:
        signer = rsaprivatekey.signer(
            cryptography.hazmat.primitives.asymmetric.padding.PSS(
                mgf=cryptography.hazmat.primitives.asymmetric.padding.MGF1(
                    cryptography.hazmat.primitives.hashes.SHA256()),
                salt_length=cryptography.hazmat.primitives.asymmetric.
                padding.PSS.MAX_LENGTH),
            cryptography.hazmat.primitives.hashes.SHA256())
        signer.update(plainkey)
        signature = signer.finalize()
    enckey = rsapublickey.encrypt(
        plainkey, cryptography.hazmat.primitives.asymmetric.padding.OAEP(
            mgf=cryptography.hazmat.primitives.asymmetric.padding.MGF1(
                algorithm=cryptography.hazmat.primitives.hashes.SHA1()),
            algorithm=cryptography.hazmat.primitives.hashes.SHA1(),
            label=None))
    if asbase64:
        return base64encode(enckey), base64encode(
            signature) if signature is not None else signature
    else:
        return enckey, signature


def rsa_decrypt_key(rsaprivatekey, enckey, signature, isbase64=True):
    """Decrypt an RSA encrypted key and optional signature verification
    Parameters:
        rsaprivatekey - rsa private key for decryption
        enckey - encrypted key
        signature - optional signature to verify encrypted data
        isbase64 - if keys are base64 encoded
    Returns:
        Decrypted key
    Raises:
        RuntimeError if RSA signature validation fails
    """
    if isbase64:
        enckey = base64.b64decode(enckey)
    deckey = rsaprivatekey.decrypt(
        enckey, cryptography.hazmat.primitives.asymmetric.padding.OAEP(
            mgf=cryptography.hazmat.primitives.asymmetric.padding.MGF1(
                algorithm=cryptography.hazmat.primitives.hashes.SHA1()),
            algorithm=cryptography.hazmat.primitives.hashes.SHA1(),
            label=None))
    if signature is not None and len(signature) > 0:
        rsapublickey = rsaprivatekey.public_key()
        if isbase64:
            signature = base64.b64decode(signature)
        verifier = rsapublickey.verifier(
            signature, cryptography.hazmat.primitives.asymmetric.padding.PSS(
                mgf=cryptography.hazmat.primitives.asymmetric.padding.MGF1(
                    cryptography.hazmat.primitives.hashes.SHA256()),
                salt_length=cryptography.hazmat.primitives.asymmetric.
                padding.PSS.MAX_LENGTH),
            cryptography.hazmat.primitives.hashes.SHA256())
        verifier.update(deckey)
        verifier.verify()
    return deckey


def encrypt_chunk(symkey, signkey, data, encmode, iv=None, pad=False):
    """Encrypt a chunk of data
    Parameters:
        symkey - symmetric key
        signkey - signing key
        data - data to encrypt
        encmode - encryption mode
        iv - initialization vector
        pad - pad data
    Returns:
        iv and hmac not specified: iv || encrypted data || signature
        else: encrypted data
    Raises:
        No special exception handling
    """
    # create iv
    if encmode == _ENCRYPTION_MODE_CHUNKEDBLOB:
        iv = os.urandom(_AES256_BLOCKSIZE_BYTES)
        # force padding on since this will be an individual encrypted chunk
        pad = True
    # encrypt data
    cipher = cryptography.hazmat.primitives.ciphers.Cipher(
        cryptography.hazmat.primitives.ciphers.algorithms.AES(symkey),
        cryptography.hazmat.primitives.ciphers.modes.CBC(iv),
        backend=cryptography.hazmat.backends.default_backend()).encryptor()
    if pad:
        encdata = cipher.update(pad_pkcs7(data)) + cipher.finalize()
    else:
        encdata = cipher.update(data) + cipher.finalize()
    # sign encrypted data
    if encmode == _ENCRYPTION_MODE_CHUNKEDBLOB:
        hmacsha256 = hmac.new(signkey, digestmod=hashlib.sha256)
        hmacsha256.update(iv + encdata)
        return iv + encdata + hmacsha256.digest()
    else:
        return encdata


def decrypt_chunk(
        symkey, signkey, encchunk, encmode, iv=None, unpad=False):
    """Decrypt a chunk of data
    Parameters:
        symkey - symmetric key
        signkey - signing key
        encchunk - data to decrypt
        encmode - encryption mode
        blockid - block id
        iv - initialization vector
        unpad - unpad data
    Returns:
        decrypted data
    Raises:
        RuntimeError if signature verification fails
    """
    # if chunked blob, then preprocess for iv and signature
    if encmode == _ENCRYPTION_MODE_CHUNKEDBLOB:
        # retrieve iv
        iv = encchunk[:_AES256_BLOCKSIZE_BYTES]
        # retrieve encrypted data
        encdata = encchunk[
            _AES256_BLOCKSIZE_BYTES:-_HMACSHA256_DIGESTSIZE_BYTES]
        # retrieve signature
        sig = encchunk[-_HMACSHA256_DIGESTSIZE_BYTES:]
        # validate integrity of data
        hmacsha256 = hmac.new(signkey, digestmod=hashlib.sha256)
        # compute hmac over iv + encdata
        hmacsha256.update(encchunk[:-_HMACSHA256_DIGESTSIZE_BYTES])
        if not cryptography.hazmat.primitives.constant_time.bytes_eq(
                hmacsha256.digest(), sig):
            raise RuntimeError(
                'Encrypted data integrity check failed for chunk')
    else:
        encdata = encchunk
    # decrypt data
    cipher = cryptography.hazmat.primitives.ciphers.Cipher(
        cryptography.hazmat.primitives.ciphers.algorithms.AES(symkey),
        cryptography.hazmat.primitives.ciphers.modes.CBC(iv),
        backend=cryptography.hazmat.backends.default_backend()).decryptor()
    decrypted = cipher.update(encdata) + cipher.finalize()
    if unpad:
        return unpad_pkcs7(decrypted)
    else:
        return decrypted


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
    lastwait = None
    while True:
        try:
            return req(*args, **kwargs)
        except requests.Timeout as exc:
            pass
        except (requests.ConnectionError,
                requests.exceptions.ChunkedEncodingError) as exc:
            if (isinstance(exc.args[0], requests.packages.urllib3.
                           exceptions.ProtocolError) and
                    isinstance(exc.args[0].args[1], socket.error)):
                err = exc.args[0].args[1].errno
                if (err != errno.ECONNRESET and
                        err != errno.ECONNREFUSED and
                        err != errno.ECONNABORTED and
                        err != errno.ENETRESET and
                        err != errno.ETIMEDOUT):
                    raise
        except requests.HTTPError as exc:
            if (exc.response.status_code < 500 or
                    exc.response.status_code == 501 or
                    exc.response.status_code == 505):
                raise
        except azure.common.AzureHttpError as exc:
            if (exc.status_code < 500 or
                    exc.status_code == 501 or
                    exc.status_code == 505):
                raise
        if timeout is not None and time.clock() - start > timeout:
            raise IOError(
                'waited {} sec for request {}, exceeded timeout of {}'.format(
                    time.clock() - start, req.__name__, timeout))
        if lastwait is None or lastwait > 8:
            wait = 1
        else:
            wait = lastwait << 1
        lastwait = wait
        time.sleep(wait)


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


def get_mime_type(filename):
    """Guess the type of a file based on its filename
    Parameters:
        filename - filename to guess the content-type
    Returns:
        A string of the form 'type/subtype',
        usable for a MIME content-type header
    Raises:
        Nothing
    """
    return (mimetypes.guess_type(filename)[0] or 'application/octet-stream')


def encode_blobname(args, blobname):
    """Encode blob name: url encode. Due to current Azure Python Storage SDK
    limitations, does not apply to non-SAS requests.
    Parameters:
        args - program arguments
    Returns:
        urlencoded blob name
    Raises:
        Nothing
    """
    if args.saskey is None:
        return blobname
    else:
        return urlquote(blobname)


def base64encode(obj):
    """Encode object to base64
    Parameters:
        obj - object to encode
    Returns:
        base64 encoded string
    Raises:
        Nothing
    """
    if _PY2:
        return base64.b64encode(obj)
    else:
        return str(base64.b64encode(obj), 'ascii')


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
        return base64encode(hasher.digest())


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
    return base64encode(hasher.digest())


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


def get_blob_listing(blob_service, args, metadata=True):
    """Convenience method for generating a blob listing of a container
    Parameters:
        blob_service - blob service
        args - program arguments
        metadata - include metadata
    Returns:
        dictionary of blob -> list [content length, content md5, enc metadata]
    Raises:
        Nothing
    """
    marker = None
    blobdict = {}
    incl = azure.storage.blob.Include(metadata=metadata)
    while True:
        try:
            result = azure_request(
                blob_service.list_blobs, timeout=args.timeout,
                container_name=args.container, marker=marker, include=incl)
        except azure.common.AzureMissingResourceHttpError:
            break
        for blob in result:
            blobdict[blob.name] = [
                blob.properties.content_length,
                blob.properties.content_settings.content_md5, None]
            if (blob.metadata is not None and
                    _ENCRYPTION_METADATA_NAME in blob.metadata):
                encmeta = EncryptionMetadataJson(
                    args, None, None, None, None, None)
                encmeta.parse_metadata_json(
                    blob.name, args.rsaprivatekey, args.rsapublickey,
                    blob.metadata)
                blobdict[blob.name][1] = encmeta.md5
                if (args.rsaprivatekey is not None or
                        args.rsapublickey is not None):
                    blobdict[blob.name][2] = encmeta
        marker = result.next_marker
        if marker is None or len(marker) < 1:
            break
    return blobdict


def generate_xferspec_download(
        blob_service, args, storage_in_queue, localfile, remoteresource,
        addfd, blobprop):
    """Generate an xferspec for download
    Parameters:
        blob_service - blob service
        args - program arguments
        storage_in_queue - storage input queue
        localfile - name of local resource
        remoteresource - name of remote resource
        addfd - create and add file handle
        blobprop - blob properties list [length, md5, metadatadict]
    Returns:
        xferspec containing instructions
    Raises:
        ValueError if get_blob_properties returns an invalid result or
            contentlength is invalid
    """
    contentlength = blobprop[0]
    contentmd5 = blobprop[1]
    encmeta = blobprop[2]
    remoteresource = encode_blobname(args, remoteresource)
    # get the file metadata
    if (contentlength is None or contentmd5 is None or
            (args.rsaprivatekey is not None and encmeta is None)):
        result = azure_request(
            blob_service.get_blob_properties, timeout=args.timeout,
            container_name=args.container, blob_name=remoteresource)
        if not result:
            raise ValueError(
                'unexpected result for get_blob_properties is None')
        contentmd5 = result.properties.content_settings.content_md5
        contentlength = result.properties.content_length
        if (args.rsaprivatekey is not None and
                _ENCRYPTION_METADATA_NAME in result.metadata):
            encmeta = EncryptionMetadataJson(
                args, None, None, None, None, None)
            encmeta.parse_metadata_json(
                remoteresource, args.rsaprivatekey, args.rsapublickey,
                result.metadata)
    if contentlength < 0:
        raise ValueError(
            'contentlength is invalid for {}'.format(remoteresource))
    # overwrite content md5 if encryption metadata exists
    if encmeta is not None:
        contentmd5 = encmeta.md5
    # check if download is needed
    if (args.skiponmatch and contentmd5 is not None and
            os.path.exists(localfile)):
        print('computing file md5 on: {} length: {}'.format(
            localfile, contentlength))
        lmd5 = compute_md5_for_file_asbase64(localfile)
        print('  >> {} <L..R> {} {} '.format(
            lmd5, contentmd5, remoteresource), end='')
        if lmd5 != contentmd5:
            print('MISMATCH: re-download')
        else:
            print('match: skip')
            return None, None, None, None
    else:
        print('remote blob: {} length: {} bytes, md5: {}'.format(
            remoteresource, contentlength, contentmd5))
    tmpfilename = localfile + '.blobtmp'
    if encmeta is not None:
        chunksize = encmeta.chunksizebytes
        symkey = encmeta.symkey
        signkey = encmeta.signkey
        if encmeta.encmode == _ENCRYPTION_MODE_FULLBLOB:
            ivmap = {
                0: encmeta.iv,
                'hmac': {
                    'hmac': None,
                    'buffered': {},
                    'curr': 0,
                    'sig': encmeta.hmac,
                }
            }
            if signkey is not None:
                ivmap['hmac']['hmac'] = hmac.new(
                    signkey, digestmod=hashlib.sha256)
            offset_mod = 0
        elif encmeta.encmode == _ENCRYPTION_MODE_CHUNKEDBLOB:
            ivmap = None
            offset_mod = _AES256CBC_HMACSHA256_OVERHEAD_BYTES + 1
        else:
            raise RuntimeError('Unknown encryption mode: {}'.format(
                encmeta.encmode))
    else:
        chunksize = args.chunksizebytes
        offset_mod = 0
        symkey = None
        signkey = None
        ivmap = None
    nchunks = contentlength // chunksize
    # compute allocation size, if encrypted this will be an
    # underallocation estimate
    if contentlength > 0:
        if encmeta is not None:
            if encmeta.encmode == _ENCRYPTION_MODE_CHUNKEDBLOB:
                allocatesize = contentlength - ((nchunks + 2) * offset_mod)
            else:
                allocatesize = contentlength - _AES256_BLOCKSIZE_BYTES
        else:
            allocatesize = contentlength
        if allocatesize < 0:
            allocatesize = 0
    else:
        allocatesize = 0
    currfileoffset = 0
    nstorageops = 0
    flock = threading.Lock()
    filedesc = None
    # preallocate file
    with flock:
        filedesc = open(tmpfilename, 'wb')
        if allocatesize > 0:
            filedesc.seek(allocatesize - 1)
            filedesc.write(b'\0')
        filedesc.close()
        if addfd:
            # reopen under r+b mode
            filedesc = open(tmpfilename, 'r+b')
        else:
            filedesc = None
    chunktoadd = min(chunksize, contentlength)
    for i in xrange(nchunks + 1):
        if chunktoadd + currfileoffset > contentlength:
            chunktoadd = contentlength - currfileoffset
        # on download, chunktoadd must be offset by 1 as the x-ms-range
        # header expects it that way. x -> y bytes means first bits of the
        # (x+1)th byte to the last bits of the (y+1)th byte. for example,
        # 0 -> 511 means byte 1 to byte 512
        encparam = [
            symkey, signkey, i * offset_mod,
            encmeta.encmode if encmeta is not None else None, ivmap, False]
        xferspec = (tmpfilename, args.container, remoteresource, i,
                    currfileoffset, chunktoadd - 1, encparam, flock, filedesc)
        currfileoffset = currfileoffset + chunktoadd
        nstorageops = nstorageops + 1
        storage_in_queue.put(PqTupleSort((i, xferspec)))
        if currfileoffset >= contentlength:
            encparam[5] = True
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
        print('computing file md5 on: {}'.format(localfile))
        md5digest = compute_md5_for_file_asbase64(
            localfile, as_page_blob(args.pageblob, args.autovhd, localfile))
        # check if upload is needed
        if args.skiponmatch and remoteresource in blobskipdict:
            print('  >> {} <L..R> {} {} '.format(
                md5digest, blobskipdict[remoteresource][1],
                remoteresource), end='')
            if md5digest != blobskipdict[remoteresource][1]:
                print('MISMATCH: re-upload')
            else:
                print('match: skip')
                return None, 0, None, None
        else:
            print('  >> md5: {}'.format(md5digest))
    # create blockids entry
    if localfile not in blockids:
        blockids[localfile] = []
    # partition local file into chunks
    filesize = os.path.getsize(localfile)
    nchunks = filesize // args.chunksizebytes
    if nchunks > 50000:
        raise RuntimeError(
            '{} chunks for file {} exceeds Azure Storage limits for a '
            'single blob'.format(nchunks, localfile))
    chunktoadd = min(args.chunksizebytes, filesize)
    currfileoffset = 0
    nstorageops = 0
    flock = threading.Lock()
    filedesc = None
    if addfd:
        with flock:
            filedesc = open(localfile, 'rb')
    symkey = None
    signkey = None
    ivmap = None
    for i in xrange(nchunks + 1):
        if chunktoadd + currfileoffset > filesize:
            chunktoadd = filesize - currfileoffset
        blockid = '{0:08d}'.format(currfileoffset // args.chunksizebytes)
        # generate the ivmap for the first block
        if (not as_page_blob(args.pageblob, args.autovhd, localfile) and
                (args.rsaprivatekey is not None or
                 args.rsapublickey is not None) and currfileoffset == 0):
            # generate sym/signing keys
            symkey, signkey = generate_aes256_keys()
            if args.encmode == _ENCRYPTION_MODE_FULLBLOB:
                ivmap = {
                    i: os.urandom(_AES256_BLOCKSIZE_BYTES),
                    'hmac': hmac.new(signkey, digestmod=hashlib.sha256),
                }
            else:
                ivmap = {}
            ivmap['md5'] = hashlib.md5()
        blockids[localfile].append(blockid)
        encparam = [symkey, signkey, ivmap, False]
        xferspec = (localfile, args.container,
                    encode_blobname(args, remoteresource), blockid,
                    currfileoffset, chunktoadd, encparam, flock, filedesc)
        currfileoffset = currfileoffset + chunktoadd
        nstorageops = nstorageops + 1
        storage_in_queue.put(PqTupleSort((i, xferspec)))
        if currfileoffset >= filesize:
            encparam[3] = True
            break
    return filesize, nstorageops, md5digest, filedesc


def apply_file_collation_and_strip(args, fname):
    """Apply collation path or component strip to a remote filename
    Parameters:
        args - arguments
        fname - file name
    Returns:
        remote filename
    Raises:
        No special exception handling
    """
    remotefname = fname.strip(os.path.sep)
    if args.collate is not None:
        remotefname = remotefname.split(os.path.sep)[-1]
        if args.collate != '.':
            remotefname = os.path.sep.join((args.collate, remotefname))
    elif args.stripcomponents > 0:
        rtmp = remotefname.split(os.path.sep)
        nsc = min((len(rtmp) - 1, args.stripcomponents))
        if nsc > 0:
            remotefname = os.path.sep.join(rtmp[nsc:])
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
    if (len(args.localresource) < 1 or len(args.storageaccount) < 1 or
            len(args.container) < 1):
        raise ValueError('invalid positional arguments')
    if len(args.blobep) < 1:
        raise ValueError('blob endpoint is invalid')
    if args.upload and args.download:
        raise ValueError(
            'cannot specify both download and upload transfer direction '
            'within the same invocation')
    if args.subscriptionid is not None and args.managementcert is None:
        raise ValueError(
            'cannot specify subscription id without a management cert')
    if args.subscriptionid is None and args.managementcert is not None:
        raise ValueError(
            'cannot specify a management cert without a subscription id')
    if args.storageaccountkey is not None and args.saskey is not None:
        raise ValueError('cannot use both a sas key and storage account key')
    if args.pageblob and args.autovhd:
        raise ValueError('cannot specify both pageblob and autovhd parameters')
    if args.collate is not None and args.stripcomponents is not None:
        raise ValueError(
            'cannot specify collate and non-default component '
            'strip: {}'.format(args.stripcomponents))
    if args.stripcomponents is None:
        args.stripcomponents = 1
    if args.stripcomponents < 0:
        raise ValueError('invalid component strip number: {}'.format(
            args.stripcomponents))
    if args.rsaprivatekey is not None and args.rsapublickey is not None:
        raise ValueError('cannot specify both RSA private and public keys')
    if args.rsapublickey is not None and args.rsakeypassphrase is not None:
        raise ValueError('cannot specify an RSA public key and passphrase')
    if args.timeout is not None and args.timeout <= 0:
        args.timeout = None

    # get key if we don't have a handle on one
    sms = None
    if args.saskey is not None:
        if len(args.saskey) < 1:
            raise ValueError('invalid sas key specified')
    elif args.storageaccountkey is None:
        if (args.managementcert is not None and
                args.subscriptionid is not None):
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
            raise ValueError('could not determine authentication to use')

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
    if (args.chunksizebytes is None or args.chunksizebytes < 64 or
            args.chunksizebytes > _MAX_BLOB_CHUNK_SIZE_BYTES):
        args.chunksizebytes = _MAX_BLOB_CHUNK_SIZE_BYTES

    # set blob ep
    blobep = None
    if sms:
        storage_acct = azure_request(
            sms.get_storage_account_properties, timeout=args.timeout,
            service_name=args.storageaccount)
        blobep = storage_acct.storage_service_properties.endpoints[0]
    else:
        blobep = 'https://{}.blob.{}/'.format(args.storageaccount, args.blobep)

    # create master block and page blob service
    blob_service = None
    if args.storageaccountkey:
        if args.blobep[0] == '.':
            args.blobep = args.blobep[1:]
        block_blob_service = azure.storage.blob.BlockBlobService(
            account_name=args.storageaccount,
            account_key=args.storageaccountkey,
            endpoint_suffix=args.blobep)
        page_blob_service = azure.storage.blob.PageBlobService(
            account_name=args.storageaccount,
            account_key=args.storageaccountkey,
            endpoint_suffix=args.blobep)
        blob_service = (block_blob_service, page_blob_service)
    elif args.saskey:
        blob_service = (
            SasBlobService(blobep, args.saskey, args.timeout),
            SasBlobService(blobep, args.saskey, args.timeout))
        # disable container creation (not possible with SAS)
        args.createcontainer = False
    if blob_service is None:
        raise ValueError('blob_service is invalid')

    # check which way we're transfering
    xfertoazure = False
    if (args.upload or
            (not args.download and os.path.exists(args.localresource))):
        xfertoazure = True
    else:
        if args.remoteresource is None:
            raise ValueError('cannot download remote file if not specified')

    # import rsa key
    if args.rsaprivatekey is not None:
        rsakeyfile = args.rsaprivatekey
    elif args.rsapublickey is not None:
        rsakeyfile = args.rsapublickey
    else:
        rsakeyfile = None
    if rsakeyfile is not None:
        # check for conflicting options
        if args.pageblob:
            raise ValueError(
                'cannot operate in page blob mode with encryption enabled')
        # check for supported encryption modes
        if (args.encmode != _ENCRYPTION_MODE_FULLBLOB and
                args.encmode != _ENCRYPTION_MODE_CHUNKEDBLOB):
            raise RuntimeError(
                'Unknown encryption mode: {}'.format(args.encmode))
        # only allow full blob encryption mode for now due to
        # possible compatibility issues
        if args.encmode == _ENCRYPTION_MODE_CHUNKEDBLOB:
            raise RuntimeError(
                '{} encryption mode not allowed'.format(args.encmode))
        with open(rsakeyfile, 'rb') as keyfile:
            if args.rsaprivatekey is not None:
                args.rsaprivatekey = cryptography.hazmat.primitives.\
                    serialization.load_pem_private_key(
                        keyfile.read(), args.rsakeypassphrase,
                        backend=cryptography.hazmat.backends.default_backend())
            else:
                args.rsapublickey = cryptography.hazmat.primitives.\
                    serialization.load_pem_public_key(
                        keyfile.read(),
                        backend=cryptography.hazmat.backends.default_backend())
        if args.rsaprivatekey is None and not xfertoazure:
            raise ValueError('imported RSA key does not have a private key')
        # adjust chunk size for padding for chunked mode
        if xfertoazure:
            if args.encmode == _ENCRYPTION_MODE_CHUNKEDBLOB:
                args.chunksizebytes -= _AES256CBC_HMACSHA256_OVERHEAD_BYTES + 1
            elif args.encmode == _ENCRYPTION_MODE_FULLBLOB:
                nchunks = args.chunksizebytes // \
                    _AES256CBC_HMACSHA256_OVERHEAD_BYTES
                args.chunksizebytes = (nchunks - 1) * \
                    _AES256CBC_HMACSHA256_OVERHEAD_BYTES
                del nchunks
        # ensure chunk size is greater than overhead
        if args.chunksizebytes <= (
                _AES256CBC_HMACSHA256_OVERHEAD_BYTES + 1) << 1:
            raise ValueError('chunksizebytes {} <= encryption min {}'.format(
                args.chunksizebytes,
                (_AES256CBC_HMACSHA256_OVERHEAD_BYTES + 1) << 1))

    # disable urllib3 warnings if specified
    if args.disableurllibwarnings:
        print('!!! WARNING: DISABLING URLLIB3 WARNINGS !!!')
        requests.packages.urllib3.disable_warnings(
            requests.packages.urllib3.exceptions.InsecurePlatformWarning)
        requests.packages.urllib3.disable_warnings(
            requests.packages.urllib3.exceptions.SNIMissingWarning)

    # collect package versions
    packages = ['az.common=' + azure.common.__version__]
    try:
        packages.append('az.sml=' + azure.servicemanagement.__version__)
    except Exception:
        pass
    try:
        packages.append('az.stor=' + azure.storage.__version__)
    except Exception:
        pass
    try:
        packages.append('crypt=' + cryptography.__version__)
    except Exception:
        pass
    packages.append(
        'req=' + requests.__version__)

    # print all parameters
    print('=====================================')
    print(' azure blobxfer parameters [v{}]'.format(_SCRIPT_VERSION))
    print('=====================================')
    print('             platform: {}'.format(platform.platform()))
    print('   python interpreter: {} {}'.format(
        platform.python_implementation(), platform.python_version()))
    print('     package versions: {}'.format(' '.join(packages)))
    del packages
    print('      subscription id: {}'.format(args.subscriptionid))
    print('      management cert: {}'.format(args.managementcert))
    print('   transfer direction: {}'.format(
        'local->Azure' if xfertoazure else 'Azure->local'))
    print('       local resource: {}'.format(args.localresource))
    print('      include pattern: {}'.format(args.include))
    print('      remote resource: {}'.format(args.remoteresource))
    print('   max num of workers: {}'.format(args.numworkers))
    print('              timeout: {}'.format(args.timeout))
    print('      storage account: {}'.format(args.storageaccount))
    print('              use SAS: {}'.format(True if args.saskey else False))
    print('  upload as page blob: {}'.format(args.pageblob))
    print('  auto vhd->page blob: {}'.format(args.autovhd))
    print('            container: {}'.format(args.container))
    print('   blob container URI: {}'.format(blobep + args.container))
    print('    compute block MD5: {}'.format(args.computeblockmd5))
    print('     compute file MD5: {}'.format(args.computefilemd5))
    print('    skip on MD5 match: {}'.format(args.skiponmatch))
    print('   chunk size (bytes): {}'.format(args.chunksizebytes))
    print('     create container: {}'.format(args.createcontainer))
    print('  keep mismatched MD5: {}'.format(args.keepmismatchedmd5files))
    print('     recursive if dir: {}'.format(args.recursive))
    print('component strip on up: {}'.format(args.stripcomponents))
    print('        remote delete: {}'.format(args.delete))
    print('           collate to: {}'.format(args.collate or 'disabled'))
    print('      local overwrite: {}'.format(args.overwrite))
    print('      encryption mode: {}'.format(
        (args.encmode or 'disabled' if xfertoazure else 'file dependent')
        if args.rsaprivatekey is not None or args.rsapublickey is not None
        else 'disabled'))
    print('         RSA key file: {}'.format(rsakeyfile or 'disabled'))
    print('         RSA key type: {}'.format(
        'private' if args.rsaprivatekey is not None else 'public'
        if args.rsapublickey is not None else 'disabled'))
    print('=======================================\n')

    # mark start time after init
    print('script start time: {}'.format(time.strftime("%Y-%m-%d %H:%M:%S")))
    start = time.time()

    # populate instruction queues
    allfilesize = 0
    storage_in_queue = queue.PriorityQueue()
    nstorageops = 0
    blockids = {}
    completed_blockids = {}
    filemap = {}
    filesizes = {}
    delblobs = None
    md5map = {}
    filedesc = None
    if xfertoazure:
        # if skiponmatch is enabled, list blobs first and check
        if args.skiponmatch:
            blobskipdict = get_blob_listing(blob_service[0], args)
        else:
            blobskipdict = {}
        if os.path.isdir(args.localresource):
            if args.remoteresource is not None:
                print('WARNING: ignorning specified remoteresource {} for '
                      'directory upload'.format(args.remoteresource))
            _remotefiles = set()
            # mirror directory
            if args.recursive:
                for root, _, files in os.walk(args.localresource):
                    for dirfile in files:
                        fname = os.path.join(root, dirfile)
                        if args.include is not None and not fnmatch.fnmatch(
                                fname, args.include):
                            continue
                        remotefname = apply_file_collation_and_strip(
                            args, fname)
                        _remotefiles.add(remotefname)
                        filesize, ops, md5digest, filedesc = \
                            generate_xferspec_upload(
                                args, storage_in_queue, blobskipdict,
                                blockids, fname, remotefname, False)
                        if filesize is not None:
                            completed_blockids[fname] = 0
                            md5map[fname] = md5digest
                            filemap[fname] = encode_blobname(args, remotefname)
                            filesizes[fname] = filesize
                            allfilesize = allfilesize + filesize
                            nstorageops = nstorageops + ops
            else:
                # copy just directory contents, non-recursively
                for lfile in os.listdir(args.localresource):
                    fname = os.path.join(args.localresource, lfile)
                    if os.path.isdir(fname) or (
                            args.include is not None and not fnmatch.fnmatch(
                                fname, args.include)):
                        continue
                    remotefname = apply_file_collation_and_strip(args, fname)
                    _remotefiles.add(remotefname)
                    filesize, ops, md5digest, filedesc = \
                        generate_xferspec_upload(
                            args, storage_in_queue, blobskipdict,
                            blockids, fname, remotefname, False)
                    if filesize is not None:
                        completed_blockids[fname] = 0
                        md5map[fname] = md5digest
                        filemap[fname] = encode_blobname(args, remotefname)
                        filesizes[fname] = filesize
                        allfilesize = allfilesize + filesize
                        nstorageops = nstorageops + ops
            # fill deletion list
            if args.delete:
                # get blob skip dict if it hasn't been populated
                if len(blobskipdict) == 0:
                    blobskipdict = get_blob_listing(
                        blob_service[0], args, metadata=False)
                delblobs = [x for x in blobskipdict if x not in _remotefiles]
            del _remotefiles
        else:
            # upload single file
            if args.remoteresource is None:
                args.remoteresource = args.localresource
            else:
                if args.stripcomponents > 0:
                    args.stripcomponents -= 1
            args.remoteresource = apply_file_collation_and_strip(
                args, args.remoteresource)
            filesize, nstorageops, md5digest, filedesc = \
                generate_xferspec_upload(
                    args, storage_in_queue, blobskipdict, blockids,
                    args.localresource, args.remoteresource, True)
            if filesize is not None:
                completed_blockids[args.localresource] = 0
                md5map[args.localresource] = md5digest
                filemap[args.localresource] = encode_blobname(
                    args, args.remoteresource)
                filesizes[args.localresource] = filesize
                allfilesize = allfilesize + filesize
        del blobskipdict
        # create container if needed
        if args.createcontainer:
            try:
                azure_request(
                    blob_service[0].create_container, timeout=args.timeout,
                    container_name=args.container, fail_on_exist=False)
            except azure.common.AzureConflictHttpError:
                pass
        # initialize page blobs
        if args.pageblob or args.autovhd:
            print('initializing page blobs')
            for key in filemap:
                if as_page_blob(args.pageblob, args.autovhd, key):
                    blob_service[1].create_blob(
                        container_name=args.container, blob_name=filemap[key],
                        content_length=page_align_content_length(
                            filesizes[key]), content_settings=None)
    else:
        if args.remoteresource == '.':
            print('attempting to copy entire container {} to {}'.format(
                args.container, args.localresource))
            blobdict = get_blob_listing(blob_service[0], args)
        else:
            blobdict = {args.remoteresource: [None, None, None]}
        if len(blobdict) > 0:
            print('generating local directory structure and '
                  'pre-allocating space')
            # make the localresource directory
            created_dirs = set()
            create_dir_ifnotexists(args.localresource)
            created_dirs.add(args.localresource)
        # generate xferspec for all blobs
        for blob in blobdict:
            # filter results
            if args.include is not None and not fnmatch.fnmatch(
                    blob, args.include):
                continue
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
                    blob_service[0], args, storage_in_queue, localfile,
                    blob, False, blobdict[blob])
            if filesize is not None:
                md5map[localfile] = md5digest
                filemap[localfile] = localfile + '.blobtmp'
                allfilesize = allfilesize + filesize
                nstorageops = nstorageops + ops
        if len(blobdict) > 0:
            del created_dirs
        del blobdict

    # delete any remote blobs if specified
    if xfertoazure and delblobs is not None:
        print('deleting {} remote blobs'.format(len(delblobs)))
        for blob in delblobs:
            azure_request(
                blob_service[0].delete_blob, timeout=args.timeout,
                container_name=args.container, blob_name=blob)
        print('deletion complete.')

    if nstorageops == 0:
        print('detected no transfer actions needed to be taken, exiting...')
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
                  'finalizing operations'.format(
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

    # spawn workers
    storage_out_queue = queue.Queue(nstorageops)
    maxworkers = min((args.numworkers, nstorageops))
    print('spawning {} worker threads'.format(maxworkers))
    exc_list = []
    threads = []
    for _ in xrange(maxworkers):
        thr = BlobChunkWorker(
            exc_list, storage_in_queue, storage_out_queue, args, blob_service,
            xfertoazure)
        thr.start()
        threads.append(thr)

    done_ops = 0
    hmacs = {}
    storage_start = time.time()
    progress_bar(
        args.progressbar, 'xfer', progress_text, nstorageops,
        done_ops, storage_start)
    while True:
        try:
            localresource, encparam = storage_out_queue.get()
        except KeyboardInterrupt:
            print('\n\nKeyboardInterrupt detected, force terminating '
                  'threads (this may take a while)...')
            for thr in threads:
                thr.terminate = True
            for thr in threads:
                thr.join()
            raise
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
                            blob_service[1].set_blob_properties,
                            timeout=args.timeout,
                            container_name=args.container,
                            blob_name=filemap[localresource],
                            content_settings=azure.storage.blob.
                            ContentSettings(content_md5=md5map[localresource]))
                else:
                    # only perform put block list on non-zero byte files
                    if filesizes[localresource] > 0:
                        if (args.rsaprivatekey is not None or
                                args.rsapublickey is not None):
                            md5 = base64encode(encparam[2]['md5'].digest())
                        else:
                            md5 = md5map[localresource]
                        block_list = []
                        for bid in blockids[localresource]:
                            block_list.append(
                                azure.storage.blob.BlobBlock(id=bid))
                        azure_request(
                            blob_service[0].put_block_list,
                            timeout=args.timeout,
                            container_name=args.container,
                            blob_name=filemap[localresource],
                            block_list=block_list,
                            content_settings=azure.storage.blob.
                            ContentSettings(
                                content_type=get_mime_type(localresource),
                                content_md5=md5))
                        # set blob metadata for encrypted blobs
                        if (args.rsaprivatekey is not None or
                                args.rsapublickey is not None):
                            if args.encmode == _ENCRYPTION_MODE_FULLBLOB:
                                encmetadata = EncryptionMetadataJson(
                                    args, encparam[0], encparam[1],
                                    encparam[2][0],
                                    encparam[2]['hmac'].digest(),
                                    md5map[localresource]
                                ).construct_metadata_json()
                            else:
                                encmetadata = EncryptionMetadataJson(
                                    args, encparam[0], encparam[1], None,
                                    None, md5map[localresource]
                                ).construct_metadata_json()
                            azure_request(
                                blob_service[0].set_blob_metadata,
                                timeout=args.timeout,
                                container_name=args.container,
                                blob_name=filemap[localresource],
                                metadata=encmetadata)
        else:
            if (args.rsaprivatekey is not None and
                    encparam[3] == _ENCRYPTION_MODE_FULLBLOB and
                    not as_page_blob(
                        args.pageblob, args.autovhd, localresource) and
                    encparam[4]['hmac']['hmac'] is not None):
                hmacs[localresource] = encparam[4]['hmac']
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
          'Throughput = {} Mbit/sec\n'.format(
              allfilesize / 1048576.0, endtime - storage_start,
              (8.0 * allfilesize / 1048576.0) / (endtime - storage_start)))

    # finalize files/blobs
    if not xfertoazure:
        print(
            'performing finalization (if applicable): {}: {}, MD5: {}'.format(
                _ENCRYPTION_AUTH_ALGORITHM,
                args.rsaprivatekey is not None, args.computefilemd5))
        for localfile in filemap:
            tmpfilename = filemap[localfile]
            finalizefile = True
            skipmd5 = False
            # check hmac
            if (args.rsaprivatekey is not None and
                    args.encmode == _ENCRYPTION_MODE_FULLBLOB):
                if tmpfilename in hmacs:
                    hmacdict = hmacs[tmpfilename]
                    # process any remaining hmac data
                    while len(hmacdict['buffered']) > 0:
                        curr = hmacdict['curr']
                        if curr in hmacdict['buffered']:
                            hmacdict['hmac'].update(hmacdict['buffered'][curr])
                            hmacdict['buffered'].pop(curr)
                            hmacdict['curr'] = curr + 1
                        else:
                            break
                    digest = base64encode(hmacdict['hmac'].digest())
                    res = 'OK'
                    if digest != hmacdict['sig']:
                        res = 'MISMATCH'
                        finalizefile = False
                    else:
                        skipmd5 = True
                    print('[{}: {}, {}] {} <L..R> {}'.format(
                        _ENCRYPTION_AUTH_ALGORITHM, res, localfile,
                        digest, hmacdict['sig']))
            # compare md5 hash
            if args.computefilemd5 and not skipmd5:
                lmd5 = compute_md5_for_file_asbase64(tmpfilename)
                if md5map[localfile] is None:
                    print('[MD5: SKIPPED, {}] {} <L..R> {}'.format(
                        localfile, lmd5, md5map[localfile]))
                else:
                    if lmd5 != md5map[localfile]:
                        res = 'MISMATCH'
                        if not args.keepmismatchedmd5files:
                            finalizefile = False
                    else:
                        res = 'OK'
                    print('[MD5: {}, {}] {} <L..R> {}'.format(
                        res, localfile, lmd5, md5map[localfile]))
            if finalizefile:
                # check for existing file first
                if os.path.exists(localfile):
                    if args.overwrite:
                        os.remove(localfile)
                    else:
                        raise IOError(
                            'cannot overwrite existing file: {}'.format(
                                localfile))
                # move tmp file to real file
                os.rename(tmpfilename, localfile)
            else:
                os.remove(tmpfilename)
        print('finalization complete.')

    # output final log lines
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
    diff = time.time() - start
    if diff <= 0:
        # arbitrarily give a small delta
        diff = 1e-6
    rate = float(qsize) / (diff / 60)
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
        description='Transfer block/page blobs to/from Azure storage')
    parser.set_defaults(
        autovhd=False, blobep=_DEFAULT_BLOB_ENDPOINT,
        chunksizebytes=_MAX_BLOB_CHUNK_SIZE_BYTES, collate=None,
        computeblockmd5=False, computefilemd5=True, createcontainer=True,
        delete=False, disableurllibwarnings=False,
        encmode=_DEFAULT_ENCRYPTION_MODE, include=None,
        managementep=_DEFAULT_MANAGEMENT_ENDPOINT,
        numworkers=_DEFAULT_MAX_STORAGEACCOUNT_WORKERS, overwrite=True,
        pageblob=False, progressbar=True, recursive=True, rsaprivatekey=None,
        rsapublickey=None, rsakeypassphrase=None, skiponmatch=True,
        stripcomponents=None, timeout=None)
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
        '--computeblockmd5', dest='computeblockmd5', action='store_true',
        help='compute block/page level MD5 during upload')
    parser.add_argument(
        '--chunksizebytes', type=int,
        help='maximum chunk size to transfer in bytes [{}]'.format(
            _MAX_BLOB_CHUNK_SIZE_BYTES))
    parser.add_argument(
        '--delete', action='store_true',
        help='delete extraneous remote blobs that have no corresponding '
        'local file when uploading directories')
    parser.add_argument(
        '--disable-urllib-warnings', action='store_true',
        dest='disableurllibwarnings',
        help='disable urllib warnings (not recommended)')
    parser.add_argument(
        '--download', action='store_true',
        help='force transfer direction to download from Azure')
    parser.add_argument(
        '--encmode',
        help='encryption mode [{}]'.format(_DEFAULT_ENCRYPTION_MODE))
    parser.add_argument(
        '--include', type=str,
        help='include pattern (Unix shell-style wildcards)')
    parser.add_argument(
        '--keepmismatchedmd5files', action='store_true',
        help='keep files with MD5 mismatches')
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
        '--no-overwrite', dest='overwrite', action='store_false',
        help='do not overwrite local files on download')
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
        '--rsaprivatekey',
        help='RSA private key file in PEM format. Specifying an RSA private '
        'key will turn on decryption (or encryption). An RSA private key is '
        'required for downloading and decrypting blobs and may be specified '
        'for encrypting and uploading blobs.')
    parser.add_argument(
        '--rsapublickey',
        help='RSA public key file in PEM format. Specifying an RSA public '
        'key will turn on encryption. An RSA public key can only be used '
        'for encrypting and uploading blobs.')
    parser.add_argument(
        '--rsakeypassphrase',
        help='Optional passphrase for decrypting an RSA private key.')
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
    parser.add_argument(
        '--strip-components', dest='stripcomponents', type=int,
        help='strip N leading components from path on upload [1]')
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
