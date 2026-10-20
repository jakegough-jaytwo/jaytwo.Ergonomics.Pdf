#!/usr/bin/env python3
"""Validate and display the repository-native agent workflow."""

from __future__ import annotations

import argparse
import re
import subprocess
import sys
from dataclasses import dataclass
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
WORK_DIR = ROOT / "agents" / "work-items"
DECISION_DIR = ROOT / "agents" / "decisions"
CURRENT_FILE = ROOT / "agents" / "state" / "current.md"
CHECKPOINT_DIR = ROOT / "agents" / "evidence" / "checkpoints"

WORK_ID = re.compile(r"W-\d+$")
DECISION_ID = re.compile(r"D-(?:O)?\d+$")
WORK_STATUSES = {"proposed", "ready", "active", "waiting", "done", "cancelled"}
DECISION_STATUSES = {"proposed", "accepted", "rejected", "superseded"}
KINDS = {"defect", "enhancement", "evidence", "research", "maintenance"}
BASE_FIELDS = {
    "id",
    "title",
    "status",
    "kind",
    "created",
    "completed",
    "source",
    "legacy_ids",
    "depends_on",
    "blocked_by",
    "decisions",
    "allowed_changes",
}
EXECUTABLE_SECTIONS = {
    "Problem",
    "Why now",
    "Scope and exclusions",
    "Acceptance criteria",
    "Evidence required",
    "Notes",
    "Closeout",
}


@dataclass(frozen=True)
class Record:
    path: Path
    meta: dict[str, str | list[str]]
    body: str


def _scalar(value: str) -> str:
    value = value.strip()
    if len(value) >= 2 and value[0] == value[-1] and value[0] in {'"', "'"}:
        return value[1:-1]
    return value


def parse_record(path: Path) -> Record:
    return parse_text(path, path.read_text(encoding="utf-8-sig"))


def parse_text(path: Path, text: str) -> Record:
    lines = text.splitlines()
    if not lines or lines[0] != "---":
        raise ValueError("missing opening front-matter delimiter")
    try:
        end = lines.index("---", 1)
    except ValueError as error:
        raise ValueError("missing closing front-matter delimiter") from error

    meta: dict[str, str | list[str]] = {}
    current_list: str | None = None
    for line in lines[1:end]:
        if line.startswith("  - "):
            if current_list is None:
                raise ValueError(f"list value without a key: {line}")
            value = meta[current_list]
            assert isinstance(value, list)
            value.append(_scalar(line[4:]))
            continue
        match = re.fullmatch(r"([a-z_]+):(.*)", line)
        if not match:
            raise ValueError(f"invalid front-matter line: {line}")
        key, raw = match.groups()
        raw = raw.strip()
        if raw == "[]":
            meta[key] = []
            current_list = None
        elif raw:
            meta[key] = _scalar(raw)
            current_list = None
        else:
            meta[key] = [] if key in {"legacy_ids", "depends_on", "blocked_by", "decisions", "allowed_changes"} else ""
            current_list = key if isinstance(meta[key], list) else None
    return Record(path, meta, "\n".join(lines[end + 1 :]).strip())


def load_records(directory: Path) -> tuple[list[Record], list[str]]:
    records: list[Record] = []
    errors: list[str] = []
    for path in sorted(directory.glob("*.md")):
        try:
            records.append(parse_record(path))
        except (OSError, ValueError) as error:
            errors.append(f"{path.relative_to(ROOT)}: {error}")
    return records, errors


def _list(meta: dict[str, str | list[str]], key: str) -> list[str]:
    value = meta.get(key, [])
    return value if isinstance(value, list) else []


def current_ids() -> tuple[list[str], list[str]]:
    errors: list[str] = []
    if not CURRENT_FILE.exists():
        return [], ["agents/state/current.md: missing"]
    ids: list[str] = []
    for line_number, line in enumerate(CURRENT_FILE.read_text(encoding="utf-8-sig").splitlines(), 1):
        match = re.fullmatch(r"\d+\. (W-\d+)", line)
        if match:
            ids.append(match.group(1))
        elif re.match(r"\d+\. ", line):
            errors.append(f"agents/state/current.md:{line_number}: invalid queue entry")
    if len(ids) != len(set(ids)):
        errors.append("agents/state/current.md: duplicate queue ID")
    return ids, errors


def validate() -> tuple[list[Record], list[Record], list[str]]:
    works, errors = load_records(WORK_DIR)
    decisions, decision_errors = load_records(DECISION_DIR)
    errors.extend(decision_errors)

    work_by_id: dict[str, Record] = {}
    for record in works:
        rel = record.path.relative_to(ROOT)
        missing = BASE_FIELDS - record.meta.keys()
        if missing:
            errors.append(f"{rel}: missing fields {', '.join(sorted(missing))}")
        item_id = record.meta.get("id", "")
        if not isinstance(item_id, str) or not WORK_ID.fullmatch(item_id):
            errors.append(f"{rel}: invalid work-item id {item_id!r}")
            continue
        if record.path.stem != item_id:
            errors.append(f"{rel}: filename does not match id {item_id}")
        if item_id in work_by_id:
            errors.append(f"{rel}: duplicate id {item_id}")
        work_by_id[item_id] = record

        status = record.meta.get("status", "")
        if status not in WORK_STATUSES:
            errors.append(f"{rel}: invalid status {status!r}")
        if record.meta.get("kind", "") not in KINDS:
            errors.append(f"{rel}: invalid kind {record.meta.get('kind')!r}")
        if status in {"ready", "active", "waiting", "done", "cancelled"}:
            headings = set(re.findall(r"^## (.+)$", record.body, re.MULTILINE))
            missing_sections = EXECUTABLE_SECTIONS - headings
            if missing_sections:
                errors.append(f"{rel}: missing sections {', '.join(sorted(missing_sections))}")
            if not _list(record.meta, "allowed_changes"):
                errors.append(f"{rel}: executable item has no allowed_changes")
        if status == "waiting":
            if not _list(record.meta, "blocked_by"):
                errors.append(f"{rel}: waiting item has no blocker")
            if "Safe checkpoint:" not in record.body:
                errors.append(f"{rel}: waiting item has no Safe checkpoint note")
        completed = record.meta.get("completed", "")
        if status in {"done", "cancelled"} and not completed:
            errors.append(f"{rel}: {status} item has no completion date")
        if status not in {"done", "cancelled"} and completed:
            errors.append(f"{rel}: open item has a completion date")
        for legacy_id in _list(record.meta, "legacy_ids"):
            if not re.fullmatch(r"B-\d+", legacy_id):
                errors.append(f"{rel}: invalid legacy id {legacy_id}")
        for allowed in _list(record.meta, "allowed_changes"):
            if "*" in allowed or "?" in allowed or allowed.startswith("/"):
                errors.append(f"{rel}: allowed_changes must be relative exact paths or directory prefixes: {allowed}")

    decision_by_id: dict[str, Record] = {}
    for record in decisions:
        rel = record.path.relative_to(ROOT)
        item_id = record.meta.get("id", "")
        if not isinstance(item_id, str) or not DECISION_ID.fullmatch(item_id):
            errors.append(f"{rel}: invalid decision id {item_id!r}")
            continue
        if record.path.stem != item_id:
            errors.append(f"{rel}: filename does not match id {item_id}")
        if item_id in decision_by_id:
            errors.append(f"{rel}: duplicate id {item_id}")
        decision_by_id[item_id] = record
        if record.meta.get("status", "") not in DECISION_STATUSES:
            errors.append(f"{rel}: invalid status {record.meta.get('status')!r}")
        for required in ("title", "status"):
            if not record.meta.get(required):
                errors.append(f"{rel}: missing {required}")

    for record in works:
        rel = record.path.relative_to(ROOT)
        for dependency in _list(record.meta, "depends_on"):
            if dependency not in work_by_id:
                errors.append(f"{rel}: unknown dependency {dependency}")
        for decision in _list(record.meta, "decisions"):
            if decision not in decision_by_id:
                errors.append(f"{rel}: unknown decision {decision}")

    active = [record for record in works if record.meta.get("status") == "active"]
    if len(active) > 1:
        errors.append("more than one active work item: " + ", ".join(str(x.meta["id"]) for x in active))

    queued, queue_errors = current_ids()
    errors.extend(queue_errors)
    for item_id in queued:
        if item_id not in work_by_id:
            errors.append(f"agents/state/current.md: unknown work item {item_id}")
        elif work_by_id[item_id].meta.get("status") in {"done", "cancelled"}:
            errors.append(f"agents/state/current.md: closed work item {item_id} is queued")
    return works, decisions, errors


def path_allowed(path: str, allowed: list[str]) -> bool:
    normalized = path.replace("\\", "/")
    return any(normalized.startswith(entry) if entry.endswith("/") else normalized == entry for entry in allowed)


def staged_paths() -> list[str]:
    result = subprocess.run(
        ["git", "diff", "--cached", "--name-only", "--diff-filter=ACMRD"],
        cwd=ROOT,
        check=True,
        capture_output=True,
        text=True,
    )
    return [line.strip().replace("\\", "/") for line in result.stdout.splitlines() if line.strip()]


def head_works() -> list[Record]:
    listing = subprocess.run(
        ["git", "ls-tree", "--name-only", "HEAD", "agents/work-items/"],
        cwd=ROOT,
        check=True,
        capture_output=True,
        text=True,
    )
    records: list[Record] = []
    for name in listing.stdout.splitlines():
        if not name.endswith(".md"):
            continue
        shown = subprocess.run(["git", "show", f"HEAD:{name}"], cwd=ROOT, check=True, capture_output=True, text=True, encoding="utf-8")
        records.append(parse_text(ROOT / name, shown.stdout.lstrip("\ufeff")))
    return records


def resolve_owner(head_records: list[Record], paths: list[str]) -> tuple[Record | None, list[str]]:
    """Return (owner, errors); a None owner means a record-only commit or an error."""
    item_paths = [path for path in paths if re.fullmatch(r"agents/work-items/[^/]+\.md", path)]
    if len(item_paths) == 1 and all(path in {item_paths[0], "agents/state/current.md"} for path in paths):
        return None, []
    active = [record for record in head_records if record.meta.get("status") == "active"]
    if len(active) != 1:
        return None, ["task-check: staged transaction needs one item active in HEAD or one record-only work-item update"]
    owner = active[0]
    allowed = [*_list(owner.meta, "allowed_changes"), str(owner.path.relative_to(ROOT)).replace("\\", "/")]
    return owner, [f"task-check: {path} is outside {owner.meta['id']} allowed_changes" for path in paths if not path_allowed(path, allowed)]


def task_check() -> list[str]:
    paths = staged_paths()
    if not paths:
        return ["task-check: no staged paths"]
    return resolve_owner(head_works(), paths)[1]


def show_status(works: list[Record], decisions: list[Record]) -> None:
    active = [record for record in works if record.meta.get("status") == "active"]
    waiting = [record for record in works if record.meta.get("status") == "waiting"]
    proposed = [record for record in decisions if record.meta.get("status") == "proposed"]
    queued, _ = current_ids()
    checkpoints = sorted(CHECKPOINT_DIR.glob("*.md"))
    print("Active:", active[0].meta["id"] if active else "none")
    print("Waiting:", ", ".join(str(x.meta["id"]) for x in waiting) or "none")
    print("Next:", " -> ".join(queued) or "none")
    print("Proposed decisions:", ", ".join(str(x.meta["id"]) for x in proposed) or "none")
    print("Latest checkpoint:", str(checkpoints[-1].relative_to(ROOT)).replace("\\", "/") if checkpoints else "none")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("command", choices=("check", "status", "task-check"))
    args = parser.parse_args()
    works, decisions, errors = validate()
    if errors:
        for error in errors:
            print(f"ERROR: {error}", file=sys.stderr)
        return 1
    if args.command == "status":
        show_status(works, decisions)
    elif args.command == "task-check":
        errors = task_check()
        if errors:
            for error in errors:
                print(f"ERROR: {error}", file=sys.stderr)
            return 1
        print("task-check: OK")
    else:
        print(f"agents-check: OK ({len(works)} work items, {len(decisions)} decisions)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
