"""Render the W-218 R1 exact-width fixture in Poppler, MuPDF and PDFium and measure visible placement.

This reports what renderers show. It cannot establish the proposed 0.001-point acceptance threshold: that is
exact arithmetic (see exact_width_check.py). Raster measurements here are bounded by pixel size and anti-aliasing.

Usage:
  python exact_width_render.py [--dir DIR] [--pdftoppm PATH] [--dpi 576]
Needs: pymupdf, pypdfium2, numpy, Pillow, and a Poppler `pdftoppm` (PATH or --pdftoppm). Writes
exact-width-render.json. Run exact_width_fixture.py and exact_width_check.py first. Intermediate PNGs go to a
temporary directory that is deleted afterwards.
"""
import argparse
import json
import platform
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

import numpy as np
import pymupdf
import pypdfium2 as pdfium
from PIL import Image

PAGE_W, PAGE_H = 160, 100
PRECISION_WINDOW = (76.6, 77.4)      # page x of the high-resolution window around widget-local 37.000
PRECISION_TOP_COORD_ROWS = (31, 65)  # page rows (top-origin) covering the ink band and the bar band
INK_ROWS_TOP = (32, 42)              # ink band, top-origin pt (page y 58..68)
BAR_ROWS_TOP = (56, 64)              # bar band, top-origin pt (page y 36..44)


def poppler_version(exe):
    p = subprocess.run([exe, "-v"], capture_output=True, text=True)
    return (p.stderr or p.stdout).splitlines()[0].strip()


def render_all(renderer, pdf, dpi, exe, tmp, forms=True):
    """Return list of HxWx3 uint8 arrays, one per page."""
    s = dpi / 72
    if renderer == "poppler":
        prefix = Path(tmp) / "pp"
        subprocess.run([exe, "-r", str(dpi), "-png", str(pdf), str(prefix)], check=True)
        files = sorted(Path(tmp).glob("pp-*.png"))
        return [np.array(Image.open(f).convert("RGB")) for f in files]
    if renderer == "mupdf":
        doc = pymupdf.open(str(pdf))
        return [np.frombuffer(px.samples, dtype=np.uint8).reshape(px.h, px.w, 3)
                for px in (p.get_pixmap(matrix=pymupdf.Matrix(s, s), alpha=False, annots=True) for p in doc)]
    doc = pdfium.PdfDocument(str(pdf))
    if forms:
        doc.init_forms()
    return [np.array(doc[i].render(scale=s, may_draw_forms=forms, draw_annots=True).to_pil().convert("RGB"))
            for i in range(len(doc))]


def ink_cov(img):
    return 1.0 - img.max(axis=2).astype(np.float64) / 255.0


def red_cov(img):
    f = img.astype(np.float64)
    return (f[..., 0] - np.maximum(f[..., 1], f[..., 2])) / 255.0


def edges(cov, s, thr=0.05):
    """Sub-pixel ink bbox in page points: (left, bottom, right, top). Straight edges: partial coverage of the
    outermost row/column gives the sub-pixel offset."""
    cols, rows = cov.max(axis=0), cov.max(axis=1)
    ci, ri = np.where(cols > thr)[0], np.where(rows > thr)[0]
    if len(ci) == 0:
        return None
    c0, c1, r0, r1 = ci[0], ci[-1], ri[0], ri[-1]
    left = (c0 + 1 - min(cols[c0], 1)) / s
    right = (c1 + min(cols[c1], 1)) / s
    top = (r0 + 1 - min(rows[r0], 1)) / s
    bottom = (r1 + min(rows[r1], 1)) / s
    return left, PAGE_H - bottom, right, PAGE_H - top


def stroke_width_left(red, s, rect, bw):
    """Mean horizontal red run on rows well inside the vertical span (best row, so dashed gaps do not bias it)."""
    x0, y0, x1, y1 = rect
    r0, r1 = int((PAGE_H - y1 + 2 * bw + 1) * s), int((PAGE_H - y0 - 2 * bw - 1) * s)
    c0, c1 = int((x0 - 1) * s), int((x0 + 2 * bw + 1) * s)
    if r1 <= r0:
        return None
    band = red[r0:r1, c0:c1].sum(axis=1) / s
    return float(band.max())


def signed_clearance(side, ink_edge, inner_ref):
    return {"left": ink_edge - inner_ref, "right": inner_ref - ink_edge,
            "bottom": ink_edge - inner_ref, "top": inner_ref - ink_edge}[side]


def measure_case(img, case, s):
    cov = ink_cov(img)
    got = edges(cov, s)
    left, bottom, right, top = got
    x0, y0, x1, y1 = case["widget_rect_page"]
    row = {"ink_left": left, "ink_bottom": bottom, "ink_right": right, "ink_top": top,
           "ink_area_ratio": float(cov.sum() / s**2 / case["ink_area"])}
    exp = case["ink_box_page"]
    row["edge_error_pt"] = {k: v - e for k, v, e in zip(("left", "bottom", "right", "top"), (left, bottom, right, top), exp)}
    # measured and predicted clearance from the inner border edge (or widget edge where no border is painted)
    cb = case["content_box_page"]
    content_edge = {"left": cb[0], "bottom": cb[1], "right": cb[2], "top": cb[3]}
    widget_edge = {"left": x0, "bottom": y0, "right": x1, "top": y1}
    meas, pred = {}, {}
    for side, ink in (("left", left), ("bottom", bottom), ("right", right), ("top", top)):
        nom = case["nominal_clearance_pt"][side]
        inner = widget_edge[side] if nom is None else (content_edge[side] - nom if side in ("left", "bottom") else content_edge[side] + nom)
        if nom is None and side in ("left", "bottom"):
            inner = widget_edge[side]
        expected_ink = exp[("left", "bottom", "right", "top").index(side)]
        meas[side] = signed_clearance(side, ink, inner)
        pred[side] = signed_clearance(side, expected_ink, inner)
    row["clearance_measured_pt"], row["clearance_predicted_pt"] = meas, pred
    if case["style"] in ("solid", "dashed"):
        w = stroke_width_left(red_cov(img), s, case["widget_rect_page"], case["border_width"])
        row["left_stroke_width_pt"] = w
    return row


def pair_diff(a, b):
    d = np.abs(a.astype(np.int16) - b.astype(np.int16))
    return {"differing_pixels": int((d.max(axis=2) > 0).sum()), "max_channel_difference": int(d.max())}


def triplets(cases):
    groups = {}
    for c in cases:
        prefix, _, value = c["name"].rpartition("_")
        try:
            float(value)
        except ValueError:
            continue
        groups.setdefault(prefix, []).append(c)
    return {p: g for p, g in groups.items() if len(g) == 3 and p[:2] in ("w_", "wf", "h_", "sp", "nb", "ro", "z_")}


def precision(renderer, pdf, deltas_micro, dpi_scale, exe, tmp):
    """High-resolution ink-edge versus vector-bar probe (1000 px per point)."""
    x0, x1 = PRECISION_WINDOW
    yt, yb = PRECISION_TOP_COORD_ROWS
    s = dpi_scale
    rows_out = []
    for i, d in enumerate(deltas_micro):
        if renderer == "poppler":
            out = Path(tmp) / "hi"
            subprocess.run([exe, "-f", str(i + 1), "-l", str(i + 1), "-r", str(int(s * 72)), "-x", str(int(x0 * s)), "-y", str(int(yt * s)),
                            "-W", str(int((x1 - x0) * s)), "-H", str(int((yb - yt) * s)), "-gray", "-png", "-singlefile",
                            str(pdf), str(out)], check=True)
            g = np.array(Image.open(str(out) + ".png").convert("L"))
        elif renderer == "mupdf":
            doc = pymupdf.open(str(pdf))
            px = doc[i].get_pixmap(matrix=pymupdf.Matrix(s, s), clip=pymupdf.Rect(x0, yt, x1, yb), colorspace=pymupdf.csGRAY,
                                   annots=True)
            g = np.frombuffer(px.samples, dtype=np.uint8).reshape(px.h, px.w)
        else:
            doc = pdfium.PdfDocument(str(pdf))
            doc.init_forms()
            g = doc[i].render(scale=s, crop=(x0, PAGE_H - yb, PAGE_W - x1, yt), may_draw_forms=True, grayscale=True).to_numpy().squeeze()
        cov = (255 - g.astype(np.float64)) / 255.0
        r_ink = slice(int((INK_ROWS_TOP[0] - yt) * s) + 2, int((INK_ROWS_TOP[1] - yt) * s) - 2)
        r_bar = slice(int((BAR_ROWS_TOP[0] - yt) * s) + 2, int((BAR_ROWS_TOP[1] - yt) * s) - 2)
        ink_edge = x0 + cov[r_ink].mean(axis=0).sum() / s              # ink on the left, blank to the right
        bar_edge = x1 - cov[r_bar].mean(axis=0).sum() / s              # blank on the left, bar to the right
        expect_ink, expect_bar = 77.0, 77.0 + d / 1e6
        rows_out.append({"delta_microPoints": d, "ink_edge_error_pt": ink_edge - expect_ink, "bar_edge_error_pt": bar_edge - expect_bar,
                         "separation_error_pt": (bar_edge - ink_edge) - (d / 1e6), "measured_separation_pt": bar_edge - ink_edge})
    return rows_out


def main():
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--dir", default=str(Path(__file__).resolve().parent))
    ap.add_argument("--pdftoppm", default=shutil.which("pdftoppm"))
    ap.add_argument("--dpi", type=int, default=576)
    ap.add_argument("--skip-precision", action="store_true")
    a = ap.parse_args()
    d = Path(a.dir)
    if not a.pdftoppm:
        sys.exit("pdftoppm not found: pass --pdftoppm PATH")
    s = a.dpi / 72
    structural = json.loads((d / "exact-width-structural.json").read_text(encoding="utf-8"))
    manifest = json.loads((d / "exact-width-manifest.json").read_text(encoding="utf-8"))
    cases = structural["cases"]
    result = {
        "platform": platform.platform(), "dpi": a.dpi, "pixels_per_point": s, "fixture_sha256": manifest["files"],
        "renderers": {
            "poppler": {"version": poppler_version(a.pdftoppm), "command": f"pdftoppm -r {a.dpi} -png <pdf> <prefix>  (defaults: font and vector antialiasing on)"},
            "mupdf": {"version": f"PyMuPDF {pymupdf.version[0]} / MuPDF {pymupdf.version[1]}",
                      "command": f"page.get_pixmap(matrix=Matrix({s:g},{s:g}), alpha=False, annots=True)  (default AA level)"},
            "pdfium": {"version": f"PDFium {pdfium.PDFIUM_INFO.build} via pypdfium2 {pdfium.version.PYPDFIUM_INFO.version}",
                       "command": f"doc.init_forms(); page.render(scale={s:g}, may_draw_forms=True, draw_annots=True)"},
        },
        "cases": {c["name"]: {} for c in cases}, "pairs": {}, "pdfium_forms_off_vs_on": {}, "absent_vs_zero_rotation": {},
        "precision": {},
    }
    with tempfile.TemporaryDirectory() as tmp:
        pdf = d / "exact-width-fit.pdf"
        images = {}
        for name in ("poppler", "mupdf", "pdfium"):
            sub = Path(tmp) / name
            sub.mkdir()
            images[name] = render_all(name, pdf, a.dpi, a.pdftoppm, sub)
            assert len(images[name]) == len(cases), (name, len(images[name]))
            for c, img in zip(cases, images[name]):
                result["cases"][c["name"]][name] = measure_case(img, c, s)
        annots_only = render_all("pdfium", pdf, a.dpi, a.pdftoppm, tmp, forms=False)
        by_name = {c["name"]: i for i, c in enumerate(cases)}
        for c, img in zip(cases, annots_only):
            result["pdfium_forms_off_vs_on"][c["name"]] = pair_diff(img, images["pdfium"][by_name[c["name"]]])
        for name in images:
            result["absent_vs_zero_rotation"][name] = pair_diff(images[name][by_name["rot_absent_39.000"]],
                                                                images[name][by_name["rot0_explicit_39.000"]])
            for prefix, grp in triplets(cases).items():
                below, at, above = (images[name][by_name[g["name"]]] for g in grp)
                result["pairs"].setdefault(prefix, {})[name] = {"below_vs_at": pair_diff(below, at), "at_vs_above": pair_diff(at, above)}
        if not a.skip_precision:
            for name in images:
                sub = Path(tmp) / (name + "_hi")
                sub.mkdir()
                result["precision"][name] = precision(name, d / "exact-width-precision.pdf", manifest["precisionDeltasMicroPoints"], 1000.0,
                                                      a.pdftoppm, sub)
    # cross-renderer disagreement per case: spread of each measured ink edge among the three renderers
    for c in cases:
        m = result["cases"][c["name"]]
        spread = {k: max(m[r][f"ink_{k}"] for r in m) - min(m[r][f"ink_{k}"] for r in m) for k in ("left", "bottom", "right", "top")}
        m["cross_renderer_edge_spread_pt"] = spread
    (d / "exact-width-render.json").write_text(json.dumps(result, indent=1) + "\n", encoding="utf-8")
    print("wrote exact-width-render.json")


if __name__ == "__main__":
    main()
