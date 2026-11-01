"""W-218 independent experiments: VALUE-C5/C6 (choice storage, clearing) and DECODE-C5 (language escapes).

Hand-written fixtures (no target library, no pypdf writer) are read by three independent engines:
pypdf (raw dictionary access), PDFium via pypdfium2 form environment, and MuPDF via PyMuPDF.
Observations are raw reader behaviour, not format validity. Usage: python this.py OUT.json
"""
import ctypes, hashlib, json, os, sys, tempfile

import pymupdf
import pypdf
import pypdfium2 as pdfium
import pypdfium2.raw as raw
from pypdf.generic import ArrayObject, NullObject

CH_COMBO, CH_EDIT, CH_MULTI = 1 << 17, 1 << 18, 1 << 21


def hexs(b):
    return "<" + b.hex().upper() + ">"


def tstr(s):
    return hexs(b"\xfe\xff" + s.encode("utf-16-be"))


def arr(items):
    return "[" + " ".join(items) + "]"


def build(fields):
    """fields: list of dicts name,parent,body(str of extra entries),widget(bool)."""
    ids = {f["name"]: 5 + i for i, f in enumerate(fields)}
    objs = {}
    top = [ids[f["name"]] for f in fields if not f.get("parent")]
    kids = {}
    for f in fields:
        if f.get("parent"):
            kids.setdefault(f["parent"], []).append(ids[f["name"]])
    widgets = [ids[f["name"]] for f in fields if f.get("widget")]
    objs[1] = "<< /Type /Catalog /Pages 3 0 R /AcroForm 2 0 R >>"
    objs[2] = "<< /Fields [%s] /DA (/Helv 0 Tf 0 g) >>" % " ".join("%d 0 R" % i for i in top)
    objs[3] = "<< /Type /Pages /Kids [4 0 R] /Count 1 >>"
    objs[4] = "<< /Type /Page /Parent 3 0 R /MediaBox [0 0 300 300] /Annots [%s] >>" % " ".join("%d 0 R" % i for i in widgets)
    for n, f in enumerate(fields):
        d = ["/T " + tstr(f["name"])]
        if f.get("parent"):
            d.append("/Parent %d 0 R" % ids[f["parent"]])
        if f["name"] in kids:
            d.append("/Kids [%s]" % " ".join("%d 0 R" % i for i in kids[f["name"]]))
        if f.get("widget"):
            d.append("/Type /Annot /Subtype /Widget /Rect [10 %d 200 %d] /P 4 0 R /F 4" % (10 + 40 * n, 40 + 40 * n))
        d.append(f.get("body", ""))
        objs[ids[f["name"]]] = "<< " + " ".join(d) + " >>"
    out = bytearray(b"%PDF-1.7\n")
    offs = {}
    for i in sorted(objs):
        offs[i] = len(out)
        out += ("%d 0 obj\n%s\nendobj\n" % (i, objs[i])).encode("latin-1")
    xr = len(out)
    out += ("xref\n0 %d\n0000000000 65535 f \n" % (max(objs) + 1)).encode()
    for i in range(1, max(objs) + 1):
        out += ("%010d 00000 n \n" % offs[i]).encode()
    out += ("trailer\n<< /Size %d /Root 1 0 R >>\nstartxref\n%d\n%%%%EOF\n" % (max(objs) + 1, xr)).encode()
    return bytes(out)


def wstr(fn, *args):
    n = fn(*args, None, 0)
    if n <= 2:
        return ""
    buf = (ctypes.c_ushort * (n // 2 + 1))()
    fn(*args, ctypes.cast(buf, ctypes.POINTER(ctypes.c_ushort)), n)
    return bytes(buf)[: n - 2].decode("utf-16-le", "surrogatepass")


def read_pypdf(path):
    r = pypdf.PdfReader(path)
    res = {}

    def walk(node, inh):
        node = node.get_object()
        k = node.get("/Kids")
        if k:
            for c in k:
                walk(c, inh)
            return
        t = str(node["/T"])
        raw_v = node.get("/V", "<absent>")
        raw_i = node.get("/I", "<absent>")
        eff = node
        chain = None
        while eff is not None and "/V" not in eff:
            eff = eff["/Parent"].get_object() if "/Parent" in eff else None
        chain = eff.get("/V") if eff is not None else None
        res[t] = {"local_V": repr(raw_v), "local_I": repr(raw_i), "inherited_effective_V": repr(chain)}

    for f in r.trailer["/Root"]["/AcroForm"]["/Fields"]:
        walk(f, None)
    return res


def read_pdfium(path):
    pdf = pdfium.PdfDocument(path)
    pdf.init_forms()
    page = pdf[0]
    h = pdf.formenv.raw
    res = {}
    for i in range(raw.FPDFPage_GetAnnotCount(page.raw)):
        a = raw.FPDFPage_GetAnnot(page.raw, i)
        name = wstr(raw.FPDFAnnot_GetFormFieldName, h, a)
        n = raw.FPDFAnnot_GetOptionCount(h, a)
        opts = [(wstr(raw.FPDFAnnot_GetOptionLabel, h, a, j), bool(raw.FPDFAnnot_IsOptionSelected(h, a, j))) for j in range(max(n, 0))]
        res[name] = {
            "value": wstr(raw.FPDFAnnot_GetFormFieldValue, h, a),
            "export": wstr(raw.FPDFAnnot_GetFormFieldExportValue, h, a),
            "selected_labels": [l for l, s in opts if s],
            "selected_rows": [j for j, (l, s) in enumerate(opts) if s],
            "option_labels": [l for l, s in opts],
        }
        raw.FPDFPage_CloseAnnot(a)
    page.close()
    pdf.close()
    return res


def read_mupdf(path):
    doc = pymupdf.open(path)
    res = {}
    for w in doc[0].widgets():
        res[w.field_name] = {"value": repr(w.field_value), "choice_values": repr(w.choice_values)}
    doc.close()
    return res


READERS = (("pypdf", read_pypdf), ("pdfium", read_pdfium), ("mupdf", read_mupdf))


def run(cases, tmp):
    out = []
    for cid, desc, fields in cases:
        data = build(fields)
        p = os.path.join(tmp, cid + ".pdf")
        with open(p, "wb") as f:
            f.write(data)
        row = {"id": cid, "desc": desc, "sha256": hashlib.sha256(data).hexdigest()}
        for rn, fn in READERS:
            try:
                row[rn] = fn(p)
            except Exception as e:  # recorded, not hidden
                row[rn] = {"error": "%s: %s" % (type(e).__name__, e)}
        out.append(row)
    return out


def ch(name, ff, opt, v=None, i=None, extra="", parent=None, widget=True):
    b = "/FT /Ch /Ff %d " % ff if not parent or extra.startswith("FT") else ""
    if opt is not None:
        b += "/Opt " + opt + " "
    if v is not None:
        b += "/V " + v + " "
    if i is not None:
        b += "/I " + i + " "
    return {"name": name, "parent": parent, "widget": widget, "body": b}


def pair(e, l):
    return "[%s %s]" % (tstr(e), tstr(l))


OPT_P = arr([pair("e1", "Alpha"), pair("e2", "Beta"), pair("e3", "Gamma")])
OPT_Q = arr([pair("x", "A"), pair("x", "B")])
OPT_R = arr([pair("x", "A"), pair("y", "A")])
OPT_S = arr([pair("", "None"), pair("a", "Alpha")])
OPT_T = arr([tstr("Red"), tstr("Green"), tstr("Blue")])


def c6_cases():
    S = tstr
    LIST = 0
    c = []
    def add(cid, desc, ff, opt, v, i):
        c.append((cid, desc, [ch("f", ff, opt, v, i)]))
    add("c6-01", "combo paired P, V=export e2, no I", CH_COMBO, OPT_P, S("e2"), None)
    add("c6-02", "combo paired P, V=display Beta, no I", CH_COMBO, OPT_P, S("Beta"), None)
    add("c6-03", "combo paired P, V=export e2, I=[1]", CH_COMBO, OPT_P, S("e2"), "[1]")
    add("c6-04", "combo paired P, V=display Beta, I=[1]", CH_COMBO, OPT_P, S("Beta"), "[1]")
    add("c6-05", "combo paired P, no V, I=[1]", CH_COMBO, OPT_P, None, "[1]")
    add("c6-06", "combo paired P, V=display Beta, I=[0] (contradict)", CH_COMBO, OPT_P, S("Beta"), "[0]")
    add("c6-07", "combo paired P, V=export e2, I=[0] (contradict)", CH_COMBO, OPT_P, S("e2"), "[0]")
    add("c6-08", "combo shared-export Q, V=export x, I=[1]", CH_COMBO, OPT_Q, S("x"), "[1]")
    add("c6-09", "combo shared-export Q, V=display B, I=[1]", CH_COMBO, OPT_Q, S("B"), "[1]")
    add("c6-10", "combo shared-export Q, V=display B, no I", CH_COMBO, OPT_Q, S("B"), None)
    add("c6-11", "combo shared-export Q, V=export x, no I", CH_COMBO, OPT_Q, S("x"), None)
    add("c6-12", "combo shared-label R, V=display A, I=[1]", CH_COMBO, OPT_R, S("A"), "[1]")
    add("c6-13", "combo shared-label R, V=export y, I=[1]", CH_COMBO, OPT_R, S("y"), "[1]")
    add("c6-14", "combo shared-label R, V=display A, no I", CH_COMBO, OPT_R, S("A"), None)
    add("c6-15", "combo empty-export S, V=empty string, no I", CH_COMBO, OPT_S, S(""), None)
    add("c6-16", "combo empty-export S, V=display None, I=[0]", CH_COMBO, OPT_S, S("None"), "[0]")
    add("c6-17", "combo empty-export S, V=empty string, I=[0]", CH_COMBO, OPT_S, S(""), "[0]")
    add("c6-18", "combo empty-export S, no V, no I", CH_COMBO, OPT_S, None, None)
    add("c6-19", "multi P, V=[exports e1 e3], I=[0 2]", CH_MULTI, OPT_P, arr([S("e1"), S("e3")]), "[0 2]")
    add("c6-20", "multi P, V=[displays Alpha Gamma], I=[0 2]", CH_MULTI, OPT_P, arr([S("Alpha"), S("Gamma")]), "[0 2]")
    add("c6-21", "multi P, V=[displays Alpha Gamma], no I", CH_MULTI, OPT_P, arr([S("Alpha"), S("Gamma")]), None)
    add("c6-22", "multi P, V=[exports e1 e3], no I", CH_MULTI, OPT_P, arr([S("e1"), S("e3")]), None)
    add("c6-23", "multi P, V=scalar display Beta, no I", CH_MULTI, OPT_P, S("Beta"), None)
    add("c6-24", "multi P, V=[] I=[]", CH_MULTI, OPT_P, "[]", "[]")
    add("c6-25", "editable combo P, V=custom, no I", CH_COMBO | CH_EDIT, OPT_P, S("custom"), None)
    add("c6-26", "editable combo P, V=display Beta", CH_COMBO | CH_EDIT, OPT_P, S("Beta"), None)
    add("c6-27", "editable combo P, V=export e2", CH_COMBO | CH_EDIT, OPT_P, S("e2"), None)
    add("c6-28", "list plain T, V=Green, no I", LIST, OPT_T, S("Green"), None)
    add("c6-29", "list plain T, V=Green, I=[1] (single-select I)", LIST, OPT_T, S("Green"), "[1]")
    add("c6-30", "list paired P single, V=display Beta, no I", LIST, OPT_P, S("Beta"), None)
    return c


def c5_cases():
    S = tstr
    c = []
    variants = [
        ("control-omit", None, None),
        ("V-null", "null", None),
        ("V-empty-string", S(""), None),
        ("V-empty-array", "[]", None),
        ("I-empty-only", None, "[]"),
        ("V-empty-array+I-empty", "[]", "[]"),
        ("V-null+I-empty", "null", "[]"),
        ("V-empty-string+I-empty", S(""), "[]"),
    ]
    for kind, ff, pv in (("single-combo", CH_COMBO, S("e2")), ("multi-list", CH_MULTI, arr([S("e1"), S("e3")]))):
        for vn, v, i in variants:
            parent = {"name": "parent", "widget": False, "body": "/FT /Ch /Ff %d /Opt %s /V %s" % (ff, OPT_P, pv)}
            a = ch("A", 0, None, v, i, parent="parent")
            a["body"] = a["body"].replace("/FT /Ch /Ff 0 ", "")
            b = {"name": "B", "parent": "parent", "widget": True, "body": ""}
            c.append(("c5-%s-%s" % (kind, vn), "%s: parent V=%s; kid A local %s; sibling B untouched" % (kind, pv, vn), [parent, a, b]))
    # empty-export option present: distinguish no selection from a selected empty export
    for kind, ff, pv in (("single-combo", CH_COMBO, S("a")), ("multi-list", CH_MULTI, arr([S("a")]))):
        for vn, v, i in (("V-empty-string", S(""), None), ("V-empty-array", "[]", None), ("V-null", "null", None), ("I-empty-only", None, "[]")):
            parent = {"name": "parent", "widget": False, "body": "/FT /Ch /Ff %d /Opt %s /V %s" % (ff, OPT_S, pv)}
            a = ch("A", 0, None, v, i, parent="parent")
            b = {"name": "B", "parent": "parent", "widget": True, "body": ""}
            c.append(("c5s-%s-%s" % (kind, vn), "%s with empty-export option: parent V=%s; kid A local %s" % (kind, pv, vn), [parent, a, b]))
    # no inheritance baselines for the same local encodings
    for vn, v, i in variants[1:]:
        c.append(("c5-baseline-single-%s" % vn, "single combo, no ancestor V; local %s" % vn, [ch("A", CH_COMBO, OPT_P, v, i)]))
    return c


def lang_cases():
    B = bytes
    def ft(name, rawbytes):
        return (name, [{"name": "f", "widget": True, "body": "/FT /Tx /V %s" % hexs(rawbytes)}])
    U = lambda s: s.encode("utf-16-be")
    ESC = b"\x00\x1b"
    bom = b"\xfe\xff"
    hi = U("Hi")
    cases = [
        ("d5-00-plain-utf16", bom + hi, "plain UTF-16BE 'Hi'"),
        ("d5-01-A-raw-ascii-en", bom + ESC + b"en" + ESC + hi, "ESC 'e''n' raw ASCII bytes ESC Hi (reading A)"),
        ("d5-02-B-utf16-en", bom + ESC + U("en") + ESC + hi, "ESC UTF-16BE chars e,n ESC Hi (reading B)"),
        ("d5-03-A-raw-enUS", bom + ESC + b"enUS" + ESC + hi, "reading A with country"),
        ("d5-04-B-utf16-enUS", bom + ESC + U("enUS") + ESC + hi, "reading B with country"),
        ("d5-05-utf8-enUS", b"\xef\xbb\xbf\x1benUS\x1bHi", "PDF 2.0 UTF-8: ESC enUS ESC Hi"),
        ("d5-06-literal-esc-unclosed", bom + ESC + hi, "literal ESC, no code, no closing"),
        ("d5-07-A-incomplete", bom + ESC + b"en" + hi, "ESC en then text, no closing ESC"),
        ("d5-08-A-adjacent", bom + ESC + b"en" + ESC + U("H") + ESC + b"fr" + ESC + U("i"), "two adjacent annotated runs"),
        ("d5-09-A-at-end", bom + hi + ESC + b"en" + ESC, "annotation at end of text"),
        ("d5-10-A-supplementary-after", bom + ESC + b"en" + ESC + U("\U0001F600"), "supplementary scalar after annotation"),
        ("d5-11-A-empty-code", bom + ESC + ESC + hi, "ESC ESC (empty code)"),
        ("d5-12-A-3byte-code", bom + ESC + b"eng" + ESC + hi + b"\x00", "3-byte code (odd total), invalid per 2-byte rule"),
    ]
    return [(cid, desc, [{"name": "f", "widget": True, "body": "/FT /Tx /V %s" % hexs(v)}]) for cid, v, desc in cases]


def main(outp):
    with tempfile.TemporaryDirectory() as tmp:
        res = {
            "env": {"python": sys.version.split()[0], "pypdf": pypdf.__version__, "pypdfium2": pdfium.version.PYPDFIUM_INFO.version if hasattr(pdfium, "version") else "?",
                    "pdfium_build": str(getattr(pdfium.version, "PDFIUM_INFO", "?")), "pymupdf": pymupdf.__version__, "mupdf": pymupdf.mupdf_version},
            "script_sha256": hashlib.sha256(open(__file__, "rb").read()).hexdigest(),
            "value_c6": run(c6_cases(), tmp),
            "value_c5": run(c5_cases(), tmp),
            "decode_c5": run(lang_cases(), tmp),
        }
    with open(outp, "w", encoding="utf-8") as f:
        json.dump(res, f, indent=1, ensure_ascii=False, default=str)
    print("wrote", outp, [len(res[k]) for k in ("value_c6", "value_c5", "decode_c5")])


if __name__ == "__main__":
    main(sys.argv[1])
