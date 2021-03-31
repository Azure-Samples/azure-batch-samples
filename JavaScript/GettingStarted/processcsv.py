import csv
import json
from azure.storage.blob import BlockBlobService
import datetime
import os
import argparse

#### Recommendation: Store these as environment variables or download it from Key Vault

storage_acc_name = '<storage-account-name>'
storage_acc_key = '<storage-account-key>'
block_blob_service = BlockBlobService(account_name=storage_acc_name,account_key=storage_acc_key)
####

def getfilename(name):
    names = str(name).split('/')
    return names[len(names)-1]

def processcsvfile(fname,seperator,outdir,outfname):    
    header = []
    isHeaderLoaded = False    
    with open(fname) as inpfile:        
        allrows = csv.reader(inpfile,delimiter=seperator)
        print("loaded file " + fname)
        all_vals = []
        for rows in allrows:
            if isHeaderLoaded is False:
                # Getting the first line as header
                header = rows
                isHeaderLoaded = true
            else:
                if len(header) > 0:
                    i = 0
                    # Creating a JSON object for every row
                    line = "{"                    
                    for r in rows:
                        if i == 0:                            
                            line = line + '"' + header[i] + '":"' + r.decode('utf-8','ignore') + '"'
                        else:                            
                            line = line + "," + '"' + header[i] + '":"' + r.decode('utf-8','ignore') + '"'
                        i = i + 1
                    line = line + "}" 
            all_vals.append(json.loads(json.dumps(line)))
            line = ""
        if not os.path.exists(outdir):
            os.makedirs(outdir)
        json_fpath = outdir + "/" + outfname + '.json'
        o = open(json_fpath,mode='w')
        json.dump(all_vals,o)         
        o.close()
        return json_fpath

if __name__ == "__main__":

    parser = argparse.ArgumentParser(description="Processes and stores data into hbase")
    parser.add_argument("--container",dest="container")
    parser.add_argument("--pattern",dest="pattern")    
    
    args = parser.parse_args()   
    container = args.container
    pattern = args.pattern   

    print("Processing files from container : " + str(container))
    if pattern is not None:
        blob_list = block_blob_service.list_blobs(container_name=container, prefix=pattern)
    else:
        blob_list = block_blob_service.list_blobs(container_name=container)
    
    for blob in blob_list:
        print("Processing blob : " + blob.name)
        blob_name = getfilename(blob.name)
        downloadedblob = "downloaded_" + blob_name 
        block_blob_service.get_blob_to_path(container_name=container,blob_name=blob.name, file_path=downloadedblob, open_mode='w')
        if pattern is not None:
            json_outpath = processcsvfile(fname=downloadedblob,seperator="|",outfname=blob_name,outdir='jsonfiles/' + container + "/" + pattern)
            print("uploading blob" + json_outpath)
            block_blob_service.create_blob_from_path(container_name=container,blob_name=str(pattern+ 'json/' + blob_name + ".json"),file_path=json_outpath) 
        else:
            json_outpath = processcsvfile(fname=downloadedblob,seperator="|",outfname=blob_name,outdir='jsonfiles/' + container + "/")
            print("uploading blob" + json_outpath)
            block_blob_service.create_blob_from_path(container_name=container,blob_name=str('json/' + blob_name + ".json"),file_path=json_outpath)            