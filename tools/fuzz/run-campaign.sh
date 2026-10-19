#!/bin/sh
# Runs a bounded libFuzzer campaign over the harness targets. Linux only; the Dockerfile `fuzz` stage has the tools.
# Usage: run-campaign.sh OUT_DIR WORKERS TARGET:SECONDS [TARGET:SECONDS...]
# Aggregate CPU time is about WORKERS x SECONDS per target. D-23 caps a campaign at 24 CPU-hours.
set -eu

ROOT=$(cd "$(dirname "$0")/../.." && pwd)
OUT=$1
WORKERS=$2
shift 2
mkdir -p "$OUT/logs"

{
  echo "commit: ${COMMIT:-$(git -C "$ROOT" rev-parse HEAD 2>/dev/null || echo unknown)}"
  echo "dirty: ${DIRTY:-unknown}"
  echo "started: $(date -u +%Y-%m-%dT%H:%M:%SZ)"
  echo "workers: $WORKERS"
  echo "cpus: $(nproc)"
  echo "kernel: $(uname -srm)"
  echo "dotnet: $(dotnet --version)"
  echo "sharpfuzz: $(dotnet tool list --global | awk '/sharpfuzz/ {print $2}')"
  echo "libfuzzer-dotnet: ${LIBFUZZER_DOTNET_VERSION:-unknown}"
  echo "limits: -timeout=10 -rss_limit_mb=2048"
} > "$OUT/run.txt"

# Release build, no coverage collection; SharpFuzz instruments only the library.
dotnet publish "$ROOT/tools/fuzz/jaytwo.Ergonomics.Pdf.Fuzz.csproj" -c Release -o "$OUT/bin" -nologo -v q
sharpfuzz "$OUT/bin/jaytwo.Ergonomics.Pdf.dll"

rm -rf "$OUT/seeds"
dotnet "$OUT/bin/jaytwo.Ergonomics.Pdf.Fuzz.dll" seeds "$OUT/seeds"
sh "$ROOT/tools/fuzz/external-seeds.sh" "$OUT/seeds"

for spec in "$@"; do
  target=${spec%%:*}
  seconds=${spec#*:}
  maxlen=$(dotnet "$OUT/bin/jaytwo.Ergonomics.Pdf.Fuzz.dll" list | awk -v t="$target" '$1 == t {print $2}')
  [ -n "$maxlen" ] || { echo "unknown target $target" >&2; exit 64; }

  mkdir -p "$OUT/corpus/$target" "$OUT/artifacts/$target"
  cp "$OUT/seeds/$target/"* "$OUT/corpus/$target/"
  if [ -d "$ROOT/tools/fuzz/regressions/$target" ]; then
    cp "$ROOT/tools/fuzz/regressions/$target/"* "$OUT/corpus/$target/"
  fi
  seed_hash=$(cd "$OUT/seeds/$target" && sha256sum * | sort | sha256sum | cut -c1-64)
  seed_count=$(ls "$OUT/seeds/$target" | wc -l)

  runner="$OUT/run-$target.sh"
  printf '#!/bin/sh\nexec dotnet "%s" libfuzzer %s\n' "$OUT/bin/jaytwo.Ergonomics.Pdf.Fuzz.dll" "$target" > "$runner"
  chmod +x "$runner"

  start=$(date +%s)
  status=0
  libfuzzer-dotnet --target_path="$runner" \
    -fork="$WORKERS" -ignore_crashes=1 -ignore_timeouts=1 -ignore_ooms=1 \
    -max_total_time="$seconds" -timeout=10 -rss_limit_mb=2048 -max_len="$maxlen" \
    -dict="$ROOT/tools/fuzz/pdf.dict" -artifact_prefix="$OUT/artifacts/$target/" -print_final_stats=1 \
    "$OUT/corpus/$target" > "$OUT/logs/$target.log" 2>&1 || status=$?
  end=$(date +%s)

  artifacts=$(ls "$OUT/artifacts/$target" | wc -l)
  corpus=$(ls "$OUT/corpus/$target" | wc -l)
  echo "$target seconds=$((end - start)) workers=$WORKERS exit=$status seeds=$seed_count seed_sha256=$seed_hash corpus=$corpus artifacts=$artifacts" | tee -a "$OUT/summary.txt"
done

echo "finished: $(date -u +%Y-%m-%dT%H:%M:%SZ)" >> "$OUT/run.txt"
