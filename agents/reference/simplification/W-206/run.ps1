$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../../../..')).Path
$scratch = Join-Path $repo ('out/W-206/run-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path "$scratch/probe" | Out-Null
(Get-Content -LiteralPath "$PSScriptRoot/probe/probe.csproj.txt" -Raw).Replace('REPO', $repo.Replace('\','/')) | Set-Content "$scratch/probe/probe.csproj"
'<Project />' | Set-Content "$scratch/probe/Directory.Build.props"
Copy-Item -LiteralPath "$PSScriptRoot/probe/Program.cs" -Destination "$scratch/probe/Program.cs"
dotnet build "$scratch/probe/probe.csproj" --source "$repo/.tools/nuget-packages" -p:NuGetAudit=false
if ($LASTEXITCODE) { throw 'Build failed' }
dotnet "$scratch/probe/bin/Debug/net10.0/probe.dll" "$scratch/files" | Set-Content -LiteralPath "$PSScriptRoot/windows.json"
if ($LASTEXITCODE) { throw 'Probe failed; see windows.json' }
