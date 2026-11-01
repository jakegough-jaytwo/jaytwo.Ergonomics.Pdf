"""Independent research fixtures: pypdf writes widget APs with embedded TrueType."""
from pathlib import Path
import hashlib, json
from fontTools.ttLib import TTFont
from fontTools.pens.boundsPen import BoundsPen
from pypdf import PdfWriter
from pypdf.generic import (DictionaryObject as D, NameObject as N, NumberObject as I,
                           FloatObject as F, ArrayObject as A, TextStringObject as S,
                           DecodedStreamObject as Stream, ByteStringObject as B)

ROOT = Path(__file__).resolve().parent
FONTS = [("Arial", Path("C:/Windows/Fonts/arial.ttf")),
         ("TimesItalic", Path("C:/Windows/Fonts/timesi.ttf"))]
CASES = [
    ("narrow", "ill", 36, 18, 0, "solid", 0, 12, 1),
    ("wide_below", "WWW", 35.9, 18, 0, "solid", 0, 12, 1),
    ("wide_at", "WWW", 36, 18, 0, "solid", 0, 12, 1),
    ("wide_above", "WWW", 36.1, 18, 0, "solid", 0, 12, 1),
    ("bearing_center", "jAy", 36, 18, 90, "dashed", 1, 12, 1),
    ("descender_right", "gyp", 36, 18, 270, "underline", 2, 12, 1),
    ("space_edge", " W  i ", 58, 18, 0, "none", 0, 12, 0),
    ("nbsp", "W\u00a0i", 36, 18, 0, "none", 1, 12, 0),
    ("two_below", "Wgj\nill", 36, 25.9, 0, "solid", 0, 12, 1),
    ("two_at", "Wgj\nill", 36, 26, 0, "solid", 0, 12, 1),
    ("two_above", "Wgj\nill", 36, 26.1, 0, "solid", 0, 12, 1),
    ("leading_12", "Wgj\nill", 36, 30, 0, "none", 0, 12, 0),
    ("leading_14", "Wgj\nill", 36, 30, 0, "none", 0, 14, 0),
    ("size_23_9", "Wgj ill", 65.5, 38, 0, "none", 0, 23.9, 0),
    ("size_24", "Wgj ill", 65.5, 38, 0, "none", 0, 24, 0),
    ("size_24_1", "Wgj ill", 65.5, 38, 0, "none", 0, 24.1, 0),
    ("size_30", "Wgj ill", 82, 44, 0, "none", 0, 30, 0),
    ("size_30_05", "Wgj ill", 82, 44, 0, "none", 0, 30.05, 0),
    ("size_30_1", "Wgj ill", 82, 44, 0, "none", 0, 30.1, 0),
]

def obj(w, x): return w._add_object(x)
def stream(w, data, extra=None):
    s = Stream(); s.set_data(data)
    if extra: s.update(extra)
    return obj(w, s)

def make_font(w, path):
    raw = path.read_bytes(); ft = TTFont(path); upm = ft['head'].unitsPerEm
    cmap = ft.getBestCmap(); order = ft.getGlyphOrder(); hmtx = ft['hmtx'].metrics
    widths = [round(hmtx[g][0]*1000/upm) for g in order]
    ff = stream(w, raw, {N('/Length1'): I(len(raw))})
    desc = obj(w, D({N('/Type'):N('/FontDescriptor'),N('/FontName'):N('/Research'+path.stem),
        N('/Flags'):I(32),N('/FontBBox'):A([I(-1000),I(-1000),I(2000),I(2000)]),
        N('/ItalicAngle'):I(0),N('/Ascent'):I(round(ft['hhea'].ascent*1000/upm)),
        N('/Descent'):I(round(ft['hhea'].descent*1000/upm)),N('/CapHeight'):I(700),
        N('/StemV'):I(80),N('/FontFile2'):ff}))
    cid = obj(w, D({N('/Type'):N('/Font'),N('/Subtype'):N('/CIDFontType2'),
        N('/BaseFont'):N('/Research'+path.stem),N('/CIDSystemInfo'):D({N('/Registry'):S('Adobe'),N('/Ordering'):S('Identity'),N('/Supplement'):I(0)}),
        N('/FontDescriptor'):desc,N('/CIDToGIDMap'):N('/Identity'),N('/DW'):I(1000),
        N('/W'):A([I(0),A([I(v) for v in widths])])}))
    mapping = sorted((order.index(cmap[cp]),cp) for cp in set(ord(c) for _,v,*_ in CASES for c in v) if cp in cmap)
    cm = b'/CIDInit /ProcSet findresource begin\n12 dict begin\nbegincmap\n/CIDSystemInfo << /Registry (Adobe) /Ordering (UCS) /Supplement 0 >> def\n/CMapName /Research-UCS def\n/CMapType 2 def\n1 begincodespacerange\n<0000> <FFFF>\nendcodespacerange\n'+str(len(mapping)).encode()+b' beginbfchar\n'
    cm += b''.join(f'<{gid:04X}> <{cp:04X}>\n'.encode() for gid,cp in mapping)
    cm += b'endbfchar\nendcmap\nCMapName currentdict /CMap defineresource pop\nend end\n'
    font = obj(w,D({N('/Type'):N('/Font'),N('/Subtype'):N('/Type0'),N('/BaseFont'):N('/Research'+path.stem),
        N('/Encoding'):N('/Identity-H'),N('/DescendantFonts'):A([cid]),N('/ToUnicode'):stream(w,cm)}))
    return ft, cmap, order, font, upm

def main():
    records=[]
    for label,path in FONTS:
        w=PdfWriter(); ft,cmap,order,font,upm=make_font(w,path)
        for name,value,width,height,rotation,style,align,leading,border in CASES:
            # Coordinates in AP local space; /MK/R asks readers to rotate widget presentation.
            page=w.add_blank_page(width=180,height=110)
            x,y=45,40; box=A([F(x),F(y),F(x+width),F(y+height)])
            chunks=[]
            if style!='none':
                if style=='dashed': chunks.append('[3 2] 0 d ')
                chunks.append(f'{border} w 0 0 {width} {height} re S ' if style!='underline' else f'{border} w 0 0 m {width} 0 l S ')
            line_info=[]
            for i,line in enumerate(value.split('\n')):
                gids=[order.index(cmap[ord(c)]) for c in line]
                size=12 if name=='leading_14' else leading
                adv=sum(ft['hmtx'].metrics[order[g]][0] for g in gids)*size/upm
                xpos=[2,(width-adv)/2,width-adv-2][align]
                baseline=height-3-size*0.8-i*leading
                encoded=''.join(f'{g:04X}' for g in gids)
                chunks.append(f'BT /F1 {size} Tf 1 0 0 1 {xpos:.4f} {baseline:.4f} Tm <{encoded}> Tj ET ')
                line_info.append({'advance':round(adv,4),'origin':round(xpos,4),'baseline':round(baseline,4)})
            ap=stream(w,''.join(chunks).encode(),{N('/Type'):N('/XObject'),N('/Subtype'):N('/Form'),
                N('/BBox'):A([I(0),I(0),F(width),F(height)]),N('/Resources'):D({N('/Font'):D({N('/F1'):font})})})
            annotation=obj(w,D({N('/Type'):N('/Annot'),N('/Subtype'):N('/Widget'),N('/FT'):N('/Tx'),
                N('/T'):S(name),N('/V'):S(value),N('/Rect'):box,N('/F'):I(4),N('/Q'):I(align),
                N('/MK'):D({N('/R'):I(rotation)}),N('/BS'):D({N('/W'):I(border),N('/S'):N('/D' if style=='dashed' else '/U' if style=='underline' else '/S')}),
                N('/AP'):D({N('/N'):ap})}))
            page[N('/Annots')]=A([annotation])
            records.append({'font':label,'case':name,'value':value,'width':width,'height':height,'rotation':rotation,'style':style,'alignment':align,'size':12 if name=='leading_14' else leading,'leading':leading,'lines':line_info})
        w._root_object[N('/AcroForm')]=obj(w,D({N('/Fields'):A([p['/Annots'][0] for p in w.pages]),N('/NeedAppearances'):I(0)}))
        out=ROOT/(label+'.pdf'); w.write(out)
        print(out.name,hashlib.sha256(out.read_bytes()).hexdigest())
        records.append({'font':label,'input':str(path),'input_sha256':hashlib.sha256(path.read_bytes()).hexdigest()})
    (ROOT/'measurements.json').write_text(json.dumps(records,indent=2,ensure_ascii=False),encoding='utf-8')

if __name__=='__main__': main()
