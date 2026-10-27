param([switch]$NoRestore)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../../../..')).Path
$scratch = Join-Path $repo 'out/W-197'
if (Test-Path -LiteralPath $scratch) { Remove-Item -LiteralPath "$scratch/inputs", "$scratch/outputs" -Recurse -Force -ErrorAction SilentlyContinue }
New-Item -ItemType Directory -Force -Path "$scratch/inputs", "$scratch/outputs", "$scratch/probe" | Out-Null
[xml]$settings = Get-Content -LiteralPath "$repo/.tools/test.runsettings"
$python = $settings.RunSettings.RunConfiguration.EnvironmentVariables.JAYTWO_PYTHON
& $python -I "$PSScriptRoot/generate.py" "$scratch/inputs"
if ($LASTEXITCODE) { throw 'Generation failed' }
$project = (Get-Content -LiteralPath "$PSScriptRoot/probe/probe.csproj.txt" -Raw).Replace('REPO', $repo.Replace('\','/'))
Set-Content -LiteralPath "$scratch/probe/probe.csproj" -Value $project
Set-Content -LiteralPath "$scratch/probe/Directory.Build.props" -Value '<Project />'
Copy-Item -LiteralPath "$PSScriptRoot/probe/Program.cs" -Destination "$scratch/probe/Program.cs"
if (-not $NoRestore) {
    dotnet restore "$scratch/probe/probe.csproj" --packages "$repo/.tools/nuget-packages" --ignore-failed-sources
    if ($LASTEXITCODE) { throw 'Restore failed' }
}
dotnet run --no-restore --project "$scratch/probe/probe.csproj" -- "$scratch/inputs" "$scratch/outputs"
if ($LASTEXITCODE) { throw 'Probe failed' }
& $python -I "$PSScriptRoot/classify.py" $repo "$scratch/inputs" "$scratch/outputs"
if ($LASTEXITCODE) { throw 'Classification failed' }
& $python -I "$PSScriptRoot/validate_probes.py" $repo "$scratch/inputs" "$scratch/outputs"
if ($LASTEXITCODE) { throw 'Inspection failed' }
