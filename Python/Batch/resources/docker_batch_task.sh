#!/usr/bin/env bash

cd $TEMP
PATH=/root/bin:$PATH

videourl=$1
fullname=$2
outputsaskey=$3
storageaccount=$4

extension="${fullname##*.}"
filename="${fullname%.*}"

# Download input
echo "downloading $videourl to $fullname"
wget -v $videourl -O $fullname
# Transcode
ffmpeg -i $fullname -vf "rotate=PI" -c:a copy $filename.output.mp4 < /dev/null 2>&1
# Upload result
python /root/blobxfer.py --saskey $outputsaskey --upload $storageaccount output $filename.output.mp4
