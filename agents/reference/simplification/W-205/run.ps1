$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../../../..')).Path
$scratch = Join-Path $repo 'out/W-205'
New-Item -ItemType Directory -Force -Path "$scratch/probe" | Out-Null
$project = (Get-Content -LiteralPath "$PSScriptRoot/probe/probe.csproj.txt" -Raw).Replace('REPO', $repo.Replace('\','/'))
Set-Content -LiteralPath "$scratch/probe/probe.csproj" -Value $project
Set-Content -LiteralPath "$scratch/probe/Directory.Build.props" -Value '<Project />'
Copy-Item -Path "$PSScriptRoot/probe/*.cs" -Destination "$scratch/probe/"
dotnet build "$scratch/probe/probe.csproj" --packages "$repo/.tools/nuget-packages" --source "$repo/.tools/nuget-packages" -p:NuGetAudit=false
if ($LASTEXITCODE) { throw 'Build failed' }
& "$scratch/probe/bin/Debug/net10.0/probe.exe" $scratch $PSScriptRoot
if ($LASTEXITCODE) { throw 'Probe failed' }
[pscustomobject]@{ Baseline = (git -C $repo rev-parse HEAD); Dotnet = (dotnet --version); ProbeExit = 0 } | ConvertTo-Json | Set-Content -LiteralPath "$PSScriptRoot/execution.json"
