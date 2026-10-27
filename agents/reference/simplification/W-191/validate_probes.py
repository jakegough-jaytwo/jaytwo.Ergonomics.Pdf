"""Offline semantic oracle and independent-tool inspection; no conformance claim."""
import hashlib
import csv
import json
import os
import re
import shutil
import subprocess
import sys
import xml.etree.ElementTree as ET
from pathlib import Path
from pypdf import PdfReader, PdfWriter, __version__ as pypdf_version
from pypdf.generic import IndirectObject, NullObject

root, inputs, outputs = map(Path, sys.argv[1:4])
settings_path = root / '.tools/test.runsettings'
settings = {e.tag: e.text for e in ET.parse(settings_path).findall('.//EnvironmentVariables/*')} if settings_path.exists() else {}
qpdf = os.environ.get('JAYTWO_QPDF') or settings.get('JAYTWO_QPDF') or shutil.which('qpdf')
if not qpdf: raise RuntimeError('qpdf required for independent structural inspection')


def run(args):
    p = subprocess.run([str(x) for x in args], capture_output=True)
    if p.returncode: raise RuntimeError(f'{args}: {p.returncode}: {p.stderr.decode(errors="replace")}')
    return p.stdout.decode(errors='replace')


def ident(ref):
    return (ref.idnum, ref.generation) if isinstance(ref, IndirectObject) else None


def inspect(path):
    reader = PdfReader(path)
    catalog = reader.trailer['/Root']
    pages = {ident(p.indirect_reference): n + 1 for n, p in enumerate(reader.pages)}
    tree = catalog.get('/StructTreeRoot')
    errors, items, roles, classes, elements = [], [], {}, {}, []
    if tree:
        tree = tree.get_object()
        roles = {str(k): str(v) for k, v in tree.get('/RoleMap', {}).items()}
        classes = {str(k): str(v) for k, v in tree.get('/ClassMap', {}).items()}
        pt = tree['/ParentTree'].get('/Nums', [])
        parent_map = {int(pt[n]): pt[n + 1] for n in range(0, len(pt), 2)}
        def walk(raw, page=None, owner=None, parent=None, active=None):
            active = set() if active is None else active
            key = ident(raw)
            if key and key in active:
                errors.append('cycle'); return
            value = raw.get_object() if hasattr(raw, 'get_object') else raw
            if isinstance(value, list):
                for child in value: walk(child, page, owner, parent, active)
                return
            if isinstance(value, int):
                if page not in pages: errors.append('invalid-page'); return
                p = reader.pages[pages[page] - 1]
                parents = parent_map.get(int(p['/StructParents']))
                if parents is None or int(value) >= len(parents.get_object()): errors.append('parent-missing')
                elif ident(parents.get_object()[int(value)]) != owner: errors.append('parent-mismatch')
                content = p['/Contents'].get_data()
                # Known fixture-only token grammar, not a general PDF content parser.
                label = re.search(rb'\((Page\d+)\) Tj', content)
                if f'/MCID {value}'.encode() not in content: errors.append('missing-fixture-mcid')
                items.append({'page': pages[page], 'mcid': int(value), 'text': label[1].decode() if label else None})
                return
            if not isinstance(value, dict): errors.append('invalid-kid'); return
            page = ident(value.get('/Pg')) or page
            kind = str(value.get('/Type', ''))
            if kind == '/OBJR':
                obj = value['/Obj']; key_value = int(obj['/StructParent'])
                if ident(parent_map.get(key_value)) != owner: errors.append('objr-parent-mismatch')
                if page not in pages or ident(value.get('/Obj')) not in [ident(a) for a in reader.pages[pages[page]-1].get('/Annots', [])]:
                    errors.append('objr-page-mismatch')
                items.append({'page': pages.get(page), 'annotation': str(obj['/Subtype']), 'alt': parent.get('/Alt')})
                return
            if kind == '/MCR':
                walk(value['/MCID'], page, owner, parent, active); return
            if kind != '/StructElem': errors.append('unknown-kind'); return
            elements.append({k: str(value[k]) for k in ['/S', '/Lang', '/Alt', '/ActualText', '/C'] if k in value})
            if ident(value.get('/P')) != (ident(tree.indirect_reference) if parent is None else ident(parent.indirect_reference)):
                errors.append('nonreciprocal-parent')
            if key: active.add(key)
            walk(value['/K'], page, key, value, active)
            if key: active.remove(key)
        walk(tree['/K'])
    marked = catalog.get('/MarkInfo', {}).get('/Marked')
    return {'pages': len(reader.pages), 'tree': bool(tree), 'marked': bool(marked and marked.value),
            'language': catalog.get('/Lang'), 'roleMap': roles, 'classMap': classes,
            'items': items, 'elements': elements, 'errors': sorted(set(errors)),
            'pageParentKeys': [p.get('/StructParents') for p in reader.pages],
            'annotationParentKeys': [a.get_object().get('/StructParent') for p in reader.pages for a in p.get('/Annots', [])]}


manifest = json.loads((inputs / 'manifest.json').read_text())
input_results = {}
for entry in manifest:
    path = inputs / (entry['id'] + '.pdf')
    assert hashlib.sha256(path.read_bytes()).hexdigest() == entry['sha256']
    info = inspect(path)
    assert bool(info['errors']) == entry['malformed'], (entry, info)
    if not entry['malformed']:
        assert [x['text'] for x in info['items'] if 'text' in x] == entry['expectedLogicalOrder']
    if entry['id'] == 'mappings':
        assert info['roleMap'] == {'/BusinessPara': '/P'}
        assert info['elements'][1]['/Alt'] == 'First page alternate description'
        assert info['elements'][1]['/ActualText'] == 'First page replacement text'
        assert info['elements'][1]['/C'] == '/BusinessStyle'
    if entry['id'] == 'tagged-form':
        assert info['items'][1] == {'page': 1, 'annotation': '/Widget', 'alt': 'Customer name'}
    info['qpdfCheck'] = run([qpdf, '--check', path])
    input_results[entry['id']] = info

probe = json.loads((outputs / 'probe.json').read_text())
output_results = []
orders = {'subset': [1], 'reorder': [2, 1], 'concat': [1, 2, 1], 'repeat': [1, 1, 2, 1]}
for entry in probe:
    assert entry['outcome'] == 'accepted', entry
    name = f"{entry['id']}-{entry['operation']}.pdf"
    path = outputs / name
    info = inspect(path)
    assert not info['tree'] and not info['marked']
    assert all(x is None for x in info['pageParentKeys'] + info['annotationParentKeys'])
    assert entry['omissions'] == {}, entry
    source = PdfReader(inputs / (entry['id'] + '.pdf'))
    target = PdfReader(path)
    order = orders[entry['operation']]
    assert len(target.pages) == len(order)
    for actual, number in zip(target.pages, order):
        assert actual['/Contents']._data == source.pages[number - 1]['/Contents']._data
        assert str(actual['/Contents'].get('/Filter')) == str(source.pages[number - 1]['/Contents'].get('/Filter'))
    check = run([qpdf, '--check', path])
    independent = json.loads(run([qpdf, '--json', path]))
    serialized = json.dumps(independent)
    assert '"/StructTreeRoot"' not in serialized and '"/StructParents"' not in serialized and '"/StructParent"' not in serialized
    if entry['id'] == 'tagged-form':
        edited_path = outputs / name.replace('.pdf', '-edit.pdf')
        assert edited_path.read_bytes().startswith(path.read_bytes())
        fields = PdfReader(edited_path).get_fields()
        assert str(fields['Input1.Name']['/V']) == 'Ada'
        if entry['operation'] in ('concat', 'repeat'): assert str(fields['Input2.Name']['/V']) == 'Original'
        assert not inspect(edited_path)['tree']
        run([qpdf, '--check', edited_path])
    output_results.append({'file': name, 'pages': info['pages'], 'tree': info['tree'],
                           'qpdfCheck': 'passed', 'qpdfJsonKeys': 'tag ownership absent', 'encodedContents': 'identical'})

collision = inspect(outputs / 'map-collision.pdf')
assert not collision['tree'] and not collision['roleMap'] and not collision['classMap']
run([qpdf, '--check', outputs / 'map-collision.pdf'])

# Same ordinary inputs, pinned installed libraries. This tests defaults, not accessibility quality.
precedents = []
for mode in ['minimal', 'tagged-form', 'cross-page']:
    path = inputs / (mode + '.pdf')
    for operation, selected in [('subset', [0]), ('reorder', [1, 0]), ('repeat', [0, 0, 1])]:
        writer = PdfWriter(); writer.append(str(path), pages=selected)
        output = outputs / f'pypdf-{mode}-{operation}.pdf'; writer.write(output)
        info = inspect(output)
        precedents.append({'library': f'pypdf {pypdf_version}', 'input': mode, 'operation': operation,
            'tree': info['tree'], 'pageParentKeys': info['pageParentKeys']})
        output = outputs / f'qpdf-empty-{mode}-{operation}.pdf'
        run([qpdf, '--empty', '--pages', path, ','.join(str(n + 1) for n in selected), '--', output])
        info = inspect(output)
        precedents.append({'library': 'qpdf 12.3.2 --empty', 'input': mode, 'operation': operation,
            'tree': info['tree'], 'pageParentKeys': info['pageParentKeys']})
        output = outputs / f'qpdf-primary-{mode}-{operation}.pdf'
        run([qpdf, path, '--pages', '.', ','.join(str(n + 1) for n in selected), '--', output])
        info = inspect(output)
        precedents.append({'library': 'qpdf 12.3.2 primary', 'input': mode, 'operation': operation,
            'tree': info['tree'], 'pageParentKeys': info['pageParentKeys'], 'items': info['items'], 'errors': info['errors']})

with (root / 'agents/reference/simplification/W-188/fixture-manifest.csv').open() as stream:
    corpus = list(csv.DictReader(stream))
for entry in corpus:
    assert hashlib.sha256((root / entry['path']).read_bytes()).hexdigest() == entry['sha256']

evidence = {'productRevision': run(['git', '-C', root, 'rev-parse', 'HEAD']).strip(),
            'pypdf': pypdf_version, 'qpdfVersion': run([qpdf, '--version']).strip(),
            'inputs': input_results, 'outputs': output_results, 'mapCollision': collision, 'precedents': precedents,
            'W188CorpusHashesRechecked': len(corpus),
            'limits': 'Fixture-specific offline semantic oracle, not an accessibility validator or independent reviewer.'}
(outputs / 'inspection.json').write_text(json.dumps(evidence, indent=2) + '\n')
print(f'Passed: {len(input_results)} semantic fixture inspections, {len(output_results)} outputs, 4 form edits, map collision; {len(precedents)} precedent observations')
