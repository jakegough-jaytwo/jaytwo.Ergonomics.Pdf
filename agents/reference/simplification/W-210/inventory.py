"""Lexical consumer inventory for W-210 (tracked files only). Run from the repository root."""
import json, re, subprocess, sys, hashlib, collections

SYMBOLS = {
    "reader": [r"CmsSignatureReader"],
    "info_type": [r"\bPdfSignatureInfo\b"],
    "list_signatures": [r"\bSignatures\b\s*[({=;]|ListSignatures|GetSignatures"],
    "list_revisions": [r"ListRevisions|\bRevisions\b\s*[({=;]|GetRevisions"],
    "revision_type": [r"\bPdfRevision\b"],
    "byte_range_status": [r"PdfByteRangeStatus"],
    "byte_range": [r"\bPdfByteRange\b"],
    "p_ByteRangeRevisionNumber": [r"ByteRangeRevisionNumber"],
    "p_HasLaterRevisions": [r"HasLaterRevisions"],
    "p_EndsAtSignatureByteRange": [r"EndsAtSignatureByteRange"],
    "p_ByteRangeEnd": [r"ByteRangeEnd"],
    "p_TimestampTime": [r"\bTimestampTime\b"],
    "p_CmsSigningTime": [r"CmsSigningTime"],
    "p_SigningTime": [r"\bSigningTime\b"],
    "p_SignerCertificate": [r"\bSignerCertificate\b"],
    "p_Certificates": [r"\.Certificates\b"],
    "p_DigestAlgorithm": [r"\.DigestAlgorithm\b"],
    "p_DigestAlgorithmOid": [r"DigestAlgorithmOid"],
    "p_SignatureAlgorithmOid": [r"SignatureAlgorithmOid"],
    "p_CertificationLevel": [r"CertificationLevel"],
    "p_HasSignatureValueDictionary": [r"HasSignatureValueDictionary"],
    "p_SubFilter": [r"\.SubFilter\b"],
    "p_Name_Reason_Location_Contact": [r"\.(Reason|Location|ContactInfo)\b"],
    "tstinfo_reader": [r"Rfc3161TimeStampToken"],
    "revision_sections": [r"RevisionSections"],
}
AREAS = [("src/jaytwo.Pdf/", "engine"), ("src/jaytwo.Ergonomics.Pdf/", "workflow"),
         ("test/", "test"), ("tools/fuzz/", "fuzz"), ("docs/", "docs")]

def area(path):
    for prefix, name in AREAS:
        if path.startswith(prefix):
            return name
    return "top-level" if "/" not in path else "other"

def grep(pattern):
    out = subprocess.run(["git", "grep", "-nE", pattern, "--", "src", "test", "tools/fuzz", "docs",
                          "README.md", "ACCEPTANCE.md", "QUALIFICATION.md", "*.cs", "*.md"],
                         capture_output=True, text=True).stdout
    hits = []
    for line in out.splitlines():
        m = re.match(r"([^:]+):(\d+):(.*)", line)
        if m and not m.group(1).startswith(("agents/", "graphify-out/")):
            hits.append((m.group(1), int(m.group(2))))
    return hits

result = {"revision": subprocess.run(["git", "rev-parse", "HEAD"], capture_output=True, text=True).stdout.strip(), "symbols": {}}
for name, patterns in SYMBOLS.items():
    hits = sorted(set(h for p in patterns for h in grep(p)))
    byarea = collections.Counter(area(f) for f, _ in hits)
    files = collections.defaultdict(list)
    for f, l in hits:
        files[f].append(l)
    result["symbols"][name] = {"lines": len(hits), "files": len(files), "by_area": dict(byarea),
                                "src_files": {f: v for f, v in files.items() if area(f) in ("engine", "workflow")}}
json.dump(result, open("agents/reference/simplification/W-210/inventory.json", "w"), indent=1, sort_keys=True)
for name, v in result["symbols"].items():
    print(f"{name:34} lines={v['lines']:4} files={v['files']:3} {v['by_area']}")
