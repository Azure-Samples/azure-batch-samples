[System.Reflection.Assembly]::LoadWithPartialName("System.IO.Compression.FileSystem")
[System.IO.Compression.ZipFile]::ExtractToDirectory("demompi.zip", "demompi")

robocopy.exe /mir demompi ${env:AZ_BATCH_NODE_SHARED_DIR}\demompi

MSMpiSetup.exe -unattend -force

sc.exe create azuremon binpath= ${env:AZ_BATCH_NODE_SHARED_DIR}\demompi\demo_binaries\azuremon\azuremon.exe
sc.exe create forwardersvc binpath= ${env:AZ_BATCH_NODE_SHARED_DIR}\demompi\demo_binaries\azuremon\ForwarderSvc.exe
