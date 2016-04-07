@echo off
setlocal EnableDelayedExpansion
setlocal
set msbuildLogger=%1

for /R %%f in (*.sln) do (
    if DEFINED msbuildLogger (
        cmd /c nuget restore "%%f" && msbuild /m "%%f" /logger:%msbuildLogger%
    ) else (
        cmd /c nuget restore "%%f" && msbuild /m "%%f"
    )
    if !errorlevel! neq 0 exit /b !errorlevel!
)