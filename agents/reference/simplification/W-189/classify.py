"""W-189 read-only feature classifier. Usage: python -I classify.py REPO_ROOT MANIFEST_CSV > features.json

Uses pypdf only; opens every tracked fixture, never writes. Encrypted files are opened with the fixture password
recorded in PASSWORDS. Each feature is a structural fact found by walking the whole object graph (every indirect
object, resolved through pypdf), so a feature is reported when a dictionary anywhere in the file carries it, whether
or not a page reaches it. That is a deliberate over-count relative to a page-reachability walk (limit L1).
"""
import csv, hashlib, json, os, sys
from pypdf import PdfReader
from pypdf.generic import IndirectObject, DictionaryObject, ArrayObject, StreamObject

PASSWORDS = {
    "test/jaytwo.Pdf.Tests/Fixtures/corpus/libreoffice-memo-rc4.pdf": "corpus-user",
    "test/jaytwo.Pdf.Tests/Fixtures/qpdf-encrypted.pdf": "user-secret",
}
for r in (2, 3, 4, 5, 6):
    PASSWORDS[f"test/jaytwo.Pdf.Tests/Fixtures/defects/pypdf-unsigned-p-r{r}.pdf"] = "user-secret"


def g(d, k):
    v = d.get(k) if isinstance(d, DictionaryObject) else None
    return v.get_object() if isinstance(v, IndirectObject) else v


def classify(root, rel):
    path = os.path.join(root, rel)
    out = {"path": rel, "features": [], "filters": [], "annotSubtypes": [], "actionTypes": [], "uriKinds": [], "errors": []}
    reader = PdfReader(path)
    if reader.is_encrypted:
        pw = PASSWORDS.get(rel)
        if not pw or not reader.decrypt(pw):
            out["features"].append("encrypted")
            out["errors"].append("cannot decrypt for classification")
            return out
        out["features"].append("encrypted")
    f = set()
    cat = reader.trailer["/Root"]
    out["pages"] = len(reader.pages)
    if g(cat, "/StructTreeRoot") is not None or (g(cat, "/MarkInfo") or {}).get("/Marked") is True:
        f.add("tagged")
    if g(cat, "/Outlines") is not None and (g(g(cat, "/Outlines"), "/First") is not None):
        f.add("outlines")
    if g(cat, "/PageLabels") is not None:
        f.add("page-labels")
    if g(cat, "/Names") is not None and g(g(cat, "/Names"), "/Dests") is not None or g(cat, "/Dests") is not None:
        f.add("named-destinations")
    if g(cat, "/Metadata") is not None:
        f.add("xmp")
    if g(cat, "/OCProperties") is not None:
        f.add("optional-content")
    if g(cat, "/AF") is not None:
        f.add("associated-files")
    if g(g(cat, "/Names") or {}, "/EmbeddedFiles") is not None:
        f.add("embedded-files")
    if g(cat, "/Perms") is not None:
        p = g(cat, "/Perms")
        if g(p, "/UR3") is not None or g(p, "/UR") is not None:
            f.add("usage-rights")
        if g(p, "/DocMDP") is not None:
            f.add("certified")
    if g(cat, "/AA") is not None or g(cat, "/OpenAction") is not None:
        f.add("catalog-actions")
    if g(cat, "/NeedsRendering") is True:
        f.add("needs-rendering")
    if g(g(cat, "/Names") or {}, "/JavaScript") is not None:
        f.add("javascript")
    form = g(cat, "/AcroForm")
    if form is not None:
        f.add("acroform")
        if g(form, "/XFA") is not None:
            f.add("xfa")
        if g(form, "/NeedAppearances") is True:
            f.add("need-appearances")
        sf = g(form, "/SigFlags")
        if sf:
            f.add("sig-flags")
    # whole-graph walk over indirect objects
    filters, subtypes, actions, uris = set(), set(), set(), set()
    n = reader.trailer["/Size"] if "/Size" in reader.trailer else 0
    seen = 0
    for num in range(1, int(n) + 1):
        try:
            o = reader.get_object(IndirectObject(num, 0, reader))
        except Exception:
            continue
        if o is None:
            continue
        seen += 1
        if isinstance(o, DictionaryObject):
            if "/Filter" in o:
                fl = o["/Filter"]
                fl = fl.get_object() if isinstance(fl, IndirectObject) else fl
                for x in (fl if isinstance(fl, ArrayObject) else [fl]):
                    filters.add(str(x.get_object() if isinstance(x, IndirectObject) else x))
            t = g(o, "/Type")
            st = g(o, "/Subtype")
            if st is not None and (t == "/Annot" or "/Rect" in o):
                subtypes.add(str(st))
                if "/IRT" in o:
                    f.add("reply-annotations")
                if st == "/Widget" or "/FT" in o:
                    pass
            if g(o, "/FT") == "/Sig":
                f.add("signature-field")
                v = g(o, "/V")
                if v is not None:
                    f.add("signed")
                    if g(v, "/Type") == "/DocTimeStamp" or g(v, "/SubFilter") == "/ETSI.RFC3161":
                        f.add("document-timestamp")
                    refs = g(v, "/Reference")
                    for r in (refs or []):
                        r = r.get_object()
                        if g(r, "/TransformMethod") == "/DocMDP":
                            f.add("certified")
                if g(o, "/Lock") is not None:
                    f.add("field-lock")
                if g(o, "/SV") is not None:
                    f.add("seed-value")
            s = g(o, "/S")
            if t == "/Action" or (s is not None and str(s).startswith("/") and ("/JS" in o or "/URI" in o or "/D" in o or "/N" in o or "/F" in o)):
                actions.add(str(s))
            if s == "/JavaScript" or "/JS" in o:
                f.add("javascript")
            if s == "/URI" and "/URI" in o:
                u = o["/URI"]
                u = str(u.get_object() if isinstance(u, IndirectObject) else u)
                kind = "absolute" if "://" in u or u.lower().startswith("mailto:") else "relative-or-other"
                uris.add(kind)
            if "/AA" in o and "/FT" in o or ("/AA" in o and st == "/Widget"):
                f.add("field-actions")
            if "/Lock" in o and g(o, "/FT") != "/Sig":
                f.add("field-lock")
            if t in ("/Catalog",):
                pass
            if "/AF" in o and t != "/Catalog":
                f.add("associated-files")
            if g(o, "/Type") == "/Filespec" or g(o, "/Subtype") == "/FileAttachment":
                if g(o, "/Subtype") == "/FileAttachment":
                    f.add("file-attachment-annotation")
    # encrypted files: all strings/streams need password; filters only from dictionaries, unaffected
    if "usage-rights" not in f and "/Perms" in str(cat.keys()):
        pass
    out["features"] = sorted(set(out["features"]) | f)
    out["filters"] = sorted(filters)
    out["annotSubtypes"] = sorted(subtypes)
    out["actionTypes"] = sorted(actions)
    out["uriKinds"] = sorted(uris)
    out["objectsSeen"] = seen
    return out


root = sys.argv[1]
rows = []
for line in list(csv.reader(open(sys.argv[2], encoding="utf-8")))[1:]:
    rel, size, sha = line[0], int(line[1]), line[2]
    actual = hashlib.sha256(open(os.path.join(root, rel), "rb").read()).hexdigest()
    try:
        c = classify(root, rel)
    except Exception as e:
        c = {"path": rel, "features": [], "filters": [], "annotSubtypes": [], "actionTypes": [], "uriKinds": [], "errors": [type(e).__name__ + ": " + str(e)[:120]]}
    c["sha256"] = actual
    c["hashMatchesManifest"] = actual == sha
    rows.append(c)
print(json.dumps(rows, indent=1))
