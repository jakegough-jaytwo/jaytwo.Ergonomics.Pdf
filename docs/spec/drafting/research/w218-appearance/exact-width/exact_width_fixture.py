"""W-218 R1 exact-width research fixture generator (research only; not jaytwo.Pdf output).

Builds a synthetic 1000-units-per-em TrueType whose glyph advances are integers equal to the serialized
PDF /W widths, so the width used for fit arithmetic, the width written in the PDF and the width a renderer
positions with are the same number. Cases sit immediately below, at and above the boundaries of the proposed
R1 profile (docs/spec/drafting/form-adoption-review.md). Every dimension is an exact decimal; arithmetic uses
fractions.Fraction, never floats.

Outputs (next to this script by default):
  exact-width-fit.pdf        one widget per page, one page per case
  exact-width-precision.pdf  ink-edge versus vector-bar probes at sub-thousandth offsets
  exact-width-manifest.json  case parameters, intended outcome and fixture hashes

Usage: python exact_width_fixture.py [--out DIR]
Needs: fontTools, pypdf (versions are recorded in the manifest). No other paths or tools.
"""
import argparse
import hashlib
import io
import json
import platform
from fractions import Fraction as Fr
from pathlib import Path

import fontTools
import pypdf
from fontTools.fontBuilder import FontBuilder
from fontTools.pens.ttGlyphPen import TTGlyphPen
from pypdf import PdfWriter
from pypdf.generic import (ArrayObject as A, BooleanObject, DecodedStreamObject as Stream, DictionaryObject as D,
                           FloatObject as F, NameObject as N, NumberObject as I, TextStringObject as T)

PAGE_W, PAGE_H = 160, 100
WIDGET_X, WIDGET_Y = 40, 30          # lower-left of the physical widget Rect on every fit page
UPM, ASC, DESC, GAP = 1000, 800, -200, 100
INSET = Fr(1)
FLOOR = Fr(6)
# gid: (character, advance in font units, ink box xMin,yMin,xMax,yMax or None)
GLYPHS = {
    0: (None, 1000, None),
    1: ("B", 1000, (0, DESC, 1000, ASC)),       # ink fills its whole advance and the whole line box
    2: (" ", 500, None),
    3: ("J", 1000, (-100, DESC, 900, ASC)),     # negative left bearing: ink starts 0.1 em before the origin
    4: (" ", 500, None),
}
CHAR_TO_GID = {c: g for g, (c, _, _) in GLYPHS.items() if c}


def fmt(x):
    """Exact decimal text for a Fraction with at most six decimals."""
    n = Fr(x) * 10**6
    assert n.denominator == 1, f"not a six-decimal value: {x}"
    n = int(n)
    sign = "-" if n < 0 else ""
    whole, frac = divmod(abs(n), 10**6)
    return f"{sign}{whole}" + (("." + f"{frac:06d}".rstrip("0")) if frac else "")


def build_font():
    fb = FontBuilder(UPM, isTTF=True)
    fb.font.recalcTimestamp = False
    order = [".notdef", "B", "space", "J", "nbsp"]
    fb.setupGlyphOrder(order)
    fb.setupCharacterMap({0x42: "B", 0x20: "space", 0x4A: "J", 0xA0: "nbsp"})
    glyphs, metrics = {}, {}
    for gid, name in enumerate(order):
        _, adv, box = GLYPHS[gid]
        pen = TTGlyphPen(None)
        if box:
            x0, y0, x1, y1 = box
            pen.moveTo((x0, y0)); pen.lineTo((x0, y1)); pen.lineTo((x1, y1)); pen.lineTo((x1, y0)); pen.closePath()
        glyphs[name] = pen.glyph()
        metrics[name] = (adv, box[0] if box else 0)
    fb.setupGlyf(glyphs)
    fb.setupHorizontalMetrics(metrics)
    fb.setupHorizontalHeader(ascent=ASC, descent=DESC, lineGap=GAP)
    fb.setupNameTable({"familyName": "ExactWidthBox", "styleName": "Regular"})
    fb.setupOS2(sTypoAscender=ASC, sTypoDescender=DESC, sTypoLineGap=GAP, usWinAscent=ASC, usWinDescent=-DESC)
    fb.setupPost()
    fb.setupHead(unitsPerEm=UPM, created=2082844800, modified=2082844800)
    buf = io.BytesIO()
    fb.save(buf)
    return buf.getvalue()


def to_unicode_cmap():
    rows = "".join(f"<{g:04X}> <{ord(c):04X}>\n" for g, (c, _, _) in GLYPHS.items() if c)
    n = sum(1 for _, (c, _, _) in GLYPHS.items() if c)
    return ("/CIDInit /ProcSet findresource begin 12 dict begin begincmap\n"
            "/CIDSystemInfo << /Registry (Adobe) /Ordering (UCS) /Supplement 0 >> def\n"
            "/CMapName /Adobe-Identity-UCS def /CMapType 2 def\n"
            "1 begincodespacerange <0000> <FFFF> endcodespacerange\n"
            f"{n} beginbfchar\n{rows}endbfchar\nendcmap CMapName currentdict /CMap defineresource pop end end\n")


# ---------------------------------------------------------------- the proposed profile, as arithmetic
def content_box(dw, dh, style, bw, rule="half"):
    """Display-space content box (left, bottom, right, top). rule "half" is R1's wording (1 pt + half the stroke
    width); rule "full" is the candidate revision (1 pt + the whole painted stroke width)."""
    f = bw / 2 if rule == "half" else bw
    side = INSET + (f if style in ("solid", "dashed") else Fr(0))
    bottom = INSET + (f if style in ("solid", "dashed", "underline") else Fr(0))
    return side, bottom, dw - side, dh - side


def line_advance(line, size):
    return sum(Fr(GLYPHS[CHAR_TO_GID[c]][1]) for c in line) * size / 1000


def layout(case):
    """Display-space lines: origin, advance, baseline; mirrors the proposed R1 wording."""
    dw, dh, size = case["dw"], case["dh"], case["size"]
    left, bottom, right, top = content_box(dw, dh, case["style"], case["bw"], case.get("rule", "half"))
    pitch = Fr(ASC - DESC + GAP) * size / 1000
    out = []
    for k, text in enumerate(case["text"].split("\n")):
        adv = line_advance(text, size)
        if case["align"] == 0:
            x = left
        elif case["align"] == 1:
            x = left + ((right - left) - adv) / 2
        else:
            x = right - adv
        out.append({"text": text, "x": x, "advance": adv, "baseline": top - Fr(ASC) * size / 1000 - k * pitch})
    return (left, bottom, right, top), out


def evaluate(case):
    """Return (fit, first failing gate) by the proposed gates, in exact arithmetic."""
    (left, bottom, right, top), lines = layout(case)
    size = case["size"]
    if right <= left or top <= bottom:
        return False, "content"
    for ln in lines:
        if ln["text"] and (ln["x"] < left or ln["x"] + ln["advance"] > right):
            return False, "advance"
    for ln in lines:
        if ln["baseline"] + Fr(DESC) * size / 1000 < bottom or ln["baseline"] + Fr(ASC) * size / 1000 > top:
            return False, "line_box"
    for ln in lines:                                   # glyph ink at emitted positions
        x = ln["x"]
        for c in ln["text"]:
            _, adv, box = GLYPHS[CHAR_TO_GID[c]]
            if box:
                if x + Fr(box[0]) * size / 1000 < left or x + Fr(box[2]) * size / 1000 > right:
                    return False, "ink"
                if ln["baseline"] + Fr(box[1]) * size / 1000 < bottom or ln["baseline"] + Fr(box[3]) * size / 1000 > top:
                    return False, "ink"
            x += Fr(adv) * size / 1000
    inked = [ln for ln in lines if any(GLYPHS[CHAR_TO_GID[c]][2] for c in ln["text"])]
    for a, b in zip(lines, lines[1:]):                 # adjacent-line ink must not overlap
        if a in inked and b in inked and b["baseline"] + Fr(ASC) * size / 1000 > a["baseline"] + Fr(DESC) * size / 1000:
            return False, "overlap"
    return True, None


def search_size(case, maximum):
    """Largest size in whole thousandths from 6.000 through floor(maximum) that passes `evaluate`; None if none."""
    top = int(maximum * 1000)                         # maximum rounded down to the grid
    lo, hi, best = int(FLOOR * 1000), top, None
    while lo <= hi:                                   # fit is monotone in size for these single-line cases
        mid = (lo + hi) // 2
        if evaluate({**case, "size": Fr(mid, 1000)})[0]:
            best, lo = mid, mid + 1
        else:
            hi = mid - 1
    return None if best is None else Fr(best, 1000)


# ---------------------------------------------------------------- cases
CASES = []


def add(group, name, intent, text, size, dw, dh, style="none", bw="0", align=0, rot=0, stroke="inside",
        search_max=None, rule="half", note=""):
    case = dict(group=group, name=name, intent=intent, text=text, size=Fr(size), dw=Fr(dw), dh=Fr(dh), style=style,
                bw=Fr(bw) if style != "none" else Fr(0), align=align, rot=rot, stroke=stroke, rule=rule, note=note)
    if search_max is not None:
        case["search_max"] = Fr(search_max)
    fit, gate = evaluate(case)
    expected = "fit" if fit else f"refuse:{gate}"
    assert intent == expected, f"{name}: hand-stated {intent} but arithmetic says {expected}"
    CASES.append(case)


def below_at_above(group, tag, base, edge, key, text, size, **kw):
    """Three cases at edge-0.001, edge, edge+0.001 of dimension `key` ('dw' or 'dh')."""
    e = Fr(edge)
    for d in (Fr(-1, 1000), Fr(0), Fr(1, 1000)):
        v = e + d
        dims = dict(base)
        dims[key] = v
        add(group, f"{tag}_{fmt(v)}", "fit" if d >= 0 else f"refuse:{'advance' if key == 'dw' else 'line_box'}",
            text, size, dims["dw"], dims["dh"], **kw)


def build_cases():
    # 1. width boundary: solid 1-pt border -> inset 1.5 each side; BBB at 12 pt = 36.000 -> dw 39.000
    below_at_above("width", "w_solid1", dict(dh=18), "39", "dw", "BBB", "12", style="solid", bw="1")
    # 2. height boundary: borderless, one line: 12 pt line box + 2 = 14.000
    below_at_above("height", "h_one", dict(dw=60), "14", "dh", "BBB", "12")
    # 3. two lines around an explicit blank line: 12 + 2*13.2 + 2 = 40.4
    below_at_above("height", "h_blank", dict(dw=30), "40.4", "dh", "B\n\nB", "12")
    # 4. edge/repeated spaces: " B  B " advance 48.000 -> dw 50.000; spaces have no ink so only the advance gate sees them
    below_at_above("spaces", "sp_left", dict(dh=20), "50", "dw", " B  B ", "12")
    for al, tag in ((1, "center"), (2, "right")):
        below_at_above("spaces", f"sp_{tag}", dict(dh=20), "50", "dw", " B  B ", "12", align=al)
    # 5. NBSP is an advance: B NBSP B = 30.000 -> dw 32.000
    below_at_above("spaces", "nbsp", dict(dh=20), "32", "dw", "B B", "12")
    # 6. rotation at the width boundary (display box 39.000 x 18, solid 1)
    for r in (90, 270):
        below_at_above("rotation", f"rot{r}", dict(dh=18), "39", "dw", "BBB", "12", style="solid", bw="1", rot=r)
    add("rotation", "rot180_39.000", "fit", "BBB", "12", "39", "18", style="solid", bw="1", rot=180)
    add("rotation", "rot0_explicit_39.000", "fit", "BBB", "12", "39", "18", style="solid", bw="1", rot=0)
    add("rotation", "rot_absent_39.000", "fit", "BBB", "12", "39", "18", style="solid", bw="1", rot=None)
    # 7. border styles and widths, ink exactly at the content edge (the proposed inset leaves 1 + bw/2 per side)
    for style in ("none", "solid", "dashed", "underline"):
        for bw in (("0",) if style == "none" else ("1", "2", "3")):
            half = Fr(bw) / 2 if style in ("solid", "dashed", "underline") else Fr(0)
            side = 1 + (half if style in ("solid", "dashed") else 0)
            bottom = 1 + (half if style in ("solid", "dashed", "underline") else 0)
            add("border", f"b_{style}{bw if style != 'none' else ''}_at", "fit", "BBB", "12", 36 + 2 * side,
                12 + side + bottom, style=style, bw=bw)      # tight in both axes: ink touches all four content edges
    for bw in ("1", "2"):                              # stroke centred on the widget edge (half clipped by /BBox)
        add("border", f"b_solid{bw}_centered_at", "fit", "BBB", "12", 36 + 2 * (1 + Fr(bw) / 2), 12 + 2 * (1 + Fr(bw) / 2),
            style="solid", bw=bw, stroke="centered")
    # 7b. candidate revision: reserve 1 pt beyond the whole painted stroke (ink-to-stroke clearance is then 1 pt for any width)
    for style in ("solid", "dashed", "underline"):
        for bw in ("1", "2", "3"):
            f = Fr(bw)
            side = 1 + (f if style in ("solid", "dashed") else 0)
            bottom = 1 + f
            add("border", f"bf_{style}{bw}_at", "fit", "BBB", "12", 36 + 2 * side, 12 + side + bottom, style=style, bw=bw, rule="full")
    below_at_above("width", "wf_solid2", dict(dh=18), "42", "dw", "BBB", "12", style="solid", bw="2", rule="full")
    # 8. DA-zero search and the 24-point ceiling: borderless BBB, content width 80 -> 26.666 fits, 26.667 does not
    for s, intent in (("26.665", "fit"), ("26.666", "fit"), ("26.667", "refuse:advance")):
        add("size", f"z_borderless_{s}", intent, "BBB", s, "82", "44", note="above 24 pt; search maximum 35", search_max="35")
    add("size", "z_max20_20.000", "fit", "BBB", "20", "82", "44", note="caller maximum 20 caps the search", search_max="20")
    # 9. 6-point floor: solid 1 -> 6 pt needs 21.000
    add("size", "z_floor_20.999_6.000", "refuse:advance", "BBB", "6", "20.999", "18", style="solid", bw="1", search_max="12",
        note="search refuses; painted at 6.000 as contrast")
    add("size", "z_floor_21.000_6.000", "fit", "BBB", "6", "21", "18", style="solid", bw="1", search_max="12")
    add("size", "z_floor_21.001_6.000", "fit", "BBB", "6", "21.001", "18", style="solid", bw="1", search_max="12")
    add("size", "z_floor_22.500_6.500", "fit", "BBB", "6.5", "22.5", "18", style="solid", bw="1", search_max="12",
        note="grid size 6.500 in 22.500")
    # 10. bearings: J has ink 1.2 pt before its origin at 12 pt; advance fits, ink does not
    add("ink", "j_left_negative_bearing", "refuse:ink", "J", "12", "39", "18", style="solid", bw="1",
        note="ink starts 0.3 pt from the widget edge and overlaps a 1-pt inside stroke")
    add("ink", "j_right_control", "fit", "J", "12", "39", "18", style="solid", bw="1", align=2)


def ap_stream(case):
    """The appearance stream: border in display coordinates, then one Tm/Tj per non-blank line."""
    dw, dh, bw = case["dw"], case["dh"], case["bw"]
    _, lines = layout(case)
    out = ["q"]
    rot = case["rot"] or 0
    pw, ph = (dh, dw) if rot in (90, 270) else (dw, dh)
    out.append({0: "", 90: f"0 1 -1 0 {fmt(pw)} 0 cm", 180: f"-1 0 0 -1 {fmt(pw)} {fmt(ph)} cm",
                270: f"0 -1 1 0 0 {fmt(ph)} cm"}[rot])
    if case["style"] != "none":
        out.append("1 0 0 RG")
        if case["style"] == "dashed":
            out.append("[3] 0 d")
        out.append(f"{fmt(bw)} w")
        h = bw / 2
        if case["style"] == "underline":
            out.append(f"0 {fmt(h)} m {fmt(dw)} {fmt(h)} l S")
        elif case["stroke"] == "centered":
            out.append(f"0 0 {fmt(dw)} {fmt(dh)} re S")
        else:
            out.append(f"{fmt(h)} {fmt(h)} {fmt(dw - bw)} {fmt(dh - bw)} re S")
    out.append("0 g")
    for ln in lines:
        if ln["text"]:
            hexed = "".join(f"{CHAR_TO_GID[c]:04X}" for c in ln["text"])
            out.append(f"BT /F1 {fmt(case['size'])} Tf 1 0 0 1 {fmt(ln['x'])} {fmt(ln['baseline'])} Tm <{hexed}> Tj ET")
    out.append("Q")
    return "\n".join(s for s in out if s) + "\n"


def make_writer(font_bytes):
    w = PdfWriter()
    add_obj = w._add_object
    stream = lambda data, extra=None: add_obj(_stream(data, extra))  # noqa: E731
    widths = [GLYPHS[g][1] for g in sorted(GLYPHS)]
    descriptor = add_obj(D({N("/Type"): N("/FontDescriptor"), N("/FontName"): N("/ExactWidthBox"), N("/Flags"): I(4),
                            N("/FontBBox"): A([I(-100), I(DESC), I(1000), I(ASC)]), N("/ItalicAngle"): I(0),
                            N("/Ascent"): I(ASC), N("/Descent"): I(DESC), N("/CapHeight"): I(700), N("/StemV"): I(80),
                            N("/FontFile2"): stream(font_bytes, {N("/Length1"): I(len(font_bytes))})}))
    cid = add_obj(D({N("/Type"): N("/Font"), N("/Subtype"): N("/CIDFontType2"), N("/BaseFont"): N("/ExactWidthBox"),
                     N("/CIDSystemInfo"): D({N("/Registry"): T("Adobe"), N("/Ordering"): T("Identity"), N("/Supplement"): I(0)}),
                     N("/FontDescriptor"): descriptor, N("/CIDToGIDMap"): N("/Identity"), N("/DW"): I(1000),
                     N("/W"): A([I(0), A([I(x) for x in widths])])}))
    font = add_obj(D({N("/Type"): N("/Font"), N("/Subtype"): N("/Type0"), N("/BaseFont"): N("/ExactWidthBox"),
                      N("/Encoding"): N("/Identity-H"), N("/DescendantFonts"): A([cid]),
                      N("/ToUnicode"): stream(to_unicode_cmap().encode("ascii"))}))
    return w, font


def _stream(data, extra=None):
    s = Stream()
    s.set_data(data)
    if extra:
        s.update(extra)
    return s


def num(x):
    x = Fr(x)
    return I(int(x)) if x.denominator == 1 else F(fmt(x))


def write_fit_pdf(path, font_bytes):
    w, font = make_writer(font_bytes)
    refs = []
    for case in CASES:
        rot = case["rot"] or 0
        pw, ph = (case["dh"], case["dw"]) if rot in (90, 270) else (case["dw"], case["dh"])
        page = w.add_blank_page(width=PAGE_W, height=PAGE_H)
        ap = _stream(ap_stream(case).encode("ascii"), {
            N("/Type"): N("/XObject"), N("/Subtype"): N("/Form"),
            N("/BBox"): A([I(0), I(0), num(pw), num(ph)]), N("/Resources"): D({N("/Font"): D({N("/F1"): font})})})
        ann = D({N("/Type"): N("/Annot"), N("/Subtype"): N("/Widget"), N("/FT"): N("/Tx"), N("/T"): T(case["name"]),
                 N("/V"): T(case["text"]), N("/F"): I(4), N("/Q"): I(case["align"]),
                 N("/DA"): T(f"/F1 {fmt(case['size'])} Tf 0 g"),
                 N("/Rect"): A([I(WIDGET_X), I(WIDGET_Y), num(WIDGET_X + pw), num(WIDGET_Y + ph)]),
                 N("/AP"): D({N("/N"): w._add_object(ap)})})
        if case["style"] != "none":
            ann[N("/BS")] = D({N("/W"): num(case["bw"]),
                               N("/S"): N({"solid": "/S", "dashed": "/D", "underline": "/U"}[case["style"]])})
            ann[N("/MK")] = D({N("/BC"): A([I(1), I(0), I(0)])})
        if case["rot"] is not None:
            ann.setdefault(N("/MK"), D())[N("/R")] = I(case["rot"])
        ref = w._add_object(ann)
        refs.append(ref)
        page[N("/Annots")] = A([ref])
    w._root_object[N("/AcroForm")] = w._add_object(D({
        N("/Fields"): A(refs), N("/NeedAppearances"): BooleanObject(False),
        N("/DR"): D({N("/Font"): D({N("/F1"): font})}), N("/DA"): T("/F1 0 Tf 0 g")}))
    w.add_metadata({"/Title": "W-218 exact-width R1 fixture"})
    w.write(path)


# ---------------------------------------------------------------- precision probe
PRECISION_DELTAS_UPT = (-2000, -1000, -500, 0, 500, 1000, 2000)       # millionths of a point
PRECISION_DH = Fr(40)
BAR_Y = (5, 15)                                                        # widget-local bar y range
BAR_THICKNESS = Fr(1, 2)


def write_precision_pdf(path, font_bytes):
    """Per delta: BBB at 12 pt (ink right edge at widget-local 37.000) plus a vector bar whose left edge is at
    37.000 + delta. Both are in one appearance, so one raster shows whether a renderer separates them."""
    w, font = make_writer(font_bytes)
    refs = []
    for d in PRECISION_DELTAS_UPT:
        dw = Fr(38) + Fr(d, 10**6)
        case = dict(text="BBB", size=Fr(12), dw=dw, dh=PRECISION_DH, style="none", bw=Fr(0), align=0, rot=0, stroke="inside")
        _, lines = layout(case)
        bar_x = dw - INSET
        stream = (f"q 0 g BT /F1 12 Tf 1 0 0 1 {fmt(lines[0]['x'])} {fmt(lines[0]['baseline'])} Tm <000100010001> Tj ET\n"
                  f"{fmt(bar_x)} {BAR_Y[0]} {fmt(BAR_THICKNESS)} {BAR_Y[1] - BAR_Y[0]} re f Q\n")
        page = w.add_blank_page(width=PAGE_W, height=PAGE_H)
        ap = _stream(stream.encode("ascii"), {
            N("/Type"): N("/XObject"), N("/Subtype"): N("/Form"),
            N("/BBox"): A([I(0), I(0), num(dw + BAR_THICKNESS), num(PRECISION_DH)]),   # wide enough for the bar
            N("/Resources"): D({N("/Font"): D({N("/F1"): font})})})
        ann = D({N("/Type"): N("/Annot"), N("/Subtype"): N("/Widget"), N("/FT"): N("/Tx"), N("/T"): T(f"p{d}"),
                 N("/V"): T("BBB"), N("/F"): I(4), N("/DA"): T("/F1 12 Tf 0 g"),
                 N("/Rect"): A([I(WIDGET_X), I(WIDGET_Y), num(WIDGET_X + dw + BAR_THICKNESS), num(WIDGET_Y + PRECISION_DH)]),
                 N("/AP"): D({N("/N"): w._add_object(ap)})})
        ref = w._add_object(ann)
        refs.append(ref)
        page[N("/Annots")] = A([ref])
    w._root_object[N("/AcroForm")] = w._add_object(D({
        N("/Fields"): A(refs), N("/DR"): D({N("/Font"): D({N("/F1"): font})}), N("/DA"): T("/F1 0 Tf 0 g")}))
    w.write(path)


def sha256(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def main():
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--out", default=str(Path(__file__).resolve().parent))
    out = Path(ap.parse_args().out)
    out.mkdir(parents=True, exist_ok=True)
    build_cases()
    font_bytes = build_font()
    fit, prec = out / "exact-width-fit.pdf", out / "exact-width-precision.pdf"
    write_fit_pdf(fit, font_bytes)
    write_precision_pdf(prec, font_bytes)
    searches = []
    for c in CASES:
        if "search_max" in c:
            found = search_size(c, c["search_max"])
            searches.append({"case": c["name"], "maximum": fmt(c["search_max"]), "selected": None if found is None else fmt(found)})
    manifest = {
        "purpose": "W-218 R1 exact-width research fixture; not jaytwo.Pdf output, not target-library evidence",
        "versions": {"python": platform.python_version(), "pypdf": pypdf.__version__, "fontTools": fontTools.version},
        "font": {"unitsPerEm": UPM, "hhea": [ASC, DESC, GAP], "sha256": hashlib.sha256(font_bytes).hexdigest(),
                 "glyphs": {str(g): {"char": ord(c) if c else None, "advance": a, "ink": b} for g, (c, a, b) in GLYPHS.items()}},
        "files": {"exact-width-fit.pdf": sha256(fit), "exact-width-precision.pdf": sha256(prec)},
        "page": [PAGE_W, PAGE_H], "widgetOrigin": [WIDGET_X, WIDGET_Y],
        "precisionDeltasMicroPoints": list(PRECISION_DELTAS_UPT),
        "daZeroSearches": searches,
        "cases": [{**{k: (fmt(v) if isinstance(v, Fr) else v) for k, v in c.items()}, "page": i + 1}
                  for i, c in enumerate(CASES)],
    }
    (out / "exact-width-manifest.json").write_text(json.dumps(manifest, indent=1, ensure_ascii=False) + "\n", encoding="utf-8")
    print(f"{len(CASES)} fit cases, {len(PRECISION_DELTAS_UPT)} precision probes")
    for name, digest in manifest["files"].items():
        print(f"{digest}  {name}")
    print(f"{manifest['font']['sha256']}  embedded font program")
    for s in searches:
        print("search", s)


if __name__ == "__main__":
    main()
