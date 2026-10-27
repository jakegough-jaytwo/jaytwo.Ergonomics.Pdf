"""W-198 bounded research models, original MIT material; no production converter.
Synthetic packets test dictionary policy only, not XFA rendering or execution.
"""
import copy, hashlib, json, subprocess, sys
from pathlib import Path
import pypdf
from pypdf import PdfReader, PdfWriter
from pypdf.generic import DictionaryObject as D, ArrayObject as A, NameObject as N, NumberObject as I, TextStringObject as T, BooleanObject as B, DecodedStreamObject

mode, root, scratch, record = sys.argv[1:5]
root, scratch, record = map(Path, (root, scratch, record))
def dump(name, value):
    (record / name).write_text(json.dumps(value, indent=2) + '\n', encoding='utf-8')
def stream(w, data):
    s = DecodedStreamObject(); s.set_data(data); return w._add_object(s)
def inspect(path):
    r = PdfReader(path)
    if r.is_encrypted: r.decrypt('user')
    c = r.trailer['/Root']; f = c.get('/AcroForm', D()).get_object()
    fields = r.get_fields() or {}
    return dict(xfa='/XFA' in f, rights='/UR3' in c.get('/Perms', D()).get_object(),
                fields={k: str(v.get('/V', '')) for k,v in fields.items()},
                rotation=r.pages[0].get('/Rotate', 0), pages=len(r.pages))

if mode == 'generate':
    for sub in ('inputs','outputs','probe'): (scratch/sub).mkdir(parents=True, exist_ok=True)
    manifest = []
    for name in ('hybrid','hybrid-rights','dynamic','xfa-only','plain'):
        w = PdfWriter(); p = w.add_blank_page(200, 200)
        font = w._add_object(D({N('/Type'):N('/Font'),N('/Subtype'):N('/Type1'),N('/BaseFont'):N('/Helvetica')}))
        form = D({N('/Fields'): A(), N('/DA'):T('/Helv 10 Tf 0 g'),N('/DR'):D({N('/Font'):D({N('/Helv'):font})})})
        if name != 'xfa-only':
            ap = DecodedStreamObject(); ap.set_data(b'BT /Helv 10 Tf 0 5 Td (old) Tj ET')
            ap.update({N('/Type'):N('/XObject'),N('/Subtype'):N('/Form'),N('/BBox'):A([I(0),I(0),I(100),I(20)]),N('/Resources'):form[N('/DR')]})
            widget=w._add_object(D({N('/Type'):N('/Annot'),N('/Subtype'):N('/Widget'),N('/FT'):N('/Tx'),N('/T'):T('Name'),N('/V'):T('old'),N('/Rect'):A([I(10),I(10),I(110),I(30)]),N('/AP'):D({N('/N'):w._add_object(ap)})}))
            form[N('/Fields')].append(widget); p[N('/Annots')]=A([widget])
        if name != 'plain': form[N('/XFA')]=A([T('template'),stream(w,b'<template xmlns="http://www.xfa.org/schema/xfa-template/2.5/"/>'),T('datasets'),stream(w,b'<datasets xmlns="http://www.xfa.org/schema/xfa-data/1.0/"/>')])
        w._root_object[N('/AcroForm')]=w._add_object(form)
        if name == 'dynamic': w._root_object[N('/NeedsRendering')]=B(True)
        if name == 'hybrid-rights':
            w._root_object[N('/Perms')]=D({N('/UR3'):w._add_object(D({N('/Type'):N('/Sig'),N('/Reference'):A([D({N('/TransformMethod'):N('/UR3'),N('/TransformParams'):D({N('/V'):N('/2.2'),N('/Form'):A([N('/FillIn')])})})])}))})
        path=scratch/'inputs'/f'{name}.pdf'; w.write(path)
        manifest.append(dict(name=name, sha256=hashlib.sha256(path.read_bytes()).hexdigest(), inspection=inspect(path)))
    # Counterfactual preparation removes two entries only, using an independent
    # new-file rewrite. This does NOT exercise incremental writing or permissions.
    corpus=['irs-fw9.pdf','va-21-4138.pdf','grants-sf424.pdf']
    existing=list((root/'test/jaytwo.Pdf.Tests/Fixtures/corpus').glob('*.pdf'))
    corpus=[p for p in existing if any(s in p.name for s in ('fw9','21-4138','sf424','sf-424'))]
    for p in corpus:
        target=scratch/'inputs'/('corpus-'+p.name); target.write_bytes(p.read_bytes())
        manifest.append(dict(name=target.stem, source=str(p.relative_to(root)).replace('\\','/'), sha256=hashlib.sha256(p.read_bytes()).hexdigest(), inspection=inspect(p)))
    for p in list((scratch/'inputs').glob('*.pdf')):
        if p.name.startswith(('modeled-', 'encrypted-')): continue
        info=inspect(p)
        if not info['xfa'] or 'dynamic' in p.name or 'xfa-only' in p.name or ('corpus-' in p.name and 'fw9' not in p.name and '21-4138' not in p.name): continue
        w=PdfWriter(clone_from=p); f=w._root_object['/AcroForm']; del f['/XFA']
        if '/Perms' in w._root_object: w._root_object['/Perms'].pop('/UR3',None)
        w.write(scratch/'inputs'/('modeled-'+p.name))
    dump('inputs.json',dict(pypdf=pypdf.__version__, fixtures=manifest))
    # Executable contract model only: no claim that these APIs exist.
    initial=dict(xfa=True, rights=True, value='old', rotation=90)
    state=copy.deepcopy(initial)
    def abandon(s):
        if not s['xfa']: return False
        if s['rights']: raise ValueError('remove rights first')
        s['xfa']=False
        return True
    try: abandon(state)
    except ValueError: pass
    assert state==initial
    state['rights']=False
    first=abandon(state); snapshot=copy.deepcopy(state); second=abandon(state)
    assert first and not second and snapshot==state and state['rotation']==90 and state['value']=='old'
    dump('ordering-model.json',dict(kind='counterfactual pure-state model, not engine API evidence',
         wrongOrderUnchanged=initial, rightsThenXfa=state, firstChanged=first, repeatedChanged=second))
else:
    rows=[]
    qpdf=sys.argv[5]
    observations=json.loads((record/'results.json').read_text(encoding='utf-8-sig'))
    assert len(observations)==70
    assert all(r['sourceUnchanged'] for r in observations)
    assert all(r['destinationBytes']==0 for r in observations if r['result']=='refused')
    for p in sorted((scratch/'outputs').glob('*.pdf')):
        check=subprocess.run([qpdf,'--password=user','--check',str(p)],capture_output=True,text=True)
        rows.append(dict(file=p.name, **inspect(p), qpdfExit=check.returncode, qpdfOutput=check.stdout+check.stderr))
    dump('inspection.json',dict(pypdf=pypdf.__version__, qpdf=subprocess.check_output([qpdf,'--version'],text=True).splitlines()[0], outputs=rows))
    permission=subprocess.run([qpdf,'--password=user','--show-encryption',str(scratch/'inputs/encrypted-fill-only.pdf')],capture_output=True,text=True,check=True)
    (record/'encryption.txt').write_text(permission.stdout,encoding='utf-8')
    assert all(r['qpdfExit']==0 for r in rows)
    by={r['file']:r for r in rows}
    assert by['hybrid-pending-extract.pdf']['fields']['Input1.Name']=='pending'
    assert by['hybrid-pending-concat.pdf']['rotation']==90
    assert by['hybrid-rights-pending-save.pdf']['xfa'] is False
    assert by['hybrid-rights-pending-save.pdf']['rights'] is False
