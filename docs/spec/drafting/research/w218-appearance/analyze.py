"""Compute unhinted outline ink bounds for generated APs; separate from reader render."""
import json
from pathlib import Path
from fontTools.ttLib import TTFont
from fontTools.pens.boundsPen import BoundsPen

root=Path(__file__).resolve().parent
rows=json.loads((root/'measurements.json').read_text(encoding='utf-8'))
out=[]
for label in ('Arial','TimesItalic'):
    path=Path(next(x['input'] for x in rows if x.get('font')==label and 'input' in x))
    ft=TTFont(path); cmap=ft.getBestCmap(); glyphs=ft.getGlyphSet(); upm=ft['head'].unitsPerEm
    for r in (x for x in rows if x.get('font')==label and 'case' in x):
        ink=[]
        for line,placement in zip(r['value'].split('\n'),r['lines']):
            penx=0; bounds=[]
            for c in line:
                gn=cmap[ord(c)]; pen=BoundsPen(glyphs); glyphs[gn].draw(pen)
                if pen.bounds:
                    x0,y0,x1,y1=pen.bounds
                    scale=r['size']/upm
                    bounds.append((placement['origin']+(penx+x0)*scale,
                                   placement['baseline']+y0*scale,
                                   placement['origin']+(penx+x1)*scale,
                                   placement['baseline']+y1*scale))
                penx+=ft['hmtx'].metrics[gn][0]
            if bounds: ink.append([round(min(b[0] for b in bounds),4),round(min(b[1] for b in bounds),4),
                                   round(max(b[2] for b in bounds),4),round(max(b[3] for b in bounds),4)])
        r2={k:r[k] for k in ('font','case','width','height','style','size','leading')}
        r2['ink_boxes']=ink
        if ink:
            x0=min(b[0] for b in ink); y0=min(b[1] for b in ink)
            x1=max(b[2] for b in ink); y1=max(b[3] for b in ink)
            r2['ink_margins']=[round(x0,4),round(y0,4),round(r['width']-x1,4),round(r['height']-y1,4)]
            r2['fits_1pt']=all(v>=1 for v in (x0,y0,r['width']-x1,r['height']-y1))
            r2['fits_2pt']=all(v>=2 for v in (x0,y0,r['width']-x1,r['height']-y1))
        out.append(r2)
(root/'outline-analysis.json').write_text(json.dumps(out,indent=2),encoding='utf-8')
for r in out:
    if r['case'] in ('wide_below','wide_at','wide_above','two_below','two_at','two_above','leading_12','leading_14','size_24','size_24_1','size_30','size_30_05','size_30_1'):
        print(r['font'],r['case'],r.get('ink_margins'),r.get('fits_1pt'),r.get('fits_2pt'))
