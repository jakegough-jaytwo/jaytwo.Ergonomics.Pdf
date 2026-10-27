$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../../../..')).Path
Push-Location $repo
try {
    dotnet build src/jaytwo.Pdf/jaytwo.Pdf.csproj -f net10.0 -c Release --no-restore
    dotnet build src/jaytwo.Ergonomics.Pdf/jaytwo.Ergonomics.Pdf.csproj -f net10.0 -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Library build failed' }
    $scratch = Join-Path ([IO.Path]::GetTempPath()) ('w208-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $scratch | Out-Null
    $fixture = Join-Path $scratch 'fixture.pdf'
    $gen = & python (Join-Path $PSScriptRoot 'gen_fixture.py') $fixture
    $dll = Join-Path $repo 'src/jaytwo.Pdf/bin/Release/net10.0/jaytwo.Pdf.dll'
    $ergo = Join-Path $repo 'src/jaytwo.Ergonomics.Pdf/bin/Release/net10.0/jaytwo.Ergonomics.Pdf.dll'
    $project = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'probe/Probe.csproj.txt'))
    $project = $project.Replace('ENGINE_DLL', [Security.SecurityElement]::Escape($dll)).Replace('ERGO_DLL', [Security.SecurityElement]::Escape($ergo))
    [IO.File]::WriteAllText((Join-Path $scratch 'Probe.csproj'), $project)
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'probe/Program.cs'), (Join-Path $PSScriptRoot 'probe/Types.cs') -Destination $scratch
    dotnet build (Join-Path $scratch 'Probe.csproj') -c Release --verbosity quiet
    if ($LASTEXITCODE -ne 0) { throw 'Probe build failed' }
    $output = & dotnet (Join-Path $scratch 'bin/Release/net10.0/Probe.dll') $fixture
    if ($LASTEXITCODE -ne 0) { throw 'Probe failed' }
    [IO.File]::WriteAllText((Join-Path $PSScriptRoot 'observations.jsonl'), ($output -join [Environment]::NewLine) + [Environment]::NewLine)
    $execution = [ordered]@{
        revision = (& git rev-parse HEAD)
        sdk = (& dotnet --version)
        os = [Environment]::OSVersion.VersionString
        framework = 'net10.0'
        configuration = 'Release'
        fixture = $gen
        ergonomicsSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $ergo).Hash
        engineSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $dll).Hash
        probeSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $PSScriptRoot 'probe/Program.cs')).Hash
        typesSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $PSScriptRoot 'probe/Types.cs')).Hash
    }
    [IO.File]::WriteAllText((Join-Path $PSScriptRoot 'execution.json'), ($execution | ConvertTo-Json) + [Environment]::NewLine)
    $output
} finally { Pop-Location }
