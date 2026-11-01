"""W-218 font size-limit evidence: iText 9.8.0 (.NET) embedding comparison driver (research only).

python -I w218-font-limit-itext.py --evidence w218-font-limit-evidence.py --harness itext-harness.exe
       --fonts DIR --sysfonts DIR --work DIR --out JSON [--runs N]

The harness source is w218-font-limit-itext.cs (+ csproj text). iText is AGPL; it is used here only as an
established comparator on local files and nothing is redistributed. Fonts are untrusted inputs passed as arguments.
"""
import argparse
import hashlib
import importlib.util
import json
import os
import subprocess
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
    ap.add_argument('--evidence')
    ap.add_argument('--harness')
    ap.add_argument('--fonts')
    ap.add_argument('--sysfonts')
    ap.add_argument('--work')
    ap.add_argument('--out')
    ap.add_argument('--runs', type=int, default=3)
    ap.add_argument('--timeout', type=int, default=900)
    a = ap.parse_args()
    ev = load(a.evidence)
    work = Path(a.work)
    work.mkdir(parents=True, exist_ok=True)
    fonts = sorted(p for p in Path(a.fonts).iterdir() if p.suffix.lower() == '.ttf')
    fonts += [Path(a.sysfonts) / n for n in ('arial.ttf', 'times.ttf', 'calibri.ttf', 'segoeui.ttf', 'malgun.ttf', 'simsunb.ttf')
              if (Path(a.sysfonts) / n).exists()]
    rec = {'script': Path(__file__).name, 'script_sha256': ev.sha256_file(__file__),
           'harness_sha256': ev.sha256_file(a.harness), 'started_utc': time.strftime('%Y-%m-%dT%H:%M:%SZ', time.gmtime()),
           'runs': []}
    for p in fonts:
        text = ev.sample_text(str(p))
        tf = work / 'itext-sample.txt'
        tf.write_text(text, encoding='utf-8')
        cls = ev.classify(str(p))
        for mode in ('subset', 'full'):
            for i in range(a.runs):
                outpdf = str(work / ('it-%s-%s-%d.pdf' % (mode, p.stem, i)))
                try:
                    r = subprocess.run([a.harness, str(p), str(tf), outpdf, mode], capture_output=True, text=True, timeout=a.timeout)
                    line = [l for l in r.stdout.splitlines() if l.startswith('RESULT ')]
                    d = json.loads(line[-1][7:]) if line else {'failed': True, 'exit': r.returncode, 'stderr_tail': r.stderr.strip().splitlines()[-3:]}
                except subprocess.TimeoutExpired:
                    d = {'failed': True, 'timeout_seconds': a.timeout, 'mode': mode, 'font': p.name}
                d.update({'run': i, 'font_class': cls.get('class'), 'font_bytes': p.stat().st_size, 'text_scalars': len(text)})
                if i == 0 and not d.get('failed') and os.path.exists(outpdf):
                    try:
                        d['embedded_in_output'] = ev.embedded_fonts_in_pdf(outpdf)
                    except Exception as e:
                        d['verify_error'] = repr(e)[:200]
                rec['runs'].append(d)
                print(p.name, mode, i, d.get('wall_seconds'), d.get('pdf_bytes'), d.get('error'), flush=True)
                if i > 0 and os.path.exists(outpdf):
                    os.unlink(outpdf)
    rec['finished_utc'] = time.strftime('%Y-%m-%dT%H:%M:%SZ', time.gmtime())
    Path(a.out).write_text(json.dumps(rec, indent=1, ensure_ascii=False), encoding='utf-8')


if __name__ == '__main__':
    main()
