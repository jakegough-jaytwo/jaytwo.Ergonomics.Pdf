"""Bounded assembly research. Synthetic fixtures are CC0; no conformance claim."""
import hashlib
import json
import subprocess
import sys
from pathlib import Path
from xml.etree import ElementTree as ET
import pypdf
from pypdf import PdfReader, PdfWriter
from pypdf.generic import (DictionaryObject as D, NameObject as N, ArrayObject as A,
    NumberObject as I, BooleanObject as B, TextStringObject as T, ByteStringObject as S,
    DecodedStreamObject)

mode, scratch, records, qpdf = sys.argv[1:]
scratch, records = Path(scratch), Path(records)
def save(name, value):
    (records/name).write_text(json.dumps(value, indent=2, ensure_ascii=False)+'\n', encoding='utf-8')
def run(*args):
    p = subprocess.run([str(a) for a in args], capture_output=True, text=True, encoding='utf-8', errors='replace')
    if p.returncode: raise RuntimeError(str(args)+': '+p.stdout+p.stderr)
    return p.stdout.strip()
def plain(value):
    value = value.get_object() if hasattr(value, 'get_object') else value
    if isinstance(value, dict): return {str(k): plain(v) for k, v in value.items() if k not in ('/Parent', '/P')}
    if isinstance(value, list):
        return ['PAGE' if hasattr(v, 'get_object') and isinstance(v.get_object(), dict) and v.get_object().get('/Type') == '/Page' else plain(v) for v in value]
    if isinstance(value, bytes): return value.hex()
    if isinstance(value, B): return bool(value.value)
    if isinstance(value, (int, float)): return value
    return str(value)
KEYS = ['/ViewerPreferences', '/PageMode', '/PageLayout', '/OpenAction', '/AA', '/OutputIntents', '/Lang']
def inspect(path):
    r = PdfReader(path)
    root = r.root_object
    metadata = root.get('/Metadata')
    raw = metadata.get_object().get_data() if metadata is not None else None
    return {'pages': len(r.pages), 'info': plain(r.metadata or {}),
        'id': [v.original_bytes.hex() for v in r.trailer.get('/ID', [])],
        'xmp': raw.decode('utf-8', errors='replace') if raw is not None else None,
        'xmpSha256': hashlib.sha256(raw).hexdigest() if raw is not None else None,
        'catalog': {k: plain(root[k]) for k in KEYS if k in root},
        'header': r.pdf_header}

scenarios = {name: [name] for name in ['identity-a','identity-b','minimal','malformed','custom']}
scenarios.update({'a-b': ['identity-a','identity-b'], 'b-a': ['identity-b','identity-a'], 'a-a': ['identity-a','identity-a']})
if mode == 'generate':
    for index, name in enumerate(list(scenarios)[:5]):
        w = PdfWriter(); w.add_blank_page(100, 100); w.pdf_header = '%PDF-1.7'; w.metadata = None
        w._ID = A([S(bytes([index+1])*16), S(bytes([index+1])*16)])
        if name != 'minimal':
            w.add_metadata({'/Title': name, '/Author': 'Source Author', '/Subject': 'Input only', '/Keywords': 'original', '/Creator': 'Source App', '/Producer': 'Source Producer', '/CreationDate': 'D:20200102030405Z', '/ModDate': 'D:20200203040506Z', '/Department': 'Accounts', '/Trapped': '/False'})
            w._info.get_object()[N('/Trapped')] = N('/False')
            w.root_object.update({N('/PageMode'): N('/FullScreen' if name == 'identity-b' else '/UseOutlines'), N('/PageLayout'): N('/TwoColumnRight'), N('/Lang'): T('en-US')})
            prefs = D({N('/HideToolbar'): B(True), N('/DisplayDocTitle'): B(True), N('/Direction'): N('/R2L'), N('/PrintScaling'): N('/None'), N('/Duplex'): N('/DuplexFlipLongEdge'), N('/PickTrayByPDFSize'): B(True), N('/PrintPageRange'): A([I(0), I(0)]), N('/NumCopies'): I(3)})
            w.root_object[N('/ViewerPreferences')] = prefs
            # No script: destination array or URI action. Nothing is executed by this probe.
            w.root_object[N('/OpenAction')] = D({N('/S'): N('/URI'), N('/URI'): T('https://example.invalid/identity')}) if name == 'identity-b' else A([w.pages[0].indirect_reference, N('/Fit')])
            body = '<biz:Record xmlns:biz="urn:w201:business" biz:code="42">source-only</biz:Record>'
            if name != 'custom': body += '<pdfaid:part xmlns:pdfaid="http://www.aiim.org/pdfa/ns/id/">2</pdfaid:part><pdfaid:conformance xmlns:pdfaid="http://www.aiim.org/pdfa/ns/id/">B</pdfaid:conformance><xmpMM:DocumentID xmlns:xmpMM="http://ns.adobe.com/xap/1.0/mm/">uuid:source-'+name+'</xmpMM:DocumentID>'
            packet = ('<x:xmpmeta xmlns:x="adobe:ns:meta/"><rdf:RDF xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#"><rdf:Description rdf:about="">'+body+'</rdf:Description></rdf:RDF></x:xmpmeta>').encode()
            if name == 'malformed': packet = b'<broken-xmp'
            stream = DecodedStreamObject(); stream.set_data(packet); stream.update({N('/Type'): N('/Metadata'), N('/Subtype'): N('/XML')})
            w.root_object[N('/Metadata')] = w._add_object(stream)
        w.write(scratch/'inputs'/f'{name}.pdf')
    save('scenarios.json', scenarios)
    save('inputs.json', {'pypdf': pypdf.__version__, 'qpdf': run(qpdf, '--version'), 'fixtures': [{'name': p.name, 'sha256': hashlib.sha256(p.read_bytes()).hexdigest(), 'bytes': p.stat().st_size, 'state': inspect(p)} for p in sorted((scratch/'inputs').glob('*.pdf'))]})
elif mode == 'compare':
    for name, sources in scenarios.items():
        paths = [scratch/'inputs'/f'{s}.pdf' for s in sources]
        for primary in [False, True]:
            out = scratch/'outputs'/f'{name}-qpdf-{"primary" if primary else "empty"}.pdf'
            selection = [v for p in paths for v in [p, '1']]
            run(qpdf, paths[0] if primary else '--empty', '--pages', *selection, '--', out)
        w = PdfWriter()
        for p in paths: w.append(p, pages=[0])
        w.write(scratch/'outputs'/f'{name}-pypdf-empty.pdf')
        w = PdfWriter(clone_from=paths[0])
        for p in paths[1:]: w.append(p, pages=[0])
        w.write(scratch/'outputs'/f'{name}-pypdf-primary.pdf')
elif mode == 'inspect':
    rows = []
    for p in sorted((scratch/'outputs').glob('*.pdf')):
        check = subprocess.run([qpdf, '--check', str(p)], capture_output=True, text=True)
        rows.append({'name': p.name, 'qpdfExit': check.returncode, 'diagnostic': check.stderr.strip(), 'state': inspect(p)})
    save('inspection.json', rows)
    engine = json.loads((records/'engine.json').read_text(encoding='utf-8-sig'))
    assert len(engine) == 8 and all(r['result'] == 'accepted' and r['sourceUnchanged'] for r in engine)
    assert len(rows) == 63, len(rows)
    for row in rows:
        assert row['qpdfExit'] == 0, row
        if row['name'].endswith('-engine.pdf'):
            state = row['state']; scenario = row['name'].removesuffix('-engine.pdf')
            assert state['info'] == {'/Producer': 'jaytwo.Pdf'}, state
            assert state['catalog'] == {} and state['xmp'] is None, state
            assert state['pages'] == len(scenarios[scenario])
            assert len(state['id']) == 2 and state['id'][0] == state['id'][1]
    ids = [r['state']['id'][0] for r in rows if r['name'].endswith('-engine.pdf')]
    assert len(set(ids)) == 8 and not set(ids) & { (bytes([i])*16).hex() for i in range(1,6) }
    recovery = json.loads((records/'recovery.json').read_text(encoding='utf-8-sig'))
    assert all(r['prefix'] and r['beforeSaveBytes'] == 0 for r in recovery)
    for kind in ('info', 'custom'):
        state = next(r['state'] for r in rows if r['name'] == 'recovery-'+kind+'.pdf')
        assert state['info']['/Title'] == 'Combined dossier' and state['info']['/Author'] == 'Accounts Team'
        assert state['id'][0] == ids[[r['name'] for r in rows if r['name'].endswith('-engine.pdf')].index('a-b-engine.pdf')]
        packet = ET.fromstring(state['xmp'])
        assert packet.find('.//{http://purl.org/dc/elements/1.1/}title/{http://www.w3.org/1999/02/22-rdf-syntax-ns#}Alt/{http://www.w3.org/1999/02/22-rdf-syntax-ns#}li').text == 'Combined dossier'
        assert packet.find('.//{http://purl.org/dc/elements/1.1/}creator/{http://www.w3.org/1999/02/22-rdf-syntax-ns#}Seq/{http://www.w3.org/1999/02/22-rdf-syntax-ns#}li').text == 'Accounts Team'
        assert ('urn:w201:business' in state['xmp']) == (kind == 'custom')
        assert 'pdfaid' not in state['xmp']
    losses = []
    originals = {Path(f['name']).stem: f['state'] for f in json.loads((records/'inputs.json').read_text())['fixtures']}
    for name in list(scenarios)[:5]:
        source = originals[name]
        dest = next(r['state'] for r in rows if r['name'] == name+'-engine.pdf')
        losses.append({'fixture': name, 'removedInfoKeys': sorted(set(source['info']) - set(dest['info'])), 'producerReplaced': '/Producer' in source['info'], 'removedCatalogKeys': sorted(source['catalog']), 'xmpOmitted': source['xmp'] is not None})
    by_name = {r['name']: r['state'] for r in rows}
    for scenario, sources in scenarios.items():
        original = originals[sources[0]]
        for suffix in ('qpdf-primary', 'pypdf-primary'):
            dest = by_name[scenario+'-'+suffix+'.pdf']
            assert dest['info'] == original['info'] and dest['xmpSha256'] == original['xmpSha256']
            assert dest['catalog'] == original['catalog']
            assert dest['id'][0] == original['id'][0]
        for suffix in ('qpdf-empty', 'pypdf-empty', 'pdfbox-OPTIMIZE_RESOURCES_MODE'):
            dest = by_name[scenario+'-'+suffix+'.pdf']
            assert dest['xmp'] is None and dest['catalog'] == {}
        dest = by_name[scenario+'-pdfbox-PDFBOX_LEGACY_MODE.pdf']
        assert dest['info'] == original['info'] and dest['xmpSha256'] == original['xmpSha256']
        assert dest['catalog'] == {k:v for k,v in original['catalog'].items() if k == '/OpenAction'}
    for name in list(scenarios)[:5]:
        original = originals[name]; dest = by_name[name+'-pdfbox-split.pdf']
        assert dest['info'] == original['info'] and dest['xmpSha256'] == original['xmpSha256']
        assert dest['catalog'] == {k:v for k,v in original['catalog'].items() if k in ('/ViewerPreferences','/Lang')}
    save('summary.json', {'outputs': len(rows), 'cleanQpdfChecks': sum(r['qpdfExit'] == 0 for r in rows), 'engineAccepted': len(engine), 'engineDistinctFreshIds': len(set(ids)), 'losses': losses, 'recovery': recovery})
