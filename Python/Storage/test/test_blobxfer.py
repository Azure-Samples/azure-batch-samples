# coding=utf-8
"""Tests for blobxfer"""

# stdlib imports
import errno
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
import azure
from mock import (MagicMock, Mock, patch)
import pytest
import requests
import requests_mock
# module under test
sys.path.append('..')
import blobxfer

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

def _func_raise_azure_exception_once(val, timeout=None):
    if len(val) > 0:
        response = MagicMock()
        return response
    val.append(0)
    ex = Exception()
    ex.message = 'TooManyRequests'
    raise ex

@patch('time.sleep', return_value=None)
def test_azure_request(patched_time_sleep):
    socket_error = socket.error()
    socket_error.errno = errno.E2BIG

    with pytest.raises(socket.error):
        blobxfer.azure_request(Mock(side_effect=socket_error))

    socket_error.errno = errno.ETIMEDOUT
    with pytest.raises(IOError):
        mock = Mock(side_effect=socket_error)
        mock.__name__ = 'name'
        blobxfer.azure_request(mock, timeout=0.001)

    with pytest.raises(Exception):
        ex = Exception()
        ex.message = 'Uncaught'
        blobxfer.azure_request(Mock(side_effect=ex))

    try:
        blobxfer.azure_request(
                _func_raise_azure_exception_once, val=[], timeout=1)
    except:
        pytest.fail('unexpected Exception raised')

    with pytest.raises(requests.HTTPError):
        exc = requests.HTTPError()
        exc.response = MagicMock()
        exc.response.status_code = 404
        mock = Mock(side_effect=exc)
        blobxfer.azure_request(mock)

    try:
        blobxfer.azure_request(
                _func_raise_requests_exception_once, val=[], timeout=1)
    except:
        pytest.fail('unexpected Exception raised')

    try:
        blobxfer.azure_request(_func_successful_requests_call)
    except:
        pytest.fail('unexpected Exception raised')

@patch('azure.storage._parse_blob_enum_results_list', return_value='parsed')
def test_sasblobservice_listblobs(patched_parse):
    session = requests.Session()
    adapter = requests_mock.Adapter()
    session.mount('mock', adapter)

    with requests_mock.mock() as m:
        m.get('mock://blobepcontainersaskey', text='data')
        sbs = blobxfer.SasBlobService('mock://blobep', 'saskey', None)
        results = sbs.list_blobs('container', 'marker')
        assert results == patched_parse.return_value

        m.get('mock://blobepcontainersaskey', text='', status_code=201)
        sbs = blobxfer.SasBlobService('mock://blobep', 'saskey', None)
        with pytest.raises(IOError):
            sbs.list_blobs('container', 'marker')

def test_sasblobservice_getblob():
    session = requests.Session()
    adapter = requests_mock.Adapter()
    session.mount('mock', adapter)

    with requests_mock.mock() as m:
        m.get('mock://blobepcontainer/blobsaskey', content=b'data')
        sbs = blobxfer.SasBlobService('mock://blobep', 'saskey', None)
        results = sbs.get_blob('container', 'blob', 'range')
        assert results == b'data'

        m.get('mock://blobepcontainer/blobsaskey', status_code=201)
        sbs = blobxfer.SasBlobService('mock://blobep', 'saskey', None)
        with pytest.raises(IOError):
            sbs.get_blob('container', 'blob', 'range')

def test_sasblobservice_getblobproperties():
    session = requests.Session()
    adapter = requests_mock.Adapter()
    session.mount('mock', adapter)

    with requests_mock.mock() as m:
        m.head('mock://blobepcontainer/blobsaskey', headers={'hello': 'world'})
        sbs = blobxfer.SasBlobService('mock://blobep', 'saskey', None)
        results = sbs.get_blob_properties('container', 'blob')
        assert results['hello'] == 'world'

        m.head('mock://blobepcontainer/blobsaskey', text='', status_code=201)
        sbs = blobxfer.SasBlobService('mock://blobep', 'saskey', None)
        with pytest.raises(IOError):
            sbs.get_blob_properties('container', 'blob')

def test_sasblobservice_putblock():
    session = requests.Session()
    adapter = requests_mock.Adapter()
    session.mount('mock', adapter)

    with requests_mock.mock() as m:
        m.put('mock://blobepcontainer/blobsaskey', status_code=201)
        sbs = blobxfer.SasBlobService('mock://blobep', 'saskey', None)
        try:
            sbs.put_block('container', 'blob', 'block', 'blockid', 'md5')
        except:
            pytest.fail('unexpected Exception raised')

        m.put('mock://blobepcontainer/blobsaskey', text='', status_code=200)
        sbs = blobxfer.SasBlobService('mock://blobep', 'saskey', None)
        with pytest.raises(IOError):
            sbs.put_block('container', 'blob', 'block', 'blockid', 'md5')

def test_sasblobservice_putblocklist():
    session = requests.Session()
    adapter = requests_mock.Adapter()
    session.mount('mock', adapter)

    with requests_mock.mock() as m:
        m.put('mock://blobepcontainer/blobsaskey', status_code=201)
        sbs = blobxfer.SasBlobService('mock://blobep', 'saskey', None)
        try:
            sbs.put_block_list('container', 'blob', ['1', '2'], 'md5')
        except:
            pytest.fail('unexpected Exception raised')

        m.put('mock://blobepcontainer/blobsaskey', text='', status_code=200)
        sbs = blobxfer.SasBlobService('mock://blobep', 'saskey', None)
        with pytest.raises(IOError):
            sbs.put_block_list('container', 'blob', ['1', '2'], 'md5')

def test_sasblobservice_setblobproperties():
    session = requests.Session()
    adapter = requests_mock.Adapter()
    session.mount('mock', adapter)

    with requests_mock.mock() as m:
        m.put('mock://blobepcontainer/blobsaskey', status_code=200)
        sbs = blobxfer.SasBlobService('mock://blobep', 'saskey', None)
        try:
            sbs.set_blob_properties('container', 'blob', 'md5')
        except:
            pytest.fail('unexpected Exception raised')

        m.put('mock://blobepcontainer/blobsaskey', text='', status_code=201)
        sbs = blobxfer.SasBlobService('mock://blobep', 'saskey', None)
        with pytest.raises(IOError):
            sbs.set_blob_properties('container', 'blob', 'md5')

def test_sasblobservice_putblob():
    session = requests.Session()
    adapter = requests_mock.Adapter()
    session.mount('mock', adapter)

    with requests_mock.mock() as m:
        m.put('mock://blobepcontainer/blobsaskey', status_code=201)
        sbs = blobxfer.SasBlobService('mock://blobep', 'saskey', None)
        try:
            sbs.put_blob('container', 'blob', None, 'PageBlob', 'md5', 4)
        except:
            pytest.fail('unexpected Exception raised')

        m.put('mock://blobepcontainer/blobsaskey', content=b'', status_code=200)
        sbs = blobxfer.SasBlobService('mock://blobep', 'saskey', None)
        with pytest.raises(IOError):
            sbs.put_blob('container', 'blob', None, 'PageBlob', 'md5', 4)

def test_blobchunkworker_run(tmpdir):
    lpath = str(tmpdir.join('test.tmp'))
    with open(lpath, 'wt') as f:
        f.write(str(uuid.uuid4()))
    exc_list = []
    sa_in_queue = queue.Queue()
    sa_out_queue = queue.Queue()
    flock = threading.Lock()
    sa_in_queue.put((True, lpath, 'blobep', 'saskey',
        'container', 'blob', 'blockid', 0, 4, flock, None))
    sa_in_queue.put((False, lpath, 'blobep', 'saskey',
        'container', 'blob', 'blockid', 0, 4, flock, None))
    args = MagicMock()
    args.pageblob = False
    args.autovhd = False
    args.timeout = None

    session = requests.Session()
    adapter = requests_mock.Adapter()
    session.mount('mock', adapter)
    with requests_mock.mock() as m:
        m.put('mock://blobepcontainer/blobsaskey', status_code=201)
        sbs = blobxfer.SasBlobService('mock://blobep', 'saskey', None)
        bcw = blobxfer.BlobChunkWorker(exc_list, sa_in_queue, sa_out_queue,
                args, sbs)
        try:
            bcw.putblobdata(lpath, 'container', 'blob', 'blockid', 0, 4, flock, None)
        except:
            pytest.fail('unexpected Exception raised')

        m.get('mock://blobepcontainer/blobsaskey', status_code=200)
        try:
            bcw.getblobrange(lpath, 'container', 'blob', 0, 4, flock, None)
        except:
            pytest.fail('unexpected Exception raised')

        m.get('mock://blobepcontainer/blobsaskey', status_code=201)
        bcw.run()
        assert len(exc_list) > 0

    exc_list = []
    sa_in_queue = queue.Queue()
    sa_out_queue = queue.Queue()
    sa_in_queue.put((True, lpath, 'blobep', 'saskey',
        'container', 'blob', 'blockid', 0, 4, flock, None))
    sa_in_queue.put((False, lpath, 'blobep', 'saskey',
        'container', 'blob', 'blockid', 0, 4, flock, None))
    args.pageblob = True
    with requests_mock.mock() as m:
        m.put('mock://blobepcontainer/blobsaskey', status_code=200)
        sbs = blobxfer.SasBlobService('mock://blobep', 'saskey', None)
        bcw = blobxfer.BlobChunkWorker(exc_list, sa_in_queue, sa_out_queue,
                args, sbs)
        with pytest.raises(IOError):
            bcw.putblobdata(lpath, 'container', 'blob', 'blockid', 0, 4, flock, None)

@patch('blobxfer.azure_request', return_value=None)
def test_generate_xferspec_download_invalid(patched_azure_request):
    args = MagicMock()
    args.storageaccount = 'blobep'
    args.container = 'container'
    args.storageaccountkey = 'saskey'
    args.chunksizebytes = 5
    args.timeout = None
    sa_in_queue = queue.Queue()

    with requests_mock.mock() as m:
        m.head('mock://blobepcontainer/blobsaskey', headers={
            'content-length': '-1', 'content-md5': 'md5'})
        sbs = blobxfer.SasBlobService('mock://blobep', 'saskey', None)
        with pytest.raises(ValueError):
            blobxfer.generate_xferspec_download(sbs,
                    args, sa_in_queue, 'tmppath', 'blob', None, None, True)

def test_generate_xferspec_download(tmpdir):
    lpath = str(tmpdir.join('test.tmp'))
    args = MagicMock()
    args.storageaccount = 'blobep'
    args.container = 'container'
    args.storageaccountkey = 'saskey'
    args.chunksizebytes = 5
    args.timeout = None
    sa_in_queue = queue.Queue()

    session = requests.Session()
    adapter = requests_mock.Adapter()
    session.mount('mock', adapter)

    with requests_mock.mock() as m:
        m.head('mock://blobepcontainer/blobsaskey', headers={
            'content-length': '-1', 'content-md5': 'md5'})
        sbs = blobxfer.SasBlobService('mock://blobep', 'saskey', None)
        with pytest.raises(ValueError):
            blobxfer.generate_xferspec_download(sbs,
                    args, sa_in_queue, lpath, 'blob', None, None, True)
        m.head('mock://blobepcontainer/blobsaskey', headers={
            'content-length': '6', 'content-md5': 'md5'})
        cl, nsops, md5, fd = blobxfer.generate_xferspec_download(sbs,
                args, sa_in_queue, lpath, 'blob', None, None, True)
        assert 6 == cl
        assert 2 == nsops
        assert 'md5' == md5
        assert None != fd
        fd.close()
        cl, nsops, md5, fd = blobxfer.generate_xferspec_download(sbs,
                args, sa_in_queue, lpath, 'blob', None, None, False)
        assert None == fd
        with open(lpath, 'wt') as f:
            f.write('012345')
        m.head('mock://blobepcontainer/blobsaskey', headers={
            'content-length': '6', 'content-md5': '1qmpM8iq/FHlWsBmK25NSg=='})
        cl, nsops, md5, fd = blobxfer.generate_xferspec_download(sbs,
                args, sa_in_queue, lpath, 'blob', None, None, True)
        assert cl == None

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
    sa_in_queue = queue.Queue()
    fs, nsops, md5, fd = blobxfer.generate_xferspec_upload(args,
            sa_in_queue, {}, {}, lpath, 'rr', True)
    stat = os.stat(lpath)
    assert stat.st_size == fs
    assert math.ceil(stat.st_size / 5.0) == nsops
    assert None != fd
    fd.close()
    args.skiponmatch = True
    with open(lpath, 'wt') as f:
        f.write('012345')
    sd = {}
    sd['rr'] = [6, '1qmpM8iq/FHlWsBmK25NSg==']
    fs, nsops, md5, fd = blobxfer.generate_xferspec_upload(args,
            sa_in_queue, sd, {}, lpath, 'rr', False)
    assert fs == None

def _mock_get_storage_account_keys(timeout=None, service_name=None):
    ret = MagicMock()
    ret.storage_service_keys.primary = 'mmkey'
    return ret

def _mock_get_storage_account_properties(timeout=None, service_name=None):
    ret = MagicMock()
    ret.storage_service_properties.endpoints = [None]
    return ret

def _mock_blobservice_create_container(timeout=None,
        container_name=None, fail_on_exist=None):
    raise azure.WindowsAzureConflictError('msg')

@patch('blobxfer.parseargs')
def test_main1(patched_parseargs, tmpdir):
    lpath = str(tmpdir.join('test.tmp'))
    args = MagicMock()
    args.numworkers = 0
    args.localresource = ''
    args.storageaccount = 'blobep'
    args.container = 'container'
    args.storageaccountkey = 'saskey'
    args.chunksizebytes = 5
    args.pageblob = False
    args.autovhd = False
    patched_parseargs.return_value = args
    with pytest.raises(ValueError):
        blobxfer.main()
    args.localresource = lpath
    args.blobep = ''
    with pytest.raises(ValueError):
        blobxfer.main()
    args.blobep = 'blobep'
    args.forceupload = True
    args.forcedownload = True
    with pytest.raises(ValueError):
        blobxfer.main()
    args.forceupload = None
    args.forcedownload = None
    with pytest.raises(ValueError):
        blobxfer.main()
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
    args.pageblob = False
    args.autovhd = False
    with patch('azure.servicemanagement.ServiceManagementService') as mock:
        mock.return_value = MagicMock()
        mock.return_value.get_storage_account_keys = _mock_get_storage_account_keys
        mock.return_value.get_storage_account_properties = _mock_get_storage_account_properties
        with pytest.raises(ValueError):
            blobxfer.main()
    args.managementep = None
    args.managementcert = None
    args.subscriptionid = None
    args.remoteresource = 'blob'
    args.chunksizebytes = None
    with patch('azure.storage.BlobService') as mock:
        mock.return_value = None
        with pytest.raises(ValueError):
            blobxfer.main()
    args.storageaccountkey = None
    args.saskey = 'saskey'
    args.remoteresource = None
    args.forcedownload = True
    with pytest.raises(ValueError):
        blobxfer.main()

    args.forcedownload = False
    args.forceupload = True
    args.remoteresource = None
    with open(lpath, 'wt') as f:
        f.write(str(uuid.uuid4()))

    session = requests.Session()
    adapter = requests_mock.Adapter()
    session.mount('mock', adapter)
    with requests_mock.mock() as m:
        m.put('https://blobep.blobep/container/blobsaskey?comp=block&blockid=00000000',
                status_code=201)
        m.put('https://blobep.blobep/container/' + lpath + 'saskey?comp=blocklist',
                status_code=201)
        m.get('https://blobep.blobep/containersaskey?comp=list&restype=container&maxresults=1000',
                text='<?xml version="1.0" encoding="utf-8"?><EnumerationResults ContainerName="https://blobep.blobep/container"><Blobs><Blob><Name>' + lpath + '</Name><Properties><Content-Length>6</Content-Length><Content-MD5>md5</Content-MD5></Properties></Blob></Blobs></EnumerationResults>')
        args.progressbar = False
        args.keeprootdir = False
        args.skiponmatch = True
        blobxfer.main()

        args.progressbar = True
        args.forcedownload = True
        args.forceupload = False
        args.remoteresource = 'blob'
        args.localresource = str(tmpdir)
        m.head('https://blobep.blobep/container/blobsaskey', headers={
            'content-length': '6', 'content-md5': '1qmpM8iq/FHlWsBmK25NSg=='})
        m.get('https://blobep.blobep/container/blobsaskey', content=b'012345')
        blobxfer.main()

        args.remoteresource = '.'
        args.keepmismatchedmd5files = False
        m.get('https://blobep.blobep/containersaskey?comp=list&restype=container&maxresults=1000',
                text='<?xml version="1.0" encoding="utf-8"?><EnumerationResults ContainerName="https://blobep.blobep/container"><Blobs><Blob><Name>blob</Name><Properties><Content-Length>6</Content-Length><Content-MD5>md5</Content-MD5></Properties></Blob></Blobs></EnumerationResults>')
        m.get('https://blobep.blobep/container/saskey')
        blobxfer.main()

    notmp_lpath = '/'.join(lpath.strip('/').split('/')[1:])

    with requests_mock.mock() as m:
        args.forcedownload = False
        args.forceupload = True
        args.remoteresource = None
        args.skiponmatch = False
        m.put('https://blobep.blobep/container/test.tmpsaskey?comp=block&blockid=00000000',
                status_code=200)
        m.put('https://blobep.blobep/container/test.tmpsaskey?comp=blocklist',
                status_code=201)
        m.put('https://blobep.blobep/container' + lpath + 'saskey?comp=block&blockid=00000000',
                status_code=200)
        m.put('https://blobep.blobep/container' + lpath + 'saskey?comp=blocklist',
                status_code=201)
        m.put('https://blobep.blobep/container/' + notmp_lpath + \
                'saskey?comp=block&blockid=00000000', status_code=200)
        m.put('https://blobep.blobep/container/' + notmp_lpath + \
                'saskey?comp=blocklist', status_code=201)
        with pytest.raises(SystemExit):
            blobxfer.main()

        args.recursive = False
        m.put('https://blobep.blobep/container/blob.blobtmpsaskey?comp=blocklist',
                status_code=201)
        m.put('https://blobep.blobep/container/test.tmp.blobtmpsaskey?comp=blocklist',
                status_code=201)
        m.put('https://blobep.blobep/container/blob.blobtmpsaskey?comp=block&blockid=00000000',
                status_code=200)
        m.put('https://blobep.blobep/container/blobsaskey?comp=blocklist',
                status_code=201)
        with pytest.raises(SystemExit):
            blobxfer.main()

        args.pageblob = True
        args.forceupload = True
        args.forcedownload = False
        m.put('https://blobep.blobep/container/blob.blobtmpsaskey', status_code=201)
        m.put('https://blobep.blobep/container/test.tmpsaskey', status_code=201)
        m.put('https://blobep.blobep/container/blob.blobtmpsaskey?comp=properties', status_code=200)
        m.put('https://blobep.blobep/container/test.tmpsaskey?comp=properties', status_code=200)
        m.put('https://blobep.blobep/container/blobsaskey', status_code=201)
        with pytest.raises(IOError):
            blobxfer.main()
        m.put('https://blobep.blobep/container/blobsaskey', status_code=200)
        with pytest.raises(IOError):
            blobxfer.main()

        m.put('https://blobep.blobep/container/' + notmp_lpath + \
                'saskey?comp=blocklist', status_code=201)
        m.put('https://blobep.blobep/container/blobsaskey', status_code=201)
        args.pageblob = False
        blobxfer.main()
        args.pageblob = False
        args.autovhd = True
        blobxfer.main()

@patch('blobxfer.parseargs')
def test_main2(patched_parseargs, tmpdir):
    lpath = str(tmpdir.join('test.tmp'))
    args = MagicMock()
    patched_parseargs.return_value = args
    args.numworkers = 64
    args.storageaccount = 'blobep'
    args.container = 'container'
    args.chunksizebytes = 5
    args.localresource = lpath
    args.blobep = 'blobep'
    args.timeout = -1
    args.pageblob = False
    args.autovhd = False
    args.managementep = None
    args.managementcert = None
    args.subscriptionid = None
    args.chunksizebytes = None
    args.forcedownload = False
    args.forceupload = True
    args.remoteresource = None
    args.saskey = None
    args.storageaccountkey = 'key'
    with open(lpath, 'wt') as f:
        f.write(str(uuid.uuid4()))

    session = requests.Session()
    adapter = requests_mock.Adapter()
    session.mount('mock', adapter)

    with patch('azure.storage.BlobService') as mock:
        args.createcontainer = True
        args.pageblob = False
        args.autovhd = False
        mock.return_value = MagicMock()
        mock.return_value.create_container = _mock_blobservice_create_container
        blobxfer.main()

        args.createcontainer = False
        args.pageblob = True
        args.autovhd = False
        blobxfer.main()

