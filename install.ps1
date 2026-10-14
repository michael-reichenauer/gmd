# Installs gmd on Windows, the counterpart of install.sh:
#   irm https://raw.githubusercontent.com/michael-reichenauer/gmd/main/install.ps1 | iex
#
# It downloads gmd.exe of the latest release to %LOCALAPPDATA%\Programs\gmd, checks it against the
# release's SHA256SUMS, and adds that folder to the user's PATH, so no admin rights are needed and
# gmd can update itself there. Run again, it installs the latest release over the one there.
#
# A file a browser downloads is marked as from the internet, which SmartScreen warns about since
# gmd.exe is not signed; a download made here is not, as with curl and install.sh.
#
# It runs in the PowerShell session that runs 'iex', so it never calls exit, which would close it,
# and it is written for Windows PowerShell 5.1 as well as PowerShell 7, in ASCII.

& {
    $ErrorActionPreference = 'Stop'
    # Invoke-WebRequest's progress bar slows a download of some 100 MB down to minutes in 5.1
    $ProgressPreference = 'SilentlyContinue'
    # Windows PowerShell 5.1 may offer only TLS 1.0, which GitHub refuses
    [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12

    # The x64 build, which Windows 11 on Arm runs too; there is no 32-bit build
    if ($env:PROCESSOR_ARCHITECTURE -eq 'x86' -and -not $env:PROCESSOR_ARCHITEW6432) {
        Write-Host 'gmd needs 64-bit Windows'
        return
    }

    $url = 'https://github.com/michael-reichenauer/gmd/releases/latest/download'
    $folder = Join-Path $env:LOCALAPPDATA 'Programs\gmd'
    $exe = Join-Path $folder 'gmd.exe'
    $download = "$exe.download"

    Write-Host 'Downloading gmd (gmd.exe) ...'
    New-Item -ItemType Directory -Force -Path $folder | Out-Null
    try {
        Invoke-WebRequest -UseBasicParsing -Uri "$url/gmd.exe" -OutFile $download

        # Every release since 0.97 has the checksums of its files, as 'sha256sum' writes them
        $sums = (Invoke-WebRequest -UseBasicParsing -Uri "$url/SHA256SUMS").Content
        if ($sums -is [byte[]]) { $sums = [Text.Encoding]::UTF8.GetString($sums) }
        $line = $sums -split "`n" | Where-Object { ($_ -split '\s+', 2)[1] -match '^\*?gmd\.exe\s*$' } | Select-Object -First 1
        $expected = if ($line) { ($line -split '\s+')[0] } else { '' }
        $actual = (Get-FileHash -Algorithm SHA256 -Path $download).Hash
        if (-not $expected -or $expected -ne $actual) {
            throw 'The download of gmd.exe does not match its checksum in SHA256SUMS'
        }
        Write-Host 'Checksum verified'

        # A gmd that runs cannot be replaced or deleted, but it can be renamed, so the one there is
        # moved aside first, by the name gmd's own updates give it, which a later start deletes, and
        # moved back should the new one not go in its place, so that a failure never leaves no gmd
        $aside = $null
        if (Test-Path $exe) {
            $aside = "$exe.tmp_" + [IO.Path]::GetRandomFileName().Replace('.', '').Substring(0, 5).ToUpper()
            Move-Item -Force $exe $aside
        }
        try {
            Move-Item -Force $download $exe
        }
        catch {
            if ($aside) { Move-Item -Force $aside $exe }
            throw
        }
        if ($aside) { Remove-Item -Force $aside -ErrorAction SilentlyContinue }
    }
    finally {
        if (Test-Path $download) { Remove-Item -Force $download }
    }

    # The user's PATH as it is written, with its %VARIABLES% kept, rather than expanded into it, as
    # [Environment]::GetEnvironmentVariable would
    $key = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey('Environment', $true)
    try {
        $path = [string]$key.GetValue('Path', '', [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames)
        $parts = $path -split ';' | Where-Object { $_ -ne '' }
        $isOnPath = $parts | Where-Object { [Environment]::ExpandEnvironmentVariables($_).TrimEnd('\') -eq $folder }
        if (-not $isOnPath) {
            $newPath = if ($parts) { (@($parts) + $folder) -join ';' } else { $folder }
            $key.SetValue('Path', $newPath, [Microsoft.Win32.RegistryValueKind]::ExpandString)
            # Writing the registry tells no one; setting a variable this way tells Windows that the
            # environment changed, so that programs started from now on have the new PATH
            [Environment]::SetEnvironmentVariable('GMD_INSTALLING', '1', 'User')
            [Environment]::SetEnvironmentVariable('GMD_INSTALLING', $null, 'User')
            Write-Host "Added $folder to your PATH"
        }
    }
    finally {
        $key.Close()
    }
    if (-not (($env:Path -split ';') | Where-Object { $_.TrimEnd('\') -eq $folder })) {
        $env:Path = "$env:Path;$folder"
    }

    Write-Host "gmd version: $(& $exe --version)"
    $other = Get-Command gmd -All -ErrorAction SilentlyContinue | Where-Object { $_.Source -ne $exe } | Select-Object -First 1
    if ($other) {
        Write-Host "There is another gmd on your PATH, $($other.Source), which may be found first"
    }
    Write-Host 'Open a new terminal and run gmd in a git repository'
}
