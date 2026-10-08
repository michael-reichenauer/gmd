@echo off&setlocal

rem Windows counterpart of the 'scripts/build' script. Kept in sync with it:
rem same steps, same artifact names. Linux/macOS are the primary targets; this
rem script exists mainly for debugging Windows-specific behavior on Windows.
rem
rem Usage:
rem   scripts\build.bat        Test and build all platforms
rem   scripts\build.bat -w     Test and build Windows only (fast path for local debugging)

rem Runs at the repository root, wherever it is started from (setlocal restores the folder at the end)
cd /d "%~dp0.."

set DOTNET=net10.0

rem The executables, and the package audit, go into artifacts\ (ignored by git)
set OUT=artifacts
if not exist "%OUT%" mkdir "%OUT%"

echo Cleaning ...
del "%OUT%\gmd.exe" 2> nul
del "%OUT%\gmd_linux" 2> nul
del "%OUT%\gmd_linux_x64" 2> nul
del "%OUT%\gmd_linux_arm64" 2> nul
del "%OUT%\gmd_osx" 2> nul
del "%OUT%\gmd_osx_arm64" 2> nul
del "%OUT%\gmd_windows" 2> nul

for %%F in (gmd.exe gmd_linux gmd_linux_x64 gmd_linux_arm64 gmd_osx gmd_osx_arm64 gmd_windows) do (
    if exist "%OUT%\%%F" (
        echo Error: %%F was not deleted!
        exit /b 1
    )
)

echo.
echo Run tests ...
rem '-tl:false': the .NET 10 SDK terminal logger hides the console test logger output (see scripts/test)
rem Both test projects, see scripts/test
dotnet test gmd.sln -tl:false -v quiet --nologo -l:"console;verbosity=normal"
if errorlevel 1 (
    echo Error: Tests failed
    exit /b 1
)

echo.
echo Checking for updates ...
dotnet list package --outdated

echo.
echo Checking for deprecated ...
dotnet list package --deprecated --include-transitive

echo.
echo Checking for vulnerabilities ...
dotnet list package --vulnerable --include-transitive > "%OUT%\build.log" 2>&1
type "%OUT%\build.log"
findstr /I /C:"critical" /C:"high" /C:"moderate" "%OUT%\build.log" >nul && echo Security Vulnerabilities found on the log output

echo.
echo Building windows ...
dotnet publish gmd/gmd.csproj -c Release -r win-x64 -p:PublishReadyToRun=true --self-contained true -p:PublishSingleFile=true
if errorlevel 1 exit /b 1
copy gmd\bin\Release\%DOTNET%\win-x64\publish\gmd.exe "%OUT%\gmd_windows"
copy gmd\bin\Release\%DOTNET%\win-x64\publish\gmd.exe "%OUT%\gmd.exe"

rem If command arg is '-w', only the windows version is built and the script exits
if "%1"=="-w" (
    echo.
    echo Built only windows.
    echo Built version:
    "%OUT%\gmd.exe" --version
    exit /b 0
)

echo.
echo Building linux (x64) ...
dotnet publish gmd/gmd.csproj -c Release -r linux-x64 -p:PublishReadyToRun=true --self-contained true -p:PublishSingleFile=true
if errorlevel 1 exit /b 1
copy gmd\bin\Release\%DOTNET%\linux-x64\publish\gmd "%OUT%\gmd_linux_x64"

echo.
echo Building linux (arm64) ...
rem Build Linux arm64 so it can run on Apple Silicon via a Linux environment
dotnet publish gmd/gmd.csproj -c Release -r linux-arm64 -p:PublishReadyToRun=true --self-contained true -p:PublishSingleFile=true
if errorlevel 1 exit /b 1
copy gmd\bin\Release\%DOTNET%\linux-arm64\publish\gmd "%OUT%\gmd_linux_arm64"

echo.
echo Building macOS (Apple Silicon) ...
rem Target Apple Silicon Macs (e.g. M4) with the arm64 RID
dotnet publish gmd/gmd.csproj -c Release -r osx-arm64 -p:PublishReadyToRun=true --self-contained true -p:PublishSingleFile=true
if errorlevel 1 exit /b 1
copy gmd\bin\Release\%DOTNET%\osx-arm64\publish\gmd "%OUT%\gmd_osx_arm64"

echo.
echo Built version:
"%OUT%\gmd.exe" --version
