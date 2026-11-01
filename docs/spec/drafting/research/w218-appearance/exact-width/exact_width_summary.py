"""Summarize exact-width-render.json and exact-width-structural.json (read-only; prints the tables quoted in the review).

Usage: python exact_width_summary.py [--dir DIR]
"""
import argparse
import json
from pathlib import Path

R = ("poppler", "mupdf", "pdfium")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--dir", default=str(Path(__file__).resolve().parent))
    d = Path(ap.parse_args().dir)
    rd = json.loads((d / "exact-width-render.json").read_text())
    st = json.loads((d / "exact-width-structural.json").read_text())
    px = 1 / rd["pixels_per_point"]
    print(f"render {rd['dpi']} dpi = {rd['pixels_per_point']:g} px/pt (1 px = {px:.4f} pt)")
    for r in R:
        print(f"  {r}: {rd['renderers'][r]['version']}")
    print("\n== Edge placement error (measured ink edge - exact expected), pt; max |error| over sides")
    print(f"{'case':30s} {'verdict':16s} " + " ".join(f"{r:>9s}" for r in R) + "   spread")
    worst = {r: 0.0 for r in R}
    for c in st["cases"]:
        m = rd["cases"][c["name"]]
        errs = [max(abs(v) for v in m[r]["edge_error_pt"].values()) for r in R]
        for r, e in zip(R, errs):
            worst[r] = max(worst[r], e)
        spread = max(m["cross_renderer_edge_spread_pt"].values())
        print(f"{c['name']:30s} {c['verdict']:16s} " + " ".join(f"{e:9.4f}" for e in errs) + f"   {spread:.4f}")
    print("worst |edge error| per renderer:", {r: round(v, 4) for r, v in worst.items()})
    print("\n== Ink clearance from the inner border edge (or widget edge), pt: predicted -> measured per renderer, min over painted sides")
    print(f"{'case':26s} {'style':9s} {'bw':>3s} {'predicted':>9s} " + " ".join(f"{r:>9s}" for r in R))
    for c in st["cases"]:
        if c["name"].startswith(("b_", "bf_", "j_", "w_solid1_39", "h_one_14")) and c["name"] not in ("w_solid1_39.001",):
            m = rd["cases"][c["name"]]
            sides = [k for k, v in c["nominal_clearance_pt"].items()]
            pred = min(m["poppler"]["clearance_predicted_pt"][k] for k in sides)
            meas = [min(m[r]["clearance_measured_pt"][k] for k in sides) for r in R]
            print(f"{c['name']:26s} {c['style']:9s} {c['border_width']:3g} {pred:9.3f} " + " ".join(f"{x:9.3f}" for x in meas))
    print("\n== Stroke calibration: left stroke width measured by renderer (pt) vs nominal width")
    for c in st["cases"]:
        if c["style"] in ("solid", "dashed") and c["name"].startswith(("b_", "bf_")):
            m = rd["cases"][c["name"]]
            print(f"{c['name']:26s} nominal {c['border_width']:g} " + " ".join(f"{r}={m[r]['left_stroke_width_pt']:.3f}" for r in R))
    print("\n== Ink area ratio (measured / exact), min..max over all cases")
    for r in R:
        v = [rd["cases"][c["name"]][r]["ink_area_ratio"] for c in st["cases"]]
        print(f"  {r}: {min(v):.4f}..{max(v):.4f}")
    print("\n== Pixel identity of the three cases around each 0.001-pt boundary (differing pixels below|at , at|above)")
    for prefix, rows in rd["pairs"].items():
        print(f"{prefix:22s} " + "  ".join(f"{r}: {rows[r]['below_vs_at']['differing_pixels']}|{rows[r]['at_vs_above']['differing_pixels']}" for r in R))
    print("\n== absent /MK vs explicit /MK/R 0 (differing pixels):", {r: v["differing_pixels"] for r, v in rd["absent_vs_zero_rotation"].items()})
    off = [v["differing_pixels"] for v in rd["pdfium_forms_off_vs_on"].values()]
    print("== PDFium forms-off vs forms-on differing pixels, max over cases:", max(off))
    if rd.get("precision"):
        print("\n== Precision probe (1000 px/pt): separation error = measured(bar edge - ink edge) - exact delta, microPoints")
        deltas = [x["delta_microPoints"] for x in rd["precision"]["poppler"]]
        print(f"{'delta(uPt)':>12s} " + " ".join(f"{x:>9d}" for x in deltas))
        for r in R:
            print(f"{r:>12s} " + " ".join(f"{x['separation_error_pt']*1e6:9.1f}" for x in rd["precision"][r]))
        for r in R:
            print(f"{r:>12s} measured separation (uPt): " + " ".join(f"{x['measured_separation_pt']*1e6:9.1f}" for x in rd["precision"][r]))
        for r in R:
            print(f"{r:>12s} ink edge err (uPt): " + " ".join(f"{x['ink_edge_error_pt']*1e6:9.1f}" for x in rd["precision"][r]))


if __name__ == "__main__":
    main()
