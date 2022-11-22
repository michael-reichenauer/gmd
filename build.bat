@echo off&setlocal

echo "Cleaning"
del "gmd.exe"
del "gmd_linux"


if exist "gmd.exe" (
    echo "Error: gmd.exe was not deleted!";
    exit -1;
) 

if exist "gmd_linux" (
    echo "Error: gmd_linux was not deleted!";
    exit -1;
) 


echo "Building ...."
dotnet publish gmd/gmd.csproj -c Release -r win-x64 -p:PublishReadyToRun=true --self-contained true -p:PublishSingleFile=true
copy gmd\bin\Release\net6.0\win-x64\publish\gmd.exe gmd.exe

dotnet publish gmd/gmd.csproj -c Release -r linux-x64 -p:PublishReadyToRun=true --self-contained true -p:PublishSingleFile=true
copy gmd\bin\Release\net6.0\linux-x64\publish\gmd gmd_linux
