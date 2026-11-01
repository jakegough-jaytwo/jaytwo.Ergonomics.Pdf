# Regenerate, check and render the W-218 R1 exact-width fixture. Run from this directory.
# Usage: pwsh ./run_all.ps1 -Python <python> [-Pdftoppm <path to pdftoppm>]
param([string]$Python = "python", [string]$Pdftoppm = "")
& $Python exact_width_fixture.py; if ($LASTEXITCODE) { exit $LASTEXITCODE }
$check = & $Python exact_width_check.py; $code = $LASTEXITCODE
$check | Select-Object -First 1
if ($code) { $check | Select-String FAIL; exit $code }
if ($Pdftoppm) { & $Python exact_width_render.py --pdftoppm $Pdftoppm } else { & $Python exact_width_render.py }
& $Python exact_width_summary.py
