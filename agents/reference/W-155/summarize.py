"""Summarize captured observations, without treating known defects as passes."""
import json
import re
from pathlib import Path

root = Path(__file__).resolve().parent
def rows(name):
    return {line.split("\t")[0]: line.split("\t")[1:] for line in (root / name).read_text(encoding="utf-8-sig").splitlines() if line and not line.startswith("#") and "\tSAVED\t" not in line}

baseline = rows("net10.0.tsv")
summary = {"cases": len(baseline), "runtime_differences_from_windows_net10": {}}
for name in ["net8.0.tsv", "net6.0.tsv", "net48.tsv", "netstandard.tsv", "linux-net10.0.tsv", "linux-net8.0.tsv", "linux-net6.0.tsv", "linux-invariant.tsv"]:
    current = rows(name)
    assert current.keys() == baseline.keys(), name
    summary["runtime_differences_from_windows_net10"][name] = {key: current[key] for key in baseline if current[key] != baseline[key]}
independent = json.loads((root / "independent.json").read_text())
summary["saved"] = []
for row in independent:
    if not row["case"].startswith("saved/"):
        continue
    assert row.get("pypdf-title") == "saved", row
    assert row.get("pypdf-payload") == "cGF5bG9hZA==", row
    assert row.get("pypdf-empty-payload") == "", row
    path = root / (row["case"] + ".pdf")
    data = path.read_bytes()
    lengths = []
    for match in re.finditer(rb"(?m)^\d+ 0 obj\s*(.*?)\bendobj", data, re.S):
        dictionary = match[1].split(b"stream")[0]
        if b"/Type /EmbeddedFile" in dictionary:
            lengths.append(int(re.search(rb"/Length\s+(\d+)", dictionary)[1]))
    summary["saved"].append({"case": row["case"], "embedded_stream_lengths": lengths})
    assert lengths[-1] == (32 if "aes-" in row["case"] else 0), (path, lengths)
summary["baseline_outcomes"] = {kind: sum(row[0] == kind for row in baseline.values()) for kind in ["OK", "FAIL"]}
(root / "summary.json").write_text(json.dumps(summary, indent=2) + "\n")
print(json.dumps(summary, indent=2))
