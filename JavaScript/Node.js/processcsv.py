import csv
import json
from azure.storage.blob import BlobServiceClient
import datetime
import os
import argparse

#### Recommendation: Store these as environment variables or download it from Key Vault

storage_acc_url = 'https://<AZURE_STORAGE_ACCOUNT_NAME>.blob.core.windows.net'
storage_acc_key = '<storage-account-key>'
block_blob_service = BlobServiceClient(account_url=storage_acc_url,credential=storage_acc_key)
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
            line = ""
            if isHeaderLoaded is False:
                # Getting the first line as header
                header = rows
                isHeaderLoaded = True
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

    container_client = block_blob_service.get_container_client(container)

    print("Processing files from container : " + str(container))
    if pattern is not None:
        blob_list = container_client.list_blobs(name_starts_with=pattern)
    else:
        blob_list = container_client.list_blobs()
    
    for blob in blob_list:
        print("Processing blob : " + blob.name)
        blob_name = getfilename(blob.name)
        blob_name_without_extension = blob_name.split('.')[0]
        downloadedblob = "downloaded_" + blob_name 

         #Download the blob locally
        blob_client = container_client.get_blob_client(blob.name)
        with open(downloadedblob, "wb") as my_blob:
            downloaded_blob_stream = blob_client.download_blob()
            my_blob.write(downloaded_blob_stream.readall())

        output_dir = ""
        uploaded_blob_name = ""

        if pattern is not None:
            output_dir = "jsonfiles/" + container + "/" + pattern
            uploaded_blob_name = str(pattern + 'json/' + blob_name_without_extension + ".json")
            
        else:
            output_dir = "jsonfiles/" + container + "/"
            uploaded_blob_name = str('json/' + blob_name_without_extension + ".json")

        json_outpath = processcsvfile(fname=downloadedblob,seperator="|",outfname=blob_name_without_extension,outdir=output_dir)
        print("uploading blob " + json_outpath)
        with open(json_outpath, "rb") as data:
                container_client.upload_blob(name=uploaded_blob_name, data=data)