$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../../../..')).Path
$scratch = Join-Path $repo 'out/W-200'
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
& "$scratch/probe/bin/Debug/net10.0/probe.exe" $scratch | Set-Content -LiteralPath "$PSScriptRoot/engine.json"
if ($LASTEXITCODE) { throw 'Engine probe failed' }
& $java --class-path "$repo/.tools/dss/lib/*" "$PSScriptRoot/probe/MetadataProbe.java" $scratch | Set-Content -LiteralPath "$PSScriptRoot/pdfbox.txt"
if ($LASTEXITCODE) { throw 'PDFBox probe failed' }
$javap = Join-Path (Split-Path -Parent $java) 'javap.exe'
$bytecode = & $javap -classpath "$repo/.tools/dss/lib/pdfbox-3.0.5.jar" -c org.apache.pdfbox.pdmodel.PDDocumentInformation | Select-String -Pattern 'setTitle' -Context 0,8 | Out-String
if ($LASTEXITCODE) { throw 'PDFBox bytecode inspection failed' }
$normalizedBytecode = (($bytecode -split "`r?`n" | ForEach-Object { $_.TrimEnd() }) -join "`n").Trim() + "`n"
[IO.File]::WriteAllText("$PSScriptRoot/pdfbox-bytecode.txt", $normalizedBytecode)
$toolHashes = Get-FileHash -Algorithm SHA256 -LiteralPath "$repo/.tools/dss/lib/pdfbox-3.0.5.jar", "$repo/.tools/dss/lib/pdfbox-io-3.0.5.jar"
$toolHashes | ForEach-Object { [pscustomobject]@{ Name = Split-Path -Leaf $_.Path; SHA256 = $_.Hash } } | ConvertTo-Json | Set-Content -LiteralPath "$PSScriptRoot/tool-hashes.json"
& $python -I "$PSScriptRoot/research.py" inspect $scratch $PSScriptRoot $qpdf
if ($LASTEXITCODE) { throw 'Inspection failed' }
