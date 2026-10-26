"""Retain DSS public fixtures and unmodified reports with a checksum inventory."""
import argparse
import hashlib
import json
import pathlib
import zipfile

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("source", type=pathlib.Path)
parser.add_argument("archive", type=pathlib.Path)
args = parser.parse_args()


def sha256(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


results = []
for path in sorted(args.source.glob("*/*/result.json")):
    result = json.loads(path.read_text())
    fixture = json.loads(path.with_name("fixture.json").read_text())
    results.append({"case": str(path.parent.relative_to(args.source)).replace("\\", "/"),
                    "fixture": fixture, "result": result})
if not results:
    raise SystemExit("No DSS results found")
files = sorted(path for path in args.source.rglob("*") if path.is_file())
allowed = {".pdf", ".cer", ".crl", ".xml", ".json", ".txt"}
if any(path.suffix not in allowed for path in files):
    raise SystemExit("Unexpected file type in evidence; private keys must never be collected")
inventory = {
    "padesStandard": "ETSI EN 319 142-1 V1.2.1 (2024-01)",
    "validationStandard": "ETSI EN 319 102-1 V1.4.1 (2024-06)",
    "policy": "Unmodified DSS 6.3 policy/constraint.xml: QES AdESQC TL based",
    "results": results,
    "files": {str(path.relative_to(args.source)).replace("\\", "/"): sha256(path) for path in files},
}
args.archive.parent.mkdir(parents=True, exist_ok=True)
with zipfile.ZipFile(args.archive, "w", zipfile.ZIP_DEFLATED) as archive:
    archive.writestr("inventory.json", json.dumps(inventory, indent=2) + "\n")
    for path in files:
        archive.write(path, str(path.relative_to(args.source)).replace("\\", "/"))
index = args.archive.with_suffix(".json")
index.write_text(json.dumps({"archive": args.archive.name, "sha256": sha256(args.archive),
                            "results": results, "files": inventory["files"]}, indent=2) + "\n")
print(f"Retained {len(results)} DSS results and {len(files)} files in {args.archive}")
