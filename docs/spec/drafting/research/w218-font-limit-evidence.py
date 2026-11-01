"""W-218 font size-limit evidence (independent research; not target-library evidence).

Parent: python -I w218-font-limit-evidence.py --repo REPO --fonts DIR --sysfonts DIR --work DIR --out JSON [--runs N]
Child (internal): python -I w218-font-limit-evidence.py --child TOOL FONT TEXTFILE OUTPDF

Fonts are untrusted inputs; paths are passed as arguments and nothing in the font directory is imported.
No font program is copied into the repository or redistributed. Output is a JSON record only.
"""
import argparse
import hashlib
import json
import os
import platform
import shutil
import struct
import subprocess
import sys
import time
from pathlib import Path

SCRIPT_RANGES = {
    'latin': list(range(0x20, 0x7F)) + list(range(0xA0, 0x100)),
    'greek': list(range(0x391, 0x3CA)),
    'cyrillic': list(range(0x410, 0x450)),
    'hebrew': list(range(0x5D0, 0x5EB)),
    'arabic': list(range(0x627, 0x64B)),
    'devanagari': list(range(0x915, 0x93A)),
    'tamil': list(range(0xB95, 0xBBA)),
    'thai': list(range(0xE01, 0xE2F)),
    'hangul': list(range(0xAC00, 0xAC00 + 60)),
    'kana': list(range(0x3042, 0x3094)),
    'han': [ord(c) for c in '的一是不了人我在有他这中大来上国个到说们为子和你地出道也时年得就那要下以生会自着去之过家学对可她里后小么心多天而能好都然没日于起还发成事只作当想看文无开手十用主行方又如前所本见经头面公同三已老从动两长知民样现分将外但身些与高意进把法此实回二理美点月明其种声全工已话儿者向情部正名定女问力机给等几很业最间新什打便位因重被走电四第门相次东政海口使教西再平真听世气信北少关并内加化由却代军产入先山无各体字字体测试汉字表单']
}

TOOLS = ['fonttools_subset', 'reportlab', 'fpdf2', 'pymupdf', 'pypdfium2']


def sha256_file(path):
    h = hashlib.sha256()
    with open(path, 'rb') as f:
        for chunk in iter(lambda: f.read(1 << 20), b''):
            h.update(chunk)
    return h.hexdigest()


def peak_working_set():
    import psutil
    mi = psutil.Process().memory_info()
    return getattr(mi, 'peak_wset', mi.rss)


def current_working_set():
    import psutil
    return psutil.Process().memory_info().rss


def read_directory(path, offset=0):
    """Header-only table directory read: no font parsing library."""
    with open(path, 'rb') as f:
        f.seek(offset)
        head = f.read(12)
        tag = head[:4]
        if tag == b'ttcf':
            return {'container': 'collection', 'faces': struct.unpack('>I', head[8:12])[0]}
        num = struct.unpack('>H', head[4:6])[0]
        tables = {}
        d = f.read(16 * num)
        for i in range(num):
            t, _c, off, ln = struct.unpack('>4sIII', d[i * 16:(i + 1) * 16])
            tables[t.decode('latin-1')] = ln
        return {'container': 'sfnt', 'sfnt_tag': tag.hex(), 'tables': tables}


def classify(path):
    try:
        d = read_directory(path)
    except Exception as e:
        return {'class': 'unreadable', 'error': repr(e)}
    if d['container'] == 'collection':
        return {'class': 'collection', 'faces': d['faces']}
    t = d['tables']
    if d['sfnt_tag'] == '774f4646' or d['sfnt_tag'] == '774f4632':
        return {'class': 'woff-other'}
    if 'fvar' in t:
        c = 'variable'
    elif 'CFF ' in t or 'CFF2' in t:
        c = 'cff'
    elif 'glyf' in t and 'loca' in t:
        c = 'static-glyf'
    else:
        c = 'other'
    return {'class': c, 'glyf_bytes': t.get('glyf'), 'tables_total': sum(t.values())}


def font_facts(path):
    from fontTools.ttLib import TTFont
    facts = {'file': Path(path).name, 'bytes': os.path.getsize(path), 'sha256': sha256_file(path)}
    facts.update(classify(path))
    t0 = time.perf_counter()
    f = TTFont(path, lazy=True, recalcTimestamp=False)
    names = f['name']

    def nm(i):
        r = names.getName(i, 3, 1, 0x409) or names.getName(i, 1, 0, 0) or names.getName(i, 3, 1)
        return str(r) if r else None
    facts.update({'family': nm(1), 'full_name': nm(4), 'version_string': nm(5), 'copyright': nm(0),
                  'name_license': (nm(13) or '')[:200], 'name_license_url': nm(14),
                  'num_glyphs': f['maxp'].numGlyphs})
    if 'OS/2' in f:
        facts['os2_version'] = f['OS/2'].version
        facts['fsType'] = f['OS/2'].fsType
    cm = f.getBestCmap() or {}
    facts['cmap_entries'] = len(cm)
    facts['script_coverage'] = {k: sum(1 for c in v if c in cm) for k, v in SCRIPT_RANGES.items()}
    facts['script_coverage_total'] = {k: len(v) for k, v in SCRIPT_RANGES.items()}
    if facts['class'] == 'static-glyf':
        glyf = f['glyf']
        names_ = f.getGlyphOrder()
        comp = {}
        n_comp = 0
        for g in names_:
            gl = glyf[g]
            if gl.isComposite():
                n_comp += 1
                comp[g] = [c.glyphName for c in gl.components]
        depth_cache = {}

        def depth(g, stack=()):
            if g in depth_cache:
                return depth_cache[g]
            if g in stack:
                return 999
            d = 0
            if g in comp:
                d = 1 + max((depth(c, stack + (g,)) for c in comp[g]), default=0)
            depth_cache[g] = d
            return d
        facts['composite_glyphs'] = n_comp
        facts['max_composite_depth'] = max((depth(g) for g in comp), default=0)
    facts['facts_seconds'] = round(time.perf_counter() - t0, 3)
    f.close()
    return facts


def sample_text(path):
    from fontTools.ttLib import TTFont
    f = TTFont(path, lazy=True, recalcTimestamp=False)
    cm = f.getBestCmap() or {}
    f.close()
    out = []
    for script, rng in SCRIPT_RANGES.items():
        have = [c for c in rng if c in cm]
        if script == 'latin':
            if have:
                out.extend(have[:95])
        elif len(have) >= 10:
            out.extend(have[:60])
    return ''.join(chr(c) for c in out)


# ---------------- child tools ----------------

def child_main(tool, font, textfile, outpdf):
    text = Path(textfile).read_text(encoding='utf-8')
    import psutil  # noqa: F401
    base_peak = peak_working_set()
    base_rss = current_working_set()
    t0 = time.perf_counter()
    c0 = time.process_time()
    rec = {'tool': tool, 'font': Path(font).name, 'text_scalars': len(text)}
    out = {}
    if tool == 'fonttools_subset':
        import io
        import fontTools
        from fontTools import subset
        from fontTools.ttLib import TTFont
        rec['tool_version'] = 'fontTools ' + fontTools.version
        opts = subset.Options()
        opts.recalc_timestamp = False
        ft = TTFont(font, lazy=False, recalcTimestamp=False)
        s = subset.Subsetter(options=opts)
        s.populate(text=text)
        s.subset(ft)
        buf = io.BytesIO()
        ft.save(buf)
        out['subset_font_bytes'] = buf.tell()
        outpdf = None
    elif tool == 'reportlab':
        import reportlab
        from reportlab.pdfbase import pdfmetrics
        from reportlab.pdfbase.ttfonts import TTFont as RLFont
        from reportlab.pdfgen import canvas
        rec['tool_version'] = 'reportlab ' + reportlab.Version
        pdfmetrics.registerFont(RLFont('F', font))
        c = canvas.Canvas(outpdf)
        c.setFont('F', 12)
        c.drawString(40, 700, text)
        c.save()
    elif tool == 'fpdf2':
        import fpdf
        rec['tool_version'] = 'fpdf2 ' + fpdf.__version__
        pdf = fpdf.FPDF()
        pdf.add_page()
        pdf.add_font('F', fname=font)
        pdf.set_font('F', size=12)
        pdf.multi_cell(0, 8, text)
        pdf.output(outpdf)
    elif tool == 'pymupdf':
        import pymupdf
        rec['tool_version'] = 'PyMuPDF ' + pymupdf.version[0] + ' (MuPDF ' + pymupdf.version[1] + ')'
        doc = pymupdf.open()
        page = doc.new_page()
        page.insert_font(fontname='F', fontfile=font)
        page.insert_text((40, 72), text, fontname='F', fontsize=12)
        doc.save(outpdf + '.whole.pdf')
        out['whole_pdf_bytes'] = os.path.getsize(outpdf + '.whole.pdf')
        doc.subset_fonts()
        doc.save(outpdf, garbage=3, deflate=True)
        doc.close()
    elif tool == 'pypdfium2':
        import ctypes
        import pypdfium2 as pdfium
        import pypdfium2.raw as raw
        rec['tool_version'] = 'pypdfium2 %s (pdfium %s)' % (pdfium.version.PYPDFIUM_INFO.version if hasattr(pdfium.version, 'PYPDFIUM_INFO') else pdfium.__version__, pdfium.version.PDFIUM_INFO.version if hasattr(pdfium.version, 'PDFIUM_INFO') else '?')
        data = Path(font).read_bytes()
        buf = (ctypes.c_uint8 * len(data)).from_buffer_copy(data)
        pdf = pdfium.PdfDocument.new()
        page = pdf.new_page(595, 842)
        fh = raw.FPDFText_LoadFont(pdf, buf, len(data), raw.FPDF_FONT_TRUETYPE, True)
        if not fh:
            raise RuntimeError('FPDFText_LoadFont returned null')
        obj = raw.FPDFPageObj_CreateTextObj(pdf, fh, 12)
        utf16 = (text + '\0').encode('utf-16-le')
        wbuf = (ctypes.c_ushort * (len(utf16) // 2)).from_buffer_copy(utf16)
        raw.FPDFText_SetText(obj, ctypes.cast(wbuf, ctypes.POINTER(ctypes.c_ushort)))
        raw.FPDFPageObj_Transform(obj, 1, 0, 0, 1, 40, 700)
        raw.FPDFPage_InsertObject(page, obj)
        raw.FPDFPage_GenerateContent(page)
        pdf.save(outpdf)
    else:
        raise SystemExit('unknown tool')
    rec['wall_seconds'] = round(time.perf_counter() - t0, 3)
    rec['cpu_seconds'] = round(time.process_time() - c0, 3)
    rec['baseline_peak_wset_bytes'] = base_peak
    rec['peak_wset_bytes'] = peak_working_set()
    rec['peak_wset_delta_bytes'] = rec['peak_wset_bytes'] - base_peak
    rec.update(out)
    if outpdf:
        rec['pdf_bytes'] = os.path.getsize(outpdf)
    print('RESULT ' + json.dumps(rec))


# ---------------- parent helpers ----------------

def embedded_fonts_in_pdf(pdf_path):
    import pymupdf
    res = []
    doc = pymupdf.open(pdf_path)
    for x in range(1, doc.xref_length()):
        try:
            if doc.xref_get_key(x, 'Type')[1] != '/FontDescriptor':
                continue
        except Exception:
            continue
        for key in ('FontFile', 'FontFile2', 'FontFile3'):
            k = doc.xref_get_key(x, key)
            if k[0] == 'xref':
                ref = int(k[1].split()[0])
                raw = doc.xref_get_key(ref, 'Length')[1]
                try:
                    dec = len(doc.xref_stream(ref))
                except Exception:
                    dec = None
                name = doc.xref_get_key(x, 'FontName')[1]
                res.append({'key': key, 'font_name': name, 'subset_tag': name[1:8] if len(name) > 8 and name[7] == '+' else None,
                            'stream_length': int(raw) if raw.isdigit() else raw, 'decoded_bytes': dec})
    doc.close()
    return res


def run_cli(args, timeout=120):
    try:
        p = subprocess.run(args, capture_output=True, text=True, timeout=timeout)
        return p.returncode, (p.stdout + p.stderr).strip()
    except Exception as e:
        return None, repr(e)


def verify_pdf(pdf_path, tooldir):
    v = {}
    qpdf = tooldir['qpdf']
    pdffonts = tooldir['pdffonts']
    rc, out = run_cli([qpdf, '--check', pdf_path])
    v['qpdf_check_exit'] = rc
    rc, out = run_cli([pdffonts, pdf_path])
    v['pdffonts_exit'] = rc
    v['pdffonts_rows'] = out.splitlines()[2:6]
    return v


def find_tool(repo, name):
    p = repo / '.tools' / 'msys64' / 'ucrt64' / 'bin' / (name + '.exe')
    return str(p) if p.exists() else shutil.which(name)


def child_runs(tool, font, text, work, runs, timeout):
    results = []
    textfile = work / 'sample.txt'
    textfile.write_text(text, encoding='utf-8')
    for i in range(runs):
        outpdf = str(work / ('out-%s-%s-%d.pdf' % (tool, Path(font).stem, i)))
        t0 = time.perf_counter()
        try:
            p = subprocess.run([sys.executable, '-I', os.path.abspath(__file__), '--child', tool, str(font), str(textfile), outpdf],
                               capture_output=True, text=True, timeout=timeout)
            line = [l for l in p.stdout.splitlines() if l.startswith('RESULT ')]
            if p.returncode == 0 and line:
                r = json.loads(line[-1][7:])
                r['run'] = i
                results.append(r)
            else:
                results.append({'tool': tool, 'font': Path(font).name, 'run': i, 'failed': True, 'exit': p.returncode,
                                'stderr_tail': p.stderr.strip().splitlines()[-3:], 'wall_seconds': round(time.perf_counter() - t0, 3)})
                break
        except subprocess.TimeoutExpired:
            results.append({'tool': tool, 'font': Path(font).name, 'run': i, 'failed': True, 'timeout_seconds': timeout})
            break
    return results


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--repo')
    ap.add_argument('--fonts')
    ap.add_argument('--sysfonts')
    ap.add_argument('--work')
    ap.add_argument('--out')
    ap.add_argument('--runs', type=int, default=3)
    ap.add_argument('--timeout', type=int, default=900)
    ap.add_argument('--child', nargs=4)
    ap.add_argument('--skip', default='')
    a = ap.parse_args()
    if a.child:
        child_main(*a.child)
        return
    repo = Path(a.repo)
    work = Path(a.work)
    work.mkdir(parents=True, exist_ok=True)
    skip = set(a.skip.split(','))
    import fontTools
    import pymupdf
    import psutil
    record = {'script': Path(__file__).name, 'script_sha256': sha256_file(__file__),
              'started_utc': time.strftime('%Y-%m-%dT%H:%M:%SZ', time.gmtime()),
              'environment': {'platform': platform.platform(), 'machine': platform.machine(), 'python': sys.version.split()[0],
                              'cpu_count': os.cpu_count(), 'ram_bytes': psutil.virtual_memory().total,
                              'fontTools': fontTools.version, 'PyMuPDF': pymupdf.version[0], 'MuPDF': pymupdf.version[1]},
              'runs_per_pair': a.runs}
    tooldir = {n: find_tool(repo, n) for n in ('qpdf', 'pdffonts', 'mutool')}
    for n, p in tooldir.items():
        rc, out = run_cli([p, '--version'] if n != 'pdffonts' else [p, '-v'])
        record['environment'][n] = out.splitlines()[0] if out else None

    fonts = sorted(Path(a.fonts).iterdir())
    installed = [Path(a.sysfonts) / n for n in ('arial.ttf', 'times.ttf', 'calibri.ttf', 'segoeui.ttf', 'malgun.ttf', 'simsunb.ttf')]
    installed = [p for p in installed if p.exists()]

    # Stage 1: acquired font facts
    if 'facts' not in skip:
        record['acquired_fonts'] = []
        for p in fonts:
            if p.suffix.lower() == '.ttf':
                print('facts', p.name, flush=True)
                try:
                    record['acquired_fonts'].append(font_facts(str(p)))
                except Exception as e:
                    record['acquired_fonts'].append({'file': p.name, 'error': repr(e)})
        record['installed_fonts_compared'] = []
        for p in installed:
            print('facts', p.name, flush=True)
            r = font_facts(str(p))
            r['installed_path'] = str(p)
            record['installed_fonts_compared'].append(r)

    # Stage 2: installed-font size distribution (header only)
    if 'sysdist' not in skip:
        dist = []
        for p in sorted(Path(a.sysfonts).iterdir()):
            if p.suffix.lower() in ('.ttf', '.otf', '.ttc', '.otc'):
                c = classify(str(p))
                c.update({'file': p.name, 'bytes': p.stat().st_size})
                dist.append(c)
        record['installed_font_distribution'] = dist

    # Stage 3: corpus embedded fonts (read-only)
    if 'corpus' not in skip:
        corp = []
        for p in sorted((repo / 'test' / 'jaytwo.Pdf.Tests' / 'Fixtures' / 'corpus').glob('*.pdf')):
            ent = {'file': p.name, 'pdf_bytes': p.stat().st_size}
            try:
                ent['embedded'] = embedded_fonts_in_pdf(str(p))
            except Exception as e:
                ent['error'] = repr(e)[:200]
            corp.append(ent)
        record['corpus_embedded_fonts'] = corp

    # Stage 4: embedder comparison
    if 'embed' not in skip:
        cmp_ = []
        for p in fonts + installed:
            if p not in installed and p.suffix.lower() != '.ttf':
                continue
            text = sample_text(str(p))
            cls = classify(str(p))
            print('embed', p.name, len(text), cls.get('class'), flush=True)
            for tool in TOOLS:
                rs = child_runs(tool, p, text, work, a.runs, a.timeout)
                for r in rs:
                    r['font_class'] = cls.get('class')
                    r['font_bytes'] = p.stat().st_size
                    outpdf = work / ('out-%s-%s-%d.pdf' % (tool, p.stem, r.get('run', 0)))
                    if not r.get('failed') and outpdf.exists() and r.get('run') == 0:
                        try:
                            r['embedded_in_output'] = embedded_fonts_in_pdf(str(outpdf))
                            r['verify'] = verify_pdf(str(outpdf), tooldir)
                        except Exception as e:
                            r['verify_error'] = repr(e)[:200]
                    cmp_.append(r)
                    print('  ', tool, 'FAILED' if r.get('failed') else '%.2fs peakdelta %d MiB' % (r['wall_seconds'], r['peak_wset_delta_bytes'] >> 20), flush=True)
                # remove intermediates beyond run 0 to bound disk use
                for f in work.glob('out-%s-%s-*' % (tool, p.stem)):
                    if not f.name.endswith('-0.pdf') and not f.name.endswith('-0.pdf.whole.pdf'):
                        f.unlink()
        record['embedder_comparison'] = cmp_
    record['finished_utc'] = time.strftime('%Y-%m-%dT%H:%M:%SZ', time.gmtime())
    Path(a.out).write_text(json.dumps(record, indent=1, ensure_ascii=False), encoding='utf-8')
    print('wrote', a.out)


if __name__ == '__main__':
    main()
