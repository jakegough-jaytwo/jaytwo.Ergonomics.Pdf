"""Read-only page-widget AP inventory, not an eligibility or rendering test."""
import hashlib
import json
import platform
import sys
from collections import Counter
from pathlib import Path

import pypdf
from pypdf import PdfReader
from pypdf.generic import ContentStream, DictionaryObject, NullObject


def resolve(value):
    return value.get_object() if hasattr(value, "get_object") else value


def inherited(widget, key):
    seen = set()
    current = widget
    for _ in range(65):
        if id(current) in seen:
            raise ValueError("parent cycle")
        seen.add(id(current))
        value = resolve(current.get(key))
        if value is not None and not isinstance(value, NullObject):
            return value
        current = resolve(current.get('/Parent'))
        if current is None:
            return None
    raise ValueError("parent depth guard")


def main():
    root = Path(__file__).resolve().parents[4]
    inputs = sorted((root / 'test/jaytwo.Pdf.Tests/Fixtures/corpus').glob('*.pdf'))
    rows, skips = [], []
    for path in inputs:
        start = len(rows)
        try:
            reader = PdfReader(path)
            if reader.is_encrypted:
                skips.append({'file': path.name, 'reason': 'encrypted; no credential attempted'})
                continue
            digest = hashlib.sha256(path.read_bytes()).hexdigest()
            seen = set()
            for page_index, page in enumerate(reader.pages):
                for ref in resolve(page.get('/Annots', [])):
                    widget = resolve(ref)
                    if widget.get('/Subtype') != '/Widget' or inherited(widget, '/FT') != '/Btn':
                        continue
                    identity = (ref.idnum, ref.generation) if hasattr(ref, 'idnum') else id(widget)
                    if identity in seen:
                        continue
                    seen.add(identity)
                    flags = int(inherited(widget, '/Ff') or 0)
                    if flags & (1 << 16):  # Pushbutton, outside the mark comparison.
                        continue
                    normal = resolve(resolve(widget.get('/AP', {})).get('/N'))
                    states = []
                    if isinstance(normal, DictionaryObject) and not hasattr(normal, 'get_data'):
                        for name, stream_ref in normal.items():
                            stream = resolve(stream_ref)
                            if not hasattr(stream, 'get_data'):
                                raise ValueError('nonstream normal state')
                            operations = ContentStream(stream, reader).operations
                            counts = Counter(op.decode('ascii') for _, op in operations)
                            resources = resolve(stream.get('/Resources', {}))
                            font_dict = resolve(resources.get('/Font', {}))
                            fonts = []
                            for args, op in operations:
                                if op == b'Tf':
                                    font = resolve(font_dict.get(args[0], {}))
                                    fonts.append({'resource': str(args[0]),
                                                  'subtype': str(font.get('/Subtype')),
                                                  'base_font': str(font.get('/BaseFont'))})
                            states.append({'state': str(name), 'decoded_sha256': hashlib.sha256(stream.get_data()).hexdigest(),
                                           'operators': dict(sorted(counts.items())), 'selected_fonts': fonts,
                                           'text_show': any(op in counts for op in ('Tj', 'TJ', "'", '"')),
                                           'nested_Do': counts.get('Do', 0)})
                    rows.append({'file': path.name, 'input_sha256': digest, 'first_page': page_index + 1,
                                 'widget_object': str(identity), 'kind': 'radio' if flags & (1 << 15) else 'checkbox',
                                 'normal_state_dictionary': bool(states), 'states': states})
        except Exception as error:
            del rows[start:]
            skips.append({'file': path.name, 'reason': type(error).__name__ + ': ' + str(error)})
    states = [s for row in rows for s in row['states']]
    summary = {'inputs': len(inputs), 'widgets': len(rows), 'kinds': dict(Counter(r['kind'] for r in rows)),
               'states': len(states), 'text_show_states': sum(s['text_show'] for s in states),
               'nested_Do_states': sum(bool(s['nested_Do']) for s in states), 'skips_or_failures': len(skips)}
    result = {'classification': 'Static page-widget syntax inventory; no canonical-field completeness, mark identity, eligibility or rendering claim',
              'python': sys.version, 'platform': platform.platform(), 'pypdf': pypdf.__version__,
              'script_sha256': hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
              'summary': summary, 'rows': rows, 'skips_or_failures': skips}
    Path(__file__).with_suffix('.json').write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(summary))


if __name__ == '__main__':
    main()
