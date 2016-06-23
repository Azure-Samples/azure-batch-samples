set benchmark=%1
set startingNumCores=%2
set numSteps=%3
set storage_account_name = ""
set storage_account_url = ""
set storage_account_key = ""
set source_sas_url = ""
set storage_container_url_for_stage = ""
set storage_container_sas_for_stage = ""
set storage_container_url_for_output = ""
set storage_container_sas_for_output = ""

cd %AZ_BATCH_TASK_SHARED_DIR%
mpiexec -wdir %AZ_BATCH_TASK_SHARED_DIR% -c 1 robocopy.exe %AZ_BATCH_NODE_SHARED_DIR%\demompi\demo_binaries\namd_benchmark_%benchmark%\ .
mpiexec -wdir %AZ_BATCH_TASK_SHARED_DIR% -c 1 cmd /c "echo numsteps %numSteps% >> %benchmark%.conf"
mpiexec -wdir %AZ_BATCH_TASK_SHARED_DIR% -n %startingNumCores% -affinity %AZ_BATCH_NODE_SHARED_DIR%\demompi\demo_binaries\namd\namd2.exe %benchmark%.conf

%AZ_BATCH_NODE_SHARED_DIR%\demompi\demo_binaries\AzCopy\azcopy.exe /Source:. /Dest:%storage_container_url_for_stage% /DestSAS:%storage_container_sas_for_stage% /Pattern:%benchmark%.dcd /Y
mpiexec -wdir %AZ_BATCH_TASK_SHARED_DIR% -c 1 %AZ_BATCH_NODE_SHARED_DIR%\demompi\demo_binaries\AzCopy\azcopy.exe /Source:%source_sas_url% /Dest:. /Pattern:%benchmark%.dcd /Y

mpiexec -wdir %AZ_BATCH_TASK_SHARED_DIR% -c 1 %AZ_BATCH_NODE_SHARED_DIR%\demompi\demo_binaries\vmd\vmd.exe -dispdev text -e vmdrender.tcl -args %benchmark% .
mpiexec -wdir %AZ_BATCH_TASK_SHARED_DIR% -c 1 %AZ_BATCH_NODE_SHARED_DIR%\demompi\demo_binaries\winffmpeg\videoscript.cmd %benchmark%
mpiexec -wdir %AZ_BATCH_TASK_SHARED_DIR% -c 1 .\CopyVideo.bat

%AZ_BATCH_NODE_SHARED_DIR%\demompi\demo_binaries\AzCopy\azcopy.exe /Source:%source_sas_url% /Dest:. /Pattern:namd_%benchmark%_%AZ_BATCH_JOB_ID% /Y /S

FOR %%c in (*.mp4) DO echo file %%c >> mp4list.txt

%AZ_BATCH_NODE_SHARED_DIR%\demompi\demo_binaries\winffmpeg\winffmpeg.exe -f concat -i .\mp4list.txt -c copy .\%AZ_BATCH_JOB_ID%_%AZ_BATCH_TASK_ID%.mp4

%AZ_BATCH_NODE_SHARED_DIR%\demompi\demo_binaries\AzCopy\azcopy.exe /Source:. /Dest:%storage_container_url_for_output% /DestSAS:%storage_container_sas_for_output% /Pattern:%AZ_BATCH_JOB_ID%_%AZ_BATCH_TASK_ID%.mp4

%AZ_BATCH_NODE_SHARED_DIR%\demompi\demo_binaries\updatemp4\updatemp4.exe %storage_account_url% %storage_account_name% %storage_account_key% output %AZ_BATCH_JOB_ID%_%AZ_BATCH_TASK_ID%.mp4
