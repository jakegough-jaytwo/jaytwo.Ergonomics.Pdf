$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../../../..')).Path
Push-Location $repo
try {
    dotnet build src/jaytwo.Pdf/jaytwo.Pdf.csproj -f net10.0 --no-restore --verbosity quiet
    if ($LASTEXITCODE -ne 0) { throw 'Engine build failed' }
    $scratch = Join-Path ([IO.Path]::GetTempPath()) ('w203-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $scratch | Out-Null
    $engine = Join-Path $repo 'src/jaytwo.Pdf/bin/Debug/net10.0/jaytwo.Pdf.dll'
    $pkcs = Join-Path $repo 'test/jaytwo.Pdf.Tests/bin/Debug/net10.0/System.Security.Cryptography.Pkcs.dll'
    $project = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'probe/Probe.csproj.txt'))
    $project = $project.Replace('ENGINE_DLL', [Security.SecurityElement]::Escape($engine)).Replace('PKCS_DLL', [Security.SecurityElement]::Escape($pkcs))
    [IO.File]::WriteAllText((Join-Path $scratch 'Probe.csproj'), $project)
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'probe/Program.cs') -Destination $scratch
    dotnet build (Join-Path $scratch 'Probe.csproj') --verbosity quiet
    if ($LASTEXITCODE -ne 0) { throw 'Probe build failed' }
    $output = & dotnet (Join-Path $scratch 'bin/Debug/net10.0/Probe.dll')
    if ($LASTEXITCODE -ne 0) { throw 'Probe failed' }
    [IO.File]::WriteAllText((Join-Path $PSScriptRoot 'observations.json'), ($output -join [Environment]::NewLine) + [Environment]::NewLine)
    $candidateDir = Join-Path $scratch 'candidate-engine'
    New-Item -ItemType Directory -Path $candidateDir | Out-Null
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'candidate-checker.cs.txt') -Destination (Join-Path $candidateDir 'PdfSignatureSeedValues.cs')
    $engineSource = [Security.SecurityElement]::Escape((Join-Path $repo 'src/jaytwo.Pdf').Replace('\', '/'))
    $candidateProject = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework><AssemblyName>jaytwo.Pdf</AssemblyName>
    <Nullable>enable</Nullable><LangVersion>latest</LangVersion><Optimize>true</Optimize>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors><EnableDefaultCompileItems>false</EnableDefaultCompileItems>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="$engineSource/**/*.cs" Exclude="$engineSource/bin/**;$engineSource/obj/**;$engineSource/Internal/PdfSignatureSeedValues.cs" />
    <Compile Include="PdfSignatureSeedValues.cs" />
  </ItemGroup>
</Project>
"@
    [IO.File]::WriteAllText((Join-Path $candidateDir 'Candidate.csproj'), $candidateProject)
    dotnet build (Join-Path $candidateDir 'Candidate.csproj') --verbosity quiet
    if ($LASTEXITCODE -ne 0) { throw 'Disposable candidate engine build failed' }
    $candidateDll = Join-Path $candidateDir 'bin/Debug/net10.0/jaytwo.Pdf.dll'
    $candidateProbe = Join-Path $scratch 'candidate-probe'
    New-Item -ItemType Directory -Path $candidateProbe | Out-Null
    $project = $project.Replace([Security.SecurityElement]::Escape($engine), [Security.SecurityElement]::Escape($candidateDll))
    [IO.File]::WriteAllText((Join-Path $candidateProbe 'Probe.csproj'), $project)
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'probe/Program.cs') -Destination $candidateProbe
    dotnet build (Join-Path $candidateProbe 'Probe.csproj') --verbosity quiet
    if ($LASTEXITCODE -ne 0) { throw 'Candidate probe build failed' }
    $candidateOutput = & dotnet (Join-Path $candidateProbe 'bin/Debug/net10.0/Probe.dll') --candidate-engine
    if ($LASTEXITCODE -ne 0) { throw 'Candidate probe failed' }
    [IO.File]::WriteAllText((Join-Path $PSScriptRoot 'candidate-observations.json'), ($candidateOutput -join [Environment]::NewLine) + [Environment]::NewLine)
    $execution = [ordered]@{
        revision = (& git rev-parse HEAD)
        sdk = (& dotnet --version)
        os = [Environment]::OSVersion.VersionString
        framework = 'net10.0'
        pkcsVersion = [Reflection.AssemblyName]::GetAssemblyName($pkcs).Version.ToString()
        engineSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $engine).Hash
        pkcsSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $pkcs).Hash
        probeSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $PSScriptRoot 'probe/Program.cs')).Hash
        candidateCheckerSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $PSScriptRoot 'candidate-checker.cs.txt')).Hash
        candidateEngineSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $candidateDll).Hash
        candidateBuild = 'isolated net10 engine source build, same sources except checker; compiler warnings as errors; no StyleCop package or framework matrix'
        scratch = $scratch
    }
    [IO.File]::WriteAllText((Join-Path $PSScriptRoot 'execution.json'), ($execution | ConvertTo-Json) + [Environment]::NewLine)
    $observations = ($output -join [Environment]::NewLine) | ConvertFrom-Json
    $observations.observations | Group-Object lost | Select-Object Name, Count
    # Keep scratch output; no recursive deletion or persistent credentials.
} finally { Pop-Location }
