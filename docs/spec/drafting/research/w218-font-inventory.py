"""Static local font inventory; not a target-library resource or embedding test.
Run: python docs/spec/drafting/research/w218-font-inventory.py
Requires fontTools. Font binaries are read locally and never copied.
"""
from pathlib import Path
import hashlib, json, platform, sys
import fontTools
from fontTools.ttLib import TTFont, TTCollection

SAMPLES = {"Latin": "A\u00e9", "Greek": "\u03a9", "Cyrillic": "\u0416", "Arabic": "\u0639", "Hebrew": "\u05d0", "Devanagari": "\u0915", "Thai": "\u0e01", "Han": "\u6f22", "Hangul": "\ud55c"}
NAMES = ["arial.ttf", "timesi.ttf", "tahoma.ttf", "sylfaen.ttf", "LeelawUI.ttf", "malgun.ttf", "Nirmala.ttc", "msyh.ttc"]
rows=[]
for name in NAMES:
    path=Path("C:/Windows/Fonts")/name
    data=path.read_bytes()
    collection=data[:4]==b"ttcf"
    owner=TTCollection(path, lazy=True) if collection else TTFont(path, lazy=True)
    fonts=owner.fonts if collection else [owner]
    for index,font in enumerate(fonts):
        eligible=[(p,e,fmt) for p,e,fmt in [(0,4,12),(3,10,12),(0,3,4),(3,1,4)] if any(t.platformID==p and t.platEncID==e and t.format==fmt for t in font["cmap"].tables)]
        key=eligible[0] if eligible else None
        maps=[t for t in font["cmap"].tables if key and (t.platformID,t.platEncID,t.format)==key]
        cmap=maps[0].cmap if len(maps)==1 else {}
        rows.append({"file":str(path),"sha256":hashlib.sha256(data).hexdigest(),"container_bytes":len(data),"collection":collection,"face":index,"family":font["name"].getDebugName(1),"subfamily":font["name"].getDebugName(2),"glyphs":font["maxp"].numGlyphs,"truetype_outlines":"glyf" in font,"table_bytes_sum":sum(t.length for t in font.reader.tables.values()),"os2_fsType":getattr(font.get("OS/2"),"fsType",None),"selected_cmap":key,"selected_map_duplicates":len(maps),"sample_coverage":{s:all(ord(c) in cmap and cmap[ord(c)]!=".notdef" for c in text) for s,text in SAMPLES.items()}})
    owner.close()
result={"classification":"Measured static input inventory only; no target processing, resource, embedding, shaping or rendering result", "python":sys.version,"platform":platform.platform(),"fontTools":fontTools.__version__,"samples":SAMPLES,"rows":rows}
out=Path(__file__).with_suffix(".json")
out.write_text(json.dumps(result,ensure_ascii=False,indent=2)+"\n",encoding="utf-8")
print(f"Inventoried {len(NAMES)} containers / {len(rows)} faces; {out}")
for row in rows: print(row["family"],row["face"],row["container_bytes"],"collection" if row["collection"] else "standalone",[s for s,v in row["sample_coverage"].items() if v])
