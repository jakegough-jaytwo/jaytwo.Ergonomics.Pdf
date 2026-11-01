"""W-218 R1 beveled (/BS /S /B) and inset (/BS /S /I) border-footprint research fixture (research only).

Sibling of exact_width_fixture.py; reuses its synthetic exact-width font, so glyph ink, advance and line box of
`B` coincide and every number below is exact. Neither ISO 32000-1 nor ISO 32000-2 gives a band width, colour or
geometry for B or I, so the painted construction is a FIXTURE CHOICE, recorded per case:

  ring_band  outer solid ring of /BS/W in /MK/BC, then a light (top/left) and a dark (bottom/right) bevel band of
             /BS/W inside it: painted footprint 2*W per side. This is the geometry of PDFium's form-filler and
             core generators (cpdfsdk_appstream.cpp / cpdf_generateap.cpp, chromium/8076).
  band_only  light and dark bands of /BS/W from the widget edge, no ring: footprint W per side (Poppler 26.08.0
             Annot.cc drawFieldBorder geometry). Control only.

Bevel shading is painted in DISPLAY coordinates (light on display top/left), i.e. it follows /MK/R (PDFium's
convention; Poppler shades in page coordinates). That too is a fixture choice.

Two PDFs with identical geometry:
  bevel-inset-fit.pdf      presentation colours (fixture choice, PDFium form-filler convention): ring /MK/BC
                           red; B light 1 g, B dark /MK/BG / 2 or 0.5 g when /MK/BG is absent; I light 0.5 g,
                           I dark 0.75 g; /MK/BG fill when present; black text.
  bevel-inset-measure.pdf  measurement palette: ring red, light band green, dark band blue, no background fill,
                           black text, so each renderer's ink and footprint edges can be separated by colour.
  bevel-inset-regen.pdf    same /BS and /MK on widgets with NO /AP and NeedAppearances true (reader-generated
                           look-alike reference; Helvetica DA).
  bevel-inset-manifest.json

Rules (content box per display side, ink of `BBB` 12 pt tight on all four content edges):
  full       1 pt beyond the whole painted footprint (candidate rule): side = 1 + footprint
  ring_only  1 pt beyond /BS/W only, as if the border were solid (negative control): side = 1 + W

Usage: python bevel_inset_fixture.py [--out DIR]
"""
import argparse
import hashlib
import json
import platform
from fractions import Fraction as Fr
from pathlib import Path

import fontTools
import pypdf
from pypdf.generic import (ArrayObject as A, BooleanObject, DictionaryObject as D, NameObject as N,
                           NumberObject as I, TextStringObject as T)

from exact_width_fixture import (ASC, CHAR_TO_GID, DESC, PAGE_H, PAGE_W, WIDGET_X, WIDGET_Y, _stream, build_font,
                                 fmt, make_writer, num)

INSET = Fr(1)
TEXT, SIZE = "BBB", Fr(12)
BC = (1, 0, 0)
BG = (Fr(6, 10), Fr(8, 10), 1)
ROTATIONS = (None, 0, 90, 180, 270)


def footprint(c):
    return c["bw"] * (2 if c["construction"] == "ring_band" else 1)


def side_inset(c):
    return INSET + (footprint(c) if c["rule"] == "full" else c["bw"])


def geometry(c):
    """Display box, content box and the single line's origin, all exact."""
    s = side_inset(c)
    dw, dh = 36 + 2 * s, 12 + 2 * s                       # BBB at 12 pt: advance 36, line box 12
    box = (s, s, dw - s, dh - s)
    return dw, dh, box, (s, dh - s - Fr(ASC) * SIZE / 1000)


CASES = []


def add(name, style, bw, rot, bg, construction="ring_band", rule="full", note=""):
    c = dict(name=name, style=style, bw=Fr(bw), rot=rot, bg=bg, construction=construction, rule=rule, note=note)
    c["footprint"] = footprint(c)
    c["nominal_clearance"] = side_inset(c) - c["footprint"]      # ink-to-footprint distance on every side
    CASES.append(c)


def build_cases():
    for style in ("B", "I"):
        for bw in ("1", "2", "3"):
            for rot in ROTATIONS:
                for bg in (True, False):
                    add(f"{style}{bw}_r{'abs' if rot is None else rot}_{'bg' if bg else 'nobg'}", style, bw, rot, bg)
    for bw in ("1", "2"):                                    # control: inset counts only /BS/W
        add(f"ctl_ringonly_B{bw}", "B", bw, 0, True, rule="ring_only", note="negative control: 1 pt beyond /BS/W only")
    for style in ("B", "I"):                                 # control: Poppler-like footprint W, candidate rule
        add(f"ctl_bandonly_{style}2", style, "2", 0, True, construction="band_only", note="footprint W, no ring")


def colours(c, measure):
    if measure:
        return {"ring": (1, 0, 0), "light": (0, 1, 0), "dark": (0, 0, 1), "bg": None}
    if c["style"] == "B":
        light, dark = (1,), (tuple(x / 2 for x in BG) if c["bg"] else (Fr(1, 2),))
    else:
        light, dark = (Fr(1, 2),), (Fr(3, 4),)
    return {"ring": BC, "light": light, "dark": dark, "bg": BG if c["bg"] else None}


def fill(col):
    return (f"{fmt(col[0])} g" if len(col) == 1 else " ".join(fmt(x) for x in col) + " rg")


def poly(points):
    p = [f"{fmt(x)} {fmt(y)}" for x, y in points]
    return p[0] + " m " + " ".join(q + " l" for q in p[1:]) + " h f"


def border_ops(c, dw, dh, col):
    w = c["bw"]
    out = []
    if c["construction"] == "ring_band":
        out.append(f"{fill(col['ring'])} 0 0 {fmt(dw)} {fmt(dh)} re {fmt(w)} {fmt(w)} {fmt(dw - 2 * w)} {fmt(dh - 2 * w)} re f*")
        o, i = w, 2 * w                                     # band from w to 2w
    else:
        o, i = Fr(0), w                                     # band from 0 to w
    out.append(fill(col["light"]) + " " + poly([(o, o), (o, dh - o), (dw - o, dh - o), (dw - i, dh - i), (i, dh - i), (i, i)]))
    out.append(fill(col["dark"]) + " " + poly([(dw - o, dh - o), (dw - o, o), (o, o), (i, i), (dw - i, i), (dw - i, dh - i)]))
    return out


def ap_stream(c, measure):
    dw, dh, _, (x, y) = geometry(c)
    rot = c["rot"] or 0
    pw, ph = (dh, dw) if rot in (90, 270) else (dw, dh)
    col = colours(c, measure)
    out = ["q", {0: "", 90: f"0 1 -1 0 {fmt(pw)} 0 cm", 180: f"-1 0 0 -1 {fmt(pw)} {fmt(ph)} cm",
                 270: f"0 -1 1 0 0 {fmt(ph)} cm"}[rot]]
    if col["bg"]:
        out.append(f"{fill(col['bg'])} 0 0 {fmt(dw)} {fmt(dh)} re f")
    out += border_ops(c, dw, dh, col)
    hexed = "".join(f"{CHAR_TO_GID[ch]:04X}" for ch in TEXT)
    out += ["0 g", f"BT /F1 {fmt(SIZE)} Tf 1 0 0 1 {fmt(x)} {fmt(y)} Tm <{hexed}> Tj ET", "Q"]
    return "\n".join(s for s in out if s) + "\n"


def mk(c, with_bc=True):
    m = D()
    if with_bc:
        m[N("/BC")] = A([num(v) for v in BC])
    if c["bg"]:
        m[N("/BG")] = A([num(v) for v in BG])
    if c["rot"] is not None:
        m[N("/R")] = I(c["rot"])
    return m


def write_fixture(path, font_bytes, measure):
    w, font = make_writer(font_bytes)
    refs = []
    for c in CASES:
        dw, dh, _, _ = geometry(c)
        rot = c["rot"] or 0
        pw, ph = (dh, dw) if rot in (90, 270) else (dw, dh)
        page = w.add_blank_page(width=PAGE_W, height=PAGE_H)
        ap = _stream(ap_stream(c, measure).encode("ascii"), {
            N("/Type"): N("/XObject"), N("/Subtype"): N("/Form"),
            N("/BBox"): A([I(0), I(0), num(pw), num(ph)]), N("/Resources"): D({N("/Font"): D({N("/F1"): font})})})
        ann = D({N("/Type"): N("/Annot"), N("/Subtype"): N("/Widget"), N("/FT"): N("/Tx"), N("/T"): T(c["name"]),
                 N("/V"): T(TEXT), N("/F"): I(4), N("/Q"): I(0), N("/DA"): T(f"/F1 {fmt(SIZE)} Tf 0 g"),
                 N("/Rect"): A([I(WIDGET_X), I(WIDGET_Y), num(WIDGET_X + pw), num(WIDGET_Y + ph)]),
                 N("/BS"): D({N("/W"): num(c["bw"]), N("/S"): N("/" + c["style"])}), N("/MK"): mk(c),
                 N("/AP"): D({N("/N"): w._add_object(ap)})})
        ref = w._add_object(ann)
        refs.append(ref)
        page[N("/Annots")] = A([ref])
    w._root_object[N("/AcroForm")] = w._add_object(D({
        N("/Fields"): A(refs), N("/NeedAppearances"): BooleanObject(False),
        N("/DR"): D({N("/Font"): D({N("/F1"): font})}), N("/DA"): T("/F1 0 Tf 0 g")}))
    w.write(path)


# ---------------------------------------------------------------- reader-generated look-alike reference
REGEN_W, REGEN_H = 60, 24
REGEN = []


def build_regen():
    def r(name, style, bw, rot=0, bg=True, bc=True, h=REGEN_H):
        REGEN.append(dict(name=name, style=style, bw=Fr(bw), rot=rot, bg=bg, bc=bc, w=Fr(REGEN_W), h=Fr(h)))
    for style in ("B", "I"):
        for bw in ("1", "2", "3"):
            for bg in (True, False):
                r(f"g_{style}{bw}_{'bg' if bg else 'nobg'}", style, bw, bg=bg)
        for rot in (None, 90, 180, 270):
            r(f"g_{style}2_r{'abs' if rot is None else rot}", style, "2", rot=rot)
        r(f"g_{style}2_nocolours", style, "2", bg=False, bc=False)
        r(f"g_{style}8_oversize", style, "8", h=14)          # /BS/W 8 > half of the 14-pt height


def write_regen(path):
    w = pypdf.PdfWriter()
    helv = w._add_object(D({N("/Type"): N("/Font"), N("/Subtype"): N("/Type1"), N("/BaseFont"): N("/Helvetica"),
                            N("/Encoding"): N("/WinAnsiEncoding")}))
    refs = []
    for c in REGEN:
        page = w.add_blank_page(width=PAGE_W, height=PAGE_H)
        ann = D({N("/Type"): N("/Annot"), N("/Subtype"): N("/Widget"), N("/FT"): N("/Tx"), N("/T"): T(c["name"]),
                 N("/V"): T("Ab"), N("/F"): I(4), N("/Q"): I(0), N("/DA"): T("/Helv 6 Tf 0 g"),
                 N("/Rect"): A([I(WIDGET_X), I(WIDGET_Y), num(WIDGET_X + c["w"]), num(WIDGET_Y + c["h"])]),
                 N("/BS"): D({N("/W"): num(c["bw"]), N("/S"): N("/" + c["style"])}), N("/MK"): mk(c, c["bc"])})
        ref = w._add_object(ann)
        refs.append(ref)
        page[N("/Annots")] = A([ref])
    w._root_object[N("/AcroForm")] = w._add_object(D({
        N("/Fields"): A(refs), N("/NeedAppearances"): BooleanObject(True),
        N("/DR"): D({N("/Font"): D({N("/Helv"): helv})}), N("/DA"): T("/Helv 0 Tf 0 g")}))
    w.write(path)


def sha256(p):
    return hashlib.sha256(Path(p).read_bytes()).hexdigest()


def main():
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--out", default=str(Path(__file__).resolve().parent))
    out = Path(ap.parse_args().out)
    out.mkdir(parents=True, exist_ok=True)
    build_cases()
    build_regen()
    font_bytes = build_font()
    files = {"bevel-inset-fit.pdf": False, "bevel-inset-measure.pdf": True}
    for name, measure in files.items():
        write_fixture(out / name, font_bytes, measure)
    write_regen(out / "bevel-inset-regen.pdf")
    rows = []
    for i, c in enumerate(CASES):
        dw, dh, box, origin = geometry(c)
        rows.append({**{k: (fmt(v) if isinstance(v, Fr) else v) for k, v in c.items()}, "page": i + 1,
                     "display_box": [fmt(dw), fmt(dh)], "content_box": [fmt(v) for v in box],
                     "origin": [fmt(v) for v in origin]})
    manifest = {
        "purpose": "W-218 R1 beveled/inset border-footprint research fixture; not jaytwo.Pdf output",
        "versions": {"python": platform.python_version(), "pypdf": pypdf.__version__, "fontTools": fontTools.version},
        "font_sha256": hashlib.sha256(font_bytes).hexdigest(),
        "files": {n: sha256(out / n) for n in (*files, "bevel-inset-regen.pdf")},
        "page": [PAGE_W, PAGE_H], "widgetOrigin": [WIDGET_X, WIDGET_Y], "text": TEXT, "size": fmt(SIZE),
        "presentation_colours": {"BC": [fmt(v) for v in BC], "BG": [fmt(v) for v in BG],
                                 "B": "light 1 g; dark BG/2, or 0.5 g without BG", "I": "light 0.5 g; dark 0.75 g"},
        "cases": rows,
        "regen": [{**{k: (fmt(v) if isinstance(v, Fr) else v) for k, v in c.items()}, "page": i + 1} for i, c in enumerate(REGEN)],
    }
    (out / "bevel-inset-manifest.json").write_text(json.dumps(manifest, indent=1) + "\n", encoding="utf-8")
    print(f"{len(CASES)} fixture cases, {len(REGEN)} reader-generated cases")
    for n, h in manifest["files"].items():
        print(f"{h}  {n}")


if __name__ == "__main__":
    main()
