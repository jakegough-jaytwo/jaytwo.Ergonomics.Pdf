#!/usr/bin/env python3
"""Focused tests for the quiet verification wrapper."""

import unittest

import quiet_run


GREEN = [
    "  Determining projects to restore...",
    "Passed!  - Failed:     0, Passed:  3610, Skipped:     0, Total:  3610, Duration: 2 m 1 s - Pdf.Tests.dll (net10.0)",
    "Passed!  - Failed:     0, Passed:  3610, Skipped:     0, Total:  3610, Duration: 2 m 9 s - Pdf.Tests.dll (net8.0)",
    "fuzz smoke: seed 1, 200 mutations per target",
    "fuzz smoke passed",
]

RED = [
    "  Failed jaytwo.Ergonomics.Pdf.Tests.SignatureTests.Signs(digest: Sha384) [12 ms]",
    "  Error Message:",
    "   Assert.Equal() Failure: Values differ",
    "  Expected: 48",
    "  Actual:   32",
    "  Stack Trace:",
    "     at jaytwo.Ergonomics.Pdf.Tests.SignatureTests.Signs()",
    "Failed!  - Failed:     1, Passed:  3609, Skipped:     0, Total:  3610, Duration: 2 m 1 s - Pdf.Tests.dll (net10.0)",
]

BUILD = [
    "C:\\src\\Pdf.cs(10,5): error SA1201: A method should not follow a property [C:\\src\\Pdf.csproj::TargetFramework=net8.0]",
    "C:\\src\\Pdf.cs(10,5): error SA1201: A method should not follow a property [C:\\src\\Pdf.csproj::TargetFramework=net6.0]",
    "Build FAILED.",
]


class SummarizeTests(unittest.TestCase):
    def test_green_run_reports_each_framework_and_no_tail(self):
        summary, missing = quiet_run.summarize(GREEN, 0, True)
        self.assertFalse(missing)
        self.assertIn("  net10.0: 3610/3610 passed, 0 failed, 0 skipped (Pdf.Tests.dll)", summary)
        self.assertIn("  net8.0: 3610/3610 passed, 0 failed, 0 skipped (Pdf.Tests.dll)", summary)
        self.assertIn("  fuzz smoke passed", summary)
        self.assertFalse(any(line.startswith("last ") for line in summary))
        self.assertNotIn("  Determining projects to restore...", summary)

    def test_failed_test_reports_name_and_message_without_tail(self):
        summary, _ = quiet_run.summarize(RED, 1, True)
        self.assertIn(
            "  jaytwo.Ergonomics.Pdf.Tests.SignatureTests.Signs(digest: Sha384): "
            "Assert.Equal() Failure: Values differ | Expected: 48 | Actual:   32",
            summary,
        )
        self.assertIn("  net10.0: 3609/3610 passed, 1 failed, 0 skipped (Pdf.Tests.dll)", summary)
        self.assertNotIn(f"last {quiet_run.TAIL_LINES} lines:", summary)

    def test_unexplained_failure_shows_tail(self):
        summary, _ = quiet_run.summarize(["docker: daemon not running"], 1, False)
        self.assertEqual([f"last {quiet_run.TAIL_LINES} lines:", "  docker: daemon not running"], summary)

    def test_build_errors_are_deduplicated_across_frameworks(self):
        summary, _ = quiet_run.summarize(BUILD, 1, False)
        errors = [line for line in summary if "SA1201" in line and not line.startswith("    ")]
        self.assertEqual(["  C:\\src\\Pdf.cs(10,5): error SA1201: A method should not follow a property"], errors[:1])
        self.assertEqual(1, summary.count(errors[0]))

    def test_missing_test_summary_is_a_failure_even_on_exit_zero(self):
        summary, missing = quiet_run.summarize(["Build succeeded."], 0, True)
        self.assertTrue(missing)
        self.assertIn("tests: no dotnet test summary found in the log", summary)

    def test_long_lists_are_capped(self):
        lines = [f"  Failed T.Case{i} [1 ms]" for i in range(quiet_run.MAX_ITEMS + 5)]
        summary, _ = quiet_run.summarize(lines, 1, False)
        self.assertIn("... and 5 more", summary)

    def test_carriage_return_progress_splits_into_lines(self):
        self.assertEqual(["a", "b", "c"], quiet_run.split_lines("a\rb\r\n\nc\n"))


if __name__ == "__main__":
    unittest.main()
