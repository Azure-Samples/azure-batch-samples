param([string]$msbuildLogger="")

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$solutions = Get-ChildItem -Filter *.sln -Recurse
$msbuildSolutions = $solutions | where {$_.Name -like "BatchExplorer.sln"}
# Skip MultiInstanceTasks.sln due to native dependency
$skippedSolutions = $solutions | where {$_.Name -like "MultiInstanceTasks.sln"}
$dotnetBuildSolutions = $solutions | where {$msbuildSolutions -notcontains $_ -and $skippedSolutions -notcontains $_}  

foreach($sln in $dotnetBuildSolutions)
{    
    Write-Output $sln.FullName
    dotnet build $sln.FullName
    if ($LastExitCode  -ne 0)
    {
        exit $LastExitCode
    }
}

foreach($sln in $msbuildSolutions)
{    
    Write-Output $sln.FullName
    $slnName = $sln.FullName
    if($msbuildLogger -ne "")
    {
        cmd /c "nuget restore $slnName && msbuild /m $slnName /logger:`"$msbuildLogger`""
    }
    else
    {
        cmd /c "nuget restore $slnName && msbuild /m $slnName"
    }
    if ($LastExitCode  -ne 0)
    {
        exit $LastExitCode
    }
}