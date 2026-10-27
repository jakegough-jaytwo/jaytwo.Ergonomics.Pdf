"""Original MIT research material. Structural counterfactuals are NOT production preparation.
Synthetic UR3 dictionaries have no valid Adobe rights credential. No viewer compatibility claim.
"""
import copy, hashlib, importlib.util, importlib.metadata, json, subprocess, sys
from pathlib import Path
import pypdf
from pypdf import PdfReader, PdfWriter
from pypdf.generic import DictionaryObject as D, NameObject as N, TextStringObject as T

mode, root, scratch, record, qpdf = sys.argv[1:6]
root, scratch, record = map(Path, (root, scratch, record))
def dump(name, value):
    (record/name).write_text(json.dumps(value, indent=2)+'\n',encoding='utf-8')
def inspect(path):
    r=PdfReader(path)
    if r.is_encrypted: assert r.decrypt('user')
    c=r.trailer['/Root']; perms=c.get('/Perms',D()).get_object(); form=c.get('/AcroForm',D()).get_object()
    fields=r.get_fields() or {}
    sigs=[v['/V'] for v in fields.values() if v.get('/FT')=='/Sig' and isinstance(v.get('/V'),D)]
    return dict(rights='/UR3' in perms, docmdp='/DocMDP' in perms, signatures=len(sigs),
                xfa='/XFA' in form, fields={k:str(v.get('/V','')) for k,v in fields.items() if v.get('/FT')!='/Sig'},
                title=str(r.metadata.title or '') if r.metadata else '', encrypted=r.is_encrypted,
                encryptEntry='/Encrypt' in r.trailer, id=[str(x) for x in r.trailer.get('/ID',[])],
                marker=str(perms.get('/W199','')))
if mode=='generate':
    seed=scratch/'seed'; seed.mkdir(exist_ok=True)
    subprocess.run([sys.executable,'-I',str(root/'agents/reference/simplification/W-198/research.py'),'generate',str(root),str(seed),str(seed)],check=True)
    for name,source in [('rights','hybrid-rights'),('hybrid-rights','hybrid-rights'),('absent','plain')]:
        w=PdfWriter(clone_from=seed/'inputs'/f'{source}.pdf')
        if name=='rights': del w._root_object['/AcroForm']['/XFA']
        if name!='absent': w._root_object['/Perms'][N('/W199')]=T('retain-me')
        w.write(scratch/'inputs'/f'{name}.pdf')
    w=PdfWriter(clone_from=scratch/'inputs/rights.pdf'); w._root_object['/Perms']['/UR3'].pop('/Reference')
    w.write(scratch/'inputs/rights-no-grant.pdf')
    # Real CMS approval/certifications over a structurally modeled rights signature.
    spec=importlib.util.spec_from_file_location('samples',root/'test/jaytwo.Pdf.Tests/Fixtures/usage_rights_samples.py')
    mod=importlib.util.module_from_spec(spec); spec.loader.exec_module(mod)
    mod.MODES['certify-p1']=('Certify',mod.fields.MDPPerm.NO_CHANGES)
    for name in ('approve','certify-p1','certify-p2','certify-p3'):
        sys.argv=['samples',name,str(scratch/'inputs/rights.pdf'),str(scratch/'inputs'/f'{name}.pdf')]; mod.main()
    for revision in (4,6):
        for name in ('rights','hybrid-rights'):
            options=['128','--use-aes=y'] if revision==4 else ['256']
            subprocess.run([qpdf,str(scratch/'inputs'/f'{name}.pdf'),'--encrypt','user','owner',*options,'--modify=none','--form=y','--extract=n','--assemble=n','--',str(scratch/'inputs'/f'encrypted-r{revision}-{name}.pdf')],check=True)
    # Independent rewrite removes only UR3, not XFA. No permission or incremental API evidence.
    for name in ('rights','hybrid-rights'):
        w=PdfWriter(clone_from=scratch/'inputs'/f'{name}.pdf'); del w._root_object['/Perms']['/UR3']
        w.write(scratch/'inputs'/f'modeled-removed-{name}.pdf')
    manifest=[]
    for p in sorted((scratch/'inputs').glob('*.pdf')):
        manifest.append(dict(name=p.stem,sha256=hashlib.sha256(p.read_bytes()).hexdigest(),inspection=inspect(p)))
    # Recheck all previously classified corpus rights files, not a new whole-corpus census.
    corpus=[]
    for row in json.loads((root/'agents/reference/simplification/W-189/features.json').read_text()):
        if 'usage-rights' not in row['features']: continue
        p=root/row['path']; info=inspect(p)
        corpus.append(dict(path=row['path'],sha256=hashlib.sha256(p.read_bytes()).hexdigest(),matchesW189=hashlib.sha256(p.read_bytes()).hexdigest()==row['sha256'],inspection=info))
    dump('inputs.json',dict(pypdf=pypdf.__version__,fixtures=manifest,corpus=corpus))
    assert len(corpus)==6 and all(row['matchesW189'] and row['inspection']['rights'] for row in corpus)
    # Executable PROPOSAL state model: not a test of an engine removal API.
    def remove(s):
        if not s['rights']: return False
        if s['certified']: raise ValueError('PdfCertificationLockedException')
        if s['signed']: raise ValueError('PdfSignatureConflictException')
        if not s['modify']: raise ValueError('PdfEncryptionRestrictedException')
        before=copy.deepcopy(s)
        try:
            s['rights']=False
            if s.get('applyFailure'): raise ValueError('injected')
        except:
            s.clear(); s.update(before); raise
        return True
    model=[]
    for label,extra in [('unsigned',{}),('fill-only',dict(modify=False)),('signed',dict(signed=True)),('certified',dict(certified=True)),('apply-failure',dict(applyFailure=True)),('absent-signed',dict(rights=False,signed=True))]:
        s=dict(rights=True,xfa=True,signed=False,certified=False,modify=True,pendingTitle='earlier'); s.update(extra); before=copy.deepcopy(s)
        try: result=remove(s); again=remove(s); assert not again; error=''
        except ValueError as e: result=False; error=str(e); assert s==before
        assert s['pendingTitle']=='earlier' and s['xfa']
        model.append(dict(case=label,changed=result,error=error,state=s))
    dump('ordering-model.json',dict(kind='proposed pure-state model only; no production API',rows=model))
else:
    rows=json.loads((record/'results.json').read_text(encoding='utf-8-sig')); outputs=[]
    assert all(r['sourceUnchanged'] for r in rows)
    assert all(r['destinationBytes']==0 for r in rows if r['result']=='refused')
    assert all(r['prefix'] for r in rows if r['result']=='accepted')
    for p in sorted((scratch/'outputs').glob('*.pdf')):
        check=subprocess.run([qpdf,'--password=user','--check',str(p)],capture_output=True,text=True)
        outputs.append(dict(file=p.name,**inspect(p),qpdfExit=check.returncode,qpdfOutput=check.stdout+check.stderr))
    assert all(r['qpdfExit']==0 for r in outputs)
    assert len(outputs)==sum(r['result']=='accepted' for r in rows)
    by={r['file']:r for r in outputs}
    assert by['rights-none-fill.pdf']['rights'] and not by['rights-no-grant-none-fill.pdf']['rights']
    assert not by['hybrid-rights-none-fill.pdf']['rights'] and not by['hybrid-rights-none-fill.pdf']['xfa']
    assert by['approve-none-fill.pdf']['rights'] and by['certify-p2-none-fill.pdf']['rights']
    assert by['modeled-removed-hybrid-rights-none-save.pdf']['xfa'] and not by['modeled-removed-hybrid-rights-none-save.pdf']['rights']
    for name in ('rights','hybrid-rights'):
        modeled=by[f'modeled-removed-{name}-none-save.pdf']
        assert modeled['marker']=='retain-me' and modeled['fields']['Name']=='old'
    for revision in (4,6):
        for name in ('rights','hybrid-rights'):
            key=f'encrypted-r{revision}-{name}'
            assert any(r['name']==key and r['credential']=='user' and r['op']=='metadata' and r['error'].startswith('PdfEncryptionRestrictedException') for r in rows)
            after=by[key+'-user-refused-metadata-fill.pdf']
            assert after['fields']['Name']=='Ada' and after['encrypted'] and after['encryptEntry']
            initial=next(x['inspection'] for x in json.loads((record/'inputs.json').read_text())['fixtures'] if x['name']==key)
            assert initial['id']==after['id'] and after['rights']==(name=='rights')
    dump('inspection.json',dict(pypdf=pypdf.__version__,qpdf=subprocess.check_output([qpdf,'--version'],text=True).splitlines()[0],outputs=outputs))
    permissions=[]
    for p in sorted((scratch/'inputs').glob('encrypted-*.pdf')):
        result=subprocess.run([qpdf,'--password=user','--show-encryption',str(p)],capture_output=True,text=True,check=True)
        permissions.append(dict(file=p.name,output=result.stdout))
    dump('permissions.json',permissions)
    dump('summary.json',dict(observations=len(rows),accepted=sum(r['result']=='accepted' for r in rows),refused=sum(r['result']=='refused' for r in rows),outputs=len(outputs),python=sys.version,pypdf=pypdf.__version__,pyhanko=importlib.metadata.version('pyhanko'),sdk=subprocess.check_output(['dotnet','--version'],text=True).strip()))
