"""Independent-reader, qpdf and two-renderer checks for the named research probes."""
import hashlib
import json
import os
import shutil
import subprocess
import sys
import xml.etree.ElementTree as ET
from pathlib import Path
from pypdf import PdfReader

root, inputs, outputs = map(Path, sys.argv[1:4])
settings_path = root / '.tools/test.runsettings'
settings = {e.tag: e.text for e in ET.parse(settings_path).findall('.//EnvironmentVariables/*')} if settings_path.exists() else {}
def locate(name, variable):
    path = os.environ.get(variable) or settings.get(variable) or shutil.which(name)
    if not path: raise RuntimeError(f'Missing {name}; install repository test tools or set {variable}')
    return Path(path)
qpdf = locate('qpdf','JAYTWO_QPDF')
poppler = locate('pdftoppm','JAYTWO_PDFTOPPM')
mupdf = locate('mutool','JAYTWO_MUTOOL')
def run(cmd):
    p = subprocess.run([str(x) for x in cmd], capture_output=True)
    if p.returncode: raise RuntimeError(f'{cmd}: {p.returncode}\n{p.stderr.decode(errors="replace")}')
    return p.stdout.decode(errors='replace').strip()
def key(ref): return (ref.idnum, ref.generation)
def widget_info(widget):
    value = None; parts = []; seen = set(); field = None
    current = widget
    while current is not None:
        obj = current.get_object(); ident = key(current)
        assert ident not in seen, 'parent cycle'; seen.add(ident)
        if '/V' in obj and value is None: value = str(obj['/V'])
        if '/T' in obj:
            parts.insert(0, str(obj['/T']))
            if field is None: field = ident
        parent = obj.get('/Parent')
        if parent:
            assert ident in [key(x) for x in parent.get_object()['/Kids']], 'nonreciprocal parent'
        current = parent
    return '.'.join(parts), value, field

def inherited(widget, name):
    obj = widget.get_object()
    for _ in range(260):
        if name in obj: return obj[name]
        if '/Parent' not in obj: return None
        obj = obj['/Parent']
    raise AssertionError('unbounded field parent chain')

checked = []
good = ['nested','courier','merged','no-p','need-appearances','resource-cycle','checkbox','choice']
rows = json.loads((outputs/'probe.json').read_text(encoding='utf-8-sig'))
for entry in json.loads((inputs/'manifest.json').read_text()):
    assert hashlib.sha256((inputs/entry['path']).read_bytes()).hexdigest() == entry['sha256']
late = json.loads((outputs/'late-refusal.json').read_text())
assert late == {'failure':'PdfAssemblyNotSupportedException','bytes':0}
assert len(rows) == 32
refusals = [r for r in rows if r['result'] == 'failed']
assert len(refusals) == 12
assert all(r['stage'] == 'assemble' and r['bytes'] == 0
           and r['type'] == 'PdfAssemblyNotSupportedException' for r in refusals)
for row in rows:
    if row['id'] in ['unsupported-ap','ap-extra-key']:
        assert row['result'] == 'accepted'
        expected = 1 if row['op'] == 'extract' else (3 if row['id'] == 'unsupported-ap' else 4)
        assert row['omissions'] == {'UnsupportedAppearance': expected}
        run([qpdf,'--check',outputs/(row['id']+'-'+row['op']+'.pdf')])
source_checks = []
for id in good + ['ap-extra-key','non-font-dr','unselected-action']:
    run([qpdf,'--check',inputs/(id+'.pdf')])
    source_checks.append(id)
def normal(widget):
    n = widget['/AP']['/N']
    return n[widget['/AS']] if '/AS' in widget else n
for id in good:
    source = PdfReader(inputs/(id+'.pdf'))
    for op, selection in [('extract',[0]),('repeat',[1,0,0,0])]:
        path = outputs/(id+'-'+op+'.pdf'); r = PdfReader(path)
        if id == 'need-appearances': assert r.trailer['/Root']['/AcroForm']['/NeedAppearances']
        run([qpdf,'--check',path])
        assert len(r.pages) == len(selection)
        ownership = []; ap_refs = []; content_refs = []
        for index, page in enumerate(r.pages):
            src = source.pages[selection[index]]
            assert list(page.mediabox) == list(src.mediabox)
            assert list(page.cropbox) == list(src.cropbox)
            assert page.rotation == src.rotation == 90
            assert page['/Contents']._data == src['/Contents']._data
            assert page['/Contents']['/Filter'] == '/FlateDecode'
            widgets = [x for x in page.get('/Annots',[]) if x.get_object().get('/Subtype') == '/Widget']
            if id == 'merged' and selection[index] == 1:
                assert len(widgets) == 0; continue
            assert len(widgets) == 1
            widget_ref = widgets[0]; widget = widget_ref.get_object()
            assert key(widget['/P'].indirect_reference) == key(page.indirect_reference)
            assert '/T' not in widget
            name, value, field = widget_info(widget_ref)
            occurrence = 2 if op == 'repeat' and index == 3 else 1
            assert name == f'Input{occurrence}.'+('Name' if id == 'merged' else 'Group.Name')
            assert value == ('/Off' if id == 'checkbox' else 'Original')
            assert inherited(widget_ref,'/FT') == ('/Btn' if id == 'checkbox' else '/Ch' if id == 'choice' else '/Tx')
            assert str(inherited(widget_ref,'/DA')).startswith('/Input'+str(occurrence)+'_0 ')
            if id != 'merged':
                assert inherited(widget_ref,'/DV') == ('/Off' if id == 'checkbox' else 'Default')
                assert inherited(widget_ref,'/Q') == 0 and inherited(widget_ref,'/Ff') == 0
            if id == 'choice': assert list(inherited(widget_ref,'/Opt')) == ['Original','Ada']
            assert normal(widget)._data == normal(src['/Annots'][0].get_object())._data
            ownership.append((key(widget_ref), field, occurrence))
            ap_refs.append(key(normal(widget).indirect_reference))
            content_refs.append(key(page['/Contents'].indirect_reference))
        assert len({w for w,f,o in ownership}) == len(ownership)
        if op == 'repeat':
            fields1 = {f for w,f,o in ownership if o == 1}; fields2 = {f for w,f,o in ownership if o == 2}
            assert len(fields1) == len(fields2) == 1 and fields1.isdisjoint(fields2)
            # Shared appearances are allowed within an occurrence; fields and widgets are not shared across it.
            assert len(set(ap_refs[:-1])) == 1 and ap_refs[0] != ap_refs[-1]
        edited_path = outputs/(id+'-'+op+'-edit.pdf')
        run([qpdf,'--check',edited_path]); edited = PdfReader(edited_path)
        assert edited_path.read_bytes().startswith(path.read_bytes())
        for index, page in enumerate(edited.pages):
            for widget in page.get('/Annots',[]):
                name, value, field = widget_info(widget)
                assert value == (('/Off' if name.startswith('Input2.') else '/Yes') if id == 'checkbox'
                                 else ('Original' if name.startswith('Input2.') else 'Ada'))
                if id == 'checkbox': assert widget.get_object()['/AS'] == value
                if id == 'choice' and not name.startswith('Input2.'):
                    indices = widget.get_object()['/Parent'].get('/I')
                    assert indices is None or list(indices) == [1]
        checked.append({'fixture': id, 'op': op, 'structure':'pass', 'edit_values':'pass',
                        'incremental_prefix':'pass', 'qpdf':'pass'})

collision = PdfReader(outputs/'font-collision.pdf')
names = [widget_info(p['/Annots'][0])[0] for p in collision.pages]
assert names == ['Input2.Group.Name','Input3.Group.Name']
fonts = collision.trailer['/Root']['/AcroForm']['/DR']['/Font']
assert fonts['/Input2_0']['/BaseFont'] == '/Helvetica'
assert fonts['/Input3_0']['/BaseFont'] == '/Courier'
for parent in collision.trailer['/Root']['/AcroForm']['/Fields']:
    p = parent.get_object(); assert p['/DA'].startswith('/'+p['/T']+'_0 ')
run([qpdf,'--check',outputs/'font-collision.pdf'])

# Pixel equality checks use PPM bytes at 72dpi, independently for each renderer.
# NeedAppearances intentionally excluded: viewers can regenerate instead of using /AP.
rasters = outputs/'rasters'; rasters.mkdir(exist_ok=True)
render_checks = []
def raster(renderer, path, page, stem):
    target = rasters/(stem+'.ppm')
    if renderer == 'poppler':
        run([poppler,'-r','72','-f',page,'-l',page,'-singlefile',path,target.with_suffix('')])
    else: run([mupdf,'draw','-q','-r','72','-o',target,path,page])
    return target.read_bytes()
for id in ['nested','courier','merged','no-p','resource-cycle','checkbox','choice']:
    for renderer in ['poppler','mupdf']:
        source = [raster(renderer,inputs/(id+'.pdf'),p,f'{renderer}-{id}-src-{p}') for p in [1,2]]
        for op, selection in [('extract',[1]),('repeat',[2,1,1,1])]:
            for i, srcpage in enumerate(selection, 1):
                before = raster(renderer,outputs/(id+'-'+op+'.pdf'),i,f'{renderer}-{id}-{op}-{i}')
                assert before == source[srcpage-1], f'{renderer} {id} {op} {i} source mismatch'
                after = raster(renderer,outputs/(id+'-'+op+'-edit.pdf'),i,f'{renderer}-{id}-{op}-edit-{i}')
                changed_expected = not (id == 'merged' and srcpage == 2) and not (op == 'repeat' and i == 4)
                assert (before != after) == changed_expected, f'{renderer} {id} {op} {i} edit mismatch'
            render_checks.append({'fixture': id, 'renderer': renderer, 'op': op, 'source_pixels':'identical',
                                  'edit_pixels':'only occurrence 1 selected widgets changed'})
for renderer in ['poppler','mupdf']:
    plain = raster(renderer,inputs/'nested.pdf',1,f'{renderer}-plain')
    extra = raster(renderer,inputs/'ap-extra-key.pdf',1,f'{renderer}-extra')
    omitted = raster(renderer,outputs/'ap-extra-key-extract.pdf',1,f'{renderer}-omitted')
    assert extra == plain and extra != omitted
result = {'reader': 'pypdf '+__import__('pypdf').__version__,
          'qpdf':run([qpdf,'--version']).splitlines()[0],
          'poppler':subprocess.run([str(poppler),'-v'],capture_output=True).stderr.decode().splitlines()[0],
          'mupdf':subprocess.run([str(mupdf),'-v'],capture_output=True).stderr.decode().splitlines()[0],
          'checks':checked,'source_qpdf_checks':source_checks,
          'refusals':'12/32 operations; typed assembly error, zero destination bytes',
          'late_refusal':late, 'source_hashes':'unchanged after operations',
          'omissions':'4/32 operations; exact UnsupportedAppearance counts verified; LastModified source renders like nested, output loses widget in both renderers',
          'font_collision':'pass; Input2=Helvetica, Input3=Courier; empty occurrence consumes Input1',
          'renders':render_checks}
(outputs/'inspection.json').write_text(json.dumps(result,indent=2)+'\n')
print(f'{len(checked)} reader/edit/prefix checks; font collision; {len(render_checks)} render scenarios passed')
