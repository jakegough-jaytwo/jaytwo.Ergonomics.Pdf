"""Original MIT-licensed, bounded synthetic annotation research fixtures."""
import hashlib
import json
import sys
from pathlib import Path
from pypdf import PdfWriter
from pypdf.generic import (DictionaryObject as D, NameObject as N, ArrayObject as A,
                          NumberObject as I, TextStringObject as T, BooleanObject as B,
                          DecodedStreamObject, IndirectObject)

def d(**kw):
    return D({N('/' + k): v for k, v in kw.items()})

def nums(*v):
    return A([I(x) for x in v])

def appearance(w, body=b'0 0.5 0 rg 0 0 60 40 re f', **kw):
    s = DecodedStreamObject()
    s.set_data(body)
    s.update(d(Type=N('/XObject'), Subtype=N('/Form'), BBox=nums(0, 0, 60, 40),
               Resources=d(), **kw))
    return w._add_object(s)

out = Path(sys.argv[1]); out.mkdir(parents=True, exist_ok=True)
cases = ['square', 'stamp', 'freetext', 'caret', 'stamp-no-ap', 'freetext-no-ap',
         'caret-no-ap', 'popup', 'reply', 'group', 'state', 'cross-page-reply',
         'cycle-reply', 'dangling-reply', 'wrong-parent', 'orphan-popup', 'duplicate-owner',
         'shared-ap', 'resource-closure', 'resource-cycle', 'dangling-ap', 'ap-depth',
         'ap-bytes', 'ap-action', 'annotation-aa', 'excluded-unsupported']
manifest = []
for case in cases:
    w = PdfWriter(); p = w.add_blank_page(140, 120); p2 = w.add_blank_page(140, 120)
    ap = appearance(w)
    def annot(subtype='Square', **kw):
        a = d(Type=N('/Annot'), Subtype=N('/' + subtype), Rect=nums(20, 30, 80, 70),
              Contents=T('Business review'), NM=T('a'), T=T('Reviewer'), F=I(4), P=p.indirect_reference,
              AP=d(N=ap), **kw)
        return w._add_object(a)
    a = annot(); annotations = [a]
    if case.split('-')[0] in ('stamp', 'freetext', 'caret'):
        a.get_object()[N('/Subtype')] = N('/' + {'stamp':'Stamp', 'freetext':'FreeText', 'caret':'Caret'}[case.split('-')[0]])
        if case.startswith('stamp'): a.get_object()[N('/Name')] = N('/Approved')
        if case.startswith('freetext'):
            font = w._add_object(d(Type=N('/Font'), Subtype=N('/Type1'), BaseFont=N('/Helvetica'), Encoding=N('/WinAnsiEncoding')))
            resources = d(Font=d(Helv=font))
            w._root_object[N('/AcroForm')] = d(Fields=A(), DR=resources)
            a.get_object().update(d(Contents=T('Review'), DA=T('/Helv 10 Tf 0 g'), Q=I(0)))
            ap.get_object()[N('/Resources')] = resources
            ap.get_object().set_data(b'BT /Helv 10 Tf 0 g 1 15 Td (Review) Tj ET')
        if case.startswith('caret'): a.get_object()[N('/Sy')] = N('/P')
        if case.endswith('no-ap'): del a.get_object()[N('/AP')]
    elif case in ('popup', 'wrong-parent', 'orphan-popup'):
        popup = w._add_object(d(Type=N('/Annot'), Subtype=N('/Popup'), Rect=nums(85,30,135,100),
                                Parent=a, P=p.indirect_reference, Open=B(False)))
        a.get_object()[N('/Popup')] = popup
        annotations.append(popup)
        if case == 'wrong-parent': popup.get_object()[N('/Parent')] = w._add_object(d())
        if case == 'orphan-popup': annotations = [popup]; del popup.get_object()[N('/Parent')]
    elif case in ('reply', 'group', 'state', 'cross-page-reply', 'cycle-reply', 'dangling-reply'):
        b = annot('Text', IRT=a, RT=N('/Group' if case == 'group' else '/R'))
        b.get_object()[N('/NM')] = T('b'); b.get_object()[N('/Rect')] = nums(85,30,105,50)
        annotations.append(b)
        if case == 'state': b.get_object().update(d(State=T('Accepted'), StateModel=T('Review')))
        if case == 'cycle-reply': a.get_object()[N('/IRT')] = b
        if case == 'dangling-reply': b.get_object()[N('/IRT')] = IndirectObject(999, 0, w)
        if case == 'cross-page-reply':
            annotations = [b]; a.get_object()[N('/P')] = p2.indirect_reference; p2[N('/Annots')] = A([a])
    elif case == 'duplicate-owner': p2[N('/Annots')] = A([a])
    elif case == 'shared-ap':
        b = annot(); b.get_object()[N('/NM')] = T('b'); b.get_object()[N('/Rect')] = nums(85,30,135,70)
        annotations.append(b)
    elif case == 'resource-closure':
        inner = appearance(w)
        ap.get_object()[N('/Resources')] = d(XObject=d(Green=inner))
        ap.get_object().set_data(b'/Green Do')
    elif case == 'resource-cycle': ap.get_object()[N('/Resources')] = d(XObject=d(Unused=ap))
    elif case == 'dangling-ap': a.get_object()[N('/AP')] = d(N=IndirectObject(999,0,w))
    elif case == 'ap-depth':
        node = d()
        for _ in range(36): node = w._add_object(d(XObject=d(Child=node)))
        ap.get_object()[N('/Resources')] = node
    elif case == 'ap-bytes': ap.get_object().set_data(b' ' * (8*1024*1024 + 1))
    elif case == 'ap-action': ap.get_object()[N('/A')] = d(S=N('/URI'), URI=T('https://example.org/'))
    elif case == 'annotation-aa': a.get_object()[N('/AA')] = d(E=d(S=N('/URI'),URI=T('https://example.org/')))
    elif case == 'excluded-unsupported':
        b = annot('Stamp'); b.get_object()[N('/P')] = p2.indirect_reference; p2[N('/Annots')] = A([b])
    p[N('/Annots')] = A(annotations)
    path = out / (case + '.pdf'); w.write(path)
    manifest.append(dict(case=case, sha256=hashlib.sha256(path.read_bytes()).hexdigest(),
                         bytes=path.stat().st_size, provenance='Original MIT synthetic fixture; W-193 generate.py'))
Path(sys.argv[2]).write_text(json.dumps(manifest, indent=2)+'\n', encoding='utf-8')
