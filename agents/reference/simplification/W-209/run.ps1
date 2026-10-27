$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../../../..')).Path
Push-Location $repo
try {
    dotnet build src/jaytwo.Pdf/jaytwo.Pdf.csproj -f net10.0 -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Library build failed' }
    $scratch = Join-Path ([IO.Path]::GetTempPath()) ('w209-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $scratch | Out-Null
    $fixtures = Join-Path $scratch 'fixtures'
    $python = Join-Path $repo '.tools/python/Scripts/python.exe'
    $qpdf = Join-Path $repo '.tools/msys64/ucrt64/bin/qpdf.exe'
    $gen = & $python -I (Join-Path $PSScriptRoot 'gen_fixtures.py') $fixtures
    if ($LASTEXITCODE -ne 0) { throw 'Fixture generation failed' }
    # Independent validity check of every non-malformed, non-unsupported, non-bomb fixture.
    $manifest = Get-Content (Join-Path $fixtures 'manifest.json') -Raw | ConvertFrom-Json
    $ErrorActionPreference = 'Continue'  # qpdf writes warnings to stderr for deliberately malformed fixtures
    $qpdfRows = foreach ($case in $manifest.cases) {
        $out = & $qpdf --check (Join-Path $fixtures $case.file) 2>&1 | Out-String
        [pscustomobject]@{ id = $case.id; kind = $case.kind; qpdfExit = $LASTEXITCODE; qpdfSummary = ($out -split "`r?`n" | Where-Object { $_ -match 'No syntax|WARNING|ERROR|qpdf:' } | Select-Object -First 2) -join ' | ' }
    }
    $ErrorActionPreference = 'Stop'
    $qpdfRows | ConvertTo-Json -Depth 3 | Set-Content -Encoding utf8 (Join-Path $PSScriptRoot 'qpdf-check.json')
    $dll = Join-Path $repo 'src/jaytwo.Pdf/bin/Release/net10.0/jaytwo.Pdf.dll'
    $project = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'probe/Probe.csproj.txt'))
    $project = $project.Replace('ENGINE_DLL', [Security.SecurityElement]::Escape($dll))
    [IO.File]::WriteAllText((Join-Path $scratch 'Probe.csproj'), $project)
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'probe/Program.cs') -Destination $scratch
    dotnet build (Join-Path $scratch 'Probe.csproj') -c Release --verbosity quiet
    if ($LASTEXITCODE -ne 0) { throw 'Probe build failed' }
    $output = & dotnet (Join-Path $scratch 'bin/Release/net10.0/Probe.dll') $fixtures
    if ($LASTEXITCODE -ne 0) { throw 'Probe failed' }
    [IO.File]::WriteAllText((Join-Path $PSScriptRoot 'observations.jsonl'), ($output -join [Environment]::NewLine) + [Environment]::NewLine)
    Copy-Item -LiteralPath (Join-Path $fixtures 'manifest.json') -Destination (Join-Path $PSScriptRoot 'fixture-manifest.json')
    $execution = [ordered]@{
        revision = (& git rev-parse HEAD)
        sdk = (& dotnet --version)
        os = [Environment]::OSVersion.VersionString
        framework = 'net10.0'
        configuration = 'Release'
        fixtures = $gen
        qpdf = (& $qpdf --version | Select-Object -First 1)
        engineSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $dll).Hash
        probeSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $PSScriptRoot 'probe/Program.cs')).Hash
        generatorSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $PSScriptRoot 'gen_fixtures.py')).Hash
    }
    [IO.File]::WriteAllText((Join-Path $PSScriptRoot 'execution.json'), ($execution | ConvertTo-Json) + [Environment]::NewLine)
    "cases: " + ($output.Count - 1)
    $global:LASTEXITCODE = 0  # the qpdf --version pipeline above stops early and leaves a stale non-zero status
} finally { Pop-Location }
