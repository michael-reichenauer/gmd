@echo off&setlocal

rem Runs gmd from source, on this repository (-d <folder> opens another one)
cd /d "%~dp0.."
dotnet run --project gmd/gmd.csproj -- %*
