"""Original MIT-licensed focused fixtures; generated with pypdf, outside G2."""
import hashlib
import json
import sys
from pathlib import Path
from pypdf import PdfWriter
from pypdf.generic import (DictionaryObject as D, NameObject as N, ArrayObject as A,
                           NumberObject as I, TextStringObject as T, BooleanObject as B,
                           DecodedStreamObject as S)

out = Path(sys.argv[1]); out.mkdir(parents=True, exist_ok=True)

def d(**kw): return D({N('/'+k): v for k, v in kw.items()})
def nums(*xs): return A([I(x) for x in xs])

def build(mode):
    w = PdfWriter(); pages = [w.add_blank_page(200, 120) for _ in range(2)]
    font = w._add_object(d(Type=N('/Font'), Subtype=N('/Type1'), BaseFont=N('/Courier' if mode == 'courier' else '/Helvetica')))
    resources = w._add_object(d(Font=d(F1=font)))
    ap = S(); ap.set_data(b'BT /F1 12 Tf 0 g 2 8 Td (Original) Tj ET')
    ap.update(d(Type=N('/XObject'), Subtype=N('/Form'), BBox=nums(0,0,120,30), Resources=resources))
    ap_ref = w._add_object(ap.flate_encode())
    root = w._add_object(d(T=T('Group'), FT=N('/Tx'), V=T('Original'), DV=T('Default'), Ff=I(0), Q=I(0)))
    field = w._add_object(d(T=T('Name'), Parent=root, MaxLen=I(30)))
    root.get_object()[N('/Kids')] = A([field])
    widgets = []
    for i, page in enumerate(pages):
        widget = w._add_object(d(Type=N('/Annot'), Subtype=N('/Widget'), Parent=field,
                                Rect=nums(10,10,130,40), F=I(4), AP=d(N=ap_ref)))
        if mode != 'no-p': widget.get_object()[N('/P')] = page.indirect_reference
        widgets.append(widget); page[N('/Annots')] = A([widget])
        content = S(); content.set_data(b'q Q\n')
        page[N('/Contents')] = w._add_object(content.flate_encode())
    field.get_object()[N('/Kids')] = A(widgets)
    form = d(Fields=A([root]), DA=T('/F1 12 Tf 0 g'), DR=resources)
    w._root_object[N('/AcroForm')] = w._add_object(form)
    if mode == 'choice':
        root.get_object()[N('/FT')] = N('/Ch')
        field.get_object()[N('/Opt')] = A([T('Original'), T('Ada')])
        del field.get_object()[N('/MaxLen')]
    if mode == 'checkbox':
        root.get_object().update(d(FT=N('/Btn'), V=N('/Off'), DV=N('/Off')))
        del field.get_object()[N('/MaxLen')]
        states = []
        for data in [b'0 G 1 w 1 1 118 28 re S', b'0 g 1 1 118 28 re f']:
            s = S(); s.set_data(data)
            s.update(d(Type=N('/XObject'), Subtype=N('/Form'), BBox=nums(0,0,120,30)))
            states.append(w._add_object(s.flate_encode()))
        for widget in widgets:
            widget.get_object().update(d(AS=N('/Off'), AP=d(N=d(Off=states[0], Yes=states[1]))))
    # Inherited geometry/resources; widgets use raw page user coordinates.
    tree = w._pages.get_object()
    tree.update(d(MediaBox=nums(0,0,200,120), CropBox=nums(0,0,190,110), Rotate=I(90), Resources=resources))
    for p in pages:
        del p[N('/MediaBox')]
    if mode == 'merged':
        m = widgets[0].get_object(); del m[N('/Parent')]
        m.update(d(T=T('Name'), FT=N('/Tx'), V=T('Original'), MaxLen=I(30)))
        form[N('/Fields')] = A([widgets[0]])
        pages[1][N('/Annots')] = A()
    elif mode == 'cycle': root.get_object()[N('/Kids')] = A([root])
    elif mode == 'multi-owner': root.get_object()[N('/Kids')] = A([field,field])
    elif mode == 'orphan': form[N('/Fields')] = A()
    elif mode == 'missing-font': form[N('/DA')] = T('/Missing 12 Tf')
    elif mode == 'non-font-dr': resources.get_object()[N('/XObject')] = d(X=ap_ref)
    elif mode == 'unsupported-ap': widgets[0].get_object()[N('/AP')] = d(N=I(42))
    elif mode == 'ap-extra-key': ap_ref.get_object()[N('/LastModified')] = T('D:20261026000000Z')
    elif mode == 'unselected-action': widgets[1].get_object()[N('/AA')] = d(K=d(S=N('/JavaScript'), JS=T('x')))
    elif mode == 'need-appearances': form[N('/NeedAppearances')] = B(True)
    elif mode == 'resource-cycle':
        ap_resources = w._add_object(d(Font=d(F1=font), XObject=d(Self=ap_ref)))
        ap_ref.get_object()[N('/Resources')] = ap_resources
    elif mode == 'direct-widgets':
        # Invalid PDF (annotations must be indirect): explicit refusal/robustness probe only.
        for p, ref in zip(pages, widgets): p[N('/Annots')] = A([ref.get_object()])
        field.get_object()[N('/Kids')] = A([x.get_object() for x in widgets])
    path = out / (mode+'.pdf'); w.write(path)
    return {'id': mode, 'path': path.name, 'sha256': hashlib.sha256(path.read_bytes()).hexdigest(),
            'producer': 'original generate.py / pypdf', 'rights': 'MIT (repository)',
            'validity': 'deliberately malformed' if mode in ['cycle','multi-owner','orphan','missing-font','unsupported-ap','direct-widgets'] else 'not a conformance claim'}

modes = ['nested','courier','merged','no-p','checkbox','choice','cycle','multi-owner','orphan','missing-font',
         'non-font-dr','unsupported-ap','ap-extra-key','unselected-action','need-appearances','resource-cycle']
rows = [build(m) for m in modes]
(out/'manifest.json').write_text(json.dumps(rows, indent=2)+'\n')
print(f'Generated {len(rows)} focused fixtures')
