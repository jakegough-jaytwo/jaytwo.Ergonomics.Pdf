"""Independent, exploratory AP renders for W-218 R1; not target-library output."""
from pathlib import Path
import sys
from pypdf import PdfWriter
from pypdf.generic import (DictionaryObject as D, NameObject as N, NumberObject as I,
                           FloatObject as F, ArrayObject as A, TextStringObject as S,
                           DecodedStreamObject as Stream)

sys.path.insert(0, str(Path(__file__).resolve().parent))
from generate import make_font, obj

ROOT = Path(__file__).resolve().parent
OUT = ROOT / "profile-render.pdf"
CASES = [
    ("missing_rotation", None, "none", 0, 36.979, 18, "WWW", 12),
    ("explicit_zero", 0, "none", 0, 36.979, 18, "WWW", 12),
    ("rotate_90", 90, "none", 0, 18, 36.979, "WWW", 12),
    ("rotate_180", 180, "none", 0, 36.979, 18, "WWW", 12),
    ("rotate_270", 270, "none", 0, 18, 36.979, "WWW", 12),
    ("solid_below", 0, "solid", 1, 36.978, 18, "WWW", 12),
    ("solid_above", 0, "solid", 1, 36.979, 18, "WWW", 12),
    ("dashed_above", 0, "dashed", 1, 36.979, 18, "WWW", 12),
    ("underline_above", 0, "underline", 1, 35.979, 18, "WWW", 12),
    ("bearing_descender", 0, "solid", 1, 36.979, 18, "gyp", 12),
    ("space_edge", 0, "none", 0, 29.329, 18, " W  i ", 12),
    ("above_24", 0, "none", 0, 82, 44, "WWW", 28.253),
    ("floor_6_below", 0, "solid", 1, 19.989, 18, "WWW", 6),
    ("floor_6_above", 0, "solid", 1, 19.990, 18, "WWW", 6),
]

def main():
    w = PdfWriter()
    ft, cmap, order, font, upm = make_font(w, Path("C:/Windows/Fonts/arial.ttf"))
    refs = []
    for name, rotation, style, border, width, height, value, size in CASES:
        page = w.add_blank_page(width=180, height=110)
        x, y = 50, 40
        dw, dh = (height, width) if rotation in (90, 270) else (width, height)
        inset = 1 + border / 2 if style in ("solid", "dashed") else 1
        bottom = 1 + border / 2 if style == "underline" else inset
        asc = ft["hhea"].ascent * size / upm
        baseline = dh - inset - asc
        gids = [order.index(cmap[ord(c)]) for c in value]
        advance = sum(ft["hmtx"].metrics[order[g]][0] for g in gids) * size / upm
        encoded = "".join(f"{g:04X}" for g in gids)
        cmds = ["q 0.2 0.65 0.2 RG 0.15 w ",
                f"{inset:.6f} {bottom:.6f} {dw-2*inset:.6f} {dh-inset-bottom:.6f} re S Q "]
        if style != "none":
            if style == "dashed": cmds.append("[3 2] 0 d ")
            if style == "underline": cmds.append(f"{border} w 0 0 m {dw} 0 l S ")
            else: cmds.append(f"{border} w 0 0 {dw} {dh} re S ")
        cmds.append(f"BT /F1 {size:.3f} Tf 1 0 0 1 {inset:.6f} {baseline:.6f} Tm <{encoded}> Tj ET ")
        transform = {90:f"0 1 -1 0 {width} 0 cm ",
                     180:f"-1 0 0 -1 {width} {height} cm ",
                     270:f"0 -1 1 0 0 {height} cm "}.get(rotation, "")
        ap = Stream(); ap.set_data(("q " + transform + "".join(cmds) + " Q").encode("ascii"))
        ap.update({N("/Type"):N("/XObject"), N("/Subtype"):N("/Form"),
                   N("/BBox"):A([I(0),I(0),F(width),F(height)]),
                   N("/Resources"):D({N("/Font"):D({N("/F1"):font})})})
        apref = obj(w, ap)
        ann = D({N("/Type"):N("/Annot"), N("/Subtype"):N("/Widget"), N("/FT"):N("/Tx"),
                 N("/T"):S(name), N("/V"):S(value), N("/Rect"):A([F(x),F(y),F(x+width),F(y+height)]),
                 N("/F"):I(4), N("/Q"):I(0), N("/AP"):D({N("/N"):apref}),
                 N("/BS"):D({N("/W"):I(border), N("/S"):N("/D" if style == "dashed" else "/U" if style == "underline" else "/S")})})
        if rotation is not None: ann[N("/MK")] = D({N("/R"):I(rotation)})
        ref = obj(w, ann); refs.append(ref); page[N("/Annots")] = A([ref])
        print(f"{name}: display={dw}x{dh}, advance={advance:.6f}, baseline={baseline:.6f}, inset={inset:.3f}, bottom={bottom:.3f}")
    w._root_object[N("/AcroForm")] = obj(w, D({N("/Fields"):A(refs), N("/NeedAppearances"):I(0)}))
    w.write(OUT)
    print(OUT)

if __name__ == "__main__": main()
