"""Independent structure/CMS inspection; no trust or rendering claim."""
import hashlib
import json
import subprocess
import sys
import xml.etree.ElementTree as ET
from importlib.metadata import version
from pathlib import Path
import pypdf
from pypdf import PdfReader
from pyhanko.pdf_utils.reader import PdfFileReader
from pyhanko.sign.validation import validate_pdf_signature
from pyhanko_certvalidator import ValidationContext

repo, inputs, outputs = map(Path, sys.argv[1:])
observations = json.loads((outputs/'probe-results.json').read_text())
assert len(observations) == 27
for row in observations:
    if row['op'] == 'sign-original':
        refused = row['id'] in ['readonly', 'seed-bad']
        assert row['hostCalls'] == (0 if refused else 1)
        assert (row['bytes'] == 0) == refused
        assert row['status'] == ('PdfFieldReadOnlyException' if row['id'] == 'readonly' else 'PdfSignException' if refused else 'signed')
        if not refused: assert row['laterFill'] == 'Generated'
    else:
        assert row['omissions'] == {'SignatureWidget': 1 if row['op'] == 'extract' else 4}
env = ET.parse(repo/'.tools/test.runsettings').find('RunConfiguration/EnvironmentVariables')
qpdf = env.find('JAYTWO_QPDF').text
rows = []
for path in sorted(inputs.glob('*.pdf')) + sorted(outputs.glob('*.pdf')):
    check = subprocess.run([qpdf, '--check', str(path)], capture_output=True, text=True)
    assert check.returncode == 0, (path.name, check.stdout, check.stderr)
    r = PdfReader(path)
    fields = r.get_fields() or {}
    sigs = [name for name, f in fields.items() if f.get('/FT') == '/Sig']
    row = dict(file=path.name, source=path.parent == inputs, pages=len(r.pages), fields=list(fields), signatures=sigs, qpdf='pass')
    if path.parent == outputs and '-signed' not in path.name:
        assert not sigs, path
        expected = 1 if '-extract' in path.name else 4
        assert len(r.pages) == expected
        row['widgets'] = [len(p.get('/Annots', [])) for p in r.pages]
    if '-signed' in path.name:
        field = r.trailer['/Root']['/AcroForm']['/Fields'][0].get_object()
        value = field['/V'].get_object()
        row['signature_reference'] = '/Reference' in value
        row['field_lock'] = '/Lock' in field
        row['lock_fields'] = list(field.get('/Lock', {}).get_object().get('/Fields', [])) if '/Lock' in field else []
        assert not row['signature_reference']
        assert row['field_lock'] == (path.stem.split('-signed')[0] in ['all','include','exclude'])
        source = inputs/(path.name.replace('-signed',''))
        row['prefix'] = path.read_bytes().startswith(source.read_bytes())
        assert row['prefix']
        with path.open('rb') as stream:
            pr = PdfFileReader(stream)
            embedded = pr.embedded_signatures[0]
            status = validate_pdf_signature(embedded, signer_validation_context=ValidationContext(trust_roots=[embedded.signer_cert], allow_fetching=False))
            row['cms_intact'] = status.intact
            row['cms_valid'] = status.valid
            row['seed_value_ok'] = status.seed_value_ok
            row['seed_value_error'] = str(status.seed_value_constraint_error) if status.seed_value_constraint_error else None
            assert status.intact and status.valid
            if path.name == 'seed-ok-signed.pdf':
                assert not status.seed_value_ok  # pyHanko 0.38.0 compares /sha256 with sha256.
            else:
                assert status.seed_value_ok
    rows.append(row)
for item in json.loads((inputs/'manifest.json').read_text()):
    assert hashlib.sha256((inputs/(item['id']+'.pdf')).read_bytes()).hexdigest() == item['sha256']
versions = dict(pypdf=pypdf.__version__, pyhanko=version('pyHanko'), python=sys.version.split()[0],
                qpdf=subprocess.check_output([qpdf, '--version'], text=True).strip())
(outputs/'inspection.json').write_text(json.dumps(dict(versions=versions, results=rows), indent=2)+'\n')
print(f'Independent inspection passed: {len(rows)} PDFs; CMS integrity/cryptography only, no public trust claim')
