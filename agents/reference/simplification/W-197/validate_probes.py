"""W-197 assertions and independent structure inspection; no trust, rendering or semantic-equivalence claim."""
import hashlib
import json
import logging
import subprocess
import sys
import xml.etree.ElementTree as ET
from pathlib import Path
import pypdf
from pypdf import PdfReader
from pypdf.generic import IndirectObject, DictionaryObject, ArrayObject

logging.getLogger('pypdf').setLevel(logging.ERROR)  # dangling references in malformed fixtures are deliberate
repo, inputs, outputs = map(Path, sys.argv[1:])
rows = json.loads((outputs / 'probe-results.json').read_text())
by = {(x['id'], x['op']): x for x in rows}
assert len(rows) == 34 * 3 + 7, len(rows)

# Current behavior that the candidate would change or keep. Every refusal happens before destination output.
REFUSED_NOW = {'budget-deep-fields': 'PdfResourceLimitException', 'catalog-af': 'PdfAssemblyNotSupportedException',
               'ocprops': 'PdfAssemblyNotSupportedException', 'widget-aa-p1': 'PdfAssemblyNotSupportedException'}
for (fid, op), row in by.items():
    if op not in ('extract1', 'extract2', 'concat'):
        continue
    if fid in REFUSED_NOW:
        assert row['status'] == 'refused' and row['exception'] == REFUSED_NOW[fid] and row['bytes'] == 0, row
        assert 'Input' not in row['message'] and 'occurrence' not in row['message'].lower(), row  # no occurrence named
    else:
        assert row['status'] == 'accepted' and row['bytes'] > 0, row
for fid in ('certified-p1', 'certified-p2', 'certified-p3', 'certified-dangling', 'timestamp-p2', 'fieldmdp-p2',
            'ur3', 'engine-signed-p2', 'doc-js', 'openaction-js', 'embedded-files', 'hybrid-xfa', 'sig-dangling-v'):
    assert by[(fid, 'extract1')]['status'] == 'accepted', fid  # silently accepted today
assert by[('certified-p1', 'extract2')]['omissions'] == {'SignatureWidget': 1}
assert by[('fileattach-p1', 'extract1')]['omissions'] == {'UnsupportedAnnotation': 1}
assert by[('link-js-p1', 'extract1')]['omissions'] == {'UnsupportedLink': 1}
for fid in ('doc-js', 'openaction-js', 'catalog-aa', 'page-aa-p1', 'annot-aa-p1', 'annot-js-p1', 'embedded-files', 'ur3', 'hybrid-xfa'):
    assert by[(fid, 'extract1')]['omissions'] == {}, fid  # loss is not reported

pub = {x['op']: x for x in rows if x['op'] not in ('extract1', 'extract2', 'concat')}
assert pub['workflow-file']['status'] == 'refused'
assert pub['preflight-refusal-workflow-stream']['bytes'] == 0
assert pub['late-destination-engine']['status'] == 'refused' and 0 < pub['late-destination-engine']['bytes'] <= 64
assert pub['late-destination-workflow-stream']['bytes'] == 0
assert pub['late-source-read-engine']['exception'] == 'IOException' and pub['late-source-read-engine']['bytes'] > 0
assert pub['late-source-read-engine']['extra'] >= 1
assert pub['source-mutation-after-open']['status'] == 'accepted' and pub['source-mutation-after-open']['extra'] == 1
files = [x for x in rows if x['op'] == 'workflow-file']
assert len(files) == 2 and all(not x['destinationExists'] and x['leftovers'] == 0 for x in files), files
assert any(x['id'] == 'late-source-read' and x['extra'] >= 1 for x in files)


def walk(reader):
    """Every indirect object, descending into its direct dictionaries and arrays."""
    found = set()

    def visit(o):
        if isinstance(o, DictionaryObject):
            for key in ('/JS', '/AA', '/OpenAction', '/EmbeddedFiles', '/Perms', '/Lock', '/XFA', '/OC', '/AF',
                        '/OCProperties', '/Reference', '/ByteRange'):
                if key in o:
                    found.add(key)
            if o.get('/Subtype') == '/FileAttachment':
                found.add('FileAttachment')
            if o.get('/FT') == '/Sig':
                found.add('SigField')
            if o.get('/S') in ('/JavaScript', '/Launch'):
                found.add(str(o.get('/S')))
            children = o.values()
        elif isinstance(o, ArrayObject):
            children = o
        else:
            return
        for child in children:
            if not isinstance(child, IndirectObject):
                visit(child)

    for num in range(1, int(reader.trailer.get('/Size', 0))):
        try:
            visit(reader.get_object(IndirectObject(num, 0, reader)))
        except Exception:
            continue
    return found


env = ET.parse(repo / '.tools/test.runsettings').find('RunConfiguration/EnvironmentVariables')
qpdf = env.find('JAYTWO_QPDF').text
inspected = []
for path in sorted(inputs.glob('*.pdf')) + sorted(outputs.glob('*.pdf')):
    check = subprocess.run([qpdf, '--check', str(path)], capture_output=True, text=True)
    reader = PdfReader(path)
    try:
        fields = sorted((reader.get_fields() or {}).keys())
    except (pypdf.errors.LimitReachedError, ValueError) as e:
        fields = 'pypdf get_fields failed: ' + type(e).__name__  # deliberate over-depth or dangling-value fixture
    row = dict(file=path.name, source=path.parent == inputs, qpdf=check.returncode, pages=len(reader.pages),
               features=sorted(walk(reader)), fields=fields)
    if path.parent == outputs:
        assert check.returncode == 0, (path.name, check.stdout, check.stderr)
        # No accepted output carries scripts, attachments, signatures, rights, locks, XFA or layers.
        assert not set(row['features']) - {'/OC'}, row
        assert '/OC' not in row['features'] or path.name.startswith('oc-annot'), row
    inspected.append(row)
    if path.name == 'painted-mark-extract1.pdf':
        assert b'Digitally signed by' in path.read_bytes()  # painted marks survive: content is never interpreted
    if path.name == 'fieldmdp-p2-extract1.pdf':
        assert row['fields'] == ['Input1', 'Input1.Name'], row  # the field the signature locked is now unlocked
prepared = inputs / 'embedded-files-removed.pdf'
assert prepared.read_bytes().startswith((inputs / 'embedded-files.pdf').read_bytes())  # incremental: original is a prefix
candidate = json.loads((outputs / 'candidate.json').read_text())
verdicts = {(x['id'], x['op']): x['verdict'] for x in candidate['fixtures']}
assert verdicts[('embedded-files', 'extract1')] == 'refuse' and verdicts[('embedded-files-removed', 'extract1')] == 'admit'
assert verdicts[('certified-p2', 'extract1')] == 'refuse' and verdicts[('approval-p2', 'extract1')].endswith('derivative')
assert verdicts[('page-aa-p2', 'extract1')] == 'admit' and verdicts[('page-aa-p2', 'extract2')] == 'refuse'
assert verdicts[('painted-mark', 'extract1')] == 'admit'  # content-painted marks are outside inspection scope
oc = next(x for x in inspected if x['file'] == 'oc-annot-no-catalog-extract1.pdf')
for item in json.loads((inputs / 'manifest.json').read_text()):
    assert hashlib.sha256((inputs / (item['id'] + '.pdf')).read_bytes()).hexdigest() == item['sha256']
sources_bad = [x['file'] for x in inspected if x['source'] and x['qpdf'] != 0]
versions = dict(pypdf=pypdf.__version__, python=sys.version.split()[0],
                qpdf=subprocess.check_output([qpdf, '--version'], text=True).splitlines()[0].strip())
(outputs / 'inspection.json').write_text(json.dumps(dict(versions=versions, sourcesFailingQpdf=sources_bad,
                                                         ocAnnotationOutput=oc, results=inspected), indent=2) + '\n')
print(f'Inspection passed: {len(inspected)} PDFs; qpdf failures limited to deliberately malformed sources: {sources_bad}')
