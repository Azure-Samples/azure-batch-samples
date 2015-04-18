# coding=utf-8
"""Tests for blobxfer"""

# stdlib imports
import errno
import socket
import sys
# non-stdlib imports
from mock import (Mock, patch)
import pytest
# module under test
sys.path.append('..')
import blobxfer

@patch('time.sleep', return_value=None)
def test_azure_request(patched_time_sleep):
    uncaught_socket_error = socket.error()
    uncaught_socket_error.errno = errno.E2BIG

    with pytest.raises(socket.error):
        blobxfer.azure_request(Mock(side_effect=uncaught_socket_error))


