#!/usr/bin/env python3
"""Print the slowest tests in TRX result files (W-122, D-52).

The fast lane leaves out tests by what they touch, not by how long they take. LargeData and Sweep tests have no single
helper that can refuse them, so the fast lane lists its slowest tests for a reviewer to classify any that build large
data or sweep. A duration here is a pointer to read the test, not a classification.
"""

from __future__ import annotations

import argparse
import sys
import xml.etree.ElementTree as ET
from pathlib import Path


NS = "{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}"


def seconds(duration: str) -> float:
    hours, minutes, rest = duration.split(":")
    return int(hours) * 3600 + int(minutes) * 60 + float(rest)


def results(path: Path) -> list[tuple[float, str]]:
    root = ET.parse(path).getroot()
    return [
        (seconds(result.get("duration", "00:00:00")), result.get("testName", "?"))
        for result in root.iter(NS + "UnitTestResult")
    ]


def slowest(paths: list[Path], top: int) -> list[str]:
    timed: list[tuple[float, str, str]] = []
    for path in paths:
        timed.extend((duration, name, path.stem) for duration, name in results(path))
    timed.sort(key=lambda entry: entry[0], reverse=True)
    return [f"  {duration:6.1f}s {name} ({source})" for duration, name, source in timed[:top]]


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("directory", type=Path, help="directory searched for *.trx")
    parser.add_argument("--top", type=int, default=10)
    args = parser.parse_args()
    paths = sorted(args.directory.glob("**/*.trx"))
    if not paths:
        print(f"slowest tests: no TRX files under {args.directory}")
        return 0
    print("slowest tests (classify any that build large data or sweep, D-52):")
    for line in slowest(paths, args.top):
        print(line)
    return 0


if __name__ == "__main__":
    sys.exit(main())
