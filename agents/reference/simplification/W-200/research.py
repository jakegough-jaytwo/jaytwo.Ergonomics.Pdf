"""Bounded research, not product or proposed synchronizer implementation."""
import hashlib
import json
import subprocess
import sys
from pathlib import Path
from xml.etree import ElementTree as ET
import pypdf
from pypdf import PdfReader, PdfWriter
from pypdf.generic import DictionaryObject, NameObject, DecodedStreamObject, NumberObject, ArrayObject

mode, scratch, record, qpdf = sys.argv[1:]
scratch, record = Path(scratch), Path(record)
NS = {'rdf': 'http://www.w3.org/1999/02/22-rdf-syntax-ns#', 'dc': 'http://purl.org/dc/elements/1.1/', 'xmp': 'http://ns.adobe.com/xap/1.0/'}
def packet(body):
    return ('<x:xmpmeta xmlns:x="adobe:ns:meta/"><rdf:RDF xmlns:rdf="'+NS['rdf']+'"><rdf:Description rdf:about="" xmlns:dc="'+NS['dc']+'" xmlns:xmp="'+NS['xmp']+'" xmlns:biz="urn:w200:custom">'+body+'</rdf:Description></rdf:RDF></x:xmpmeta>').encode()
def title(value):
    return '<dc:title><rdf:Alt><rdf:li xml:lang="x-default">'+value+'</rdf:li></rdf:Alt></dc:title>'
def save_json(name, value):
    (record/name).write_text(json.dumps(value, indent=2, ensure_ascii=False)+'\n', encoding='utf-8')
def run(*args):
    p = subprocess.run([str(a) for a in args], capture_output=True, text=True, encoding='utf-8', errors='replace')
    if p.returncode: raise RuntimeError(str(args)+': '+p.stdout+p.stderr)
    return p.stdout
def inspect(path):
    r = PdfReader(path)
    if r.is_encrypted: r.decrypt('owner')
    root = r.trailer['/Root']
    stream = root.get('/Metadata')
    raw = stream.get_object().get_data() if stream else None
    parsed, xml_error = None, None
    if raw is not None:
        # DTD fixtures are never parsed here; this is not a proposed XML validator.
        if b'<!DOCTYPE' in raw: xml_error = 'DTD not inspected'
        else:
            try: parsed = ET.fromstring(raw)
            except ET.ParseError as e: xml_error = str(e)
    titles = [] if parsed is None else [{ 'lang': e.get('{http://www.w3.org/XML/1998/namespace}lang'), 'value': e.text } for e in parsed.findall('.//dc:title/rdf:Alt/rdf:li', NS)]
    authors = [] if parsed is None else [e.text for e in parsed.findall('.//dc:creator/rdf:Seq/rdf:li', NS)]
    return {'info': dict(r.metadata or {}), 'hasXmp': raw is not None, 'xmpSha256': hashlib.sha256(raw).hexdigest() if raw is not None else None, 'titles': titles, 'authors': authors, 'modifyDates': [] if parsed is None else [e.text for e in parsed.findall('.//xmp:ModifyDate', NS)], 'custom': [] if parsed is None else [ET.tostring(e, encoding='unicode') for e in parsed.findall('.//{urn:w200:custom}Record')], 'xmlError': xml_error, 'header': r.pdf_header, 'catalogVersion': str(root.get('/Version', '')), 'hasRdf': parsed is not None and parsed.find('.//rdf:RDF', NS) is not None}

if mode == 'generate':
    common = title('Old')+'<dc:creator><rdf:Seq><rdf:li>Ada</rdf:li><rdf:li>Bob</rdf:li></rdf:Seq></dc:creator><xmp:ModifyDate>2020-01-01T00:00:00Z</xmp:ModifyDate>'
    custom = '<biz:Record biz:flag="keep"><biz:Child>opaque &amp; retained</biz:Child></biz:Record>'
    fixtures = {
        'neither': (None, None), 'info-only': ('Old', None),
        'xmp-only': (None, packet(common)), 'consistent': ('Old', packet(common)),
        'conflict': ('Info wins?', packet(title('XMP wins?'))),
        'malformed': ('Old', b'<broken'), 'no-rdf': ('Old', b'<not-xmp>well formed</not-xmp>'),
        'custom': ('Old', packet(common+custom)),
        'multilingual': ('Old', packet('<dc:title><rdf:Alt><rdf:li xml:lang="fr">Bonjour</rdf:li><rdf:li xml:lang="x-default">Old</rdf:li><rdf:li xml:lang="de">Hallo</rdf:li></rdf:Alt></dc:title>'+custom)),
        'duplicate': ('Old', packet(title('Old')+title('Other'))),
        'dtd': ('Old', b'<!DOCTYPE x [<!ENTITY e SYSTEM "file:///W200-do-not-resolve">]><x>&e;</x>'),
    }
    manifest = []
    for name, (info_title, xmp) in fixtures.items():
        w = PdfWriter(); w.add_blank_page(100, 100); w.pdf_header = '%PDF-1.3'; w.metadata = None
        if info_title is not None: w.add_metadata({'/Title': info_title, '/Author': 'Ada; Bob', '/Department': 'keep', '/ModDate': 'D:20200101000000Z'})
        if xmp is not None:
            stream = DecodedStreamObject(); stream.set_data(xmp)
            stream.update({NameObject('/Type'): NameObject('/Metadata'), NameObject('/Subtype'): NameObject('/XML')})
            w.root_object[NameObject('/Metadata')] = w._add_object(stream)
        w.write(scratch/'inputs'/f'{name}.pdf')
    # Permission and certification probes exercise gates only, not signature validity.
    for level in (1, 2, 3):
        w = PdfWriter(clone_from=scratch/'inputs/consistent.pdf')
        params = DictionaryObject({NameObject('/P'): NumberObject(level)})
        ref = DictionaryObject({NameObject('/TransformMethod'): NameObject('/DocMDP'), NameObject('/TransformParams'): params})
        sig = DictionaryObject({NameObject('/Type'): NameObject('/Sig'), NameObject('/Reference'): ArrayObject([ref])})
        w.root_object[NameObject('/Perms')] = DictionaryObject({NameObject('/DocMDP'): w._add_object(sig)})
        w.write(scratch/'inputs'/f'gate-P{level}.pdf')
    run(qpdf, '--encrypt', 'user', 'owner', '256', '--modify=none', '--', scratch/'inputs/consistent.pdf', scratch/'inputs/restricted.pdf')
    for path in sorted((scratch/'inputs').glob('*.pdf')):
        manifest.append({'name': path.name, 'bytes': path.stat().st_size, 'sha256': hashlib.sha256(path.read_bytes()).hexdigest(), 'state': inspect(path)})
    save_json('inputs.json', {'pypdf': pypdf.__version__, 'qpdf': run(qpdf, '--version').strip(), 'fixtures': manifest})
elif mode == 'inspect':
    rows = []
    for path in sorted((scratch/'inputs').glob('*.pdf')):
        if path.stem.startswith(('restricted', 'gate-')): continue
        p = scratch/'outputs'/f'{path.stem}-pypdf.pdf'
        w = PdfWriter(clone_from=path); w.add_metadata({'/Title': 'W200 日本語'}); w.write(p)
        p = scratch/'outputs'/f'{path.stem}-qpdf.pdf'
        run(qpdf, path, p)
    for path in sorted((scratch/'outputs').glob('*.pdf')):
        password = ['--password=owner'] if path.stem.startswith('restricted') else []
        check = subprocess.run([qpdf, *password, '--check', str(path)], capture_output=True, text=True)
        rows.append({'name': path.name, 'qpdfExit': check.returncode, 'qpdfError': check.stderr.strip(), 'state': inspect(path)})
    engine = json.loads((record/'engine.json').read_text(encoding='utf-8-sig'))
    assert len(engine) == 112 and len(rows) == 141
    assert all(r['sourceUnchanged'] and r['afterOperation'] == 0 for r in engine)
    assert all(r['prefix'] for r in engine if r['result'] == 'accepted')
    save_json('inspection.json', rows)
    assert all(r['qpdfExit'] == 0 or (r['name'].endswith('-pdfbox.pdf') and r['qpdfExit'] == 3 and 'xref entry for the xref stream itself is missing' in r['qpdfError']) for r in rows)
    by_name = {r['name']: r['state'] for r in rows}
    inputs = json.loads((record/'inputs.json').read_text())['fixtures']
    originals = {Path(r['name']).stem: r['state'] for r in inputs}
    for name in ('malformed', 'no-rdf', 'dtd'):
        state = by_name[name+'-engine-none-title.pdf']
        assert state['xmpSha256'] == originals[name]['xmpSha256']
        assert state['info']['/Title'] == 'W200 日本語'
    assert by_name['multilingual-engine-none-title.pdf']['titles'][0]['value'] == 'W200 日本語'
    assert by_name['multilingual-engine-none-title.pdf']['titles'][1]['value'] == 'Old'
    assert by_name['custom-engine-none-title.pdf']['custom'] == originals['custom']['custom']
    assert by_name['duplicate-engine-none-title.pdf']['titles'][1]['value'] == 'Other'
    for name in ('consistent', 'info-only', 'neither', 'malformed', 'custom'):
        assert by_name[name+'-pypdf.pdf']['xmpSha256'] == originals[name]['xmpSha256']
        assert by_name[name+'-pdfbox.pdf']['xmpSha256'] == originals[name]['xmpSha256']
        assert by_name[name+'-qpdf.pdf']['xmpSha256'] == originals[name]['xmpSha256']
    assert by_name['custom-engine-none-author.pdf']['authors'] == ['Ada, Bob; Company', 'Bob']
    # Current failure-state hazard, retained as a revision-bound reproduction, not a fixed regression claim.
    assert next(r for r in engine if r['name'] == 'consistent' and r['op'] == 'invalid-char')['operationError'] == 'ArgumentException'
    assert by_name['consistent-engine-none-invalid-char.pdf']['info']['/Title'] == 'bad\x01title'
    assert by_name['consistent-engine-none-invalid-char.pdf']['xmpSha256'] == originals['consistent']['xmpSha256']
    assert all(r['operationError'] == 'PdfCertificationLockedException' for r in engine if r['name'].startswith('gate-') and r['op'] != 'raw-invalid')
    assert all(r['operationError'] == 'PdfEncryptionRestrictedException' for r in engine if r['name'] == 'restricted' and r['role'] == 'user' and r['op'] != 'raw-invalid')
    for r in engine:
        if r['op'] == 'raw-invalid' and r['result'] == 'accepted':
            assert r['operationError'] == 'PdfXmpException'
            assert by_name[r['name']+'-engine-'+r['role']+'-raw-invalid.pdf']['info']['/Department'] == 'pending'
        if r['operationError'] in ('PdfCertificationLockedException', 'PdfEncryptionRestrictedException'):
            assert r['pendingTitle'] == 'Old'
    assert by_name['consistent-engine-none-date.pdf']['info']['/ModDate'] == "D:20240203040506+05'30'"
    assert by_name['consistent-engine-none-date.pdf']['modifyDates'] == ['2024-02-02T22:35:06Z']
    save_json('inspection.json', rows)
    summary = {'engineRuns': len(engine), 'engineAcceptedSaves': sum(r['result']=='accepted' for r in engine), 'operationRefusals': sum(bool(r['operationError']) for r in engine), 'outerRefusals': sum(r['result']=='refused' for r in engine), 'independentlyInspectedOutputs': len(rows), 'qpdfClean': sum(r['qpdfExit']==0 for r in rows), 'qpdfWarnings': sum(r['qpdfExit']==3 for r in rows), 'pdfboxFixtures': len(list((scratch/'outputs').glob('*-pdfbox.pdf'))), 'pypdfFixtures': len(list((scratch/'outputs').glob('*-pypdf.pdf'))), 'qpdfRewrites': len(list((scratch/'outputs').glob('*-qpdf.pdf')))}
    save_json('summary.json', summary); print(json.dumps(summary))
else: raise ValueError(mode)
