"""W-218 font size-limit evidence: synthetic size-ladder (valid sfnt padded with an unreferenced private table).

python -I w218-font-limit-padding.py --evidence w218-font-limit-evidence.py --base FONT --work DIR --out JSON
       [--sizes-mib 8,16,32,64,128,256] [--harness itext-harness.exe] [--runs N]

SYNTHETIC: the padding table (tag 'PADD', seeded pseudo-random bytes) is not a real font structure. It shows how
an embedder's wall time, peak memory and output size respond to total file size with glyph data, text and cmap held
fixed. It is not a representative font and not a claim about real-font composition. Each ladder font is deleted
after its runs; only the record is kept. The base font is untrusted input passed as an argument.
"""
import argparse
import importlib.util
import json
import os
import random
import subprocess
import time
from pathlib import Path


def load(path):
    spec = importlib.util.spec_from_file_location('ev', path)
    m = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(m)
    return m


def build(base, target_bytes, out):
    from fontTools.ttLib import TTFont
    from fontTools.ttLib.tables.DefaultTable import DefaultTable
    f = TTFont(base, recalcTimestamp=False)
    base_len = os.path.getsize(base)
    t = DefaultTable('PADD')
    rng = random.Random(218)
    n = max(0, target_bytes - base_len - 64)
    t.data = b''.join(rng.randbytes(min(1 << 26, n - i)) for i in range(0, n, 1 << 26))
    f['PADD'] = t
    f.save(out)
    f.close()


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--evidence')
    ap.add_argument('--base')
    ap.add_argument('--work')
    ap.add_argument('--out')
    ap.add_argument('--sizes-mib', default='8,16,32,64,128,256')
    ap.add_argument('--harness')
    ap.add_argument('--runs', type=int, default=1)
    ap.add_argument('--timeout', type=int, default=900)
    a = ap.parse_args()
    ev = load(a.evidence)
    work = Path(a.work)
    work.mkdir(parents=True, exist_ok=True)
    text = ev.sample_text(a.base)
    rec = {'script': Path(__file__).name, 'script_sha256': ev.sha256_file(__file__), 'base_font': Path(a.base).name,
           'base_sha256': ev.sha256_file(a.base), 'base_bytes': os.path.getsize(a.base), 'synthetic': True,
           'started_utc': time.strftime('%Y-%m-%dT%H:%M:%SZ', time.gmtime()), 'ladder': []}
    for mib in [int(x) for x in a.sizes_mib.split(',')]:
        path = work / ('ladder-%dMiB.ttf' % mib)
        build(a.base, mib << 20, str(path))
        cls = ev.classify(str(path))
        entry = {'target_mib': mib, 'actual_bytes': path.stat().st_size, 'sha256': ev.sha256_file(str(path)), 'class': cls.get('class'), 'runs': []}
        for tool in ev.TOOLS:
            for r in ev.child_runs(tool, str(path), text, work, a.runs, a.timeout):
                entry['runs'].append(r)
                print(mib, tool, 'FAILED' if r.get('failed') else '%.2fs peakdelta %d MiB pdf %s' % (r['wall_seconds'], r['peak_wset_delta_bytes'] >> 20, r.get('pdf_bytes')), flush=True)
        if a.harness:
            tf = work / 'itext-ladder-sample.txt'
            tf.write_text(text, encoding='utf-8')
            for mode in ('subset', 'full'):
                outpdf = str(work / ('it-ladder-%s-%d.pdf' % (mode, mib)))
                try:
                    p = subprocess.run([a.harness, str(path), str(tf), outpdf, mode], capture_output=True, text=True, timeout=a.timeout)
                    line = [l for l in p.stdout.splitlines() if l.startswith('RESULT ')]
                    d = json.loads(line[-1][7:]) if line else {'failed': True, 'exit': p.returncode, 'stderr_tail': p.stderr.strip().splitlines()[-3:]}
                except subprocess.TimeoutExpired:
                    d = {'failed': True, 'timeout_seconds': a.timeout, 'mode': mode}
                entry['runs'].append(d)
                print(mib, 'itext', mode, d.get('wall_seconds'), d.get('pdf_bytes'), d.get('error'), flush=True)
        for f in work.glob('*'):
            if f.name.startswith(('out-', 'it-ladder')) or f == path:
                f.unlink()
        rec['ladder'].append(entry)
        Path(a.out).write_text(json.dumps(rec, indent=1), encoding='utf-8')
    rec['finished_utc'] = time.strftime('%Y-%m-%dT%H:%M:%SZ', time.gmtime())
    Path(a.out).write_text(json.dumps(rec, indent=1), encoding='utf-8')


if __name__ == '__main__':
    main()
