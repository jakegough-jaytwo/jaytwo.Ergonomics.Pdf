"""Structural check of the W-218 R1 exact-width fixture, derived from the PDF bytes, not from generator constants.

For every fit page it re-reads the widget dictionary, embedded font program, /W array, appearance stream and
ToUnicode map, then verifies that the serialized widths, text positions, border and content box obey the
proposed R1 profile, and that the fit/refuse verdict equals the case's intended verdict. Arithmetic is exact
(fractions.Fraction); final ink bounds are rounded outward to 0.001 pt before containment, as R1 proposes.
Only the case names/intents/search maxima are taken from the manifest; no geometry is.

Usage: python exact_width_check.py [--dir DIR]   (writes exact-width-structural.json; exit 1 on any failure)
"""
import argparse
import io
import json
import re
import sys
from fractions import Fraction as Fr
from math import ceil, floor
from pathlib import Path

import pypdf
from fontTools.ttLib import TTFont
from pypdf import PdfReader

THOUSANDTH = Fr(1, 1000)
TOKEN = re.compile(r"<[0-9A-Fa-f]*>|/[^\s/<>\[\]()]+|\[|\]|[^\s/<>\[\]()]+")


def exact(v):
    return Fr(repr(float(v))).limit_denominator(10**6)


def out_lo(x):
    return Fr(floor(x / THOUSANDTH), 1000)


def out_hi(x):
    return Fr(ceil(x / THOUSANDTH), 1000)


class FontModel:
    def __init__(self, font_dict):
        cid = font_dict["/DescendantFonts"][0].get_object()
        self.dw = int(cid["/DW"])
        w = cid["/W"]
        first, arr = int(w[0]), [int(x) for x in w[1]]
        self.w = {first + i: x for i, x in enumerate(arr)}
        data = cid["/FontDescriptor"]["/FontFile2"].get_object().get_data()
        self.tt = TTFont(io.BytesIO(data))
        self.upm = self.tt["head"].unitsPerEm
        self.asc, self.desc, self.gap = (self.tt["hhea"].ascent, self.tt["hhea"].descent, self.tt["hhea"].lineGap)
        self.order = self.tt.getGlyphOrder()
        self.glyf = self.tt["glyf"]
        self.hmtx = self.tt["hmtx"].metrics
        cmap_text = font_dict["/ToUnicode"].get_object().get_data().decode("ascii")
        self.uni = {int(a, 16): chr(int(b, 16)) for a, b in re.findall(r"<([0-9A-F]{4})> <([0-9A-F]{4})>", cmap_text)}

    def width(self, gid):
        return self.w.get(gid, self.dw)

    def ink(self, gid):
        g = self.glyf[self.order[gid]]
        return None if g.numberOfContours == 0 else (g.xMin, g.yMin, g.xMax, g.yMax)

    def width_matches_font(self):
        return all(self.width(g) * self.upm == self.hmtx[n][0] * 1000 for g, n in enumerate(self.order))


def interpret(stream_text):
    """Tiny interpreter for exactly the operators the fixture emits; anything else is an error."""
    ctm, stack, ops, text, path, state = (1, 0, 0, 1, 0, 0), [], [], [], [], {"w": None, "dash": None}
    toks = TOKEN.findall(stream_text)
    for t in toks:
        if t.startswith("<") or t.startswith("/") or t in "[]" or re.fullmatch(r"-?\d+(\.\d+)?", t):
            stack.append(t)
            continue
        if t == "cm":
            ctm = tuple(Fr(x) for x in stack[-6:])
        elif t == "w":
            state["w"] = Fr(stack[-1])
        elif t == "d":
            state["dash"] = [Fr(x) for x in stack[stack.index("[") + 1:stack.index("]")]]
        elif t == "re":
            path = [("re", *[Fr(x) for x in stack[-4:]])]
        elif t == "m":
            path = [("m", Fr(stack[-2]), Fr(stack[-1]))]
        elif t == "l":
            path.append(("l", Fr(stack[-2]), Fr(stack[-1])))
        elif t == "S":
            ops.append(("S", list(path), dict(state)))
        elif t == "Tm":
            state["tm"] = tuple(Fr(x) for x in stack[-6:])
        elif t == "Tf":
            state["tf"] = (stack[-2], Fr(stack[-1]))
        elif t == "Tj":
            text.append({"tm": state["tm"], "font": state["tf"], "hex": stack[-1][1:-1]})
        elif t in ("q", "Q", "BT", "ET", "RG", "g"):
            pass
        else:
            raise ValueError(f"unexpected operator {t}")
        stack = [] if t not in ("q", "Q", "BT", "ET") else stack
        if t in ("q", "Q", "BT", "ET"):
            stack = []
    return ctm, ops, text


def apply(ctm, x, y):
    a, b, c, d, e, f = ctm
    return a * x + c * y + e, b * x + d * y + f


def expected_ctm(rot, pw, ph):
    return {0: (1, 0, 0, 1, 0, 0), 90: (0, 1, -1, 0, pw, 0), 180: (-1, 0, 0, -1, pw, ph), 270: (0, -1, 1, 0, 0, ph)}[rot]


def norm(p, q):
    return min(p[0], q[0]), min(p[1], q[1]), max(p[0], q[0]), max(p[1], q[1])


def profile(dw, dh, style, bw, rule="half"):
    """Own implementation of the content-box rule (independent of the generator's). "half" is R1's wording;
    "full" is the candidate revision that reserves the whole painted stroke width."""
    f = bw / 2 if rule == "half" else bw
    side = 1 + (f if style in ("solid", "dashed") else 0)
    bottom = 1 + (f if style in ("solid", "dashed", "underline") else 0)
    return side, bottom, dw - side, dh - side


def fit_verdict(font, text, size, dw, dh, style, bw, align, rule="half"):
    """Gates in display space from parameters: advance, line box, ink, overlap. Returns (fit, gate, lines)."""
    left, bottom, right, top = profile(dw, dh, style, bw, rule)
    if right <= left or top <= bottom:
        return False, "content", []
    em = Fr(size) / font.upm
    pitch = (font.asc - font.desc + font.gap) * em
    lines = []
    for k, line in enumerate(text.split("\n")):
        gids = [next(g for g, c in font.uni.items() if c == ch) for ch in line]
        adv = sum(font.width(g) for g in gids) * size / 1000
        x = left if align == 0 else left + ((right - left) - adv) / 2 if align == 1 else right - adv
        lines.append(dict(k=k, text=line, gids=gids, x=x, adv=adv, base=top - font.asc * em - k * pitch))
    for ln in lines:
        if ln["text"] and (ln["x"] < left or ln["x"] + ln["adv"] > right):
            return False, "advance", lines
    for ln in lines:
        if ln["base"] + font.desc * em < bottom or ln["base"] + font.asc * em > top:
            return False, "line_box", lines
    for ln in lines:
        x = ln["x"]
        for g in ln["gids"]:
            box = font.ink(g)
            if box and (out_lo(x + box[0] * em) < left or out_hi(x + box[2] * em) > right or
                        out_lo(ln["base"] + box[1] * em) < bottom or out_hi(ln["base"] + box[3] * em) > top):
                return False, "ink", lines
            x += font.width(g) * size / 1000
    inked = [ln for ln in lines if any(font.ink(g) for g in ln["gids"])]
    for a, b in zip(lines, lines[1:]):
        if a in inked and b in inked and b["base"] + font.asc * em > a["base"] + font.desc * em:
            return False, "overlap", lines
    return True, None, lines


def da_zero_linear(font, text, maximum, dw, dh, style, bw, align, rule="half"):
    s = int(maximum * 1000)
    while s >= 6000:
        if fit_verdict(font, text, Fr(s, 1000), dw, dh, style, bw, align, rule)[0]:
            return Fr(s, 1000)
        s -= 1
    return None


def check_fit_page(font, page, manifest_case, searches):
    checks, notes = {}, []
    ann = page["/Annots"][0].get_object()
    rect = [exact(v) for v in ann["/Rect"]]
    ox, oy = rect[0], rect[1]
    pw, ph = rect[2] - rect[0], rect[3] - rect[1]
    mk = ann.get("/MK")
    rot = int(mk["/R"]) if mk is not None and "/R" in mk else 0
    bs = ann.get("/BS")
    style = {"/S": "solid", "/D": "dashed", "/U": "underline"}[bs["/S"]] if bs is not None else "none"
    bw = exact(bs["/W"]) if bs is not None else Fr(0)
    dw, dh = (ph, pw) if rot in (90, 270) else (pw, ph)
    align = int(ann.get("/Q", 0))
    value = str(ann["/V"])
    ap = ann["/AP"]["/N"].get_object()
    bbox = [exact(v) for v in ap["/BBox"]]
    checks["bbox_is_physical_widget"] = bbox == [0, 0, pw, ph]
    ctm, ops, text = interpret(ap.get_data().decode("ascii"))
    checks["ctm_matches_rotation"] = ctm == expected_ctm(rot, pw, ph)
    da_size = Fr(re.fullmatch(r"/F1 (\S+) Tf 0 g", str(ann["/DA"])).group(1))
    checks["tf_size_equals_da"] = all(t["font"][1] == da_size for t in text)
    size = da_size
    # emitted text equals /V (blank lines have no text object)
    emitted = [("".join(font.uni[int(t["hex"][i:i + 4], 16)] for i in range(0, len(t["hex"]), 4)), t) for t in text]
    nonblank = [(k, s) for k, s in enumerate(value.split("\n")) if s]
    checks["emitted_text_equals_value"] = [e for e, _ in emitted] == [s for _, s in nonblank]
    rule = manifest_case.get("rule", "half")
    fit, gate, lines = fit_verdict(font, value, size, dw, dh, style, bw, align, rule)
    # emitted positions equal the profile's positions, and advances come from the serialized /W
    want = {ln["k"]: ln for ln in lines if ln["text"]}
    pos_ok = len(emitted) == len(nonblank)
    if pos_ok:
        for (k, s), (_, t) in zip(nonblank, emitted):
            ln = want[k]
            tm = t["tm"]
            pos_ok &= tm[:4] == (1, 0, 0, 1) and tm[4] == ln["x"] and tm[5] == ln["base"]
    checks["emitted_positions_equal_profile"] = bool(pos_ok)
    left, bottom, right, top = profile(dw, dh, style, bw, rule)
    # physical content box and ink (page coordinates, ink rounded outward to 0.001 pt)
    cpx = [apply(ctm, x, y) for x, y in ((left, bottom), (right, top))]
    cb = norm(*cpx)
    content_phys = [cb[0] + ox, cb[1] + oy, cb[2] + ox, cb[3] + oy]
    em = size / font.upm
    ink_boxes, ink_area = [], Fr(0)
    for ln in want.values():
        x = ln["x"]
        for g in ln["gids"]:
            box = font.ink(g)
            if box:
                p = apply(ctm, x + box[0] * em, ln["base"] + box[1] * em)
                q = apply(ctm, x + box[2] * em, ln["base"] + box[3] * em)
                n = norm(p, q)
                ink_boxes.append(n)
                ink_area += (n[2] - n[0]) * (n[3] - n[1])
            x += font.width(g) * size / 1000
    ink_phys = None
    if ink_boxes:
        ink_phys = [out_lo(min(b[0] for b in ink_boxes)) + ox, out_lo(min(b[1] for b in ink_boxes)) + oy,
                    out_hi(max(b[2] for b in ink_boxes)) + ox, out_hi(max(b[3] for b in ink_boxes)) + oy]
    # border: painted footprint after /BBox clipping, inner edges, nominal clearance to the content box
    inner_disp = {}
    if style != "none":
        (kind, *args), st = ops[0][1][0], ops[0][2]
        checks["border_width_matches_bs"] = st["w"] == bw and (st["dash"] == [3] if style == "dashed" else st["dash"] is None)
        h = bw / 2
        if kind == "re":
            x, y, w, hh = args
            inner_disp = {"left": x + h, "bottom": y + h, "right": x + w - h, "top": y + hh - h}
            checks["border_stroke_inside_or_centered"] = (x, y, w, hh) in ((h, h, dw - bw, dh - bw), (0, 0, dw, dh))
        else:
            (_, x0, y0), (_, x1, y1) = ops[0][1][0], ops[0][1][1]
            inner_disp = {"bottom": y0 + h}
            checks["border_stroke_inside_or_centered"] = (x0, y0, x1, y1) == (0, h, dw, h)
        clip_inner_shift = {k: v for k, v in inner_disp.items()}
        nominal = {"left": left - inner_disp["left"] if "left" in inner_disp else None,
                   "bottom": bottom - inner_disp["bottom"],
                   "right": inner_disp["right"] - right if "right" in inner_disp else None,
                   "top": inner_disp["top"] - top if "top" in inner_disp else None}
    else:
        checks["no_stroke_when_borderless"] = not ops
        nominal = {"left": left, "bottom": bottom, "right": dw - right, "top": dh - top}   # distance to widget edge
    # map display-side values to physical sides through the CTM
    side_phys = {}
    dsides = {"left": ((left, 0), (left, dh)), "right": ((right, 0), (right, dh)),
              "bottom": ((0, bottom), (dw, bottom)), "top": ((0, top), (dw, top))}
    for name, (p, q) in dsides.items():
        a, b = apply(ctm, *p), apply(ctm, *q)
        side_phys[name] = ("left" if a[0] < pw / 2 else "right") if a[0] == b[0] else ("bottom" if a[1] < ph / 2 else "top")
    nominal_phys = {side_phys[k]: (None if v is None else v) for k, v in nominal.items()}
    clearance = {k: (None if v is None else float(v)) for k, v in nominal_phys.items()}
    verdict = "fit" if fit else f"refuse:{gate}"
    checks["verdict_equals_intent"] = verdict == manifest_case["intent"]
    # DA-zero search: independent linear scan against the case's fixed maximum
    search = None
    if "search_max" in manifest_case:
        sel = da_zero_linear(font, value, Fr(manifest_case["search_max"]), dw, dh, style, bw, align, rule)
        recorded = next(s for s in searches if s["case"] == manifest_case["name"])["selected"]
        search = {"maximum": manifest_case["search_max"], "linear_scan": None if sel is None else str(float(sel)),
                  "manifest": recorded}
        checks["manifest_search_equals_linear_scan"] = (None if sel is None else sel) == (None if recorded is None else Fr(recorded))
    return dict(
        name=manifest_case["name"], page=manifest_case["page"], rule=rule, verdict=verdict, intent=manifest_case["intent"],
        rotation_degrees=rot, style=style, border_width=float(bw), size=float(size), display=[float(dw), float(dh)],
        widget_rect_page=[float(v) for v in rect],
        content_box_page=[float(v) for v in content_phys],
        ink_box_page=None if ink_phys is None else [float(v) for v in ink_phys], ink_area=float(ink_area),
        nominal_clearance_pt=clearance, search=search, checks=checks, ok=all(checks.values()))


def check_precision(reader, font, deltas):
    rows, ok = [], True
    for i, d in enumerate(deltas):
        ann = reader.pages[i]["/Annots"][0].get_object()
        ap = ann["/AP"]["/N"].get_object()
        text = ap.get_data().decode("ascii")
        tm = re.search(r"1 0 0 1 (\S+) (\S+) Tm <(\w+)> Tj", text)
        bar = re.search(r"(\S+) 5 0\.5 10 re f", text)
        ink_right = Fr(tm.group(1)) + 36
        gap = Fr(bar.group(1)) - ink_right
        # the bar left edge is 1 pt inside the right edge of the widget (R1 content-box right) by construction
        rect = [exact(v) for v in ann["/Rect"]]
        content_right = (rect[2] - rect[0] - Fr(1, 2)) - 1
        good = Fr(d, 10**6) == Fr(bar.group(1)) - 37 and gap == Fr(d, 10**6) and Fr(bar.group(1)) == content_right
        ok &= good
        rows.append({"delta_microPoints": d, "ink_right_local": float(ink_right), "bar_left_local": float(Fr(bar.group(1))),
                     "bar_minus_ink_pt": float(gap), "ok": good})
    return rows, ok


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--dir", default=str(Path(__file__).resolve().parent))
    d = Path(ap.parse_args().dir)
    manifest = json.loads((d / "exact-width-manifest.json").read_text(encoding="utf-8"))
    reader = PdfReader(str(d / "exact-width-fit.pdf"))
    font_dict = reader.pages[0]["/Annots"][0].get_object()["/AP"]["/N"]["/Resources"]["/Font"]["/F1"].get_object()
    font = FontModel(font_dict)
    results = {"pypdf": pypdf.__version__, "pages": len(reader.pages), "cases": []}
    results["font"] = {"unitsPerEm": font.upm, "hhea": [font.asc, font.desc, font.gap], "W_equals_hmtx_for_every_glyph": font.width_matches_font(),
                       "W_entries_all_integers": all(isinstance(v, int) for v in font.w.values())}
    assert len(reader.pages) == len(manifest["cases"])
    for case in manifest["cases"]:
        r = check_fit_page(font, reader.pages[case["page"] - 1], case, manifest["daZeroSearches"])
        results["cases"].append(r)
    prec_reader = PdfReader(str(d / "exact-width-precision.pdf"))
    results["precision_rows"], prec_ok = check_precision(prec_reader, font, manifest["precisionDeltasMicroPoints"])
    results["ok"] = results["font"]["W_equals_hmtx_for_every_glyph"] and results["font"]["W_entries_all_integers"] \
        and all(c["ok"] for c in results["cases"]) and prec_ok
    (d / "exact-width-structural.json").write_text(json.dumps(results, indent=1) + "\n", encoding="utf-8")
    bad = [c for c in results["cases"] if not c["ok"]]
    print(f"cases checked: {len(results['cases'])}; failing: {len(bad)}; precision rows ok: {prec_ok}; font /W == hmtx: "
          f"{results['font']['W_equals_hmtx_for_every_glyph']}")
    for c in bad:
        print("FAIL", c["name"], {k: v for k, v in c["checks"].items() if not v})
    for c in results["cases"]:
        print(f"{c['name']:34s} {c['verdict']:16s} size={c['size']:<7g} clearance={c['nominal_clearance_pt']}")
    return 0 if results["ok"] else 1


if __name__ == "__main__":
    sys.exit(main())
