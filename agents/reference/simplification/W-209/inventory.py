"""W-209 resolved-stream inventory over the W-188 pinned corpus.

For every PDF in W-188/fixture-manifest.csv (hash-checked) this resolves the streams the library must consume --
cross-reference streams (startxref/Prev/XRefStm chain), object streams, the catalog /Metadata stream and embedded-file
streams (/EF values and /Type /EmbeddedFile) -- through qpdf's object model, which sees inside object streams. Every other
stream is counted as "other" (copied/ignored; not decoder demand). Run from any directory.
"""
import csv, hashlib, json, os, re, subprocess

root = os.path.abspath(os.path.join(os.path.dirname(__file__), '../../../..'))
here = os.path.dirname(os.path.abspath(__file__))
qpdf = os.environ.get('JAYTWO_QPDF') or os.path.join(root, '.tools/msys64/ucrt64/bin/qpdf.exe')
CANON = {'AHx': 'ASCIIHexDecode', 'A85': 'ASCII85Decode', 'LZW': 'LZWDecode', 'Fl': 'FlateDecode', 'RL': 'RunLengthDecode'}


def canon(name):
    name = name.lstrip('/')
    return CANON.get(name, name), name in CANON


def chain_of(d):
    f = d.get('/Filter')
    if f is None:
        return []
    return f if isinstance(f, list) else [f]


def describe(d):
    chain = chain_of(d)
    parms = d.get('/DecodeParms')
    plist = parms if isinstance(parms, list) else [parms]
    predictor = any(isinstance(p, dict) and isinstance(p.get('/Predictor', 1), int) and p.get('/Predictor', 1) > 1 for p in plist if p)
    return {'chain': [canon(c)[0] for c in chain], 'abbreviated': [canon(c)[0] for c in chain if canon(c)[1]], 'predictor': predictor}


def xref_chain(data):
    """Follow startxref -> sections. Raw header read at known xref offsets only (no whole-file scan)."""
    out, seen = [], set()
    m = re.search(rb'startxref\s+(\d+)', data[-2048:])
    todo = [int(m.group(1))] if m else []
    while todo:
        off = todo.pop()
        if off in seen or off >= len(data):
            continue
        seen.add(off)
        head = data[off:off + 4096]
        if re.match(rb'\s*\d+\s+\d+\s+obj', head):
            end = head.find(b'stream')
            h = head[:end if end > 0 else len(head)].decode('latin-1')
            flt = re.search(r'/Filter\s*(\[[^\]]*\]|/\w+)', h)
            chain = re.findall(r'/(\w+)', flt.group(1)) if flt else []
            out.append({'chain': [canon(c)[0] for c in chain], 'abbreviated': [canon(c)[0] for c in chain if canon(c)[1]],
                        'predictor': bool(re.search(r'/Predictor\s+(?:[2-9]|1\d)', h)), 'offset': off, 'kind': 'xref-stream'})
            prev = re.search(r'/Prev\s+(\d+)', h)
        else:
            out.append({'chain': [], 'abbreviated': [], 'predictor': False, 'offset': off, 'kind': 'xref-table'})
            t = data.find(b'trailer', off)
            h = data[t:t + 4096].decode('latin-1') if t > 0 else ''
            prev = re.search(r'/Prev\s+(\d+)', h)
            hy = re.search(r'/XRefStm\s+(\d+)', h)
            if hy:
                todo.append(int(hy.group(1)))
        if prev:
            todo.append(int(prev.group(1)))
    return out


# Passwords: corpus/manifest.json (libreoffice-memo-rc4) and the tests (pypdf-unsigned-p-r*, qpdf-encrypted).
PASSWORDS = {'libreoffice-memo-rc4.pdf': 'corpus-user', 'qpdf-encrypted.pdf': 'user-secret'}


def inventory(path):
    data = open(path, 'rb').read()
    res = {'xref': xref_chain(data), 'objstm': [], 'metadata': [], 'attachment': [], 'other': {}, 'otherAbbreviated': 0}
    p = subprocess.run([qpdf, '--json=2', '--json-key=qpdf', '--password=' + PASSWORDS.get(os.path.basename(path), 'user-secret' if 'pypdf-unsigned' in path else ''), path], capture_output=True)
    if p.returncode not in (0, 3):
        res['qpdfError'] = p.stderr.decode('latin-1')[:300]
        return res
    objs = json.loads(p.stdout)['qpdf'][1]
    trailer = objs.get('trailer', {}).get('value', {})
    root_ref = trailer.get('/Root', '')
    cat = objs.get('obj:' + root_ref, {}).get('value', {}) if root_ref else {}
    meta_ref = cat.get('/Metadata')
    ef_refs = set()
    for v in objs.values():
        val = v.get('value') if isinstance(v, dict) else None
        if isinstance(val, dict) and isinstance(val.get('/EF'), dict):
            ef_refs.update(x for x in val['/EF'].values() if isinstance(x, str))
    for k, v in objs.items():
        if not k.startswith('obj:') or 'stream' not in v:
            continue
        ref = k[4:]
        d = v['stream']['dict']
        info = describe(d)
        info['object'] = ref
        name = '/'.join(info['chain']) or 'none'
        if d.get('/Type') == '/ObjStm':
            res['objstm'].append(info)
        elif d.get('/Type') == '/XRef':
            continue  # already covered by the startxref chain
        elif ref == meta_ref:
            res['metadata'].append(info)
        elif ref in ef_refs or d.get('/Type') == '/EmbeddedFile':
            res['attachment'].append(info)
        else:
            res['other'][name] = res['other'].get(name, 0) + 1
            res['otherAbbreviated'] += 1 if info['abbreviated'] else 0
    return res


def main():
    manifest = list(csv.DictReader(open(os.path.join(root, 'agents/reference/simplification/W-188/fixture-manifest.csv'))))
    files, agg, mismatch = {}, {}, []
    for row in manifest:
        path = os.path.join(root, row['path'])
        sha = hashlib.sha256(open(path, 'rb').read()).hexdigest()
        if sha != row['sha256']:
            mismatch.append(row['path'])
        r = inventory(path)
        files[row['path']] = r
        for role in ('xref', 'objstm', 'metadata', 'attachment'):
            for s in r[role]:
                if s.get('kind') == 'xref-table':
                    continue
                key = (role, '+'.join(s['chain']) or 'none')
                a = agg.setdefault(key, {'streams': 0, 'files': set(), 'abbreviated': 0, 'predictor': 0})
                a['streams'] += 1
                a['files'].add(row['path'])
                a['abbreviated'] += 1 if s['abbreviated'] else 0
                a['predictor'] += 1 if s['predictor'] else 0
    other = {}
    for r in files.values():
        for k, n in r['other'].items():
            other[k] = other.get(k, 0) + n
    summary = [{'role': k[0], 'chain': k[1], 'streams': v['streams'], 'files': len(v['files']), 'abbreviatedStreams': v['abbreviated'], 'predictorStreams': v['predictor']} for k, v in sorted(agg.items())]
    candidates = ('ASCIIHexDecode', 'ASCII85Decode', 'LZWDecode', 'RunLengthDecode')
    out = {'qpdf': subprocess.check_output([qpdf, '--version'], text=True).splitlines()[0], 'corpusPdfs': len(manifest), 'hashMismatches': mismatch,
           'requiredStreamSummary': summary,
           'candidateFilterInRequiredStream': [s for s in summary if any(c in s['chain'] for c in candidates)],
           'xrefSectionKinds': {k: sum(1 for r in files.values() for s in r['xref'] if s['kind'] == k) for k in ('xref-table', 'xref-stream')},
           'abbreviatedStreams': {'required': sum(1 for r in files.values() for role in ('xref', 'objstm', 'metadata', 'attachment') for x in r[role] if x['abbreviated']), 'otherCopiedOrIgnored': sum(r.get('otherAbbreviated', 0) for r in files.values())},
           'otherStreamChainsNotDecoderDemand': dict(sorted(other.items(), key=lambda x: -x[1])), 'files': files}
    json.dump(out, open(os.path.join(here, 'inventory.json'), 'w'), indent=1)
    print(json.dumps({k: out[k] for k in out if k != 'files'}, indent=1))
    errs = {p: r['qpdfError'] for p, r in files.items() if 'qpdfError' in r}
    if errs:
        print('qpdf errors', errs)


main()
