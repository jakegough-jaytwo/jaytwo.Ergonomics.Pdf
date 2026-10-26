#!/bin/sh
# Real runner regression, in the existing fuzz image; output must be a fresh directory.
# The two one-second allocations check startup, instrumentation and finalization, not qualification.
set -eu
ROOT=$(cd "$(dirname "$0")/../.." && pwd)
OUT=${1:?usage: test-campaign-startup.sh OUT_DIR}
[ ! -e "$OUT/run.txt" ] || { echo "output directory already contains a run" >&2; exit 64; }
sh "$ROOT/tools/fuzz/run-campaign.sh" "$OUT" 1 cms:1 tsr:1
for target in cms tsr; do
  grep -Eq "^$target seconds=[0-9]+ workers=1 exit=0 seeds=[1-9][0-9]* .*artifacts=0$" "$OUT/summary.txt"
  grep -Eq 'cov: [1-9][0-9]*' "$OUT/logs/$target.log"
done
grep -q '^finished: ' "$OUT/run.txt"
echo "campaign startup regression: PASS (cms and tsr, instrumented, zero artifacts)"
