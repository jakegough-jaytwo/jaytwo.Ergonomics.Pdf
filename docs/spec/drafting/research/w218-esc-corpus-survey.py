"""Read-only corpus count of PDF string objects whose raw bytes contain 0x1B (ESC), by dictionary key. Counts only; no values recorded."""
from pathlib import Path
import json, sys, hashlib
import pypdf
from pypdf import PdfReader
from pypdf.generic import ByteStringObject, TextStringObject, DictionaryObject, ArrayObject, IndirectObject

ROOT = Path(__file__).resolve().parents[4]
rows, skipped = [], []
def rawbytes(s):
    ob = getattr(s, 'original_bytes', None)
    return ob if ob is not None else (bytes(s) if isinstance(s, ByteStringObject) else str(s).encode('utf-16-be'))
for p in sorted((ROOT / 'test/jaytwo.Pdf.Tests/Fixtures/corpus').glob('*.pdf')):
    try:
        r = PdfReader(p)
        if r.is_encrypted: skipped.append(p.name); continue
        st = {'strings': 0, 'with_ESC': 0, 'by_key': {}}
        def walk(o, key, d=0):
            if d > 40 or isinstance(o, IndirectObject): return
            if isinstance(o, (TextStringObject, ByteStringObject)):
                st['strings'] += 1
                if b'\x1b' in rawbytes(o):
                    st['with_ESC'] += 1; st['by_key'][str(key)] = st['by_key'].get(str(key), 0) + 1
            elif isinstance(o, DictionaryObject):
                for k, v in o.items(): walk(v, k, d + 1)
            elif isinstance(o, ArrayObject):
                for v in o: walk(v, key, d + 1)
        for i in range(1, int(r.trailer['/Size'])):
            try: o = r.get_object(IndirectObject(i, 0, r))
            except Exception: continue
            if isinstance(o, (DictionaryObject, ArrayObject)): walk(o, None)
        rows.append({'file': p.name, **st})
    except Exception as e:
        skipped.append('%s: %s' % (p.name, e))
out = {'pypdf': pypdf.__version__, 'script_sha256': hashlib.sha256(open(__file__,'rb').read()).hexdigest(), 'files': len(rows), 'skipped': skipped,
       'total_strings': sum(x['strings'] for x in rows), 'total_with_ESC': sum(x['with_ESC'] for x in rows), 'rows': rows}
Path(sys.argv[1]).write_text(json.dumps(out, indent=1))
print({k: out[k] for k in ('files','total_strings','total_with_ESC')}); print([(x['file'], x['by_key']) for x in rows if x['with_ESC']])
