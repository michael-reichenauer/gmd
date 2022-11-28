@echo off&setlocal

echo "Cleaning"
del "gmd.exe"
del "gmd_linux"
del "gmd_osx"
del "gmd_windows"


if exist "gmd.exe" (
    echo "Error: gmd.exe was not deleted!";
    exit -1;
) 
if exist "gmd_windows" (
    echo "Error: gmd_windows was not deleted!";
    exit -1;
) 
if exist "gmd_linux" (
    echo "Error: gmd_linux was not deleted!";
    exit -1;
) 
if exist "gmd_osx" (
    echo "Error: gmd_osx was not deleted!";
    exit -1;
) 


echo "Building ...."
dotnet publish gmd/gmd.csproj -c Release -r win-x64 -p:PublishReadyToRun=true --self-contained true -p:PublishSingleFile=true
copy gmd\bin\Release\net6.0\win-x64\publish\gmd.exe gmd.exe
copy gmd\bin\Release\net6.0\win-x64\publish\gmd.exe gmd_windows

dotnet publish gmd/gmd.csproj -c Release -r linux-x64 -p:PublishReadyToRun=true --self-contained true -p:PublishSingleFile=true
copy gmd\bin\Release\net6.0\linux-x64\publish\gmd gmd_linux

dotnet publish gmd/gmd.csproj -c Release -r osx-x64 -p:PublishReadyToRun=true --self-contained true -p:PublishSingleFile=true
copy gmd\bin\Release\net6.0\osx-x64\publish\gmd gmd_osx

echo "Version:"
gmd.exe --version
pause