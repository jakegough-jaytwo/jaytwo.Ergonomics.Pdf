"""Render the W-218 beveled/inset fixture in Poppler, MuPDF and PDFium and measure what they paint.

Same renderer settings as exact_width_render.py (576 dpi = 8 px/pt; PDFium with init_forms and form drawing).
  * bevel-inset-measure.pdf: per physical side, ink edge (partial coverage of black), painted footprint depth
    (summed saturation of the ring/band palette along the middle 40 % of the side) and ink-to-footprint clearance;
    plus which physical sides are painted light (green) versus dark (blue).
  * bevel-inset-fit.pdf: sampled colour at the middle of the ring and of the band on each physical side.
  * bevel-inset-regen.pdf (no /AP, NeedAppearances true): run-length colour profile from each physical widget
    edge inward along the side's mid-line, i.e. what each renderer's own appearance generator paints. PDFium is
    rendered twice: with init_forms (form-filler path) and without (core path).
Raster readings are good to roughly 0.1 pt; exact geometry is bevel_inset_check.py's job.

Usage: python bevel_inset_render.py [--dir DIR] [--pdftoppm PATH] [--dpi 576]   (writes bevel-inset-render.json)
"""
import argparse
import json
import platform
import shutil
import sys
import tempfile
from pathlib import Path

import numpy as np
import pymupdf
import pypdfium2 as pdfium

from exact_width_render import PAGE_H, ink_cov, poppler_version, render_all

SIDES = ("left", "bottom", "right", "top")


def sat(img):
    f = img.astype(np.float64)
    return (f.max(axis=2) - f.min(axis=2)) / 255.0


def side_strip(arr, s, rect, side, depth_pt):
    """Pixels of the middle 40 % of a physical side, `depth_pt` deep, oriented so axis 1 runs inward."""
    x0, y0, x1, y1 = rect
    cx0, cx1 = int((x0 + 0.3 * (x1 - x0)) * s), int((x0 + 0.7 * (x1 - x0)) * s)
    ry0, ry1 = int((PAGE_H - y1 + 0.3 * (y1 - y0)) * s), int((PAGE_H - y1 + 0.7 * (y1 - y0)) * s)
    d = int(depth_pt * s)
    if side == "left":
        return arr[ry0:ry1, int(x0 * s):int(x0 * s) + d]
    if side == "right":
        return arr[ry0:ry1, int(x1 * s) - d:int(x1 * s)][:, ::-1]
    if side == "top":
        r = int((PAGE_H - y1) * s)
        return np.swapaxes(arr[r:r + d, cx0:cx1], 0, 1)
    r = int((PAGE_H - y0) * s)
    return np.swapaxes(arr[r - d:r, cx0:cx1][::-1], 0, 1)


def physical_rect(case):
    pw, ph = [float(v) for v in case["display_box"]]
    if case["rot"] in (90, 270):
        pw, ph = ph, pw
    return 40.0, 30.0, 40.0 + pw, 30.0 + ph


def measure_page(img, case, s):
    rect = physical_rect(case)
    fp = float(case["footprint"])
    row = {"footprint_measured_pt": {}, "ink_depth_measured_pt": {}, "clearance_measured_pt": {}, "shade": {}}
    sv, ic = sat(img), ink_cov(img)
    for side in SIDES:
        # Measured in the mid-side strip only: an anti-aliased blend of two palette colours (mitred corners, or a
        # ring/band boundary off the pixel grid) is dark but not black, so ink is searched for only from the
        # visible footprint's inner edge inward. Where ink overlaps the band (negative controls) the band is
        # hidden and the visible footprint reads short; clearance then reads 0, and the overlap shows as ink
        # depth below the nominal footprint.
        depth = float(np.median(side_strip(sv, s, rect, side, fp + 0.6).sum(axis=1)) / s)
        prof = np.median(side_strip(ic, s, rect, side, fp + 3), axis=0)
        start = max(int(depth * s) - 2, 0)
        k = start + int(np.argmax(prof[start:] > 0.05))
        ink_depth = (k + 1 - min(float(prof[k]), 1.0)) / s
        row["footprint_measured_pt"][side] = depth
        row["ink_depth_measured_pt"][side] = ink_depth
        row["clearance_measured_pt"][side] = ink_depth - depth
        band = side_strip(img, s, rect, side, fp).reshape(-1, 3).astype(int)
        g, b = int(((band[:, 1] > 200) & (band[:, 0] < 60) & (band[:, 2] < 60)).sum()), int(((band[:, 2] > 200) & (band[:, 0] < 60) & (band[:, 1] < 60)).sum())
        row["shade"][side] = "light" if g > b else "dark" if b > g else "none"
    return row


def sample_colours(img, case, s):
    rect = physical_rect(case)
    w = float(case["bw"])
    ring_mid, band_mid = (w / 2, 1.5 * w) if case["construction"] == "ring_band" else (None, w / 2)
    out = {}
    for side in SIDES:
        strip = side_strip(img, s, rect, side, float(case["footprint"]) + 0.5)
        col = lambda at: [round(float(v) / 255, 3) for v in np.median(strip[:, int(at * s)], axis=0)]  # noqa: E731
        out[side] = {"ring": col(ring_mid) if ring_mid is not None else None, "band": col(band_mid)}
    return out


def runs(img, case, s, max_pt=12.0):
    """Colour runs from each physical edge inward: [[r,g,b], width_pt] with near-equal colours merged."""
    x0, y0 = 40.0, 30.0
    x1, y1 = x0 + float(case["w"]), y0 + float(case["h"])
    rot = case["rot"] or 0
    out = {}
    for side in SIDES:
        depth = min(max_pt, ((y1 - y0) if side in ("bottom", "top") else (x1 - x0)) / 2)
        strip = side_strip(img, s, (x0, y0, x1, y1), side, depth)
        line = np.median(strip, axis=0).astype(int)
        seq = []
        for px in line:
            if seq and np.abs(np.array(seq[-1][0]) - px).max() <= 10:
                seq[-1][1] += 1
            else:
                seq.append([px.tolist(), 1])
        out[side] = [[[round(c / 255, 2) for c in col], n / s] for col, n in seq if n >= 2]
    return {"rotation": rot, "runs": out}


def main():
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--dir", default=str(Path(__file__).resolve().parent))
    ap.add_argument("--pdftoppm", default=shutil.which("pdftoppm"))
    ap.add_argument("--dpi", type=int, default=576)
    a = ap.parse_args()
    if not a.pdftoppm:
        sys.exit("pdftoppm not found: pass --pdftoppm PATH")
    d, s = Path(a.dir), a.dpi / 72
    manifest = json.loads((d / "bevel-inset-manifest.json").read_text(encoding="utf-8"))
    cases, regen = manifest["cases"], manifest["regen"]
    result = {"platform": platform.platform(), "dpi": a.dpi, "fixture_sha256": manifest["files"],
              "renderers": {"poppler": poppler_version(a.pdftoppm),
                            "mupdf": f"PyMuPDF {pymupdf.version[0]} / MuPDF {pymupdf.version[1]}",
                            "pdfium": f"PDFium {pdfium.PDFIUM_INFO.build} via pypdfium2 {pdfium.version.PYPDFIUM_INFO.version}"},
              "measure": {}, "colours": {}, "regen": {}, "fit_vs_measure_ink_edge_pt": {}}
    with tempfile.TemporaryDirectory() as tmp:
        for name in ("poppler", "mupdf", "pdfium"):
            imgs = {}
            for f in ("measure", "fit"):
                sub = Path(tmp) / f"{name}-{f}"
                sub.mkdir()
                imgs[f] = render_all(name, d / f"bevel-inset-{f}.pdf", a.dpi, a.pdftoppm, sub)
            for c, im, fit in zip(cases, imgs["measure"], imgs["fit"]):
                result["measure"].setdefault(c["name"], {})[name] = measure_page(im, c, s)
                result["colours"].setdefault(c["name"], {})[name] = sample_colours(fit, c, s)
            variants = [(name, True)] + ([("pdfium_noforms", False)] if name == "pdfium" else [])
            for label, forms in variants:
                sub = Path(tmp) / f"{label}-regen"
                sub.mkdir()
                for c, im in zip(regen, render_all(name, d / "bevel-inset-regen.pdf", a.dpi, a.pdftoppm, sub, forms=forms)):
                    result["regen"].setdefault(c["name"], {})[label] = runs(im, c, s)
    for c in cases:
        m = result["measure"][c["name"]]
        m["cross_renderer_spread_pt"] = {
            "footprint": max(max(m[r]["footprint_measured_pt"].values()) for r in m) - min(min(m[r]["footprint_measured_pt"].values()) for r in m),
            "clearance": max(max(m[r]["clearance_measured_pt"].values()) for r in m) - min(min(m[r]["clearance_measured_pt"].values()) for r in m)}
    (d / "bevel-inset-render.json").write_text(json.dumps(result, indent=1) + "\n", encoding="utf-8")
    print("wrote bevel-inset-render.json")


if __name__ == "__main__":
    main()
