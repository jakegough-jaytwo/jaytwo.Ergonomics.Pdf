"""Read-only representative corpus survey; no target acceptance or rendering claim."""
from pathlib import Path
import hashlib
import json
import platform
import sys
import pypdf
from pypdf import PdfReader

ROOT = Path(__file__).resolve().parents[4]
rows, failures = [], []
inputs = sorted((ROOT / 'test/jaytwo.Pdf.Tests/Fixtures/corpus').glob('*.pdf'))
for path in inputs:
    start_count = len(rows)
    try:
        reader = PdfReader(path)
        if reader.is_encrypted:
            failures.append({'file': path.name, 'reason': 'encrypted; no credential attempted'})
            continue
        acro = reader.trailer['/Root'].get('/AcroForm')
        if not acro:
            continue
        seen = set()
        def walk(ref, inherited, depth=0):
            if depth > 64:
                raise ValueError('survey traversal depth')
            field = ref.get_object()
            identity = (ref.idnum, ref.generation) if hasattr(ref, 'idnum') else id(field)
            if identity in seen:
                raise ValueError('survey repeated field reference')
            seen.add(identity)
            effective = dict(inherited)
            effective.update({k: field[k] for k in ('/FT', '/Ff', '/DA', '/Q', '/MaxLen', '/V') if k in field})
            kids = [r.get_object() for r in field.get('/Kids', [])]
            if kids and any('/T' in kid or '/FT' in kid for kid in kids):
                for child in field['/Kids']:
                    walk(child, effective, depth + 1)
                return
            widgets = kids or ([field] if field.get('/Subtype') == '/Widget' else [])
            ft, flags = str(effective.get('/FT')), int(effective.get('/Ff', 0))
            if ft not in ('/Tx', '/Ch'):
                return
            opt_ref = field.get('/Opt', [])
            opt = opt_ref.get_object() if hasattr(opt_ref, 'get_object') else opt_ref
            index_ref = field.get('/I', [])
            indices = index_ref.get_object() if hasattr(index_ref, 'get_object') else index_ref
            rows.append({'file': path.name, 'sha256': hashlib.sha256(path.read_bytes()).hexdigest(),
                         'object': str(identity), 'type': ft, 'flags': flags,
                         'widgets': len(widgets), 'rects': [list(map(float, w.get('/Rect', []))) for w in widgets],
                         'da': str(effective.get('/DA', acro.get_object().get('/DA', ''))),
                         'option_count': len(opt), 'top_index': int(field.get('/TI', 0)),
                         'selected_indices': list(indices),
                         'has_value': '/V' in effective, 'value_is_array': isinstance(effective.get('/V'), list)})
        for ref in acro.get_object().get('/Fields', []):
            walk(ref, {})
    except Exception as error:
        del rows[start_count:]
        failures.append({'file': path.name, 'reason': type(error).__name__ + ': ' + str(error)})

result = {'classification': 'Measured static survey; no rendering, semantic eligibility or target verification',
          'python': sys.version, 'platform': platform.platform(), 'pypdf': pypdf.__version__,
          'input_count': len(inputs), 'rows': rows, 'failures_or_skips': failures}
Path(__file__).with_suffix('.json').write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
print('Inputs', len(inputs), 'text/choice fields', len(rows), 'failures/skips', len(failures))
for ft in ('/Tx', '/Ch'):
    selected = [r for r in rows if r['type'] == ft]
    print(ft, 'fields', len(selected), 'flags', sorted(set(r['flags'] for r in selected)))
for row in rows:
    if row['type'] == '/Ch':
        print(row['file'], row['object'], 'flags', row['flags'], 'options', row['option_count'],
              'TI', row['top_index'], 'I', row['selected_indices'], 'rects', row['rects'], 'DA', row['da'])
