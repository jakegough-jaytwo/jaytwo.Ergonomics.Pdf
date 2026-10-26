"""Permanent research regression assertions against captured public-API results.

Intentionally red at the investigated revision. Follow-up items must migrate the
fixtures/assertions into the ordinary suite and gather green execution evidence.
This checks measured results, not source text; re-run Probe before using it.
"""
import sys
from pathlib import Path

root = Path(__file__).resolve().parent
lines = (root / "net10.0.tsv").read_text(encoding="utf-8-sig").splitlines()
rows = {row[0]: row[1:] for line in lines if not line.startswith("#") and (row := line.split("\t")) and len(row) > 1 and row[1] != "SAVED"}
checks = {
    "W-214 R5 exact 127 bytes": rows["qpdf-256-r5-split127"][0] == "OK",
    "W-214 R6 exact 127 bytes": rows["qpdf-256-default-split127"][0] == "OK",
    "W-215 explicit Identity stream": rows["aes-128-crypt-identity"][:3] == ["OK", "cHJvYmU=", "cGF5bG9hZA=="],
    "W-215 explicit StdCF stream": rows["aes-128-crypt-stdcf"][:3] == ["OK", "cHJvYmU=", "cGF5bG9hZA=="],
    "W-216 prohibited supplementary scalar": rows["aes-256-noncharacter"][:4] == ["FAIL", "open", "PdfEncryptedException", "PasswordRejected"],
}
for name, passed in checks.items():
    print(("PASS " if passed else "FAIL ") + name)
sys.exit(0 if all(checks.values()) else 1)
