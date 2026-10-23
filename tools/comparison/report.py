#!/usr/bin/env python3
"""Tabulate the COMPARE lines that `make compare-itext` writes to its per-test logs (W-111)."""

from __future__ import annotations

import sys
from pathlib import Path


FIELDS = ("workload", "library", "passes", "cold", "warm", "heap", "allocated", "working_set", "baseline_set", "source_bytes")
OURS = "jaytwo.Ergonomics.Pdf"
ITEXT = "iText 9.8.0"
# iText cannot flatten in append mode, so its flatten row is a full rewrite.
ITEXT_FALLBACK = "iText 9.8.0, full rewrite"


def read_results(paths: list[str]) -> tuple[list[dict[str, str]], list[str]]:
    results: list[dict[str, str]] = []
    missing: list[str] = []
    for path in paths:
        log = Path(path)
        found = None
        if log.exists():
            for line in log.read_text(encoding="utf-8", errors="replace").splitlines():
                text = line.strip()
                if text.startswith("COMPARE\t"):
                    found = dict(zip(FIELDS, text.split("\t")[1:]))
        if found is None:
            missing.append(log.name)
        else:
            results.append(found)
    return results, missing


def ratio(numerator: str, denominator: str) -> str:
    try:
        top, bottom = float(numerator), float(denominator)
    except ValueError:
        return "-"
    return "-" if bottom == 0 else f"{top / bottom:.1f}x"


def summary(results: list[dict[str, str]]) -> list[str]:
    workloads: dict[str, dict[str, dict[str, str]]] = {}
    for result in results:
        workloads.setdefault(result["workload"], {})[result["library"]] = result
    lines = [
        "| Workload | Warm time, s (this / iText) | iText / this | Peak heap growth, MiB (this / iText) | Peak working set, MiB (this / iText) |",
        "|---|---|---|---|---|",
    ]
    for workload, libraries in workloads.items():
        ours, itext = libraries.get(OURS), libraries.get(ITEXT) or libraries.get(ITEXT_FALLBACK)
        if ours is None or itext is None:
            continue
        label = workload if itext["library"] == ITEXT else f"{workload} (iText: {itext['library'][len(ITEXT) + 2:]})"
        lines.append(
            f"| {label} | {ours['warm']} / {itext['warm']} | {ratio(itext['warm'], ours['warm'])}"
            f" | {ours['heap']} / {itext['heap']} | {ours['working_set']} / {itext['working_set']} |"
        )
    return lines


def detail(results: list[dict[str, str]]) -> list[str]:
    lines = [
        "| Workload | Library | Passes | Cold, s | Warm, s | Peak heap growth, MiB | Allocated, last pass, MiB | Peak working set, MiB | Source bytes read |",
        "|---|---|---|---|---|---|---|---|---|",
    ]
    for result in results:
        source = result["source_bytes"] if result["source_bytes"] == "-" else f"{int(result['source_bytes']):,}"
        lines.append(
            f"| {result['workload']} | {result['library']} | {result['passes']} | {result['cold']} | {result['warm']}"
            f" | {result['heap']} | {result['allocated']} | {result['working_set']} | {source} |"
        )
    return lines


def main(argv: list[str]) -> int:
    results, missing = read_results(argv)
    print()
    print("\n".join(summary(results)))
    print()
    print("\n".join(detail(results)))
    for name in missing:
        print(f"no result in {name}")
    return 1 if missing else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
