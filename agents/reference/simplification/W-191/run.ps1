param(
    [string]$Repo = (Join-Path $PSScriptRoot '../../../..'),
    [switch]$NoRestore
)
$ErrorActionPreference = 'Stop'
$Repo = (Resolve-Path -LiteralPath $Repo).Path
$scratch = Join-Path $Repo 'out/W-191'
New-Item -ItemType Directory -Force -Path "$scratch/inputs", "$scratch/outputs", "$scratch/probe" | Out-Null
$python = $env:JAYTWO_PYTHON
if (-not $python -and (Test-Path -LiteralPath "$Repo/.tools/test.runsettings")) {
    [xml]$settings = Get-Content -LiteralPath "$Repo/.tools/test.runsettings"
    $python = $settings.RunSettings.RunConfiguration.EnvironmentVariables.JAYTWO_PYTHON
}
if (-not $python) { $python = 'python' }
& $python -I "$PSScriptRoot/generate.py" "$scratch/inputs"
if ($LASTEXITCODE) { throw 'Fixture generation failed' }
$project = (Get-Content -LiteralPath "$PSScriptRoot/probe/probe.csproj.txt" -Raw).Replace('REPO', $Repo.Replace('\','/'))
Set-Content -LiteralPath "$scratch/probe/probe.csproj" -Value $project
Set-Content -LiteralPath "$scratch/probe/Directory.Build.props" -Value '<Project />'
Copy-Item -LiteralPath "$PSScriptRoot/probe/Program.cs" -Destination "$scratch/probe/Program.cs"
if (-not $NoRestore) {
    dotnet restore "$scratch/probe/probe.csproj" --packages "$Repo/.tools/nuget-packages"
    if ($LASTEXITCODE) { throw 'Restore failed' }
}
dotnet run --no-restore --project "$scratch/probe/probe.csproj" -- "$scratch/inputs" "$scratch/outputs"
if ($LASTEXITCODE) { throw 'Public-API probe failed' }
& $python -I "$PSScriptRoot/validate_probes.py" $Repo "$scratch/inputs" "$scratch/outputs"
if ($LASTEXITCODE) { throw 'Independent inspection failed' }
