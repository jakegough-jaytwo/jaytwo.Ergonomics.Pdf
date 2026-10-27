"""Splits test-side property reads between inspection-specific files and files that use inspection as an oracle for other features."""
import json, re, subprocess, collections
SPECIFIC = {"test/jaytwo.Pdf.Tests/SignatureInspectionTests.cs", "test/jaytwo.Pdf.Tests/SignatureInspectionInteropTests.cs",
            "test/jaytwo.Pdf.Tests/RevisionTests.cs", "test/jaytwo.Pdf.Tests/CmsFuzzTests.cs"}
PROPS = ["FieldName", "HasSignatureValueDictionary", "SubFilter", "SignerCertificate", "Certificates", "DigestAlgorithm", "DigestAlgorithmOid",
         "SignatureAlgorithmOid", "SigningTime", "CmsSigningTime", "Name", "Reason", "Location", "ContactInfo", "ByteRange", "ByteRangeStatus",
         "ByteRangeEnd", "ByteRangeRevisionNumber", "HasLaterRevisions", "TimestampTime", "CertificationLevel", "EndsAtSignatureByteRange"]
files = subprocess.run(["git", "grep", "-lE", r"\.Signatures\b|PdfSignatureInfo|\.Revisions\b|CmsSignatureReader", "--", "test", "tools/fuzz"],
                       capture_output=True, text=True).stdout.split()
out = {}
for prop in PROPS:
    spec = other = 0
    oracle_files = set()
    for f in files:
        text = open(f, encoding="utf-8-sig").read()
        n = len(re.findall(r"\.%s\b" % prop, text))
        if f in SPECIFIC or f.startswith("tools/fuzz"):
            spec += n
        else:
            other += n
            if n:
                oracle_files.add(f.split("/")[-1])
    out[prop] = {"inspection_specific_reads": spec, "oracle_reads": other, "oracle_files": sorted(oracle_files)}
json.dump({"files_scanned": len(files), "note": "lexical member-name matches in files that mention the inspection API; overcounts unrelated same-named members", "properties": out},
          open("agents/reference/simplification/W-210/test_oracle.json", "w"), indent=1)
for p, v in out.items():
    print(f"{p:28} specific={v['inspection_specific_reads']:3} oracle={v['oracle_reads']:3} {v['oracle_files'][:6]}")
