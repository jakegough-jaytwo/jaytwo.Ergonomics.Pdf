#!/usr/bin/env python3
"""Focused tests for the fast lane's slowest-test report."""

import tempfile
import unittest
from pathlib import Path

import slowest_tests


TRX = """<?xml version="1.0" encoding="utf-8"?>
<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
  <Results>
    <UnitTestResult testName="Pdf.Tests.A.quick" duration="00:00:00.0100000" outcome="Passed" />
    <UnitTestResult testName="Pdf.Tests.B.slow" duration="00:01:02.5000000" outcome="Passed" />
    <UnitTestResult testName="Pdf.Tests.C.middling" duration="00:00:04.2500000" outcome="Passed" />
  </Results>
</TestRun>
"""


class SlowestTestsTests(unittest.TestCase):
    def test_seconds_reads_hours_minutes_and_fractions(self) -> None:
        self.assertAlmostEqual(slowest_tests.seconds("01:02:03.5"), 3723.5)

    def test_slowest_orders_by_duration_and_caps_the_list(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "fast_net10.0.trx"
            path.write_text(TRX, encoding="utf-8")
            lines = slowest_tests.slowest([path], 2)
        self.assertEqual(len(lines), 2)
        self.assertIn("Pdf.Tests.B.slow", lines[0])
        self.assertIn("62.5s", lines[0])
        self.assertIn("Pdf.Tests.C.middling", lines[1])
        self.assertIn("fast_net10.0", lines[1])


if __name__ == "__main__":
    unittest.main()
