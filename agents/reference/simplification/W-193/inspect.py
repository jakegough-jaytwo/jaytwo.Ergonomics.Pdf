"""Independent reader/renderer observations; no candidate implementation."""
import hashlib
import json
import re
import shutil
import subprocess
import sys
from pathlib import Path
import pypdf
from pypdf import PdfReader

scratch, qpdf, record, poppler, mutool = sys.argv[1:]
scratch, record = Path(scratch), Path(record)
pdftoppm = shutil.which('pdftoppm', path=poppler)
assert pdftoppm, 'Poppler renderer missing'
def run(*args):
    p = subprocess.run([str(a) for a in args], capture_output=True, text=True)
    if p.returncode: raise RuntimeError(f'{args}: {p.returncode}: {p.stderr} {p.stdout}')
    return p.stdout + p.stderr

def ref(value):
    return [value.idnum, value.generation] if hasattr(value, 'idnum') else None

def inspect(path):
    r = PdfReader(path); rows=[]
    for index, page in enumerate(r.pages):
        for raw in page.get('/Annots', []):
            a=raw.get_object()
            ap = a.get('/AP', {}).get('/N')
            rows.append(dict(page=index+1, ref=ref(raw), subtype=str(a['/Subtype']),
                             keys=sorted(a.keys()), owner=ref(a.get('/P')), expected_owner=ref(page.indirect_reference),
                             relationships={k:ref(a[k]) for k in ('/Popup','/Parent','/IRT') if k in a},
                             rt=str(a.get('/RT','')), ap=ref(ap),
                             appearance_sha256=hashlib.sha256(ap.get_object().get_data()).hexdigest() if ap else None))
    return rows

results=json.loads((record/'results.json').read_text(encoding='utf-8-sig'))
checks=[]
for row in results:
    path=scratch/'outputs'/f"{row['name']}-{row['operation']}.pdf"
    if row['error']:
        assert row['name']=='dangling-ap' and row['error']=='PdfParseException' and row['bytes']==0, row
        checks.append(dict(name=row['name'],operation=row['operation'],error=row['error'],bytes=row['bytes']))
        continue
    run(qpdf,'--check',path)
    annotations=inspect(path)
    assert all(a['owner']==a['expected_owner'] for a in annotations), row
    assert all(not a['relationships'] for a in annotations), row
    assert len({tuple(a['ref']) for a in annotations})==len(annotations), row
    if row['name'] in ('stamp','freetext','caret','stamp-no-ap','freetext-no-ap','caret-no-ap','orphan-popup','ap-depth','ap-bytes','ap-action'):
        assert not annotations, row
    elif row['name'] in ('popup','wrong-parent'):
        assert len(annotations)==row['pages'].count(1) * (2 if row['operation']=='twice' else 1), row
    if row['name']=='shared-ap':
        # Shared immutable resources within an occurrence; independent maps between occurrences.
        groups=[annotations] if row['operation']!='twice' else [annotations[:4],annotations[4:]]
        assert all(len({tuple(a['ap']) for a in group})==1 for group in groups), row
        if row['operation']=='twice': assert groups[0][0]['ap']!=groups[1][0]['ap'],row
    if row['name']=='state':
        assert all('/State' not in a['keys'] and '/StateModel' not in a['keys'] for a in annotations),row
    if row['name']=='annotation-aa': assert all('/AA' not in a['keys'] for a in annotations),row
    if row['name']=='resource-closure':
        reader=PdfReader(path)
        root=reader.pages[0]['/Annots'][0].get_object()['/AP']['/N']
        assert root['/Resources']['/XObject']['/Green'].get_data()==b'0 0.5 0 rg 0 0 60 40 re f'
    if row['name']=='resource-cycle':
        reader=PdfReader(path)
        root=reader.pages[0]['/Annots'][0].get_object()['/AP']['/N']
        assert root['/Resources']['/XObject'].raw_get('/Unused')==root.indirect_reference
    checks.append(dict(name=row['name'],operation=row['operation'],sha256=hashlib.sha256(path.read_bytes()).hexdigest(),
                       annotations=annotations,qpdf='accepted'))

def ppm(path):
    data=path.read_bytes()
    match=re.match(rb'P6\s+(?:#[^\n]*\n\s*)*(\d+)\s+(\d+)\s+255\s',data)
    assert match, data[:100]
    w,h=map(int,match.groups()); pixels=data[match.end():]
    assert len(pixels)==w*h*3,(path,w,h,len(pixels))
    return w,h,pixels

renders=[]
renderdir=scratch/'renders'; renderdir.mkdir(exist_ok=True)
for case in ('square','stamp','freetext','caret','resource-closure','shared-ap'):
    for renderer in ('poppler','mupdf'):
        raster=[]
        for side,source in [('input',scratch/'inputs'/f'{case}.pdf'),('output',scratch/'outputs'/f'{case}-single.pdf')]:
            stem=renderdir/f'{case}-{renderer}-{side}'
            if renderer=='poppler':
                run(pdftoppm,'-r','72','-f','1','-l','1','-singlefile',source,stem)
            else: run(mutool,'draw','-q','-r','72','-o',str(stem)+'.ppm',source,'1')
            raster.append(ppm(Path(str(stem)+'.ppm')))
        assert raster[0][:2]==raster[1][:2]
        a,b=raster[0][2],raster[1][2]
        changed=sum(a[i:i+3]!=b[i:i+3] for i in range(0,len(a),3))
        expected_loss=case in ('stamp','freetext','caret')
        assert (changed>0)==expected_loss,(case,renderer,changed)
        renders.append(dict(case=case,renderer=renderer,width=raster[0][0],height=raster[0][1],changed_pixels=changed,
                            input_sha256=hashlib.sha256(a).hexdigest(),output_sha256=hashlib.sha256(b).hexdigest()))
run(mutool,'draw','-q','-r','144','-o',renderdir/'stamp-input.png',scratch/'inputs/stamp.pdf','1')
run(mutool,'draw','-q','-r','144','-o',renderdir/'stamp-output.png',scratch/'outputs/stamp-single.pdf','1')
inputs=[]
for case in ('popup','reply','group','state','cross-page-reply','cycle-reply','dangling-reply','wrong-parent','orphan-popup','duplicate-owner'):
    reader=PdfReader(scratch/'inputs'/f'{case}.pdf')
    inputs.append(dict(case=case,pages=[dict(page=i+1,annotations=[dict(ref=ref(raw),subtype=str(raw.get_object()['/Subtype']),
                       links={k:ref(raw.get_object()[k]) for k in ('/Popup','/Parent','/IRT','/P') if k in raw.get_object()},
                       rt=str(raw.get_object().get('/RT',''))) for raw in p.get('/Annots',[])]) for i,p in enumerate(reader.pages)]))
versions=dict(python=sys.version,pypdf=pypdf.__version__,qpdf=run(qpdf,'--version').strip(),
              poppler=run(pdftoppm,'-v').strip(),mupdf=run(mutool,'-v').strip())
(record/'inspection.json').write_text(json.dumps(dict(versions=versions,inputs=inputs,operations=checks,renders=renders),indent=2)+'\n',encoding='utf-8')
candidate=[]
for row in results:
    reason=None
    if row['name'] in ('stamp-no-ap','freetext-no-ap','caret-no-ap'): reason='Added subtype requires existing AP'
    elif row['name'] in ('orphan-popup','wrong-parent','cross-page-reply','cycle-reply','dangling-reply'): reason='Invalid or unsupported relationship'
    elif row['name']=='duplicate-owner' and row['operation'] in ('repeat','twice'): reason='Multiply owned source annotation'
    elif row['name'] in ('ap-action','ap-depth','ap-bytes'): reason='Unsupported AP or local resource limit'
    elif row['name']=='annotation-aa': reason='Unsupported event action'
    candidate.append(dict(name=row['name'],operation=row['operation'],baseline='refused' if row['error'] else 'accepted',
                          candidate='refused' if row['error'] or reason else 'accepted',reason=reason,
                          evidence='Structural counterfactual inference; candidate not implemented'))
new=sum(r['baseline']=='accepted' and r['candidate']=='refused' for r in candidate)
assert new==17
(record/'candidate.json').write_text(json.dumps(dict(accepted_baseline=43,new_refusals=new,remaining_accepted=26,operations=candidate),indent=2)+'\n',encoding='utf-8')
print(f'{len(checks)} operation checks; {sum(not r["error"] for r in results)} qpdf outputs; {len(renders)} render comparisons passed')
