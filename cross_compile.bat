@echo off
setlocal enabledelayedexpansion

set runtimes=win-x64 linux-x64 osx-x64

for %%R in (%runtimes%) do (
    dotnet publish -r %%R -p:PublishSingleFile=true --configuration Release --self-contained true
)
pause