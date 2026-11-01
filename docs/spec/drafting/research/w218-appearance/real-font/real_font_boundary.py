"""Real-font both-sides boundary arithmetic for the proposed (unadopted) R1 profile.

Independent arithmetic only: no renderer, no target library. Advances are the integer
/W widths a PDF serializes (round(hmtx*1000/upm)); they are cross-checked against the
/W array in the PDFs written by ../generate.py when those PDFs are present. Ink comes
from unhinted outlines. All containment uses exact Fractions; ink is rounded outward to
0.001 pt.
Usage: python real_font_boundary.py [out.json]
"""
import json
import sys
import hashlib
from fractions import Fraction as Fr
from math import floor, ceil
from pathlib import Path
from fontTools.ttLib import TTFont
from fontTools.pens.boundsPen import BoundsPen

HERE = Path(__file__).resolve().parent
FONTS = {"Arial": Path("C:/Windows/Fonts/arial.ttf"),
         "TimesItalic": Path("C:/Windows/Fonts/timesi.ttf")}
INSET = Fr(1)
G = 1000  # grid: thousandths of a point


class Font:
    def __init__(self, path):
        self.sha256 = hashlib.sha256(path.read_bytes()).hexdigest()
        ft = TTFont(path)
        self.upm = ft['head'].unitsPerEm
        self.asc, self.desc, self.gap = ft['hhea'].ascent, ft['hhea'].descent, ft['hhea'].lineGap
        self.cmap = ft.getBestCmap()
        self.hmtx = ft['hmtx'].metrics
        self.gs = ft.getGlyphSet()
        self.order = ft.getGlyphOrder()
        self.ink = {}

    def pdf_w(self, ch):
        """Emitted /W width, integer thousandths of an em."""
        return round(self.hmtx[self.cmap[ord(ch)]][0] * 1000 / self.upm)

    def bounds(self, ch):
        if ch not in self.ink:
            pen = BoundsPen(self.gs)
            self.gs[self.cmap[ord(ch)]].draw(pen)
            self.ink[ch] = pen.bounds
        return self.ink[ch]


def layout(f, line, size):
    """Emitted advance and ink extents of one line, baseline at 0, left origin at 0."""
    x = Fr(0)
    xmin = xmax = ymin = ymax = None
    for ch in line:
        b = f.bounds(ch)
        if b:
            lo, hi = x + Fr(b[0]) * size / f.upm, x + Fr(b[2]) * size / f.upm
            xmin = lo if xmin is None else min(xmin, lo)
            xmax = hi if xmax is None else max(xmax, hi)
            yl, yh = Fr(b[1]) * size / f.upm, Fr(b[3]) * size / f.upm
            ymin = yl if ymin is None else min(ymin, yl)
            ymax = yh if ymax is None else max(ymax, yh)
        x += Fr(f.pdf_w(ch)) * size / 1000
    return x, xmin, xmax, ymin, ymax


def out_floor(v):
    return Fr(floor(v * G), G)


def out_ceil(v):
    return Fr(ceil(v * G), G)


def fit(f, text, size, width, height, border, align):
    """(True, 'fits') if the whole value fits the proposed profile, else (False, reason)."""
    pad = INSET + border
    cw, ch = Fr(width) - 2 * pad, Fr(height) - 2 * pad
    if cw <= 0 or ch <= 0:
        return False, "nonpositive content box"
    s = Fr(size)
    asc = Fr(f.asc) * s / f.upm
    desc = Fr(f.desc) * s / f.upm
    step = Fr(f.asc - f.desc + f.gap) * s / f.upm
    inks = []
    for i, line in enumerate(text.split("\n")):
        adv, xmin, xmax, ymin, ymax = layout(f, line, s)
        if adv > cw:
            return False, "advance of line %d exceeds content width" % i
        off = {"left": Fr(0), "center": (cw - adv) / 2, "right": cw - adv}[align]
        if xmin is not None:
            if out_floor(off + xmin) < 0:
                return False, "ink of line %d left of content box" % i
            if out_ceil(off + xmax) > cw:
                return False, "ink of line %d right of content box" % i
        base = -asc - i * step  # measured down from content top
        if base + desc < -ch:
            return False, "line box %d below content bottom" % i
        if ymin is not None:
            if out_ceil(base + ymax) > 0:
                return False, "ink of line %d above content top" % i
            if out_floor(base + ymin) < -ch:
                return False, "ink of line %d below content bottom" % i
            inks.append((base + ymin, base + ymax))
        else:
            inks.append(None)
    for a, b in zip(inks, inks[1:]):
        if a and b and out_floor(a[0]) < out_ceil(b[1]):
            return False, "adjacent line ink overlaps"
    return True, "fits"


def threshold(pred, lo, hi):
    """Smallest integer thousandth where pred is true, plus a +-40 monotonicity scan."""
    ok, why = pred(hi)
    if not ok:
        return None, why
    a, b = lo, hi
    while a < b:
        mid = (a + b) // 2
        if pred(mid)[0]:
            b = mid
        else:
            a = mid + 1
    mono = all(pred(t)[0] == (t >= a) for t in range(max(1, a - 40), a + 41))
    return (a, mono), None


CASES = [  # font, text, size, align, border width, note
    ("Arial", "WWW", 12, "left", 1, "three W"),
    ("Arial", "WWW", 12, "left", 0, "three W borderless"),
    ("Arial", "jAy", 12, "center", 1, "bearings, centre"),
    ("Arial", "gyp", 12, "right", 1, "descenders, right"),
    ("Arial", " W  i ", 12, "left", 0, "edge/repeated spaces"),
    ("Arial", "W\u00a0i", 12, "left", 0, "NBSP"),
    ("Arial", "Wgj\n\nWgj", 12, "left", 0, "blank middle line"),
    ("Arial", "Wgj ill", 28, "left", 0, "size above 24"),
    ("TimesItalic", "WWW", 12, "left", 1, "three W"),
    ("TimesItalic", "jAy", 12, "left", 1, "negative left bearing"),
    ("TimesItalic", "jAy", 12, "center", 1, "centre"),
    ("TimesItalic", "fff", 12, "left", 0, "right overhang"),
    ("TimesItalic", "fff", 12, "right", 0, "right overhang, right align"),
    ("TimesItalic", "Wgj\n\nWgj", 12, "left", 0, "blank middle line"),
]


def axis(f, text, size, border, align, which):
    big = 200
    if which == "width":
        def pred(t):
            return fit(f, text, size, Fr(t, G), big, border, align)
    else:
        def pred(t):
            return fit(f, text, size, big, Fr(t, G), border, align)
    res, why = threshold(pred, 1, big * G)
    if not res:
        return {which + "_refused_at_any_size": why}
    t, mono = res
    return {"min_%s_thousandths" % which: t, which + "_monotonic_pm40": mono,
            which + "_below": {"pt": (t - 1) / G, "verdict": pred(t - 1)[1]},
            which + "_at": {"pt": t / G, "verdict": pred(t)[1]}}


def run():
    fonts = {k: Font(p) for k, p in FONTS.items()}
    rows = []
    for fk, text, size, align, border, note in CASES:
        f = fonts[fk]
        row = {"font": fk, "text": text, "size_pt": size, "align": align,
               "border_pt": border, "note": note,
               "emitted_advance_pt": [float(layout(f, l, Fr(size))[0]) for l in text.split("\n")]}
        row.update(axis(f, text, size, Fr(border), align, "width"))
        row.update(axis(f, text, size, Fr(border), align, "height"))
        rows.append(row)
    return {"fonts": {k: {"sha256": v.sha256, "upm": v.upm, "hhea": [v.asc, v.desc, v.gap]}
                      for k, v in fonts.items()},
            "profile": "proposed R1: 1 pt inset + whole painted stroke, hhea line box, "
                       "emitted /W advances, outline ink, 0.001 pt outward rounding; "
                       "exact Fractions; NOT adopted",
            "rows": rows}


def crosscheck_pdf():
    """Compare this script's /W values with the /W array serialized by ../generate.py."""
    from pypdf import PdfReader
    out = {}
    for name, path in FONTS.items():
        pdf = HERE.parent / (name + ".pdf")
        if not pdf.exists():
            out[name] = "PDF absent; widths recomputed with generate.py rounding only"
            continue
        w = None
        for pg in PdfReader(pdf).pages:
            for a in pg.get("/Annots", []):
                ap = a.get_object().get("/AP", {}).get("/N")
                res = ap.get_object().get("/Resources", {}) if ap else {}
                for fo in res.get("/Font", {}).values():
                    w = fo.get_object()["/DescendantFonts"][0].get_object()["/W"]
                    break
                if w:
                    break
            if w:
                break
        f = Font(path)
        arr = [int(x) for x in w[1]]
        chars = [c for c in " WijAgypfl\u00a0" if ord(c) in f.cmap]
        bad = [c for c in chars if arr[f.order.index(f.cmap[ord(c)])] != f.pdf_w(c)]
        out[name] = {"checked_chars": len(chars), "mismatched": bad, "w_entries": len(arr)}
    return out


if __name__ == "__main__":
    doc = run()
    doc["pdf_w_crosscheck"] = crosscheck_pdf()
    text = json.dumps(doc, indent=1, ensure_ascii=False)
    if len(sys.argv) > 1:
        Path(sys.argv[1]).write_text(text + "\n", encoding="utf-8")
    else:
        print(text)
