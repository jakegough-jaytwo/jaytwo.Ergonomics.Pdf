"""W-218 closure, concern 2: classify downloaded fonts against the owner's 32 MiB supplied-font limit (research only).

python -I w218-closure-font-survey.py --evidence w218-font-limit-evidence.py --out OUT.json [--limit 33554432] \
       --provenance PROV.json FONT [FONT ...]

Fonts are untrusted: they are passed as arguments, only header and table data are read through fontTools (lazy),
and nothing is executed from the font directory. PROV.json maps a font file name to {"url", "licence_source", "note"}.
The classification, facts and hashing functions are reused from the evidence script so that both runs agree.
"""
import argparse
import importlib.util
import json
import os
import sys
import time
from pathlib import Path


def load(path):
    spec = importlib.util.spec_from_file_location('ev', path)
    m = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(m)
    return m


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--evidence', required=True)
    ap.add_argument('--out', required=True)
    ap.add_argument('--limit', type=int, default=33554432)
    ap.add_argument('--provenance')
    ap.add_argument('fonts', nargs='+')
    a = ap.parse_args()
    ev = load(a.evidence)
    prov = json.loads(Path(a.provenance).read_text(encoding='utf-8')) if a.provenance else {}
    import fontTools
    rec = {'script': Path(__file__).name, 'script_sha256': ev.sha256_file(__file__),
           'evidence_script_sha256': ev.sha256_file(a.evidence), 'fontTools': fontTools.version,
           'python': sys.version.split()[0], 'limit_bytes': a.limit,
           'started_utc': time.strftime('%Y-%m-%dT%H:%M:%SZ', time.gmtime()), 'fonts': []}
    for f in a.fonts:
        p = Path(f)
        print('facts', p.name, flush=True)
        try:
            r = ev.font_facts(str(p))
        except Exception as e:  # untrusted input: record and continue
            r = {'file': p.name, 'bytes': p.stat().st_size, 'error': repr(e)[:300]}
        r['mib'] = round(r['bytes'] / 1048576, 3)
        r['at_or_above_limit'] = r['bytes'] >= a.limit
        r['eligible_for_form_font_03'] = r.get('class') == 'static-glyf'
        r['would_refuse_at_limit'] = bool(r['eligible_for_form_font_03'] and r['at_or_above_limit'])
        r['bytes_over_limit'] = r['bytes'] - a.limit
        r['provenance'] = prov.get(p.name, {})
        rec['fonts'].append(r)
    rec['finished_utc'] = time.strftime('%Y-%m-%dT%H:%M:%SZ', time.gmtime())
    Path(a.out).write_text(json.dumps(rec, indent=1, ensure_ascii=False), encoding='utf-8')
    for r in rec['fonts']:
        print(r['file'], r['bytes'], r.get('class'), 'REFUSES' if r['would_refuse_at_limit'] else 'passes-gate-or-ineligible')


if __name__ == '__main__':
    main()
