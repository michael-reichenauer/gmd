@echo off&setlocal

set DOTNET="net7.0"

echo "Cleaning"
del "gmd.exe" 2> nul
del "gmd_linux" 2> nul
del "gmd_osx" 2> nul
del "gmd_windows" 2> nul


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


echo "Building Windows ...."
dotnet publish gmd/gmd.csproj -c Release -r win-x64 -p:PublishReadyToRun=true --self-contained true -p:PublishSingleFile=true
copy gmd\bin\Release\%DOTNET%\win-x64\publish\gmd.exe gmd.exe
copy gmd\bin\Release\%DOTNET%\win-x64\publish\gmd.exe gmd_windows

rem if command arg is '-w' only windows version will be built and the script will exit
if "%1"=="-w" (
    echo "Version:"
    gmd.exe --version
    pause
    exit 0
)

echo "Building Linux ...."
dotnet publish gmd/gmd.csproj -c Release -r linux-x64 -p:PublishReadyToRun=true --self-contained true -p:PublishSingleFile=true
copy gmd\bin\Release\%DOTNET%\linux-x64\publish\gmd gmd_linux

echo "Building OSX ...."
dotnet publish gmd/gmd.csproj -c Release -r osx-x64 -p:PublishReadyToRun=true --self-contained true -p:PublishSingleFile=true
copy gmd\bin\Release\%DOTNET%\osx-x64\publish\gmd gmd_osx

echo "Version:"
gmd.exe --version
pause