"""Physical-line partition of the signature-forensics surface at the current revision (not a complexity metric; do not add overlapping rows)."""
import json, subprocess
def n(path): return len(open(path, encoding="utf-8-sig").read().splitlines())
def rng(path, a, b): return b - a + 1
I = "src/jaytwo.Pdf/Internal/PdfSignatureInspector.cs"
S = "src/jaytwo.Pdf/Signatures/"
parts = {
 "basic_detection_and_text_fields": {"inspector_ListSignatures_and_SignatureText": rng(I, 26, 66) + 4, "note": "field walk itself is PdfFieldTree.CollectFields, shared with forms and settled"},
 "rich_cms_reader": {"CmsSignatureReader.cs": n(S + "CmsSignatureReader.cs"), "inspector_cms_call_site": 12, "tools/fuzz/CmsFuzzTarget.cs": n("tools/fuzz/CmsFuzzTarget.cs"),
                     "tools/fuzz/CmsSeeds.cs": n("tools/fuzz/CmsSeeds.cs"), "tools/fuzz/cms-seeds.py": n("tools/fuzz/cms-seeds.py"),
                     "test/CmsFuzzTests.cs": n("test/jaytwo.Pdf.Tests/CmsFuzzTests.cs")},
 "revision_attribution": {"inspector_ListRevisions..RevisionEnd": rng(I, 68, 190), "inspector_byte_helpers": 24 + 17 + 22, "PdfRevision.cs": n("src/jaytwo.Pdf/PdfRevision.cs"),
                          "RevisionSection.cs": n("src/jaytwo.Pdf/Internal/RevisionSection.cs"), "test/RevisionTests.cs": n("test/jaytwo.Pdf.Tests/RevisionTests.cs")},
 "coverage_hints": {"inspector_ReadByteRange..lexer_skip": rng(I, 232, 366), "PdfByteRangeStatus.cs": n(S + "PdfByteRangeStatus.cs"),
                    "PdfByteRange.cs_retained_by_signing_context": n(S + "PdfByteRange.cs")},
 "public_model": {"PdfSignatureInfo.cs": n(S + "PdfSignatureInfo.cs")},
 "retained_signing_der_and_timestamp_validation": {"DerCodec.cs": n(S + "DerCodec.cs"), "CmsDetachedSignatureProvider.cs": n(S + "CmsDetachedSignatureProvider.cs"),
                    "Rfc3161TimeStampToken.cs": n(S + "Rfc3161TimeStampToken.cs"), "DocTimeStampSignatureProvider.cs": n(S + "DocTimeStampSignatureProvider.cs")},
 "mixed_test_files": {t: n("test/jaytwo.Pdf.Tests/" + t) for t in ["SignatureInspectionTests.cs", "SignatureInspectionInteropTests.cs"]},
}
seeds = subprocess.run(["git", "ls-files", "tools/fuzz/seeds/cms"], capture_output=True, text=True).stdout.split()
parts["rich_cms_reader"]["tools/fuzz/seeds/cms_tracked_files"] = len(seeds)
json.dump({"revision": subprocess.run(["git", "rev-parse", "HEAD"], capture_output=True, text=True).stdout.strip(), "unit": "physical lines (comments and blanks included)", "parts": parts},
          open("agents/reference/simplification/W-210/partition.json", "w"), indent=1)
for k, v in parts.items(): print(k, v)
