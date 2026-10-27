"""Original MIT-licensed, disposable signature-field fixtures; outside G2."""
import hashlib
import json
import sys
from pathlib import Path
from pypdf import PdfWriter
from pypdf.generic import (DictionaryObject as D, NameObject as N, ArrayObject as A,
                           NumberObject as I, TextStringObject as T, NullObject)

def d(**kw): return D({N('/'+k): v for k, v in kw.items()})
def nums(*xs): return A([I(x) for x in xs])
out = Path(sys.argv[1]); out.mkdir(parents=True, exist_ok=True)
rows = []
for mode in ['plain', 'null-value', 'seed-ok', 'seed-encoding', 'seed-bad', 'include', 'all', 'exclude', 'readonly']:
    w = PdfWriter(); pages = [w.add_blank_page(200,120) for _ in range(2)]
    sig = w._add_object(d(FT=N('/Sig'), T=T('SignHere'), Ff=I(1 if mode == 'readonly' else 0)))
    widgets = []
    for page in pages:
        widget = w._add_object(d(Type=N('/Annot'), Subtype=N('/Widget'), Parent=sig,
                                Rect=nums(10,10,130,40), P=page.indirect_reference, F=I(4)))
        widgets.append(widget); page[N('/Annots')] = A([widget])
    sig.get_object()[N('/Kids')] = A(widgets)
    if mode == 'null-value': sig.get_object()[N('/V')] = NullObject()
    if mode.startswith('seed'):
        sig.get_object()[N('/SV')] = w._add_object(d(Ff=I(64), DigestMethod=A([N('/SHA256' if mode == 'seed-ok' else '/SHA384')])))
        if mode == 'seed-encoding':
            sig.get_object()[N('/SV')] = w._add_object(d(Ff=I(2), SubFilter=A([N('/adbe.pkcs7.detached')])))
    if mode in ['include', 'all', 'exclude']:
        lock = d(Type=N('/SigFieldLock'), Action=N('/'+mode.title()))
        if mode != 'all': lock[N('/Fields')] = A([T('Other')])
        sig.get_object()[N('/Lock')] = w._add_object(lock)
    other = w._add_object(d(FT=N('/Tx'), T=T('Other'), V=T('Original')))
    widget = w._add_object(d(Type=N('/Annot'), Subtype=N('/Widget'), Parent=other,
                            Rect=nums(10,60,130,90), P=pages[1].indirect_reference))
    other.get_object()[N('/Kids')] = A([widget]); pages[1][N('/Annots')].append(widget)
    font = w._add_object(d(Type=N('/Font'), Subtype=N('/Type1'), BaseFont=N('/Helvetica')))
    w._root_object[N('/AcroForm')] = w._add_object(d(Fields=A([sig,other]), DA=T('/F1 12 Tf 0 g'), DR=d(Font=d(F1=font))))
    path = out/(mode+'.pdf'); w.write(path)
    rows.append(dict(id=mode, sha256=hashlib.sha256(path.read_bytes()).hexdigest(), rights='MIT (repository)', producer='pypdf original generate.py'))
(out/'manifest.json').write_text(json.dumps(rows, indent=2)+'\n')
print(f'Generated {len(rows)} original fixtures')
