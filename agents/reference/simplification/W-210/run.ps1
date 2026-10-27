$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../../../..')).Path
Push-Location $repo
try {
    dotnet build src/jaytwo.Pdf/jaytwo.Pdf.csproj -f net10.0 --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Library build failed' }
    python agents/reference/simplification/W-210/inventory.py
    if ($LASTEXITCODE -ne 0) { throw 'Inventory failed' }
    python agents/reference/simplification/W-210/test_oracle.py
    if ($LASTEXITCODE -ne 0) { throw 'Oracle split failed' }
    $scratch = Join-Path ([IO.Path]::GetTempPath()) ('w210-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $scratch | Out-Null
    $project = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'probe/Probe.csproj.txt'))
    $project = $project.Replace('ENGINE_DLL', [Security.SecurityElement]::Escape((Join-Path $repo 'src/jaytwo.Pdf/bin/Debug/net10.0/jaytwo.Pdf.dll')))
    $pkcs = Join-Path $repo 'test/jaytwo.Pdf.Tests/bin/Debug/net10.0/System.Security.Cryptography.Pkcs.dll'
    if (!(Test-Path -LiteralPath $pkcs)) { throw 'Build the existing net10 engine tests to provide their test-only Pkcs assembly' }
    $project = $project.Replace('PKCS_DLL', [Security.SecurityElement]::Escape($pkcs))
    [IO.File]::WriteAllText((Join-Path $scratch 'Probe.csproj'), $project)
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'probe/Program.cs') -Destination $scratch
    dotnet build (Join-Path $scratch 'Probe.csproj') --verbosity quiet
    if ($LASTEXITCODE -ne 0) { throw 'Probe build failed' }
    $output = & dotnet (Join-Path $scratch 'bin/Debug/net10.0/Probe.dll')
    if ($LASTEXITCODE -ne 0) { throw 'Probe failed' }
    [IO.File]::WriteAllText((Join-Path $PSScriptRoot 'observations.json'), ($output -join [Environment]::NewLine) + [Environment]::NewLine)
    $dll = Join-Path $repo 'src/jaytwo.Pdf/bin/Debug/net10.0/jaytwo.Pdf.dll'
    $execution = [ordered]@{
        revision = (& git rev-parse HEAD)
        sdk = (& dotnet --version)
        os = [Environment]::OSVersion.VersionString
        framework = 'net10.0'
        pkcsSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $pkcs).Hash
        engineSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $dll).Hash
        probeSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $PSScriptRoot 'probe/Program.cs')).Hash
    }
    [IO.File]::WriteAllText((Join-Path $PSScriptRoot 'execution.json'), ($execution | ConvertTo-Json) + [Environment]::NewLine)
    $output
} finally { Pop-Location }
