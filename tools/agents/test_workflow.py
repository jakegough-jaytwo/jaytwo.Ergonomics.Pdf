#!/usr/bin/env python3
"""Focused tests for the agent workflow validator."""

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

    def test_closing_head_active_item_with_allowed_extra_path_passes(self):
        head = [_record("W-1", "active", ["agents/evidence/"])]
        paths = ["agents/work-items/W-1.md", "agents/evidence/x.md"]
        owner, errors = workflow.resolve_owner(head, paths)
        self.assertEqual("W-1", owner.meta["id"])
        self.assertEqual([], errors)

    def test_closing_head_active_item_with_disallowed_path_fails(self):
        head = [_record("W-1", "active", ["agents/evidence/"])]
        owner, errors = workflow.resolve_owner(head, ["agents/work-items/W-1.md", "src/a.cs"])
        self.assertEqual(1, len(errors))
        self.assertIn("src/a.cs", errors[0])

    def test_record_only_change_passes_without_active_item(self):
        head = [_record("W-1", "done", ["x/"])]
        owner, errors = workflow.resolve_owner(head, ["agents/work-items/W-1.md", "agents/state/current.md"])
        self.assertIsNone(owner)
        self.assertEqual([], errors)

    def test_non_record_only_change_without_head_active_item_fails(self):
        head = [_record("W-1", "done", ["x/"])]
        owner, errors = workflow.resolve_owner(head, ["agents/work-items/W-1.md", "x/a.txt"])
        self.assertEqual(1, len(errors))


if __name__ == "__main__":
    unittest.main()
