"""Structural check of the W-218 beveled/inset fixture, derived from the PDF bytes (exact arithmetic).

For every page of bevel-inset-fit.pdf and bevel-inset-measure.pdf: re-read /Rect, /BS, /MK and the appearance
stream; interpret its fills (even-odd for f*, which equals nonzero for the simple polygons used); intersect every
non-background painted path with the mid-line of each display side to get the painted footprint depth on that
side; position the text by the embedded font's /W and glyph boxes; and require
  * the footprint depth equals the manifest's construction (2*W ring_band, W band_only) on all four sides,
  * the ink touches the content box on all four sides, and
  * ink-to-footprint clearance equals the case's nominal clearance (1 pt under the candidate rule).
Also confirms the /BBox and the explicit rotation `cm` match /MK/R (absent /MK/R -> 0).

Usage: python bevel_inset_check.py [--dir DIR]   (writes bevel-inset-structural.json; exit 1 on any failure)
"""
import argparse
import io
import json
import re
import sys
from fractions import Fraction as Fr
from pathlib import Path

from fontTools.ttLib import TTFont
from pypdf import PdfReader

TOKEN = re.compile(r"<[0-9A-Fa-f]*>|/[^\s/<>\[\]()]+|[^\s/<>\[\]()]+")
ROT = {0: (1, 0, 0, 1), 90: (0, 1, -1, 0), 180: (-1, 0, 0, -1), 270: (0, -1, 1, 0)}


def exact(v):
    return Fr(repr(float(v))).limit_denominator(10**6)


def interpret(text):
    """Return (cm or None, [fill paths: (colour, [polygons])], [(size, x, y, gids)])."""
    stack, cm, colour, path, cur, fills, texts, size = [], None, None, [], [], [], [], None
    for t in TOKEN.findall(text):
        if t.startswith("<") or t.startswith("/") or re.fullmatch(r"-?\d+(\.\d+)?", t):
            stack.append(t)
            continue
        args = [Fr(x) for x in stack if not x.startswith(("/", "<"))]
        if t == "cm":
            cm = tuple(args[-6:])
        elif t in ("g", "rg"):
            colour = tuple(args[-(1 if t == "g" else 3):])
        elif t == "re":
            x, y, w, h = args[-4:]
            path.append([(x, y), (x, y + h), (x + w, y + h), (x + w, y)])
        elif t == "m":
            cur = [tuple(args[-2:])]
        elif t == "l":
            cur.append(tuple(args[-2:]))
        elif t == "h":
            path.append(cur)
            cur = []
        elif t in ("f", "f*"):
            fills.append((colour, path))
            path = []
        elif t == "Tf":
            size = args[-1]
        elif t == "Tm":
            tx, ty = args[-2:]
        elif t == "Tj":
            hexs = stack[-1][1:-1]
            texts.append((size, tx, ty, [int(hexs[i:i + 4], 16) for i in range(0, len(hexs), 4)]))
        elif t not in ("q", "Q", "BT", "ET"):
            raise ValueError(f"unexpected operator {t}")
        stack = []
    return cm, fills, texts


def crossings(polys, axis, at):
    """Even-odd intervals where the path covers the line {axis coordinate == at}; axis 'y' = horizontal line."""
    xs = []
    for p in polys:
        for (x0, y0), (x1, y1) in zip(p, p[1:] + p[:1]):
            a0, a1, b0, b1 = (y0, y1, x0, x1) if axis == "y" else (x0, x1, y0, y1)
            if (a0 <= at < a1) or (a1 <= at < a0):
                xs.append(b0 + (at - a0) * (b1 - b0) / (a1 - a0))
    xs.sort()
    return list(zip(xs[0::2], xs[1::2]))


def footprint_depths(fills, bg, dw, dh):
    """Depth of contiguous paint from each display edge along that side's mid-line."""
    horiz, vert = [], []
    for colour, polys in fills:
        if bg is not None and colour == bg:
            continue
        horiz += crossings(polys, "y", dh / 2)
        vert += crossings(polys, "x", dw / 2)

    def depth(intervals, start, inward):
        reach, changed = start, True
        while changed:
            changed = False
            for lo, hi in intervals:
                if inward > 0 and lo <= reach < hi:
                    reach, changed = hi, True
                if inward < 0 and lo < reach <= hi:
                    reach, changed = lo, True
        return abs(reach - start)

    return {"left": depth(horiz, Fr(0), 1), "right": depth(horiz, dw, -1),
            "bottom": depth(vert, Fr(0), 1), "top": depth(vert, dh, -1)}


def check_pdf(path, cases, measure):
    reader = PdfReader(str(path))
    rows, fails = [], 0
    for case, page in zip(cases, reader.pages):
        a = page["/Annots"][0].get_object()
        rect = [exact(v) for v in a["/Rect"]]
        pw, ph = rect[2] - rect[0], rect[3] - rect[1]
        mk = a.get("/MK", {})
        rot = int(mk["/R"]) if "/R" in mk else 0
        dw, dh = (ph, pw) if rot in (90, 270) else (pw, ph)
        bw = exact(a["/BS"]["/W"])
        style = str(a["/BS"]["/S"])[1:]
        bg = tuple(exact(v) for v in mk["/BG"]) if "/BG" in mk else None
        ap = a["/AP"]["/N"].get_object()
        bbox = [exact(v) for v in ap["/BBox"]]
        font = ap["/Resources"]["/Font"]["/F1"].get_object()
        cid = font["/DescendantFonts"][0].get_object()
        widths = {i: int(x) for i, x in enumerate(cid["/W"][1])}
        tt = TTFont(io.BytesIO(cid["/FontDescriptor"]["/FontFile2"].get_object().get_data()))
        order, glyf, upm = tt.getGlyphOrder(), tt["glyf"], tt["head"].unitsPerEm
        cm, fills, texts = interpret(ap.get_data().decode("ascii"))
        errors = []
        if bbox != [0, 0, pw, ph]:
            errors.append("bbox")
        want_cm = None if rot == 0 else ROT[rot] + {90: (pw, 0), 180: (pw, ph), 270: (0, ph)}[rot]
        if cm != want_cm:
            errors.append("cm")
        depth = footprint_depths(fills, None if measure else bg, dw, dh)
        want_fp = Fr(case["footprint"])
        if any(v != want_fp for v in depth.values()):
            errors.append(f"footprint {depth}")
        size, x, y, gids = texts[0]
        ink = [None] * 4
        for g in gids:
            gl = glyf[order[g]]
            if gl.numberOfContours:
                box = (x + Fr(gl.xMin) * size / upm, y + Fr(gl.yMin) * size / upm,
                       x + Fr(gl.xMax) * size / upm, y + Fr(gl.yMax) * size / upm)
                ink = [box[i] if ink[i] is None else (min if i < 2 else max)(ink[i], box[i]) for i in range(4)]
            x += Fr(widths[g]) * size / 1000
        content = [Fr(v) for v in case["content_box"]]
        if ink != content:
            errors.append(f"ink {ink} != content {content}")
        clearance = {"left": ink[0] - depth["left"], "bottom": ink[1] - depth["bottom"],
                     "right": dw - depth["right"] - ink[2], "top": dh - depth["top"] - ink[3]}
        nominal = Fr(case["nominal_clearance"])
        if any(v != nominal for v in clearance.values()):
            errors.append(f"clearance {clearance}")
        if style != case["style"] or bw != Fr(case["bw"]):
            errors.append("BS")
        fails += bool(errors)
        rows.append({"name": case["name"], "rotation": rot, "rotation_entry": "R" in [k[1:] for k in mk],
                     "footprint_depth_pt": {k: str(v) for k, v in depth.items()},
                     "clearance_pt": {k: str(v) for k, v in clearance.items()}, "nominal_pt": str(nominal),
                     "ok": not errors, "errors": errors})
    return rows, fails


def main():
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--dir", default=str(Path(__file__).resolve().parent))
    d = Path(ap.parse_args().dir)
    manifest = json.loads((d / "bevel-inset-manifest.json").read_text(encoding="utf-8"))
    cases = manifest["cases"]
    out, total = {"files": manifest["files"]}, 0
    for name, measure in (("bevel-inset-fit.pdf", False), ("bevel-inset-measure.pdf", True)):
        rows, fails = check_pdf(d / name, cases, measure)
        out[name] = rows
        total += fails
        print(f"{name}: {len(rows) - fails}/{len(rows)} pages pass")
        for r in rows:
            if not r["ok"]:
                print("FAIL", r["name"], r["errors"])
    (d / "bevel-inset-structural.json").write_text(json.dumps(out, indent=1) + "\n", encoding="utf-8")
    sys.exit(1 if total else 0)


if __name__ == "__main__":
    main()
