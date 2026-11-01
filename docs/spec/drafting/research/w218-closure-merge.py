"""W-218 closure: merge raw records into w218-closure-concerns.json (research only).

python -I w218-closure-merge.py --out OUT.json --probe PROBE.json --survey SURVEY.json --measure MEASURE.json
       [--embed EMBED.json] [--itext ITEXT.json] [--listing NAME=PATH ...] [--script PATH ...]

Every input is copied verbatim under its own key with its SHA-256; nothing is recomputed here. CRLF line endings
are written so the file matches the repository convention for these records.
"""
import argparse
import hashlib
import json
import sys
import time
from pathlib import Path


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--out', required=True)
    ap.add_argument('--probe')
    ap.add_argument('--survey')
    ap.add_argument('--measure')
    ap.add_argument('--embed')
    ap.add_argument('--itext')
    ap.add_argument('--listing', action='append', default=[])
    ap.add_argument('--script', action='append', default=[])
    a = ap.parse_args()
    rec = {'merged_by': Path(__file__).name, 'merged_utc': time.strftime('%Y-%m-%dT%H:%M:%SZ', time.gmtime()),
           'python': sys.version.split()[0], 'script_sha256': {Path(s).name: sha(s) for s in a.script + [__file__]},
           'inputs_sha256': {}}
    for key, path in (('concern1_probe', a.probe), ('concern2_survey', a.survey), ('concern3_measure', a.measure),
                      ('concern2_embedder_run', a.embed), ('concern2_itext_run', a.itext)):
        if path:
            rec[key] = json.loads(Path(path).read_text(encoding='utf-8'))
            rec['inputs_sha256'][key] = sha(path)
    rec['concern2_listings'] = {}
    for item in a.listing:
        name, path = item.split('=', 1)
        d = json.loads(Path(path).read_text(encoding='utf-8'))
        rec['inputs_sha256']['listing:' + name] = sha(path)
        if isinstance(d, dict) and 'tree' in d:
            fonts = [{'path': e['path'], 'bytes': e['size']} for e in d['tree']
                     if e.get('type') == 'blob' and e['path'].lower().endswith(('.ttf', '.otf', '.ttc'))]
            fonts.sort(key=lambda f: -f['bytes'])
            rec['concern2_listings'][name] = {'tree_truncated': d.get('truncated'), 'font_files': len(fonts), 'largest_40': fonts[:40]}
        elif isinstance(d, list):
            rec['concern2_listings'][name] = [{'tag': r.get('tag_name'), 'assets': [{'name': x['name'], 'bytes': x['size']} for x in r.get('assets', [])]}
                                              for r in d[:3]]
    text = json.dumps(rec, indent=1, ensure_ascii=False).replace('\r\n', '\n').replace('\n', '\r\n')
    Path(a.out).write_bytes(text.encode('utf-8'))
    print('wrote', a.out, len(text))


if __name__ == '__main__':
    main()
