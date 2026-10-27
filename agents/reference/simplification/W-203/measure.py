"""Generate an UNADOPTED checker draft and lexical size inventory; never edit product files."""
import hashlib
import json
from pathlib import Path
import re
import subprocess

OUT = Path(__file__).resolve().parent
ROOT = OUT.parents[3]
PATH = ROOT / "src/jaytwo.Pdf/Internal/PdfSignatureSeedValues.cs"
source = PATH.read_text(encoding="utf-8")


def extent(text, signature):
    start = text.index(signature)
    opening = text.index("{", start)
    depth = 1
    end = opening + 1
    while depth:
        depth += (text[end] == "{") - (text[end] == "}")
        end += 1
    return start, end


candidate = source
for method in ["CheckEncoding", "CheckDigest", "CheckReason", "CheckLock"]:
    start, end = extent(candidate, "    private void " + method + "(")
    candidate = candidate[:start] + candidate[end:].lstrip("\n")
start, end = extent(candidate, "    internal void Check(")
replacement = '''    internal void Check(int fieldId, PdfSignatureOptions options)
    {
        var field = (PdfObject.Dict)_store.Load(fieldId);
        if (Get(field, "SV") is not PdfObject.Dict seed)
        {
            return;
        }

        long flags = Flags(seed);
        string[] entries = { "Filter", "SubFilter", "V", "Reasons", "LegalAttestation", "AddRevInfo", "DigestMethod", "LockDocument", "AppearanceFilter" };
        int count = _store.EffectiveVersion() >= 20 ? 9 : 7;
        for (int i = 0; i < count; i++)
        {
            if ((flags & (1L << i)) != 0 && (entries[i] == "Reasons" || Get(seed, entries[i]) != null))
            {
                Refuse(entries[i]);
            }
        }

        if (Get(seed, "Cert") is PdfObject.Dict certificate)
        {
            CheckCertificate(certificate);
        }

        if (Get(seed, "MDP") is PdfObject.Dict mdp && Get(mdp, "P") != null)
        {
            Refuse("MDP/P");
        }

        if (Get(seed, "TimeStamp") is PdfObject.Dict timestamp && Flags(timestamp) != 0)
        {
            Refuse("TimeStamp");
        }
    }'''
candidate = candidate[:start] + replacement + candidate[end:]
candidate = candidate.replace("// ISO 32000-1 Table 234 and ISO 32000-2 Tables 237–238. W-151's owner disposition\n// defines the noninteractive, timestamp, and arbitrary-provider interpretations.", "// UNADOPTED R203 draft: blanket applicable-mandatory refusal supersedes W-151 only if approved.")
(OUT / "candidate-checker.cs.txt").write_text(candidate, encoding="utf-8")


def metrics(text):
    # Lexical counts only, not compiler CFG/cyclomatic complexity, timing, or maintenance value.
    return {
        "lines": len(text.splitlines()),
        "nonblankLines": sum(bool(s.strip()) for s in text.splitlines()),
        "ifTokens": len(re.findall(r"\bif\s*\(", text)),
        "loopTokens": len(re.findall(r"\b(?:for|foreach|while)\s*\(", text)),
        "methodsIncludingConstructor": len(re.findall(r"^    (?:internal|private) (?:\w+\?? )?\w+\(", text, re.M)),
    }


reference = Path("C:/jake/git/pdfdocs/text")
files = [PATH, ROOT / "src/jaytwo.Pdf/Internal/PdfSigner.cs", ROOT / "test/jaytwo.Pdf.Tests/SignatureSeedValueTests.cs",
         ROOT / "agents/reference/w-151-owner-disposition.md",
         reference / "iso-32000-2/295-12-7-5-5-signature-fields.md",
         reference / "iso-32000-1/242-12-7-4-5-signature-fields.md"]
inventory = {
    "revision": subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip(),
    "baseline": metrics(source), "candidateDraft": metrics(candidate),
    "candidateExecutionEvidence": "run.ps1 creates an isolated source build; see execution.json and candidate-observations.json for successful execution",
    "files": [{"path": str(p.relative_to(ROOT)) if p.is_relative_to(ROOT) else str(p),
               "sha256": hashlib.sha256(p.read_bytes()).hexdigest()} for p in files],
}
(OUT / "inventory.json").write_text(json.dumps(inventory, indent=2) + "\n", encoding="utf-8")
print(json.dumps({"baseline": inventory["baseline"], "candidateDraft": inventory["candidateDraft"]}))
