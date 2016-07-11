# azure_storage_helper.py - Azure storage helper to upload files to storage
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

from __future__ import print_function
import argparse
import os

import azure.storage.blob as azureblob

if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--filepath', required=True,
                        help='The path to the text file to process. The path '
                             'may include a compute node\'s environment '
                             'variables, such as '
                             '$AZ_BATCH_NODE_SHARED_DIR/filename.txt')
    parser.add_argument('--blobname', required=True,
                        help='Name of blob to create or update')
    parser.add_argument('--storageaccount', required=True,
                        help='The name the Azure Storage account that owns '
                             'the blob storage container to which to upload '
                             'results.')
    parser.add_argument('--storagecontainer', required=True,
                        help='The Azure Blob storage container to which to '
                             'upload results.')
    parser.add_argument('--sastoken', required=True,
                        help='The SAS token providing write access to the '
                             'Storage container.')
    args = parser.parse_args()

    input_file_path = os.path.realpath(args.filepath)
    # Create the blob client using the container's SAS token.
    # This allows us to create a client that provides write
    # access only to the container.
    blob_client = azureblob.BlockBlobService(account_name=args.storageaccount,
                                             sas_token=args.sastoken)

    print(
        'Uploading file {} container [{}], python {}/azure_storage_helper.py '
        '--filepath {} --blobname {} --storageaccount {} --storagecontainer {}'
        ' --sastoken ...'.format(
            input_file_path,
            args.storagecontainer,
            '$AZ_BATCH_TASK_SHARED_DIR',
            args.filepath,
            args.blobname,
            args.storageaccount,
            args.storagecontainer))

    blob_client.create_blob_from_path(args.storagecontainer,
                                      args.blobname,
                                      input_file_path)
