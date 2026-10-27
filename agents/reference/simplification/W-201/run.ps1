$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../../../..')).Path
$scratch = Join-Path $repo 'out/W-201'
New-Item -ItemType Directory -Force -Path "$scratch/inputs", "$scratch/outputs", "$scratch/probe" | Out-Null
[xml]$settings = Get-Content -LiteralPath "$repo/.tools/test.runsettings"
$python = $settings.RunSettings.RunConfiguration.EnvironmentVariables.JAYTWO_PYTHON
$qpdf = $settings.RunSettings.RunConfiguration.EnvironmentVariables.JAYTWO_QPDF
$java = $settings.RunSettings.RunConfiguration.EnvironmentVariables.JAYTWO_JAVA
& $python -I "$PSScriptRoot/research.py" generate $scratch $PSScriptRoot $qpdf
if ($LASTEXITCODE) { throw 'Generation failed' }
$project = (Get-Content -LiteralPath "$PSScriptRoot/probe/probe.csproj.txt" -Raw).Replace('REPO', $repo.Replace('\','/'))
Set-Content -LiteralPath "$scratch/probe/probe.csproj" -Value $project
Set-Content -LiteralPath "$scratch/probe/Directory.Build.props" -Value '<Project />'
Copy-Item -LiteralPath "$PSScriptRoot/probe/Program.cs" -Destination "$scratch/probe/Program.cs"
dotnet build "$scratch/probe/probe.csproj" --packages "$repo/.tools/nuget-packages" --ignore-failed-sources
if ($LASTEXITCODE) { throw 'Build failed' }
& "$scratch/probe/bin/Debug/net10.0/probe.exe" $scratch "$PSScriptRoot/scenarios.json" "$PSScriptRoot/recovery.json" | Set-Content -LiteralPath "$PSScriptRoot/engine.json"
if ($LASTEXITCODE) { throw 'Engine probe failed' }
& $java --class-path "$repo/.tools/dss/lib/*" "$PSScriptRoot/probe/IdentityProbe.java" $scratch | Set-Content -LiteralPath "$PSScriptRoot/pdfbox.txt"
if ($LASTEXITCODE) { throw 'PDFBox probe failed' }
& $python -I "$PSScriptRoot/research.py" compare $scratch $PSScriptRoot $qpdf
if ($LASTEXITCODE) { throw 'Comparison failed' }
& $python -I "$PSScriptRoot/research.py" inspect $scratch $PSScriptRoot $qpdf
if ($LASTEXITCODE) { throw 'Inspection failed' }
Get-FileHash -Algorithm SHA256 -LiteralPath "$repo/.tools/dss/lib/pdfbox-3.0.5.jar", "$repo/.tools/dss/lib/pdfbox-io-3.0.5.jar" | ForEach-Object { [pscustomobject]@{ Name = Split-Path -Leaf $_.Path; SHA256 = $_.Hash } } | ConvertTo-Json | Set-Content -LiteralPath "$PSScriptRoot/tool-hashes.json"
[pscustomobject]@{ Baseline = (git -C $repo rev-parse HEAD); Dotnet = (dotnet --version); Java = ((& $java --version) -join "`n"); Python = (& $python --version); BuildExit = 0; ComparisonExit = 0; InspectionExit = 0 } | ConvertTo-Json | Set-Content -LiteralPath "$PSScriptRoot/execution.json"
