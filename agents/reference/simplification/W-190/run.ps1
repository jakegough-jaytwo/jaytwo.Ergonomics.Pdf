param(
    [string]$Repo = (Join-Path $PSScriptRoot '../../../..'),
    [switch]$NoRestore
)
$ErrorActionPreference = 'Stop'
$Repo = (Resolve-Path -LiteralPath $Repo).Path
$scratch = Join-Path $Repo 'out/W-190'
New-Item -ItemType Directory -Force -Path "$scratch/inputs", "$scratch/outputs", "$scratch/probe" | Out-Null
$python = $env:JAYTWO_PYTHON
if (-not $python) {
    $settingsPath = Join-Path $Repo '.tools/test.runsettings'
    if (Test-Path -LiteralPath $settingsPath) {
        [xml]$settings = Get-Content -LiteralPath $settingsPath
        $python = $settings.RunSettings.RunConfiguration.EnvironmentVariables.JAYTWO_PYTHON
    }
}
if (-not $python) { $python = 'python' }
& $python "$PSScriptRoot/generate.py" "$scratch/inputs"
if ($LASTEXITCODE) { throw 'Fixture generation failed' }
$project = (Get-Content -LiteralPath "$PSScriptRoot/probe/probe.csproj.txt" -Raw).Replace('REPO', $Repo.Replace('\','/'))
Set-Content -LiteralPath "$scratch/probe/probe.csproj" -Value $project
# The disposable console has no repository analyzers; the engine retains its normal build properties.
Set-Content -LiteralPath "$scratch/probe/Directory.Build.props" -Value '<Project />'
Copy-Item -LiteralPath "$PSScriptRoot/probe/Program.cs" -Destination "$scratch/probe/Program.cs"
if (-not $NoRestore) {
    dotnet restore "$scratch/probe/probe.csproj" --packages "$Repo/.tools/nuget-packages"
    if ($LASTEXITCODE) { throw 'Restore failed' }
}
dotnet run --no-restore --project "$scratch/probe/probe.csproj" -- "$scratch/inputs" "$scratch/outputs"
if ($LASTEXITCODE) { throw 'Public-API probe failed' }
& $python "$PSScriptRoot/validate_probes.py" $Repo "$scratch/inputs" "$scratch/outputs"
if ($LASTEXITCODE) { throw 'Independent validation failed' }
Write-Output "Research evidence: $scratch/outputs/probe.json and inspection.json; inputs/manifest.json"
