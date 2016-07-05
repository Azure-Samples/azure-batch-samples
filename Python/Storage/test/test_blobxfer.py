# coding=utf-8
"""Tests for blobxfer"""

# stdlib imports
import base64
import copy
import errno
import json
import math
import os
try:
    import queue
except ImportError:
    import Queue as queue
import socket
import sys
import threading
import uuid
# non-stdlib imports
import azure.common
import azure.storage.blob
import cryptography.exceptions
import cryptography.hazmat.backends
import cryptography.hazmat.primitives.asymmetric.rsa
import cryptography.hazmat.primitives.serialization
from mock import (MagicMock, Mock, patch)
import pytest
import requests
import requests_mock
# module under test
sys.path.append('..')
import blobxfer  # noqa


# global defines
_RSAKEY = cryptography.hazmat.primitives.asymmetric.rsa.generate_private_key(
    public_exponent=65537, key_size=2048,
    backend=cryptography.hazmat.backends.default_backend())


def test_encrypt_decrypt_chunk():
    enckey, signkey = blobxfer.generate_aes256_keys()
    assert len(enckey) == blobxfer._AES256_KEYLENGTH_BYTES
    assert len(signkey) == blobxfer._AES256_KEYLENGTH_BYTES

    # test random binary data, unaligned
    iv = os.urandom(16)
    plaindata = os.urandom(31)
    encdata = blobxfer.encrypt_chunk(
        enckey, signkey, plaindata, blobxfer._ENCRYPTION_MODE_CHUNKEDBLOB,
        pad=True)
    assert encdata != plaindata
    decdata = blobxfer.decrypt_chunk(
        enckey, signkey, encdata, blobxfer._ENCRYPTION_MODE_CHUNKEDBLOB,
        unpad=True)
    assert decdata == plaindata
    with pytest.raises(RuntimeError):
        badsig = base64.b64encode(b'0')
        blobxfer.decrypt_chunk(
            enckey, badsig, encdata, blobxfer._ENCRYPTION_MODE_CHUNKEDBLOB,
            unpad=True)

    encdata = blobxfer.encrypt_chunk(
        enckey, signkey, plaindata, blobxfer._ENCRYPTION_MODE_FULLBLOB,
        iv=iv, pad=True)
    decdata = blobxfer.decrypt_chunk(
        enckey, signkey, encdata, blobxfer._ENCRYPTION_MODE_FULLBLOB,
        iv=iv, unpad=True)
    assert decdata == plaindata

    # test random binary data aligned on boundary
    plaindata = os.urandom(32)
    encdata = blobxfer.encrypt_chunk(
        enckey, signkey, plaindata, blobxfer._ENCRYPTION_MODE_FULLBLOB,
        iv=iv, pad=True)
    assert encdata != plaindata
    decdata = blobxfer.decrypt_chunk(
        enckey, signkey, encdata, blobxfer._ENCRYPTION_MODE_FULLBLOB,
        iv=iv, unpad=True)
    assert decdata == plaindata

    # test text data
    plaindata = b'attack at dawn!'
    encdata = blobxfer.encrypt_chunk(
        enckey, signkey, plaindata, blobxfer._ENCRYPTION_MODE_FULLBLOB,
        iv, pad=True)
    assert encdata != plaindata
    decdata = blobxfer.decrypt_chunk(
        enckey, signkey, encdata, blobxfer._ENCRYPTION_MODE_FULLBLOB,
        iv, unpad=True)
    assert decdata == plaindata


def test_rsa_keys():
    symkey = os.urandom(32)
    enckey, sig = blobxfer.rsa_encrypt_key(
        _RSAKEY, None, symkey, asbase64=False)
    assert enckey is not None
    assert sig is not None
    plainkey = blobxfer.rsa_decrypt_key(_RSAKEY, enckey, sig, isbase64=False)
    assert symkey == plainkey

    with pytest.raises(cryptography.exceptions.InvalidSignature):
        badsig = base64.b64encode(b'0')
        blobxfer.rsa_decrypt_key(_RSAKEY, enckey, badsig, isbase64=False)

    enckey, sig = blobxfer.rsa_encrypt_key(
        _RSAKEY, None, symkey, asbase64=True)
    assert enckey is not None
    assert sig is not None
    plainkey = blobxfer.rsa_decrypt_key(_RSAKEY, enckey, sig, isbase64=True)
    assert symkey == plainkey

    with pytest.raises(cryptography.exceptions.InvalidSignature):
        badsig = base64.b64encode(b'0')
        blobxfer.rsa_decrypt_key(_RSAKEY, enckey, badsig, isbase64=True)


def test_compute_md5(tmpdir):
    lpath = str(tmpdir.join('test.tmp'))
    testdata = str(uuid.uuid4())
    with open(lpath, 'wt') as f:
        f.write(testdata)
    md5_file = blobxfer.compute_md5_for_file_asbase64(lpath)
    md5_data = blobxfer.compute_md5_for_data_asbase64(testdata.encode('utf8'))
    assert md5_file == md5_data

    # test non-existent file
    with pytest.raises(IOError):
        blobxfer.compute_md5_for_file_asbase64(testdata)


def test_page_align_content_length():
    assert 0 == blobxfer.page_align_content_length(0)
    assert 512 == blobxfer.page_align_content_length(511)
    assert 512 == blobxfer.page_align_content_length(512)
    assert 1024 == blobxfer.page_align_content_length(513)


def _func_successful_requests_call(timeout=None):
    response = MagicMock()
    response.raise_for_status = lambda: None
    return response


def _func_raise_requests_exception_once(val, timeout=None):
    if len(val) > 0:
        response = MagicMock()
        response.raise_for_status = lambda: None
        return response
    val.append(0)
    ex = requests.Timeout()
    raise ex


def _func_raise_requests_connection_error_once(val, timeout=None):
    if len(val) > 0:
        response = MagicMock()
        response.raise_for_status = lambda: None
        return response
    val.append(0)
    ex = requests.ConnectionError(
        requests.packages.urllib3.exceptions.ProtocolError(
            'Connection aborted.',
            socket.error(errno.ECONNRESET, 'Connection reset by peer')))
    raise ex


def _func_raise_requests_chunked_encoding_error_once(val, timeout=None):
    if len(val) > 0:
        response = MagicMock()
        response.raise_for_status = lambda: None
        return response
    val.append(0)
    ex = requests.exceptions.ChunkedEncodingError(
        requests.packages.urllib3.exceptions.ProtocolError(
            'Connection aborted.',
            socket.error(errno.ECONNRESET, 'Connection reset by peer')))
    raise ex


def _func_raise_azurehttperror_once(val, timeout=None):
    if len(val) > 0:
        response = MagicMock()
        return response
    val.append(0)
    ex = azure.common.AzureHttpError('ServerBusy', 503)
    raise ex


@patch('time.sleep', return_value=None)
def test_azure_request(patched_time_sleep):
    azcomerr = azure.common.AzureHttpError('ServerBusy', 503)

    with pytest.raises(IOError):
        mock = Mock(side_effect=azcomerr)
        mock.__name__ = 'name'
        blobxfer.azure_request(mock, timeout=0.001)

    with pytest.raises(Exception):
        ex = Exception()
        ex.message = 'Uncaught'
        blobxfer.azure_request(Mock(side_effect=ex))

    with pytest.raises(Exception):
        ex = Exception()
        ex.__delattr__('message')
        blobxfer.azure_request(Mock(side_effect=ex))

    blobxfer.azure_request(
        _func_raise_requests_connection_error_once, val=[], timeout=1)

    blobxfer.azure_request(
        _func_raise_requests_chunked_encoding_error_once, val=[], timeout=1)

    blobxfer.azure_request(
        _func_raise_azurehttperror_once, val=[], timeout=1)

    with pytest.raises(requests.HTTPError):
        exc = requests.HTTPError()
        exc.response = MagicMock()
        exc.response.status_code = 404
        mock = Mock(side_effect=exc)
        blobxfer.azure_request(mock)

    try:
        blobxfer.azure_request(
            _func_raise_requests_exception_once, val=[], timeout=1)
    except Exception:
        pytest.fail('unexpected Exception raised')

    try:
        blobxfer.azure_request(_func_successful_requests_call)
    except Exception:
        pytest.fail('unexpected Exception raised')


def test_sasblobservice_listblobs():
    session = requests.Session()
    adapter = requests_mock.Adapter()
    session.mount('mock', adapter)
    content = b'<?xml version="1.0" encoding="utf-8"?><EnumerationResults ' + \
        b'ServiceEndpoint="http://myaccount.blob.core.windows.net/" ' + \
        b'ContainerName="mycontainer"><Prefix>string-value</Prefix>' + \
        b'<Marker>string-value</Marker><MaxResults>int-value</MaxResults>' + \
        b'<Delimiter>string-value</Delimiter><Blobs><Blob><Name>blob-name' + \
        b'</Name><Snapshot>date-time-value</Snapshot><Properties>' + \
        b'<Last-Modified>date-time-value</Last-Modified><Etag>etag</Etag>' + \
        b'<Content-Length>2147483648</Content-Length><Content-Type>' + \
        b'blob-content-type</Content-Type><Content-Encoding />' + \
        b'<Content-Language /><Content-MD5>abc</Content-MD5>' + \
        b'<Cache-Control /><x-ms-blob-sequence-number>sequence-number' + \
        b'</x-ms-blob-sequence-number><BlobType>BlockBlob</BlobType>' + \
        b'<LeaseStatus>locked|unlocked</LeaseStatus><LeaseState>' + \
        b'available | leased | expired | breaking | broken</LeaseState>' + \
        b'<LeaseDuration>infinite | fixed</LeaseDuration><CopyId>id' + \
        b'</CopyId><CopyStatus>pending | success | aborted | failed' + \
        b'</CopyStatus><CopySource>source url</CopySource><CopyProgress>' + \
        b'bytes copied/bytes total</CopyProgress><CopyCompletionTime>' + \
        b'datetime</CopyCompletionTime><CopyStatusDescription>' + \
        b'error string</CopyStatusDescription></Properties><Metadata>' + \
        b'<Name>value</Name></Metadata></Blob><BlobPrefix><Name>' + \
        b'blob-prefix</Name></BlobPrefix></Blobs><NextMarker>nm' + \
        b'</NextMarker></EnumerationResults>'

    with requests_mock.mock() as m:
        m.get('mock://blobepcontainer?saskey', content=content)
        sbs = blobxfer.SasBlobService('mock://blobep', 'saskey', None)
        metamock = MagicMock()
        metamock.metadata = True
        result = sbs.list_blobs('container', 'marker', include=metamock)
        assert len(result) == 1
        assert result[0].name == 'blob-name'
        assert result[0].properties.content_length == 2147483648
        assert result[0].properties.content_settings.content_md5 == 'abc'
        assert result[0].properties.blobtype == 'BlockBlob'
        assert result[0].metadata['Name'] == 'value'
        assert result.next_marker == 'nm'

        m.get('mock://blobepcontainer?saskey', content=b'', status_code=201)
        sbs = blobxfer.SasBlobService('mock://blobep', 'saskey', None)
        with pytest.raises(IOError):
            sbs.list_blobs('container', 'marker')


def test_sasblobservice_setblobmetadata():
    session = requests.Session()
    adapter = requests_mock.Adapter()
    session.mount('mock', adapter)

    with requests_mock.mock() as m:
        m.put('mock://blobepcontainer/blob?saskey')
        sbs = blobxfer.SasBlobService('mock://blobep', 'saskey', None)
        sbs.set_blob_metadata('container', 'blob', None)
        sbs.set_blob_metadata('container', 'blob', {'name': 'value'})

        m.put('mock://blobepcontainer/blob?saskey', status_code=201)
        with pytest.raises(IOError):
            sbs.set_blob_metadata('container', 'blob', {'name': 'value'})


def test_sasblobservice_getblob():
    session = requests.Session()
    adapter = requests_mock.Adapter()
    session.mount('mock', adapter)

    with requests_mock.mock() as m:
        m.get('mock://blobepcontainer/blob?saskey', content=b'data')
        sbs = blobxfer.SasBlobService('mock://blobep', 'saskey', None)
        results = sbs._get_blob('container', 'blob', 0, 1)
        assert results.content == b'data'

        m.get('mock://blobepcontainer/blob?saskey', status_code=201)
        sbs = blobxfer.SasBlobService('mock://blobep', 'saskey', None)
        with pytest.raises(IOError):
            sbs._get_blob('container', 'blob', 0, 1)


def test_sasblobservice_getblobproperties():
    session = requests.Session()
    adapter = requests_mock.Adapter()
    session.mount('mock', adapter)

    with requests_mock.mock() as m:
        m.head('mock://blobepcontainer/blob?saskey',
               headers={'x-ms-meta-hello': 'world', 'content-length': '1'})
        sbs = blobxfer.SasBlobService('mock://blobep', '?saskey', None)
        results = sbs.get_blob_properties('container', 'blob')
        assert results.metadata['hello'] == 'world'

        m.head('mock://blobepcontainer/blob?saskey', text='', status_code=201)
        sbs = blobxfer.SasBlobService('mock://blobep', 'saskey', None)
        with pytest.raises(IOError):
            sbs.get_blob_properties('container', 'blob')


def test_sasblobservice_putblock():
    session = requests.Session()
    adapter = requests_mock.Adapter()
    session.mount('mock', adapter)

    with requests_mock.mock() as m:
        m.put('mock://blobepcontainer/blob?saskey', status_code=201)
        sbs = blobxfer.SasBlobService('mock://blobep', '?saskey', None)
        try:
            sbs.put_block(
                'container', 'blob', 'block', 'blockid',
                validate_content=False)
        except Exception:
            pytest.fail('unexpected Exception raised')

        m.put('mock://blobepcontainer/blob?saskey', text='', status_code=200)
        sbs = blobxfer.SasBlobService('mock://blobep', 'saskey', None)
        with pytest.raises(IOError):
            sbs.put_block(
                'container', 'blob', 'block', 'blockid',
                validate_content=False)


def test_sasblobservice_putblocklist():
    session = requests.Session()
    adapter = requests_mock.Adapter()
    session.mount('mock', adapter)

    with requests_mock.mock() as m:
        m.put('mock://blobepcontainer/blob?saskey', status_code=201)
        sbs = blobxfer.SasBlobService('mock://blobep', 'saskey', None)
        block_list = [
            azure.storage.blob.BlobBlock(id='1'),
            azure.storage.blob.BlobBlock(id='2')
        ]
        cs = azure.storage.blob.ContentSettings(content_md5='md5')
        sbs.put_block_list('container', 'blob', block_list, cs)

        m.put('mock://blobepcontainer/blob?saskey', text='', status_code=200)
        sbs = blobxfer.SasBlobService('mock://blobep', 'saskey', None)
        with pytest.raises(IOError):
            sbs.put_block_list('container', 'blob', block_list, cs)


def test_sasblobservice_setblobproperties():
    session = requests.Session()
    adapter = requests_mock.Adapter()
    session.mount('mock', adapter)

    with requests_mock.mock() as m:
        m.put('mock://blobepcontainer/blob?saskey', status_code=200)
        sbs = blobxfer.SasBlobService('mock://blobep', 'saskey', None)
        cs = azure.storage.blob.ContentSettings(content_md5='md5')
        sbs.set_blob_properties('container', 'blob', cs)

        m.put('mock://blobepcontainer/blob?saskey', text='', status_code=201)
        sbs = blobxfer.SasBlobService('mock://blobep', 'saskey', None)
        with pytest.raises(IOError):
            sbs.set_blob_properties('container', 'blob', cs)


def test_sasblobservice_putblob():
    session = requests.Session()
    adapter = requests_mock.Adapter()
    session.mount('mock', adapter)

    with requests_mock.mock() as m:
        m.put('mock://blobepcontainer/blob?saskey', status_code=201)
        sbs = blobxfer.SasBlobService('mock://blobep', 'saskey', None)
        cs = azure.storage.blob.ContentSettings(
            content_type='a', content_md5='md5')
        sbs._put_blob('container', 'blob', None, cs)

        m.put('mock://blobepcontainer/blob?saskey', content=b'',
              status_code=200)
        sbs = blobxfer.SasBlobService('mock://blobep', 'saskey', None)
        with pytest.raises(IOError):
            sbs._put_blob('container', 'blob', None, cs)


def test_sasblobservice_createblob():
    session = requests.Session()
    adapter = requests_mock.Adapter()
    session.mount('mock', adapter)

    with requests_mock.mock() as m:
        m.put('mock://blobepcontainer/blob?saskey', content=b'',
              status_code=201)
        sbs = blobxfer.SasBlobService('mock://blobep', 'saskey', None)
        cs = azure.storage.blob.ContentSettings(content_md5='md5')
        sbs.create_blob('container', 'blob', 0, cs)

        m.put('mock://blobepcontainer/blob?saskey', content=b'',
              status_code=200)
        sbs = blobxfer.SasBlobService('mock://blobep', 'saskey', None)
        with pytest.raises(IOError):
            sbs.create_blob('container', 'blob', 0, cs)


def test_storagechunkworker_run(tmpdir):
    lpath = str(tmpdir.join('test.tmp'))
    with open(lpath, 'wt') as f:
        f.write(str(uuid.uuid4()))
    args = MagicMock()
    args.rsakey = None
    args.pageblob = True
    args.autovhd = False
    args.timeout = None
    args.fileshare = False

    session = requests.Session()
    adapter = requests_mock.Adapter()
    session.mount('mock', adapter)

    exc_list = []
    flock = threading.Lock()
    sa_in_queue = queue.PriorityQueue()
    sa_out_queue = queue.Queue()
    with requests_mock.mock() as m:
        m.put('mock://blobepcontainer/blob?saskey', status_code=200)
        sbs = blobxfer.SasBlobService('mock://blobep', 'saskey', None)
        bcw = blobxfer.StorageChunkWorker(
            exc_list, sa_in_queue, sa_out_queue, args, True, (sbs, sbs), None)
        with pytest.raises(IOError):
            bcw.put_storage_data(
                lpath, 'container', 'blob', 'blockid', 0, 4, None, flock, None)

    args.pageblob = False
    with requests_mock.mock() as m:
        m.put('mock://blobepcontainer/blob?saskey', status_code=201)
        sbs = blobxfer.SasBlobService('mock://blobep', 'saskey', None)
        bcw = blobxfer.StorageChunkWorker(
            exc_list, sa_in_queue, sa_out_queue, args, True, (sbs, sbs), None)
        bcw.put_storage_data(
            lpath, 'container', 'blob', 'blockid', 0, 4, None, flock, None)

        m.get('mock://blobepcontainer/blob?saskey', status_code=200)
        bcw.get_storage_range(
            lpath, 'container', 'blob', 0, 0, 4,
            [None, None, None, None, None, False], flock, None)

        # test zero-length putblob
        bcw.put_storage_data(
            lpath, 'container', 'blob', 'blockid', 0, 0, None, flock, None)
        bcw._pageblob = True
        bcw.put_storage_data(
            lpath, 'container', 'blob', 'blockid', 0, 0, None, flock, None)

        # test empty page
        with open(lpath, 'wb') as f:
            f.write(b'\0' * 4 * 1024 * 1024)
        bcw.put_storage_data(
            lpath, 'container', 'blob', 'blockid', 0, 4 * 1024 * 1024,
            None, flock, None)
        with open(lpath, 'wb') as f:
            f.write(b'\0' * 4 * 1024)
        bcw.put_storage_data(
            lpath, 'container', 'blob', 'blockid', 0, 4 * 1024,
            None, flock, None)

    sa_in_queue.put((0, (lpath, 'container', 'blob', 'blockid', 0, 4,
                         [None, None, None, None], flock, None)))
    with requests_mock.mock() as m:
        sbs = blobxfer.SasBlobService('mock://blobep', 'saskey', None)
        bcw = blobxfer.StorageChunkWorker(
            exc_list, sa_in_queue, sa_out_queue, args, False, (sbs, sbs), None)
        m.get('mock://blobepcontainer/blob?saskey', status_code=201)
        bcw.run()
        assert len(exc_list) > 0


@patch('azure.storage.file.FileService.update_range')
@patch('azure.storage.file.FileService._get_file')
def test_storagechunkworker_files_run(
        patched_get_file, patched_update_range, tmpdir):
    lpath = str(tmpdir.join('test.tmp'))
    with open(lpath, 'wt') as f:
        f.write(str(uuid.uuid4()))
    args = MagicMock()
    args.rsakey = None
    args.pageblob = False
    args.autovhd = False
    args.timeout = None
    args.fileshare = True

    exc_list = []
    flock = threading.Lock()
    sa_in_queue = queue.PriorityQueue()
    sa_out_queue = queue.Queue()
    fs = azure.storage.file.FileService(account_name='sa', account_key='key')
    bcw = blobxfer.StorageChunkWorker(
        exc_list, sa_in_queue, sa_out_queue, args, True, None, fs)
    patched_update_range.return_value = MagicMock()
    bcw.put_storage_data(
        lpath, 'container', 'blob', 'blockid', 0, 4, None, flock, None)

    bcw = blobxfer.StorageChunkWorker(
        exc_list, sa_in_queue, sa_out_queue, args, False, None, fs)
    patched_get_file.return_value = MagicMock()
    patched_get_file.return_value.content = b''
    bcw.get_storage_range(
        lpath, 'container', 'blob', 0, 0, 4,
        [None, None, None, None, None, False], flock, None)


@patch('blobxfer.azure_request', return_value=None)
def test_generate_xferspec_download_invalid(patched_azure_request):
    args = MagicMock()
    args.storageaccount = 'blobep'
    args.container = 'container'
    args.storageaccountkey = 'saskey'
    args.chunksizebytes = 5
    args.timeout = None
    args.fileshare = False
    sa_in_queue = queue.PriorityQueue()

    with requests_mock.mock() as m:
        m.head('mock://blobepcontainer/blob?saskey', headers={
            'content-length': '-1', 'content-md5': 'md5'})
        sbs = blobxfer.SasBlobService('mock://blobep', 'saskey', None)
        with pytest.raises(ValueError):
            blobxfer.generate_xferspec_download(
                sbs, None, args, sa_in_queue, 'tmppath', 'blob', True,
                [None, None, None])


def test_generate_xferspec_download(tmpdir):
    lpath = str(tmpdir.join('test.tmp'))
    args = MagicMock()
    args.rsakey = None
    args.storageaccount = 'blobep'
    args.container = 'container'
    args.storageaccountkey = 'saskey'
    args.chunksizebytes = 5
    args.timeout = None
    args.fileshare = False
    sa_in_queue = queue.PriorityQueue()

    session = requests.Session()
    adapter = requests_mock.Adapter()
    session.mount('mock', adapter)

    with requests_mock.mock() as m:
        m.head('mock://blobepcontainer/blob?saskey', headers={
            'content-length': '-1', 'content-md5': 'md5'})
        sbs = blobxfer.SasBlobService('mock://blobep', 'saskey', None)
        with pytest.raises(ValueError):
            blobxfer.generate_xferspec_download(
                sbs, None, args, sa_in_queue, lpath, 'blob', True,
                [None, None, None])
        assert sa_in_queue.qsize() == 0
        m.head('mock://blobepcontainer/blob?saskey', headers={
            'content-length': '6', 'content-md5': 'md5'})
        cl, nsops, md5, fd = blobxfer.generate_xferspec_download(
            sbs, None, args, sa_in_queue, lpath, 'blob', True,
            [None, None, None])
        assert sa_in_queue.qsize() == 2
        assert 2 == nsops
        assert 6 == cl
        assert 2 == nsops
        assert 'md5' == md5
        assert fd is not None
        fd.close()
        cl, nsops, md5, fd = blobxfer.generate_xferspec_download(
            sbs, None, args, sa_in_queue, lpath, 'blob', False,
            [None, None, None])
        assert 2 == nsops
        assert fd is None
        assert sa_in_queue.qsize() == 4
        with open(lpath, 'wt') as f:
            f.write('012345')
        m.head('mock://blobepcontainer/blob?saskey', headers={
            'content-length': '6', 'content-md5': '1qmpM8iq/FHlWsBmK25NSg=='})
        cl, nsops, md5, fd = blobxfer.generate_xferspec_download(
            sbs, None, args, sa_in_queue, lpath, 'blob', True,
            [None, None, None])
        assert nsops is None
        assert cl is None
        assert sa_in_queue.qsize() == 4

        sa_in_queue = queue.PriorityQueue()
        args.rsaprivatekey = _RSAKEY
        args.rsapublickey = None
        symkey, signkey = blobxfer.generate_aes256_keys()
        args.encmode = blobxfer._ENCRYPTION_MODE_CHUNKEDBLOB
        metajson = blobxfer.EncryptionMetadataJson(
            args, symkey, signkey, iv=b'0', encdata_signature=b'0',
            preencrypted_md5=None)
        encmeta = metajson.construct_metadata_json()
        goodencjson = json.loads(encmeta[blobxfer._ENCRYPTION_METADATA_NAME])
        goodauthjson = json.loads(
            encmeta[blobxfer._ENCRYPTION_METADATA_AUTH_NAME])
        metajson2 = blobxfer.EncryptionMetadataJson(
            args, None, None, None, None, None)
        metajson2.parse_metadata_json(
            'blob', args.rsaprivatekey, args.rsapublickey, encmeta)
        assert metajson2.symkey == symkey
        assert metajson2.signkey == signkey
        assert metajson2.encmode == args.encmode
        assert metajson2.chunksizebytes == args.chunksizebytes + \
            blobxfer._AES256CBC_HMACSHA256_OVERHEAD_BYTES + 1
        encjson = json.loads(encmeta[blobxfer._ENCRYPTION_METADATA_NAME])
        encjson[blobxfer._ENCRYPTION_METADATA_LAYOUT][
            blobxfer._ENCRYPTION_METADATA_CHUNKSTRUCTURE] = 'X'
        headers = {
            'content-length': '64',
            'content-md5': 'md5',
            'x-ms-meta-' + blobxfer._ENCRYPTION_METADATA_NAME:
            json.dumps(encjson),
            'x-ms-meta-' + blobxfer._ENCRYPTION_METADATA_AUTH_NAME:
            json.dumps(goodauthjson),
        }
        m.head('mock://blobepcontainer/blob?saskey', headers=headers)
        with pytest.raises(RuntimeError):
            blobxfer.generate_xferspec_download(
                sbs, None, args, sa_in_queue, lpath, 'blob', False,
                [None, None, None])

        # switch to full blob mode tests
        args.encmode = blobxfer._ENCRYPTION_MODE_FULLBLOB
        metajson = blobxfer.EncryptionMetadataJson(
            args, symkey, signkey, iv=b'0', encdata_signature=b'0',
            preencrypted_md5=None)
        encmeta = metajson.construct_metadata_json()
        goodencjson = json.loads(encmeta[blobxfer._ENCRYPTION_METADATA_NAME])
        goodauthjson = json.loads(
            encmeta[blobxfer._ENCRYPTION_METADATA_AUTH_NAME])
        headers['x-ms-meta-' + blobxfer._ENCRYPTION_METADATA_NAME] = \
            json.dumps(goodencjson)
        headers['x-ms-meta-' + blobxfer._ENCRYPTION_METADATA_AUTH_NAME] = \
            json.dumps(goodauthjson)

        encjson = copy.deepcopy(goodencjson)
        encjson[blobxfer._ENCRYPTION_METADATA_AGENT][
            blobxfer._ENCRYPTION_METADATA_PROTOCOL] = 'X'
        headers['x-ms-meta-' + blobxfer._ENCRYPTION_METADATA_NAME] = \
            json.dumps(encjson)
        m.head('mock://blobepcontainer/blob?saskey', headers=headers)
        with pytest.raises(RuntimeError):
            blobxfer.generate_xferspec_download(
                sbs, None, args, sa_in_queue, lpath, 'blob', False,
                [None, None, None])

        encjson = copy.deepcopy(goodencjson)
        encjson[blobxfer._ENCRYPTION_METADATA_AGENT][
            blobxfer._ENCRYPTION_METADATA_ENCRYPTION_ALGORITHM] = 'X'
        headers['x-ms-meta-' + blobxfer._ENCRYPTION_METADATA_NAME] = \
            json.dumps(encjson)
        m.head('mock://blobepcontainer/blob?saskey', headers=headers)
        with pytest.raises(RuntimeError):
            blobxfer.generate_xferspec_download(
                sbs, None, args, sa_in_queue, lpath, 'blob', False,
                [None, None, None])

        encjson = copy.deepcopy(goodencjson)
        encjson[blobxfer._ENCRYPTION_METADATA_INTEGRITY_AUTH][
            blobxfer._ENCRYPTION_METADATA_ALGORITHM] = 'X'
        headers['x-ms-meta-' + blobxfer._ENCRYPTION_METADATA_NAME] = \
            json.dumps(encjson)
        m.head('mock://blobepcontainer/blob?saskey', headers=headers)
        with pytest.raises(RuntimeError):
            blobxfer.generate_xferspec_download(
                sbs, None, args, sa_in_queue, lpath, 'blob', False,
                [None, None, None])

        encjson = copy.deepcopy(goodencjson)
        encjson[blobxfer._ENCRYPTION_METADATA_WRAPPEDCONTENTKEY][
            blobxfer._ENCRYPTION_METADATA_ALGORITHM] = 'X'
        headers['x-ms-meta-' + blobxfer._ENCRYPTION_METADATA_NAME] = \
            json.dumps(encjson)
        m.head('mock://blobepcontainer/blob?saskey', headers=headers)
        with pytest.raises(RuntimeError):
            blobxfer.generate_xferspec_download(
                sbs, None, args, sa_in_queue, lpath, 'blob', False,
                [None, None, None])

        authjson = copy.deepcopy(goodauthjson)
        authjson.pop(blobxfer._ENCRYPTION_METADATA_AUTH_METAAUTH, None)
        headers['x-ms-meta-' + blobxfer._ENCRYPTION_METADATA_NAME] = \
            json.dumps(goodencjson)
        headers['x-ms-meta-' + blobxfer._ENCRYPTION_METADATA_AUTH_NAME] = \
            json.dumps(authjson)
        m.head('mock://blobepcontainer/blob?saskey', headers=headers)
        with pytest.raises(RuntimeError):
            blobxfer.generate_xferspec_download(
                sbs, None, args, sa_in_queue, lpath, 'blob', False,
                [None, None, None])

        authjson = copy.deepcopy(goodauthjson)
        authjson[blobxfer._ENCRYPTION_METADATA_AUTH_METAAUTH].pop(
            blobxfer._ENCRYPTION_METADATA_AUTH_ENCODING, None)
        headers['x-ms-meta-' + blobxfer._ENCRYPTION_METADATA_NAME] = \
            json.dumps(goodencjson)
        headers['x-ms-meta-' + blobxfer._ENCRYPTION_METADATA_AUTH_NAME] = \
            json.dumps(authjson)
        m.head('mock://blobepcontainer/blob?saskey', headers=headers)
        with pytest.raises(RuntimeError):
            blobxfer.generate_xferspec_download(
                sbs, None, args, sa_in_queue, lpath, 'blob', False,
                [None, None, None])

        authjson = copy.deepcopy(goodauthjson)
        authjson[blobxfer._ENCRYPTION_METADATA_AUTH_METAAUTH][
            blobxfer._ENCRYPTION_METADATA_ALGORITHM] = 'X'
        headers['x-ms-meta-' + blobxfer._ENCRYPTION_METADATA_NAME] = \
            json.dumps(goodencjson)
        headers['x-ms-meta-' + blobxfer._ENCRYPTION_METADATA_AUTH_NAME] = \
            json.dumps(authjson)
        m.head('mock://blobepcontainer/blob?saskey', headers=headers)
        with pytest.raises(RuntimeError):
            blobxfer.generate_xferspec_download(
                sbs, None, args, sa_in_queue, lpath, 'blob', False,
                [None, None, None])

        authjson = copy.deepcopy(goodauthjson)
        authjson[blobxfer._ENCRYPTION_METADATA_AUTH_METAAUTH][
            blobxfer._ENCRYPTION_METADATA_MAC] = blobxfer.base64encode(b'X')
        headers['x-ms-meta-' + blobxfer._ENCRYPTION_METADATA_NAME] = \
            json.dumps(goodencjson)
        headers['x-ms-meta-' + blobxfer._ENCRYPTION_METADATA_AUTH_NAME] = \
            json.dumps(authjson)
        m.head('mock://blobepcontainer/blob?saskey', headers=headers)
        with pytest.raises(RuntimeError):
            blobxfer.generate_xferspec_download(
                sbs, None, args, sa_in_queue, lpath, 'blob', False,
                [None, None, None])

        args.chunksizebytes = 5
        metajson.chunksizebytes = args.chunksizebytes
        metajson.md5 = headers['content-md5']
        args.encmode = blobxfer._ENCRYPTION_MODE_FULLBLOB
        encjson = copy.deepcopy(goodencjson)
        headers['x-ms-meta-' + blobxfer._ENCRYPTION_METADATA_NAME] = \
            json.dumps(encjson)
        headers['x-ms-meta-' + blobxfer._ENCRYPTION_METADATA_AUTH_NAME] = \
            json.dumps(goodauthjson)
        hcl = int(headers['content-length'])
        cl, nsops, md5, fd = blobxfer.generate_xferspec_download(
            sbs, None, args, sa_in_queue, lpath, 'blob', False,
            [hcl, headers['content-md5'], metajson])
        assert hcl == cl
        calcops = hcl // args.chunksizebytes
        hclmod = hcl % args.chunksizebytes
        if hclmod > 0:
            calcops += 1
        assert calcops == nsops
        assert headers['content-md5'] == md5
        assert fd is None
        assert sa_in_queue.qsize() == nsops
        data = sa_in_queue.get()
        assert data is not None


def test_generate_xferspec_upload(tmpdir):
    lpath = str(tmpdir.join('test.tmp'))
    with open(lpath, 'wt') as f:
        f.write(str(uuid.uuid4()))
    args = MagicMock()
    args.storageaccount = 'sa'
    args.container = 'container'
    args.storageaccountkey = 'key'
    args.chunksizebytes = 5
    args.skiponmatch = False
    args.pageblob = False
    args.autovhd = False
    sa_in_queue = queue.PriorityQueue()
    fs, nsops, md5, fd = blobxfer.generate_xferspec_upload(
        args, sa_in_queue, {}, {}, lpath, 'rr', True)
    stat = os.stat(lpath)
    assert stat.st_size == fs
    assert math.ceil(stat.st_size / 5.0) == nsops
    assert fd is not None
    fd.close()
    args.skiponmatch = True
    with open(lpath, 'wt') as f:
        f.write('012345')
    sd = {}
    sd['rr'] = [6, '1qmpM8iq/FHlWsBmK25NSg==']
    fs, nsops, md5, fd = blobxfer.generate_xferspec_upload(
        args, sa_in_queue, sd, {}, lpath, 'rr', False)
    assert fs is None


def test_apply_file_collation_and_strip():
    args = MagicMock()
    args.collate = 'collatedir'
    rfname = blobxfer.apply_file_collation_and_strip(
        args, 'tmpdir/file0')
    assert rfname == 'collatedir/file0'

    args.collate = None
    args.stripcomponents = 0
    rfname = blobxfer.apply_file_collation_and_strip(
        args, 'tmpdir/file0')
    assert rfname == 'tmpdir/file0'
    args.stripcomponents = 1
    rfname = blobxfer.apply_file_collation_and_strip(
        args, 'tmpdir/file0')
    assert rfname == 'file0'
    args.stripcomponents = 2
    rfname = blobxfer.apply_file_collation_and_strip(
        args, 'tmpdir/file0')
    assert rfname == 'file0'
    args.stripcomponents = 1
    rfname = blobxfer.apply_file_collation_and_strip(
        args, '/tmpdir/tmpdir2/file0')
    assert rfname == 'tmpdir2/file0'
    args.stripcomponents = 2
    rfname = blobxfer.apply_file_collation_and_strip(
        args, 'tmpdir/tmpdir2/file0')
    assert rfname == 'file0'


@patch('azure.storage.file.FileService.create_directory')
def test_create_all_parent_directories_fileshare(patched_cd):
    patched_cd.return_value = MagicMock()
    fsfile = ['tmp/a/b', None]
    file_service = MagicMock()
    args = MagicMock()
    args.container = 'fshare'
    args.timeout = None
    dirscreated = set()
    blobxfer.create_all_parent_directories_fileshare(
        file_service, args, fsfile, dirscreated)
    assert len(dirscreated) == 3
    assert 'tmp' in dirscreated
    assert 'tmp/a' in dirscreated
    assert 'tmp/a/b' in dirscreated
    fsfile = ['tmp/a/b/c', None]
    blobxfer.create_all_parent_directories_fileshare(
        file_service, args, fsfile, dirscreated)
    assert len(dirscreated) == 4
    assert 'tmp/a/b/c' in dirscreated
    fsfile = ['x/a/b/c', None]
    blobxfer.create_all_parent_directories_fileshare(
        file_service, args, fsfile, dirscreated)
    assert len(dirscreated) == 8
    assert 'x/a/b/c' in dirscreated


def _mock_get_storage_account_keys(timeout=None, service_name=None):
    ret = MagicMock()
    ret.storage_service_keys.primary = 'mmkey'
    return ret


def _mock_get_storage_account_properties(timeout=None, service_name=None):
    ret = MagicMock()
    ret.storage_service_properties.endpoints = [None]
    return ret


def _mock_blobservice_create_container(timeout=None, container_name=None,
                                       fail_on_exist=None):
    raise azure.common.AzureConflictHttpError('conflict', 409)


@patch('blobxfer.parseargs')
@patch('azure.servicemanagement.ServiceManagementService.'
       'get_storage_account_keys')
@patch('azure.servicemanagement.ServiceManagementService.'
       'get_storage_account_properties')
def test_main1(
        patched_sms_saprops, patched_sms_sakeys, patched_parseargs, tmpdir):
    lpath = str(tmpdir.join('test.tmp'))
    args = MagicMock()
    args.include = None
    args.stripcomponents = 0
    args.delete = False
    args.rsaprivatekey = None
    args.rsapublickey = None
    args.rsakeypassphrase = None
    args.numworkers = 0
    args.localresource = ''
    args.storageaccount = 'blobep'
    args.container = 'container'
    args.storageaccountkey = None
    os.environ[blobxfer._ENVVAR_STORAGEACCOUNTKEY] = 'saskey'
    args.chunksizebytes = 5
    args.pageblob = False
    args.autovhd = False
    args.fileshare = False
    patched_parseargs.return_value = args
    with pytest.raises(ValueError):
        blobxfer.main()
    args.localresource = lpath
    args.endpoint = ''
    with pytest.raises(ValueError):
        blobxfer.main()
    args.endpoint = 'blobep'
    args.upload = True
    args.download = True
    with pytest.raises(ValueError):
        blobxfer.main()
    args.upload = None
    args.download = None
    with pytest.raises(ValueError):
        blobxfer.main()
    os.environ.pop(blobxfer._ENVVAR_STORAGEACCOUNTKEY)
    args.storageaccountkey = None
    args.timeout = -1
    args.saskey = ''
    with pytest.raises(ValueError):
        blobxfer.main()
    args.saskey = None
    args.storageaccountkey = None
    args.managementcert = 'cert.spam'
    args.subscriptionid = '1234'
    with pytest.raises(ValueError):
        blobxfer.main()
    args.managementcert = 'cert.pem'
    args.managementep = None
    with pytest.raises(ValueError):
        blobxfer.main()
    args.managementep = 'mep'
    args.subscriptionid = None
    with pytest.raises(ValueError):
        blobxfer.main()
    args.subscriptionid = '1234'
    args.pageblob = True
    args.autovhd = True
    with pytest.raises(ValueError):
        blobxfer.main()
    args.autovhd = False
    args.fileshare = True
    with pytest.raises(ValueError):
        blobxfer.main()
    args.pageblob = False
    args.autovhd = True
    with pytest.raises(ValueError):
        blobxfer.main()
    args.autovhd = False
    args.fileshare = False
    with patch('azure.servicemanagement.ServiceManagementService') as mock:
        mock.return_value = MagicMock()
        mock.return_value.get_storage_account_keys = \
            _mock_get_storage_account_keys
        mock.return_value.get_storage_account_properties = \
            _mock_get_storage_account_properties
        with pytest.raises(ValueError):
            blobxfer.main()
    args.managementep = None
    args.managementcert = None
    args.subscriptionid = None
    args.remoteresource = 'blob'
    args.chunksizebytes = None
    with patch('azure.storage.blob.BlockBlobService') as mock:
        mock.return_value = None
        with pytest.raises(ValueError):
            blobxfer.main()
    args.storageaccountkey = None
    args.saskey = None
    os.environ[blobxfer._ENVVAR_SASKEY] = 'saskey'
    args.remoteresource = None
    args.download = True
    with pytest.raises(ValueError):
        blobxfer.main()

    args.download = False
    args.upload = True
    args.remoteresource = None
    args.storageaccountkey = ''
    args.saskey = None
    with pytest.raises(ValueError):
        blobxfer.main()

    args.collate = 'collatetmp'
    with pytest.raises(ValueError):
        blobxfer.main()

    args.collate = None
    args.storageaccountkey = None
    args.saskey = ''
    with pytest.raises(ValueError):
        blobxfer.main()

    args.saskey = None
    os.environ.pop(blobxfer._ENVVAR_SASKEY)
    with pytest.raises(ValueError):
        blobxfer.main()
    args.managementcert = '0'
    args.managementep = ''
    args.subscriptionid = '0'
    with pytest.raises(ValueError):
        blobxfer.main()
    args.managementcert = 'test.pem'
    with pytest.raises(ValueError):
        blobxfer.main()
    args.managementep = 'mep.mep'
    ssk = MagicMock()
    ssk.storage_service_keys = MagicMock()
    ssk.storage_service_keys.primary = ''
    patched_sms_sakeys.return_value = ssk
    ssp = MagicMock()
    ssp.storage_service_properties = MagicMock()
    ssp.storage_service_properties.endpoints = ['blobep']
    patched_sms_saprops.return_value = ssp
    with pytest.raises(ValueError):
        blobxfer.main()
    ssk.storage_service_keys.primary = 'key1'
    args.storageaccountkey = None
    args.rsaprivatekey = ''
    args.rsapublickey = ''
    with pytest.raises(ValueError):
        blobxfer.main()
    args.rsaprivatekey = ''
    args.rsapublickey = None
    args.encmode = blobxfer._ENCRYPTION_MODE_FULLBLOB
    with pytest.raises(IOError):
        blobxfer.main()

    args.rsaprivatekey = None
    args.storageaccountkey = None
    args.managementcert = None
    args.managementep = None
    args.subscriptionid = None
    os.environ[blobxfer._ENVVAR_SASKEY] = 'saskey'
    with open(lpath, 'wt') as f:
        f.write(str(uuid.uuid4()))

    session = requests.Session()
    adapter = requests_mock.Adapter()
    session.mount('mock', adapter)
    with requests_mock.mock() as m:
        m.put('https://blobep.blob.blobep/container/blob?saskey'
              '&comp=block&blockid=00000000', status_code=201)
        m.put('https://blobep.blob.blobep/container' + lpath +
              '?saskey&blockid=00000000&comp=block', status_code=201)
        m.put('https://blobep.blob.blobep/container' + lpath +
              '?saskey&comp=blocklist', status_code=201)
        m.put('https://blobep.blob.blobep/container' + lpath +
              '?saskey&comp=block&blockid=00000000', status_code=201)
        m.put('https://blobep.blob.blobep/container' + lpath +
              '?saskey&comp=metadata', status_code=200)
        m.get('https://blobep.blob.blobep/container?saskey&comp=list'
              '&restype=container&maxresults=1000',
              text='<?xml version="1.0" encoding="utf-8"?>'
              '<EnumerationResults ContainerName="https://blobep.blob.blobep/'
              'container"><Blobs><Blob><Name>' + lpath + '</Name>'
              '<Properties><Content-Length>6</Content-Length>'
              '<Content-MD5>md5</Content-MD5><BlobType>BlockBlob</BlobType>'
              '</Properties><Metadata/></Blob></Blobs></EnumerationResults>')
        args.progressbar = False
        args.skiponmatch = True
        blobxfer.main()

        args.progressbar = True
        args.download = True
        args.upload = False
        args.remoteresource = None
        with pytest.raises(ValueError):
            blobxfer.main()

        args.remoteresource = 'blob'
        args.localresource = str(tmpdir)
        m.head('https://blobep.blob.blobep/container/blob?saskey', headers={
            'content-length': '6', 'content-md5': '1qmpM8iq/FHlWsBmK25NSg=='})
        m.get('https://blobep.blob.blobep/container/blob?saskey',
              content=b'012345')
        blobxfer.main()

        args.pageblob = False
        args.autovhd = False
        args.skiponmatch = False
        pemcontents = _RSAKEY.private_bytes(
            encoding=cryptography.hazmat.primitives.serialization.
            Encoding.PEM,
            format=cryptography.hazmat.primitives.serialization.
            PrivateFormat.PKCS8,
            encryption_algorithm=cryptography.hazmat.primitives.
            serialization.NoEncryption())
        pempath = str(tmpdir.join('rsa.pem'))
        with open(pempath, 'wb') as f:
            f.write(pemcontents)
        args.rsaprivatekey = pempath
        blobxfer.main()
        os.remove(pempath)

        args.rsaprivatekey = None
        args.skiponmatch = True
        args.remoteresource = '.'
        args.keepmismatchedmd5files = False
        m.get('https://blobep.blob.blobep/container?saskey&comp=list'
              '&restype=container&maxresults=1000',
              text='<?xml version="1.0" encoding="utf-8"?>'
              '<EnumerationResults ContainerName="https://blobep.blob.blobep/'
              'container"><Blobs><Blob><Name>blob</Name><Properties>'
              '<Content-Length>6</Content-Length><Content-MD5>'
              '</Content-MD5><BlobType>BlockBlob</BlobType></Properties>'
              '<Metadata/></Blob></Blobs></EnumerationResults>')
        m.get('https://blobep.blob.blobep/container/?saskey')
        with pytest.raises(SystemExit):
            blobxfer.main()

        m.get('https://blobep.blob.blobep/container?saskey&comp=list'
              '&restype=container&maxresults=1000',
              text='<?xml version="1.0" encoding="utf-8"?>'
              '<EnumerationResults ContainerName="https://blobep.blob.blobep/'
              'container"><Blobs><Blob><Name>blob</Name><Properties>'
              '<Content-Length>6</Content-Length><Content-MD5>md5'
              '</Content-MD5><BlobType>BlockBlob</BlobType></Properties>'
              '<Metadata/></Blob></Blobs></EnumerationResults>')
        blobxfer.main()

        tmplpath = str(tmpdir.join('test', 'test2', 'test3'))
        args.localresource = tmplpath
        blobxfer.main()

    args.localresource = str(tmpdir)
    notmp_lpath = '/'.join(lpath.strip('/').split('/')[1:])

    with requests_mock.mock() as m:
        args.delete = True
        args.download = False
        args.upload = True
        args.remoteresource = None
        args.skiponmatch = False
        m.put('https://blobep.blob.blobep/container/test.tmp?saskey'
              '&comp=block&blockid=00000000', status_code=200)
        m.put('https://blobep.blob.blobep/container/test.tmp?saskey'
              '&comp=blocklist', status_code=201)
        m.put('https://blobep.blob.blobep/container' + lpath +
              '?saskey&comp=block&blockid=00000000', status_code=200)
        m.put('https://blobep.blob.blobep/container' + lpath +
              '?saskey&comp=blocklist', status_code=201)
        m.put('https://blobep.blob.blobep/container/' + notmp_lpath +
              '?saskey&comp=block&blockid=00000000', status_code=200)
        m.put('https://blobep.blob.blobep/container/' + notmp_lpath +
              '?saskey&comp=blocklist', status_code=201)
        m.get('https://blobep.blob.blobep/container?saskey&comp=list'
              '&restype=container&maxresults=1000',
              text='<?xml version="1.0" encoding="utf-8"?>'
              '<EnumerationResults ContainerName="https://blobep.blob.blobep/'
              'container"><Blobs><Blob><Name>blob</Name><Properties>'
              '<Content-Length>6</Content-Length><Content-MD5>md5'
              '</Content-MD5><BlobType>BlockBlob</BlobType></Properties>'
              '<Metadata/></Blob></Blobs></EnumerationResults>')
        m.delete('https://blobep.blob.blobep/container/blob?saskey',
                 status_code=202)
        with pytest.raises(SystemExit):
            blobxfer.main()

        args.recursive = False
        m.put('https://blobep.blob.blobep/container/blob.blobtmp?saskey'
              '&comp=blocklist', status_code=201)
        m.put('https://blobep.blob.blobep/container/test.tmp.blobtmp?saskey'
              '&comp=blocklist', status_code=201)
        m.put('https://blobep.blob.blobep/container/blob.blobtmp?saskey'
              '&comp=block&blockid=00000000', status_code=200)
        m.put('https://blobep.blob.blobep/container/blob?saskey'
              '&comp=blocklist', status_code=201)
        with pytest.raises(SystemExit):
            blobxfer.main()

        args.stripcomponents = None
        args.collate = '.'
        args.pageblob = True
        args.upload = True
        args.download = False
        m.put('https://blobep.blob.blobep/container/blob.blobtmp?saskey',
              status_code=201)
        m.put('https://blobep.blob.blobep/container/test.tmp?saskey',
              status_code=201)
        m.put('https://blobep.blob.blobep/container/blob.blobtmp?saskey'
              '&comp=properties', status_code=200)
        m.put('https://blobep.blob.blobep/container/test.tmp?saskey'
              '&comp=properties', status_code=200)
        m.put('https://blobep.blob.blobep/container/blob?saskey',
              status_code=201)
        with pytest.raises(IOError):
            blobxfer.main()

        args.stripcomponents = None
        m.put('https://blobep.blob.blobep/container/blobsaskey',
              status_code=200)
        with pytest.raises(IOError):
            blobxfer.main()

        args.stripcomponents = None
        args.pageblob = False
        m.put('https://blobep.blob.blobep/container/' + notmp_lpath +
              '?saskey&comp=blocklist', status_code=201)
        m.put('https://blobep.blob.blobep/container/blob?saskey',
              status_code=201)
        blobxfer.main()

        args.stripcomponents = None
        args.autovhd = True
        blobxfer.main()

        args.stripcomponents = None
        args.include = 'nofiles'
        with pytest.raises(SystemExit):
            blobxfer.main()

        args.stripcomponents = None
        args.include = '*'
        blobxfer.main()

        args.include = None
        args.stripcomponents = None
        args.pageblob = False
        args.autovhd = False
        pempath = str(tmpdir.join('rsa.pem'))
        with open(pempath, 'wb') as f:
            f.write(pemcontents)
        args.rsaprivatekey = pempath
        m.put('https://blobep.blob.blobep/container/rsa.pem?saskey&comp=block'
              '&blockid=00000000', status_code=201)
        m.put('https://blobep.blob.blobep/container/rsa.pem?saskey'
              '&comp=blocklist', status_code=201)
        m.put('https://blobep.blob.blobep/container/rsa.pem?saskey'
              '&comp=metadata', status_code=200)
        m.put('https://blobep.blob.blobep/container/blob?saskey'
              '&comp=metadata', status_code=200)
        m.put('https://blobep.blob.blobep/container/blob.blobtmp?saskey'
              '&comp=metadata', status_code=200)
        m.put('https://blobep.blob.blobep/container/test.tmp.blobtmp?saskey'
              '&comp=metadata', status_code=200)
        m.put('https://blobep.blob.blobep/container/test.tmp?saskey'
              '&comp=metadata', status_code=200)
        blobxfer.main()

        args.stripcomponents = None
        args.download = True
        args.upload = False
        args.rsaprivatekey = pempath
        args.remoteresource = 'blob'
        args.localresource = str(tmpdir)
        m.head('https://blobep.blob.blobep/container/blob?saskey', headers={
            'content-length': '6', 'content-md5': '1qmpM8iq/FHlWsBmK25NSg=='})
        m.get('https://blobep.blob.blobep/container/blob?saskey',
              content=b'012345')
        # TODO add encrypted data json
        blobxfer.main()

        os.environ.pop(blobxfer._ENVVAR_SASKEY)


@patch('blobxfer.parseargs')
def test_main2(patched_parseargs, tmpdir):
    lpath = str(tmpdir.join('test.tmp'))
    args = MagicMock()
    patched_parseargs.return_value = args
    args.include = None
    args.stripcomponents = 1
    args.delete = False
    args.rsaprivatekey = None
    args.rsapublickey = None
    args.numworkers = 64
    args.storageaccount = 'blobep'
    args.container = 'container'
    args.chunksizebytes = 5
    args.localresource = lpath
    args.endpoint = '.blobep'
    args.timeout = 10
    args.managementep = None
    args.managementcert = None
    args.subscriptionid = None
    args.chunksizebytes = None
    args.download = False
    args.upload = True
    args.remoteresource = None
    args.collate = None
    args.saskey = None
    args.storageaccountkey = 'key'
    args.fileshare = False
    with open(lpath, 'wt') as f:
        f.write(str(uuid.uuid4()))

    session = requests.Session()
    adapter = requests_mock.Adapter()
    session.mount('mock', adapter)

    with patch('azure.storage.blob.BlockBlobService') as mock:
        args.createcontainer = True
        args.pageblob = False
        args.autovhd = False
        mock.return_value = MagicMock()
        mock.return_value.create_container = _mock_blobservice_create_container
        blobxfer.main()


@patch('azure.storage.file.FileService.create_share')
@patch('azure.storage.file.FileService.create_file')
@patch('azure.storage.file.FileService.create_directory')
@patch('azure.storage.file.FileService.get_file_properties')
@patch('azure.storage.file.FileService.get_file_metadata')
@patch('azure.storage.file.FileService.list_directories_and_files')
@patch('azure.storage.file.FileService.update_range')
@patch('azure.storage.file.FileService._get_file')
@patch('azure.storage.file.FileService.set_file_properties')
@patch('azure.storage.file.FileService.set_file_metadata')
@patch('azure.storage.file.FileService.resize_file')
@patch('blobxfer.parseargs')
def test_main3(
        patched_parseargs, patched_rf, patched_sfm, patched_sfp,
        patched_get_file, patched_update_range, patched_ldaf, patched_gfm,
        patched_gfp, patched_cd, patched_cf, patched_cs, tmpdir):
    lpath = str(tmpdir.join('test.tmp'))
    args = MagicMock()
    patched_parseargs.return_value = args
    args.include = None
    args.stripcomponents = 1
    args.delete = False
    args.rsaprivatekey = None
    args.rsapublickey = None
    args.numworkers = 64
    args.storageaccount = 'sa'
    args.container = 'myshare'
    args.chunksizebytes = 5
    args.localresource = lpath
    args.endpoint = 'core.windows.net'
    args.timeout = 10
    args.managementep = None
    args.managementcert = None
    args.subscriptionid = None
    args.chunksizebytes = None
    args.download = False
    args.upload = True
    args.remoteresource = None
    args.collate = None
    args.saskey = None
    args.storageaccountkey = 'key'
    args.pageblob = False
    args.autovhd = False
    args.fileshare = True
    args.computefilemd5 = True
    args.skiponmatch = True
    with open(lpath, 'wt') as f:
        f.write(str(uuid.uuid4()))

    patched_cs.return_value = MagicMock()
    patched_cf.return_value = MagicMock()
    patched_gfp.return_value = MagicMock()
    patched_update_range.return_value = MagicMock()
    patched_get_file.return_value = MagicMock()
    patched_get_file.return_value.content = b'\0' * 8

    pemcontents = _RSAKEY.private_bytes(
        encoding=cryptography.hazmat.primitives.serialization.
        Encoding.PEM,
        format=cryptography.hazmat.primitives.serialization.
        PrivateFormat.PKCS8,
        encryption_algorithm=cryptography.hazmat.primitives.
        serialization.NoEncryption())
    pempath = str(tmpdir.join('rsa.pem'))
    with open(pempath, 'wb') as f:
        f.write(pemcontents)

    args.rsaprivatekey = pempath
    args.rsakeypassphrase = None
    args.encmode = blobxfer._ENCRYPTION_MODE_FULLBLOB
    blobxfer.main()

    args.download = True
    args.upload = False
    args.rsaprivatekey = pempath
    args.remoteresource = '.'
    with pytest.raises(SystemExit):
        blobxfer.main()

    patched_ldaf.return_value = [azure.storage.file.File(name='test.tmp')]
    patched_gfp.return_value = MagicMock()
    patched_gfp.return_value.properties = MagicMock()
    patched_gfp.return_value.properties.content_length = 1
    patched_gfp.return_value.properties.content_settings = MagicMock()
    patched_gfp.return_value.properties.content_settings.content_md5 = 'md5'
    args.rsaprivatekey = pempath
    args.localresource = lpath.rstrip(os.path.sep + 'test.tmp')
    blobxfer.main()

    os.remove(pempath)
