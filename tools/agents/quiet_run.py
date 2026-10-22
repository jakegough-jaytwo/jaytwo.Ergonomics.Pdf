#!/usr/bin/env python3
"""Run a verification command with its output in a log file and print only a short summary."""

from __future__ import annotations

import argparse
import re
import subprocess
import sys
import time
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
LOG_DIR = ROOT / "out" / "logs"
MAX_ITEMS = 20
TAIL_LINES = 20

TEST_SUMMARY = re.compile(
    r"^\s*(?:Passed|Failed)!\s+-\s+Failed:\s*(?P<failed>\d+),\s*Passed:\s*(?P<passed>\d+),"
    r"\s*Skipped:\s*(?P<skipped>\d+),\s*Total:\s*(?P<total>\d+).*?-\s*(?P<assembly>\S+)\s+\((?P<framework>[^)]+)\)"
)
FAILED_TEST = re.compile(r"^\s+Failed (?P<name>\S.*?) \[[^\]]*\]\s*$")
BUILD_ERROR = re.compile(r": error [A-Z]+\d+:")
PROJECT_SUFFIX = re.compile(r"\s*\[[^\]]*\.csproj[^\]]*\]\s*$")
FUZZ_RESULT = re.compile(r"^(?:fuzz smoke (?:passed|failed.*)|UNCONTROLLED .*)$")


def split_lines(text: str) -> list[str]:
    return [line for line in re.split(r"\r\n|\r|\n", text) if line.strip()]


def _capped(items: list[str]) -> list[str]:
    if len(items) <= MAX_ITEMS:
        return items
    return items[:MAX_ITEMS] + [f"... and {len(items) - MAX_ITEMS} more"]


def summarize(lines: list[str], exit_code: int, expect_tests: bool) -> tuple[list[str], bool]:
    """Return the summary lines and whether the log is missing an expected test summary."""
    out: list[str] = []
    tests: list[str] = []
    failures: list[str] = []
    errors: list[str] = []
    fuzz: list[str] = []
    for index, line in enumerate(lines):
        match = TEST_SUMMARY.match(line)
        if match:
            entry = (
                f"  {match['framework']}: {match['passed']}/{match['total']} passed, {match['failed']} failed, "
                f"{match['skipped']} skipped ({match['assembly']})"
            )
            if entry not in tests:
                tests.append(entry)
            continue
        match = FAILED_TEST.match(line)
        if match:
            failures.append(f"  {match['name']}: {_failure_message(lines, index + 1)}")
            continue
        if BUILD_ERROR.search(line):
            entry = "  " + PROJECT_SUFFIX.sub("", line.strip())
            if entry not in errors:
                errors.append(entry)
            continue
        if FUZZ_RESULT.match(line.strip()):
            fuzz.append("  " + line.strip())
    if tests:
        out.append("tests:")
        out.extend(tests)
    missing = expect_tests and not tests
    if missing:
        out.append("tests: no dotnet test summary found in the log")
    if failures:
        out.append("failed tests:")
        out.extend(_capped(failures))
    if errors:
        out.append("build errors:")
        out.extend(_capped(errors))
    if fuzz:
        out.append("fuzz:")
        out.extend(_capped(fuzz))
    if (exit_code != 0 and not failures and not errors) or missing:
        out.append(f"last {TAIL_LINES} lines:")
        out.extend("  " + line for line in lines[-TAIL_LINES:])
    return out, missing


def _failure_message(lines: list[str], start: int) -> str:
    message: list[str] = []
    in_message = False
    for line in lines[start : start + 12]:
        text = line.strip()
        if text == "Error Message:":
            in_message = True
            continue
        if text == "Stack Trace:" or FAILED_TEST.match(line) or TEST_SUMMARY.match(line):
            break
        if in_message:
            message.append(text)
        if len(message) == 3:
            break
    return " | ".join(message) or "(no message in the log)"


def _duration(seconds: float) -> str:
    minutes, rest = divmod(int(seconds), 60)
    return f"{minutes}m{rest:02d}s"


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--name", required=True, help="log name; the log is out/logs/<name>.log")
    parser.add_argument("--tests", action="store_true", help="fail when the log has no dotnet test summary")
    parser.add_argument("command", nargs=argparse.REMAINDER)
    args = parser.parse_args()
    command = args.command[1:] if args.command[:1] == ["--"] else args.command
    if not command:
        parser.error("a command is required after --")
    LOG_DIR.mkdir(parents=True, exist_ok=True)
    log_path = LOG_DIR / f"{args.name}.log"
    started = time.monotonic()
    with log_path.open("wb") as log:
        exit_code = subprocess.call(command, stdout=log, stderr=subprocess.STDOUT, cwd=ROOT)
    elapsed = time.monotonic() - started
    lines = split_lines(log_path.read_bytes().decode("utf-8", errors="replace"))
    summary, missing = summarize(lines, exit_code, args.tests)
    passed = exit_code == 0 and not missing
    relative = str(log_path.relative_to(ROOT)).replace("\\", "/")
    print(f"{args.name}: {'PASS' if passed else 'FAIL'} (exit {exit_code}, {_duration(elapsed)}); full log {relative}")
    for line in summary:
        print(line)
    sys.stdout.flush()
    return exit_code if exit_code != 0 else (1 if missing else 0)


if __name__ == "__main__":
    sys.exit(main())
