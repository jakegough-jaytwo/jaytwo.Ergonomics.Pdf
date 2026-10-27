$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../../../..')).Path
$scratch = Join-Path $repo 'out/W-192'
New-Item -ItemType Directory -Force -Path "$scratch/inputs", "$scratch/outputs", "$scratch/probe" | Out-Null
[xml]$settings = Get-Content -LiteralPath "$repo/.tools/test.runsettings"
$python = $settings.RunSettings.RunConfiguration.EnvironmentVariables.JAYTWO_PYTHON
$qpdf = $settings.RunSettings.RunConfiguration.EnvironmentVariables.JAYTWO_QPDF
& $python -I "$PSScriptRoot/generate.py" "$scratch/inputs" "$PSScriptRoot/inputs.json"
if ($LASTEXITCODE) { throw 'Generation failed' }
$project = (Get-Content -LiteralPath "$PSScriptRoot/probe/probe.csproj.txt" -Raw).Replace('REPO', $repo.Replace('\','/'))
Set-Content -LiteralPath "$scratch/probe/probe.csproj" -Value $project
Set-Content -LiteralPath "$scratch/probe/Directory.Build.props" -Value '<Project />'
Copy-Item -LiteralPath "$PSScriptRoot/probe/Program.cs" -Destination "$scratch/probe/Program.cs"
dotnet build "$scratch/probe/probe.csproj" --packages "$repo/.tools/nuget-packages" --ignore-failed-sources
if ($LASTEXITCODE) { throw 'Build failed' }
& "$scratch/probe/bin/Debug/net10.0/probe.exe" "$scratch/inputs" "$scratch/outputs" | Set-Content -LiteralPath "$PSScriptRoot/results.json"
if ($LASTEXITCODE) { throw 'Probe failed' }
& $python -I "$PSScriptRoot/inspect.py" $scratch $qpdf $PSScriptRoot
if ($LASTEXITCODE) { throw 'Inspection failed' }
