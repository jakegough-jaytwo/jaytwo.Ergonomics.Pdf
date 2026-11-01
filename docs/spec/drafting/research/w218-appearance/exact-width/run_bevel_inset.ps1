# Regenerate, check and render the W-218 beveled/inset border-footprint fixture. Run from this directory.
# Usage: pwsh ./run_bevel_inset.ps1 -Python <python> [-Pdftoppm <path to pdftoppm>]
param([string]$Python = "python", [string]$Pdftoppm = "")
& $Python bevel_inset_fixture.py; if ($LASTEXITCODE) { exit $LASTEXITCODE }
& $Python bevel_inset_check.py; if ($LASTEXITCODE) { exit $LASTEXITCODE }
if ($Pdftoppm) { & $Python bevel_inset_render.py --pdftoppm $Pdftoppm } else { & $Python bevel_inset_render.py }
& $Python bevel_inset_summary.py
