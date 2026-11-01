"""Read-only corpus survey: terminal fields whose effective V comes from a non-terminal ancestor.
No values are recorded; counts only. No target acceptance claim."""
from pathlib import Path
import json, sys, hashlib
import pypdf
from pypdf import PdfReader

ROOT = Path(__file__).resolve().parents[4]
res, skipped = [], []
for path in sorted((ROOT / 'test/jaytwo.Pdf.Tests/Fixtures/corpus').glob('*.pdf')):
    try:
        r = PdfReader(path)
        if r.is_encrypted:
            skipped.append(path.name); continue
        acro = r.trailer['/Root'].get('/AcroForm')
        if not acro:
            continue
        c = {'file': path.name, 'terminal': 0, 'local_V': 0, 'inherited_V': 0, 'inherited_V_choice': 0, 'terminal_choice': 0, 'nonterminal_with_V': 0}
        seen = set()
        def walk(ref, anc_v, ft, depth=0):
            if depth > 64: raise ValueError('depth')
            f = ref.get_object()
            key = getattr(ref, 'idnum', id(f))
            if key in seen: raise ValueError('cycle')
            seen.add(key)
            ft = str(f.get('/FT', ft)) if f.get('/FT', ft) is not None else None
            kids = f.get('/Kids', [])
            sub = [k.get_object() for k in kids]
            nonterminal = bool(sub) and any('/T' in k for k in sub)
            has_v = '/V' in f and f['/V'] is not None and not isinstance(f['/V'], pypdf.generic.NullObject)
            if nonterminal:
                if has_v: c['nonterminal_with_V'] += 1
                for k in kids: walk(k, anc_v or has_v, ft, depth + 1)
                return
            c['terminal'] += 1
            ischoice = ft == '/Ch'
            c['terminal_choice'] += ischoice
            if has_v: c['local_V'] += 1
            elif anc_v:
                c['inherited_V'] += 1; c['inherited_V_choice'] += ischoice
        for f in acro.get('/Fields', []): walk(f, False, None)
        res.append(c)
    except Exception as e:
        skipped.append('%s: %s' % (path.name, e))
tot = {k: sum(x[k] for x in res) for k in res[0] if k != 'file'}
out = {'pypdf': pypdf.__version__, 'python': sys.version.split()[0], 'script_sha256': hashlib.sha256(open(__file__, 'rb').read()).hexdigest(),
       'files_with_acroform': len(res), 'skipped': skipped, 'totals': tot, 'per_file_nonzero_inherited': [x for x in res if x['inherited_V'] or x['nonterminal_with_V']]}
Path(sys.argv[1]).write_text(json.dumps(out, indent=1))
print(json.dumps({k: out[k] for k in ('files_with_acroform', 'skipped', 'totals')}, indent=1)); print(out['per_file_nonzero_inherited'])
