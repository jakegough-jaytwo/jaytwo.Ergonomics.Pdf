"""Option (d) variant of real_font_boundary.py (W-218 owner decision 5, arithmetic only).

Option (d): emitted advances and hhea line boxes must lie in the content box (inset and
border excluded), but glyph ink need only stay inside the border's inner edge, i.e. ink
may use the 1-pt inset. Everything else (whole-stroke border, /W advances, unhinted
outline ink, exact Fractions, 0.001-pt outward rounding, adjacent-line ink check) is
unchanged from real_font_boundary.py, whose fit() is reproduced here with one change.
Also evaluates option (b) for comparison: ink must stay in the content box, but a left- or
right-aligned line is shifted inward by its ink overhang (rounded up to 0.001 pt). Also
reports, per line, ink overhang beyond the advance span and the font size at which
that overhang first exceeds the 1-pt inset (left/right alignment refuses above it).
No renderer, no target library; NOT adopted.
Usage: python real_font_boundary_d.py [out.json]
"""
import json
import sys
from fractions import Fraction as Fr
from pathlib import Path

import real_font_boundary as base
from real_font_boundary import Font, layout, out_floor, out_ceil, FONTS, INSET, G

INK_ALLOW = INSET  # option (d): ink may extend into the inset band, not past the border


def fit_d(f, text, size, width, height, border, align, ink_allow=INK_ALLOW, shift=False):
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
        if shift and xmin is not None:  # option (b): pull the line in by its edge overhang
            if align == "left":
                off = max(Fr(0), out_ceil(-xmin))
            elif align == "right":
                off = cw - max(adv, out_ceil(xmax))
            if off + adv > cw:
                return False, "shifted advance of line %d exceeds content width" % i
        if xmin is not None:
            if out_floor(off + xmin) < -ink_allow:
                return False, "ink of line %d left of border inner edge" % i
            if out_ceil(off + xmax) > cw + ink_allow:
                return False, "ink of line %d right of border inner edge" % i
        b = -asc - i * step
        if b + desc < -ch:
            return False, "line box %d below content bottom" % i
        if ymin is not None:
            if out_ceil(b + ymax) > ink_allow:
                return False, "ink of line %d above border inner edge" % i
            if out_floor(b + ymin) < -ch - ink_allow:
                return False, "ink of line %d below border inner edge" % i
            inks.append((b + ymin, b + ymax))
        else:
            inks.append(None)
    for a, b in zip(inks, inks[1:]):
        if a and b and out_floor(a[0]) < out_ceil(b[1]):
            return False, "adjacent line ink overlaps"
    return True, "fits"


def overhang(f, text):
    """Worst ink overhang beyond the advance span, in points per point of font size."""
    left = right = Fr(0)
    for line in text.split("\n"):
        adv, xmin, xmax, _, _ = layout(f, line, Fr(1))
        if xmin is not None:
            left, right = max(left, -xmin), max(right, xmax - adv)
    return left, right


def axis(fitfn, f, text, size, border, align, which):
    big = 200
    if which == "width":
        def pred(t):
            return fitfn(f, text, size, Fr(t, G), big, border, align)
    else:
        def pred(t):
            return fitfn(f, text, size, big, Fr(t, G), border, align)
    res, why = base.threshold(pred, 1, big * G)
    if not res:
        return {"refused_at_any_size": why}
    t, mono = res
    return {"min_thousandths": t, "monotonic_pm40": mono,
            "below": pred(t - 1)[1], "at": pred(t)[1]}


CASES = base.CASES + [
    ("Arial", "jAy", 12, "left", 1, "Arial negative left bearing"),
    ("TimesItalic", "Jeff", 12, "left", 0, "name, left"),
    ("TimesItalic", "Jeff", 12, "right", 0, "name, right"),
    ("TimesItalic", "fff", 24, "left", 0, "right overhang, 24 pt"),
    ("TimesItalic", "jAy", 24, "left", 1, "negative left bearing, 24 pt"),
]


def run():
    fonts = {k: Font(p) for k, p in FONTS.items()}
    rows = []
    for fk, text, size, align, border, note in CASES:
        f = fonts[fk]
        lo, ro = overhang(f, text)
        bind = {"left": lo, "right": ro, "center": None}[align]  # overhang pinned to the edge
        row = {"font": fk, "text": text, "size_pt": size, "align": align,
               "border_pt": border, "note": note,
               "overhang_left_em": float(lo), "overhang_right_em": float(ro),
               "overhang_left_pt": float(lo * size), "overhang_right_pt": float(ro * size)}
        if align in ("left", "right") and bind > 0:
            row["edge_overhang_exceeds_1pt_above_size_pt"] = float(INSET / bind)
        def fit_b(*a):
            return fit_d(*a, ink_allow=Fr(0), shift=True)
        for name, fn in (("current", base.fit), ("option_d", fit_d), ("option_b", fit_b)):
            row[name] = {"width": axis(fn, f, text, size, Fr(border), align, "width"),
                         "height": axis(fn, f, text, size, Fr(border), align, "height")}
        rows.append(row)
    return {"fonts": {k: {"sha256": v.sha256, "upm": v.upm, "hhea": [v.asc, v.desc, v.gap]}
                      for k, v in fonts.items()},
            "profile": "option (d): advances and hhea line boxes in the content box; ink "
                       "inside the border inner edge (may use the 1-pt inset); otherwise as "
                       "real_font_boundary.py; exact Fractions; NOT adopted",
            "rows": rows}


if __name__ == "__main__":
    text = json.dumps(run(), indent=1, ensure_ascii=False)
    if len(sys.argv) > 1:
        Path(sys.argv[1]).write_text(text + "\n", encoding="utf-8")
    else:
        print(text)
