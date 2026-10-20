#!/usr/bin/env python3
"""Focused tests for the agent workflow validator."""

import subprocess
import tempfile
import unittest
from pathlib import Path

import workflow


def _record(item_id, status, allowed):
    return workflow.Record(
        workflow.WORK_DIR / f"{item_id}.md",
        {"id": item_id, "status": status, "allowed_changes": allowed},
        "",
    )


def _item_text(item_id, status, allowed):
    lines = [
        "---",
        f"id: {item_id}",
        "title: Test item",
        f"status: {status}",
        "kind: maintenance",
        "created: 2026-10-20",
        "completed: 2026-10-20" if status == "done" else "completed:",
        "source: test",
        "legacy_ids: []",
        "depends_on: []",
        "blocked_by: []",
        "decisions: []",
        "allowed_changes:",
        *[f"  - {entry}" for entry in allowed],
        "---",
        "",
        f"# {item_id}. Test item",
        "",
        *[f"## {section}\n\nText.\n" for section in sorted(workflow.EXECUTABLE_SECTIONS)],
    ]
    return "\n".join(lines)


class WorkflowTests(unittest.TestCase):
    def test_parses_scalar_and_block_lists(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "W-1.md"
            path.write_text(
                "---\nid: W-1\nstatus: ready\ndepends_on: []\nallowed_changes:\n  - agents/\n---\n\n# Body\n",
                encoding="utf-8",
            )
            record = workflow.parse_record(path)
            self.assertEqual("W-1", record.meta["id"])
            self.assertEqual([], record.meta["depends_on"])
            self.assertEqual(["agents/"], record.meta["allowed_changes"])

    def test_exact_and_prefix_path_rules(self):
        allowed = ["README.md", "agents/work-items/"]
        self.assertTrue(workflow.path_allowed("README.md", allowed))
        self.assertTrue(workflow.path_allowed("agents/work-items/W-1.md", allowed))
        self.assertFalse(workflow.path_allowed("README.md.bak", allowed))
        self.assertFalse(workflow.path_allowed("agents/state/current.md", allowed))

    def test_head_active_item_owns_allowed_work(self):
        head = [_record("W-1", "active", ["agents/evidence/"])]
        owner, errors = workflow.resolve_owner(head, head, ["agents/evidence/x.md"])
        self.assertEqual("W-1", owner.meta["id"])
        self.assertEqual([], errors)

    def test_unrelated_pre_staged_file_fails(self):
        head = [_record("W-1", "active", ["src/"])]
        owner, errors = workflow.resolve_owner(head, head, ["src/a.cs", "docs/unrelated.md"])
        self.assertEqual("W-1", owner.meta["id"])
        self.assertEqual(1, len(errors))
        self.assertIn("docs/unrelated.md", errors[0])

    def test_two_activations_are_ambiguous(self):
        head = [_record("W-1", "ready", ["src/"]), _record("W-2", "proposed", ["src/"])]
        staged = [_record("W-1", "active", ["src/"]), _record("W-2", "active", ["src/"])]
        owner, errors = workflow.resolve_owner(head, staged, ["agents/work-items/W-1.md", "agents/work-items/W-2.md", "src/a.cs"])
        self.assertIsNone(owner)
        self.assertEqual(1, len(errors))
        self.assertIn("ambiguous", errors[0])

    def test_activation_and_completion_together_are_ambiguous(self):
        head = [_record("W-1", "active", ["src/"]), _record("W-2", "ready", ["src/"])]
        staged = [_record("W-1", "done", ["src/"]), _record("W-2", "active", ["src/"])]
        owner, errors = workflow.resolve_owner(head, staged, ["src/a.cs"])
        self.assertIsNone(owner)
        self.assertIn("ambiguous", errors[0])

    def test_activation_with_disallowed_path_fails(self):
        head = [_record("W-1", "ready", ["src/"])]
        staged = [_record("W-1", "active", ["src/"])]
        owner, errors = workflow.resolve_owner(head, staged, ["agents/work-items/W-1.md", "README.md"])
        self.assertEqual("W-1", owner.meta["id"])
        self.assertEqual(1, len(errors))
        self.assertIn("README.md", errors[0])

    def test_activation_with_allowed_work_passes(self):
        head = [_record("W-1", "ready", ["agents/work-items/W-1.md"])]
        staged = [_record("W-1", "active", ["src/"])]
        owner, errors = workflow.resolve_owner(head, staged, ["agents/work-items/W-1.md", "src/a.cs"])
        self.assertEqual("W-1", owner.meta["id"])
        self.assertEqual([], errors)

    def test_completion_with_work_leaving_no_active_item_passes(self):
        head = [_record("W-1", "active", ["src/"])]
        staged = [_record("W-1", "done", ["src/"])]
        owner, errors = workflow.resolve_owner(head, staged, ["agents/work-items/W-1.md", "src/a.cs"])
        self.assertEqual("W-1", owner.meta["id"])
        self.assertEqual([], errors)

    def test_ready_item_completed_with_work_passes(self):
        head = [_record("W-1", "ready", ["src/"])]
        staged = [_record("W-1", "done", ["src/"])]
        owner, errors = workflow.resolve_owner(head, staged, ["agents/work-items/W-1.md", "src/a.cs"])
        self.assertEqual("W-1", owner.meta["id"])
        self.assertEqual([], errors)

    def test_record_only_change_passes_without_active_item(self):
        head = [_record("W-1", "done", ["x/"]), _record("W-2", "proposed", [])]
        paths = ["agents/work-items/W-1.md", "agents/work-items/W-2.md", "agents/state/current.md"]
        owner, errors = workflow.resolve_owner(head, head, paths)
        self.assertIsNone(owner)
        self.assertEqual([], errors)

    def test_work_without_owner_fails(self):
        head = [_record("W-1", "done", ["x/"])]
        owner, errors = workflow.resolve_owner(head, head, ["agents/work-items/W-1.md", "x/a.txt"])
        self.assertIsNone(owner)
        self.assertEqual(1, len(errors))

    def test_work_for_item_leaving_active_without_completion_fails(self):
        head = [_record("W-1", "active", ["src/"])]
        staged = [_record("W-1", "waiting", ["src/"])]
        owner, errors = workflow.resolve_owner(head, staged, ["agents/work-items/W-1.md", "src/a.cs"])
        self.assertIsNone(owner)
        self.assertEqual(1, len(errors))


class StagedIndexTests(unittest.TestCase):
    def setUp(self):
        self._directory = tempfile.TemporaryDirectory()
        self.root = Path(self._directory.name)
        self._git("init", "-q")
        self._write("agents/work-items/W-1.md", _item_text("W-1", "ready", ["agents/work-items/W-1.md"]))
        self._write("agents/state/current.md", "# Next\n\n1. W-1\n")
        self._git("add", ".")
        self._git("commit", "-q", "-m", "base")

    def tearDown(self):
        self._directory.cleanup()

    def _git(self, *args):
        config = ["-c", "user.name=Test", "-c", "user.email=test@example.invalid", "-c", "commit.gpgsign=false"]
        subprocess.run(["git", *config, *args], cwd=self.root, check=True, capture_output=True)

    def _write(self, name, text):
        path = self.root / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(text, encoding="utf-8")

    def test_activation_and_work_in_one_transaction_passes(self):
        self._write("agents/work-items/W-1.md", _item_text("W-1", "active", ["src/"]))
        self._write("src/a.cs", "class A {}\n")
        self._git("add", ".")
        self.assertEqual([], workflow.task_check(self.root))

    def test_unrelated_pre_staged_file_fails(self):
        self._write("agents/work-items/W-1.md", _item_text("W-1", "active", ["src/"]))
        self._write("src/a.cs", "class A {}\n")
        self._write("docs/other.md", "unrelated\n")
        self._git("add", ".")
        errors = workflow.task_check(self.root)
        self.assertEqual(1, len(errors))
        self.assertIn("docs/other.md", errors[0])

    def test_reads_the_index_not_the_working_tree(self):
        self._write("agents/work-items/W-1.md", _item_text("W-1", "active", ["src/"]))
        self._write("src/a.cs", "class A {}\n")
        self._git("add", ".")
        self._write("agents/work-items/W-1.md", _item_text("W-1", "active", ["docs/"]))
        self.assertEqual([], workflow.task_check(self.root))

    def test_staged_state_with_two_active_items_fails(self):
        self._write("agents/work-items/W-1.md", _item_text("W-1", "active", ["src/"]))
        self._git("add", ".")
        self._git("commit", "-q", "-m", "activate")
        self._write("agents/work-items/W-2.md", _item_text("W-2", "active", ["src/"]))
        self._write("src/a.cs", "class A {}\n")
        self._git("add", ".")
        errors = workflow.task_check(self.root)
        self.assertTrue(any("more than one active work item" in error for error in errors))

    def test_no_staged_paths_fails(self):
        self.assertEqual(["task-check: no staged paths"], workflow.task_check(self.root))


if __name__ == "__main__":
    unittest.main()
