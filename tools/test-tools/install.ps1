<#
.SYNOPSIS
Installs the external test tools into .tools/ and writes .tools/test.runsettings (W-119, D-51).

.DESCRIPTION
Windows only. Linux gets the same tools from the Dockerfile base stage. Every download is pinned and checked against
its SHA-256 before it is unpacked. Nothing needs administrator rights, and nothing outside the tools directory changes.
Each component leaves a stamp holding its pin, so a second run with the same pins downloads nothing. A changed pin
reinstalls that component.

The test projects load .tools/test.runsettings when it exists, so `dotnet test` and IDE test runners find the tools
without any JAYTWO_* variable.

.PARAMETER ToolsDirectory
Where to install. Defaults to .tools/ at the repository root.
#>
[CmdletBinding()]
param(
    [string] $ToolsDirectory
)

$ErrorActionPreference = 'Stop'
# Invoke-WebRequest on Windows PowerShell 5.1 is many times slower while it draws a progress bar.
$ProgressPreference = 'SilentlyContinue'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if (-not $ToolsDirectory) {
    $ToolsDirectory = Join-Path $RepositoryRoot '.tools'
}

# Pins. A new test tool is added here and to the Dockerfile base stage in the same change (D-51).
$Msys2Pin = @{
    Url = 'https://github.com/msys2/msys2-installer/releases/download/2026-09-27/msys2-base-x86_64-20260927.sfx.exe'
    Sha256 = 'ad336cccfda47758b5e15cda993fbba421115cb0b126697daef1ee4dfe37209f'
}

# MSYS2 ucrt64 packages are native Windows binaries. Their versions follow the MSYS2 repository, as the Dockerfile's
# apt packages follow Ubuntu's; .tools/versions.txt records what was installed.
$Msys2Packages = @(
    'mingw-w64-ucrt-x86_64-nss',
    'mingw-w64-ucrt-x86_64-openssl',
    'mingw-w64-ucrt-x86_64-poppler',
    'mingw-w64-ucrt-x86_64-qpdf'
)

$SoftHsmPin = @{
    Url = 'https://github.com/disig/SoftHSM2-for-Windows/releases/download/v2.5.0/SoftHSM2-2.5.0-portable.zip'
    Sha256 = '85273bcc1a6b90e877f7bb4f7e90221d57103d8f5241d154a79dd730a135b910'
}

$UvPin = @{
    Url = 'https://github.com/astral-sh/uv/releases/download/0.13.5/uv-x86_64-pc-windows-msvc.zip'
    Sha256 = 'a38b8d1902b7cfbc76b423ba1bb456f623f42064cdc72628b6075b89afd1019a'
}

$PythonVersion = '3.13'
$Requirements = Join-Path $PSScriptRoot 'requirements.txt'

$DotnetRuntimes = @('6.0', '8.0', '10.0')

function Write-Step([string] $message) {
    Write-Host "==> $message"
}

function Test-Stamp([string] $name, [string] $pin) {
    $path = Join-Path $ToolsDirectory "$name.stamp"
    return (Test-Path $path) -and ((Get-Content -Raw $path).Trim() -eq $pin)
}

function Set-Stamp([string] $name, [string] $pin) {
    Set-Content -Encoding ascii -Path (Join-Path $ToolsDirectory "$name.stamp") -Value $pin
}

function Get-PinnedFile([hashtable] $pin) {
    $downloads = Join-Path $ToolsDirectory 'downloads'
    New-Item -ItemType Directory -Force $downloads | Out-Null
    $path = Join-Path $downloads ([IO.Path]::GetFileName($pin.Url))
    $partial = "$path.partial"
    Write-Step "Downloading $($pin.Url)"
    Invoke-WebRequest -UseBasicParsing -Uri $pin.Url -OutFile $partial
    $actual = (Get-FileHash -Algorithm SHA256 $partial).Hash.ToLowerInvariant()
    if ($actual -ne $pin.Sha256) {
        Remove-Item -Force $partial
        throw "SHA-256 mismatch for $($pin.Url): expected $($pin.Sha256), got $actual. Nothing was unpacked."
    }

    Move-Item -Force $partial $path
    return $path
}

function Expand-PinnedZip([hashtable] $pin, [string] $destination) {
    $zip = Get-PinnedFile $pin
    if (Test-Path $destination) {
        Remove-Item -Recurse -Force $destination
    }

    Expand-Archive -Path $zip -DestinationPath $destination
    Remove-Item -Force $zip
}

# Runs a native command with extra environment variables, restoring the caller's environment afterwards.
function Invoke-Native([string] $file, [string[]] $arguments, [hashtable] $environment, [switch] $AllowFailure) {
    $saved = @{}
    foreach ($key in $environment.Keys) {
        $saved[$key] = [Environment]::GetEnvironmentVariable($key, 'Process')
        [Environment]::SetEnvironmentVariable($key, $environment[$key], 'Process')
    }

    try {
        & $file @arguments
        if ($LASTEXITCODE -ne 0 -and -not $AllowFailure) {
            throw "$file $($arguments -join ' ') exited $LASTEXITCODE."
        }
    } finally {
        foreach ($key in $saved.Keys) {
            [Environment]::SetEnvironmentVariable($key, $saved[$key], 'Process')
        }
    }
}

# The first line a tool prints, from stdout or stderr. Windows PowerShell turns native stderr into errors, which
# $ErrorActionPreference = 'Stop' would make fatal, so this scope relaxes it.
function Get-FirstLine([string] $file, [string] $argument) {
    $ErrorActionPreference = 'Continue'
    $line = & $file $argument 2>&1 | Select-Object -First 1
    return "$line".Trim()
}

function Find-One([string] $directory, [string] $fileName) {
    $found = Get-ChildItem -Path $directory -Recurse -File -Filter $fileName | Select-Object -First 1
    if (-not $found) {
        throw "$fileName was not found under $directory."
    }

    return $found.FullName
}

if (-not [Environment]::Is64BitOperatingSystem -or $env:OS -ne 'Windows_NT') {
    throw 'install.ps1 supports 64-bit Windows only. On Linux, use the Dockerfile base stage.'
}

New-Item -ItemType Directory -Force $ToolsDirectory | Out-Null
$ToolsDirectory = (Resolve-Path $ToolsDirectory).Path

# MSYS2: Poppler (pdftotext, pdftoppm, pdfinfo, and pdfsig with NSS), NSS certutil, qpdf, and OpenSSL.
$msys64 = Join-Path $ToolsDirectory 'msys64'
$bash = Join-Path $msys64 'usr\bin\bash.exe'
$msysEnvironment = @{ MSYSTEM = 'UCRT64'; CHERE_INVOKING = '1'; MSYS2_PATH_TYPE = 'minimal' }
if (-not (Test-Stamp 'msys2' $Msys2Pin.Sha256)) {
    if (Test-Path $msys64) {
        Remove-Item -Recurse -Force $msys64
    }

    $sfx = Get-PinnedFile $Msys2Pin
    Write-Step 'Unpacking MSYS2'
    $process = Start-Process -FilePath $sfx -ArgumentList '-y', "-o`"$ToolsDirectory`"" -NoNewWindow -Wait -PassThru
    if ($process.ExitCode -ne 0) {
        throw "The MSYS2 archive exited $($process.ExitCode)."
    }

    Remove-Item -Force $sfx
    Write-Step 'Initializing MSYS2 and updating its core packages'
    Invoke-Native $bash @('-lc', 'true') $msysEnvironment
    # The first update can replace the MSYS2 runtime and end the shell, so it may fail; the second finishes it.
    Invoke-Native $bash @('-lc', 'pacman -Syuu --noconfirm') $msysEnvironment -AllowFailure
    Invoke-Native $bash @('-lc', 'pacman -Syuu --noconfirm') $msysEnvironment
    Set-Stamp 'msys2' $Msys2Pin.Sha256
    Remove-Item -Force -ErrorAction SilentlyContinue (Join-Path $ToolsDirectory 'msys2-packages.stamp')
}

$packagePin = $Msys2Packages -join ' '
if (-not (Test-Stamp 'msys2-packages' $packagePin)) {
    Write-Step "Installing $packagePin"
    Invoke-Native $bash @('-lc', "pacman -Syu --needed --noconfirm $packagePin && pacman -Scc --noconfirm") $msysEnvironment
    Set-Stamp 'msys2-packages' $packagePin
}

# SoftHSM 2, loaded in-process by the PKCS#11 tests.
$softhsm = Join-Path $ToolsDirectory 'softhsm'
if (-not (Test-Stamp 'softhsm' $SoftHsmPin.Sha256)) {
    Write-Step 'Installing SoftHSM 2'
    Expand-PinnedZip $SoftHsmPin $softhsm
    Set-Stamp 'softhsm' $SoftHsmPin.Sha256
}

# Python in a virtual environment, so the pinned packages never mix with a system or user site.
$uvDirectory = Join-Path $ToolsDirectory 'uv'
$uvExe = Join-Path $uvDirectory 'uv.exe'
$venv = Join-Path $ToolsDirectory 'python'
$python = Join-Path $venv 'Scripts\python.exe'
$uvEnvironment = @{
    UV_CACHE_DIR = (Join-Path $uvDirectory 'cache')
    UV_PYTHON_INSTALL_DIR = (Join-Path $uvDirectory 'python')
    UV_PYTHON_PREFERENCE = 'only-managed'
    UV_NO_CONFIG = '1'
}
if (-not (Test-Stamp 'uv' $UvPin.Sha256)) {
    Write-Step 'Installing uv'
    Expand-PinnedZip $UvPin $uvDirectory
    Set-Stamp 'uv' $UvPin.Sha256
    Remove-Item -Force -ErrorAction SilentlyContinue (Join-Path $ToolsDirectory 'python.stamp')
}

$pythonPin = $PythonVersion + ' ' + (Get-FileHash -Algorithm SHA256 $Requirements).Hash.ToLowerInvariant()
if (-not (Test-Stamp 'python' $pythonPin)) {
    Write-Step "Creating the Python $PythonVersion environment"
    if (Test-Path $venv) {
        Remove-Item -Recurse -Force $venv
    }

    Invoke-Native $uvExe @('venv', '--quiet', '--python', $PythonVersion, $venv) $uvEnvironment
    Invoke-Native $uvExe @('pip', 'install', '--quiet', '--python', $python, '-r', $Requirements) $uvEnvironment
    Invoke-Native $uvExe @('cache', 'clean', '--quiet') $uvEnvironment
    Set-Stamp 'python' $pythonPin
}

# The run settings the test projects load. Every path is checked, so a broken install fails here, not in a test.
$ucrtBin = Join-Path $msys64 'ucrt64\bin'
$softhsmLibrary = Find-One $softhsm 'softhsm2-x64.dll'
$variables = [ordered]@{
    JAYTWO_CERTUTIL = Join-Path $ucrtBin 'certutil.exe'
    JAYTWO_OPENSSL = Join-Path $ucrtBin 'openssl.exe'
    JAYTWO_PDFINFO = Join-Path $ucrtBin 'pdfinfo.exe'
    JAYTWO_PDFSIG = Join-Path $ucrtBin 'pdfsig.exe'
    JAYTWO_PDFTOPPM = Join-Path $ucrtBin 'pdftoppm.exe'
    JAYTWO_PDFTOTEXT = Join-Path $ucrtBin 'pdftotext.exe'
    JAYTWO_PKCS11 = $softhsmLibrary
    JAYTWO_POPPLER = $ucrtBin
    JAYTWO_PYTHON = $python
    JAYTWO_QPDF = Join-Path $ucrtBin 'qpdf.exe'
    JAYTWO_SOFTHSM_UTIL = Join-Path (Split-Path -Parent (Split-Path -Parent $softhsmLibrary)) 'bin\softhsm2-util.exe'
}
foreach ($name in $variables.Keys) {
    if (-not (Test-Path $variables[$name])) {
        throw "$name points to $($variables[$name]), which does not exist."
    }
}

$lines = @(
    '<?xml version="1.0" encoding="utf-8"?>',
    '<!-- Written by tools/test-tools/install.ps1 (D-51). Rerun the script instead of editing this file. -->',
    '<RunSettings>',
    '  <RunConfiguration>',
    '    <EnvironmentVariables>'
)
foreach ($name in $variables.Keys) {
    $lines += "      <$name>$([Security.SecurityElement]::Escape($variables[$name]))</$name>"
}

$lines += @('    </EnvironmentVariables>', '  </RunConfiguration>', '</RunSettings>')
$runSettings = Join-Path $ToolsDirectory 'test.runsettings'
Set-Content -Encoding utf8 -Path $runSettings -Value $lines

# The installed versions, for evidence records.
$versions = @()
$versions += (Get-FirstLine $variables.JAYTWO_QPDF '--version')
$versions += 'poppler ' + (Get-FirstLine $variables.JAYTWO_PDFINFO '-v')
$versions += (Get-FirstLine $variables.JAYTWO_OPENSSL 'version')
$versions += (Get-FirstLine $python '--version')
$versions += (Invoke-Native $uvExe @('pip', 'list', '--python', $python) $uvEnvironment | Select-String -Pattern '^(pyhanko|pyhanko-certvalidator|pypdf) ' | ForEach-Object { $_.Line })
$versions += "SoftHSM $($SoftHsmPin.Url)"
Set-Content -Encoding utf8 -Path (Join-Path $ToolsDirectory 'versions.txt') -Value $versions

# .NET is not installed here; report what the test targets need.
$runtimes = @()
if (Get-Command dotnet -ErrorAction SilentlyContinue) {
    $runtimes = & dotnet --list-runtimes
}

$missing = @()
foreach ($version in $DotnetRuntimes) {
    if (-not ($runtimes | Select-String -SimpleMatch "Microsoft.NETCore.App $version.")) {
        $missing += $version
    }
}

Write-Step "Wrote $runSettings"
$versions | ForEach-Object { Write-Host "    $_" }
if ($missing.Count -gt 0) {
    Write-Warning ('Missing .NET runtimes for the test targets: ' + ($missing -join ', ') + '. Install them with dotnet-install.ps1 -Runtime dotnet -Channel <version>.')
}
