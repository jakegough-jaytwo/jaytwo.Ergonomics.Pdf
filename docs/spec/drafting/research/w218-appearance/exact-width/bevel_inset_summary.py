"""Print the beveled/inset tables quoted in the research README from bevel-inset-render.json.

Usage: python bevel_inset_summary.py [--dir DIR]
"""
import argparse
import json
from pathlib import Path

RENDERERS = ("poppler", "mupdf", "pdfium")
SIDES = ("left", "bottom", "right", "top")


def main():
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--dir", default=str(Path(__file__).resolve().parent))
    d = Path(ap.parse_args().dir)
    man = json.loads((d / "bevel-inset-manifest.json").read_text(encoding="utf-8"))
    r = json.loads((d / "bevel-inset-render.json").read_text(encoding="utf-8"))
    print("renderers:", r["renderers"])
    groups = {}
    for c in man["cases"]:
        key = (c["construction"], c["rule"], c["bw"])
        g = groups.setdefault(key, {"n": 0, "fp": c["footprint"], "nom": c["nominal_clearance"], **{x: {"fp": [], "ink": [], "cl": []} for x in RENDERERS}})
        g["n"] += 1
        for x in RENDERERS:
            m = r["measure"][c["name"]][x]
            g[x]["fp"] += m["footprint_measured_pt"].values()
            g[x]["ink"] += m["ink_depth_measured_pt"].values()
            g[x]["cl"] += m["clearance_measured_pt"].values()
    print("\n| Construction / rule / W | Pages | Nominal footprint, clearance | Visible footprint P / M / F | Ink depth P / M / F | Clearance P / M / F |")
    print("| --- | --- | --- | --- | --- | --- |")
    rng = lambda v: f"{min(v):.3f}" if max(v) - min(v) < 0.0005 else f"{min(v):.3f}–{max(v):.3f}"  # noqa: E731
    for (con, rule, bw), g in sorted(groups.items()):
        cols = [" / ".join(rng(g[x][k]) for x in RENDERERS) for k in ("fp", "ink", "cl")]
        print(f"| {con} / {rule} / {bw} | {g['n']} | {g['fp']}, {g['nom']} | " + " | ".join(cols) + " |")
    print("\nshading by physical side (left, bottom, right, top), all ring_band/full pages:")
    seen = {}
    for c in man["cases"]:
        if c["rule"] != "full" or c["construction"] != "ring_band":
            continue
        for x in RENDERERS:
            seen.setdefault((c["rot"], x), set()).add(tuple(r["measure"][c["name"]][x]["shade"][s] for s in SIDES))
    for (rot, x), v in sorted(seen.items(), key=lambda kv: (str(kv[0][0]), kv[0][1])):
        print(f"  rot {rot} {x}: {sorted(v)}")
    print("\npresentation colours, band mid-point per physical side (rotation 0, W 2):")
    for n in ("B2_r0_bg", "B2_r0_nobg", "I2_r0_bg", "I2_r0_nobg"):
        for x in RENDERERS:
            v = r["colours"][n][x]
            print(f"  {n} {x}: " + "; ".join(f"{s} ring {v[s]['ring']} band {v[s]['band']}" for s in SIDES))
    print("\nreader-generated runs from each physical edge inward ([r,g,b], pt), first three runs:")
    for c in man["regen"]:
        for x, v in r["regen"][c["name"]].items():
            print(f"  {c['name']} {x}: " + "; ".join(f"{s} " + " ".join(f"{col}x{w:.3f}" for col, w in v["runs"][s][:3]) for s in SIDES))


if __name__ == "__main__":
    main()
