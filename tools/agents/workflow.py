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
WORK_PREFIX = "agents/work-items/"
DECISION_PREFIX = "agents/decisions/"
CURRENT_PATH = "agents/state/current.md"

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


def read_current() -> str | None:
    return CURRENT_FILE.read_text(encoding="utf-8-sig") if CURRENT_FILE.exists() else None


def current_ids(text: str | None) -> tuple[list[str], list[str]]:
    errors: list[str] = []
    if text is None:
        return [], ["agents/state/current.md: missing"]
    ids: list[str] = []
    for line_number, line in enumerate(text.splitlines(), 1):
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
    errors.extend(validate_state(works, decisions, read_current()))
    return works, decisions, errors


def validate_state(works: list[Record], decisions: list[Record], current: str | None) -> list[str]:
    errors: list[str] = []
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

    queued, queue_errors = current_ids(current)
    errors.extend(queue_errors)
    for item_id in queued:
        if item_id not in work_by_id:
            errors.append(f"agents/state/current.md: unknown work item {item_id}")
        elif work_by_id[item_id].meta.get("status") in {"done", "cancelled"}:
            errors.append(f"agents/state/current.md: closed work item {item_id} is queued")
    return errors


def path_allowed(path: str, allowed: list[str]) -> bool:
    normalized = path.replace("\\", "/")
    return any(normalized.startswith(entry) if entry.endswith("/") else normalized == entry for entry in allowed)


def _git(root: Path, *args: str, data: bytes | None = None) -> bytes:
    return subprocess.run(["git", *args], cwd=root, check=True, capture_output=True, input=data).stdout


def staged_paths(root: Path = ROOT) -> list[str]:
    output = _git(root, "diff", "--cached", "--name-only", "--no-renames", "--diff-filter=ACMRD", "-z")
    return [path for path in output.decode("utf-8").split("\0") if path]


def git_texts(index: bool, root: Path = ROOT) -> dict[str, str]:
    """Return the workflow record files as they are in the staged index or in HEAD."""
    pathspec = ["--", WORK_PREFIX, DECISION_PREFIX, CURRENT_PATH]
    listing = _git(root, "ls-files", "-s", "-z", *pathspec) if index else _git(root, "ls-tree", "-r", "-z", "HEAD", *pathspec)
    entries: list[tuple[str, str]] = []
    for entry in listing.decode("utf-8").split("\0"):
        if entry:
            info, path = entry.split("\t", 1)
            entries.append((path, info.split()[1 if index else 2]))
    if not entries:
        return {}
    output = _git(root, "cat-file", "--batch", data="".join(f"{sha}\n" for _, sha in entries).encode())
    texts: dict[str, str] = {}
    offset = 0
    for path, _ in entries:
        header_end = output.index(b"\n", offset)
        size = int(output[offset:header_end].split()[2])
        texts[path] = output[header_end + 1 : header_end + 1 + size].decode("utf-8").lstrip("\ufeff")
        offset = header_end + 2 + size
    return texts


def split_records(texts: dict[str, str]) -> tuple[list[Record], list[Record], str | None, list[str]]:
    """Parse git-sourced record texts; record paths are labelled relative to ROOT."""
    works: list[Record] = []
    decisions: list[Record] = []
    errors: list[str] = []
    for path, text in sorted(texts.items()):
        prefix = next((x for x in (WORK_PREFIX, DECISION_PREFIX) if path.startswith(x)), None)
        if prefix is None or "/" in path[len(prefix) :] or not path.endswith(".md"):
            continue
        try:
            (works if prefix == WORK_PREFIX else decisions).append(parse_text(ROOT / path, text))
        except ValueError as error:
            errors.append(f"{path}: {error}")
    return works, decisions, texts.get(CURRENT_PATH), errors


def is_record_path(path: str) -> bool:
    return path == CURRENT_PATH or re.fullmatch(r"agents/work-items/[^/]+\.md", path) is not None


def _owns_transaction(before: str | list[str] | None, after: str | list[str] | None) -> bool:
    return (after == "active" and before != "active") or (after == "done" and before not in {"done", "cancelled"})


def resolve_owner(head_records: list[Record], staged_records: list[Record], paths: list[str]) -> tuple[Record | None, list[str]]:
    """Return (owner, errors); a None owner without errors is a record-only transaction."""
    work_paths = [path for path in paths if not is_record_path(path)]
    if not work_paths:
        return None, []
    before = {record.meta.get("id"): record.meta.get("status") for record in head_records}
    transitions = [record for record in staged_records if _owns_transaction(before.get(record.meta.get("id")), record.meta.get("status"))]
    if len(transitions) > 1:
        names = ", ".join(str(record.meta["id"]) for record in transitions)
        return None, [f"task-check: ambiguous owner; the transaction activates or completes {names}"]
    if transitions:
        owner = transitions[0]
    else:
        head_active = {record.meta.get("id") for record in head_records if record.meta.get("status") == "active"}
        active = [record for record in staged_records if record.meta.get("id") in head_active and record.meta.get("status") == "active"]
        if len(active) != 1:
            return None, ["task-check: staged work needs an item this transaction activates or completes, or one item still active from HEAD"]
        owner = active[0]
    allowed = _list(owner.meta, "allowed_changes")
    return owner, [f"task-check: {path} is outside {owner.meta['id']} allowed_changes" for path in work_paths if not path_allowed(path, allowed)]


def task_check(root: Path = ROOT) -> list[str]:
    paths = staged_paths(root)
    if not paths:
        return ["task-check: no staged paths"]
    head_records = split_records(git_texts(False, root))[0]
    works, decisions, current, errors = split_records(git_texts(True, root))
    errors = [f"task-check: staged {error}" for error in errors + validate_state(works, decisions, current)]
    return errors + resolve_owner(head_records, works, paths)[1]


def show_status(works: list[Record], decisions: list[Record]) -> None:
    active = [record for record in works if record.meta.get("status") == "active"]
    waiting = [record for record in works if record.meta.get("status") == "waiting"]
    proposed = [record for record in decisions if record.meta.get("status") == "proposed"]
    queued, _ = current_ids(read_current())
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
    if args.command == "task-check":
        errors = task_check()
        for error in errors:
            print(f"ERROR: {error}", file=sys.stderr)
        if errors:
            return 1
        print("task-check: OK")
        return 0
    works, decisions, errors = validate()
    if errors:
        for error in errors:
            print(f"ERROR: {error}", file=sys.stderr)
        return 1
    if args.command == "status":
        show_status(works, decisions)
    else:
        print(f"agents-check: OK ({len(works)} work items, {len(decisions)} decisions)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
