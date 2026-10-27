"""W-209 deterministic minimal required-stream fixtures.

For each required decoding site (xref stream, object stream, XMP metadata, embedded file) and each candidate filter this
writes a tiny PDF whose only stream using that filter is the one the library must decode. Variants cover full names,
ISO 32000-1 abbreviations, chains, LZW EarlyChange 0 and a PNG predictor, malformed encodings, unsupported filters and
decompression-limit bombs (decoded sizes just above the 32 MiB structural cap or a 1 MiB caller cap).

Usage: gen_fixtures.py <output-dir> [--no-bombs]. Writes <output-dir>/manifest.json (one entry per file with the expected
plain-text SHA-256). Every LZW/A85/AHx/RL encoding is round-tripped through pypdf, and qpdf --check is run by run.ps1, so
the fixtures do not depend on this library's decoder to be judged valid.
"""
import base64, hashlib, json, os, random, sys, zlib

OUT = sys.argv[1]
BOMBS = '--no-bombs' not in sys.argv
os.makedirs(OUT, exist_ok=True)
STRUCT_CAP = 32 * 1024 * 1024
ATTACH_CAP = 1024 * 1024


# ---------- encoders ----------
def enc_flate(d):
    return zlib.compress(d, 9)


def enc_ahx(d):
    return d.hex().upper().encode() + b'>'


def enc_a85(d):
    return base64.a85encode(d) + b'~>'


def enc_rl(d):
    out = bytearray()
    i, n = 0, len(d)
    while i < n:
        j = i
        while j + 1 < n and d[j + 1] == d[i] and j - i < 127:
            j += 1
        run = j - i + 1
        if run >= 3:
            out += bytes([257 - run, d[i]])
            i += run
        else:
            k = i
            while k < n and k - i < 128 and not (k + 2 < n and d[k] == d[k + 1] == d[k + 2]):
                k += 1
            if k == i:
                k = i + 1
            out += bytes([k - i - 1]) + d[i:k]
            i = k
    return bytes(out) + b'\x80'


def width_for(size, early):
    for w, lim in ((9, 512), (10, 1024), (11, 2048)):
        if size + early < lim:
            return w
    return 12


def enc_lzw(d, early=1):
    out, state = bytearray(), {'buf': 0, 'bits': 0}

    def put(code, width):
        state['buf'] = (state['buf'] << width) | code
        state['bits'] += width
        while state['bits'] >= 8:
            out.append((state['buf'] >> (state['bits'] - 8)) & 255)
            state['bits'] -= 8
        state['buf'] &= (1 << state['bits']) - 1

    table, nxt, m = {}, 258, 0  # m = codes emitted since the last clear (decoder's view)

    def emit(code):
        nonlocal m
        m += 1
        put(code, width_for(258 + max(0, m - 2), early))

    def clear():
        nonlocal table, nxt, m
        emit(256)
        table, nxt, m = {}, 258, 0

    m = 0
    emit(256)
    m = 0
    w = -1
    for b in d:
        if w < 0:
            w = b
            continue
        c = table.get((w, b))
        if c is not None:
            w = c
            continue
        emit(w)
        table[(w, b)] = nxt
        nxt += 1
        if nxt >= 4094:
            clear()
        w = b
    if w >= 0:
        emit(w)
    emit(257)
    if state['bits']:
        out.append((state['buf'] << (8 - state['bits'])) & 255)
    return bytes(out)


def predict_up(d, cols):
    assert len(d) % cols == 0
    out, prev = bytearray(), bytes(cols)
    for i in range(0, len(d), cols):
        row = d[i:i + cols]
        out.append(2)
        out += bytes((row[j] - prev[j]) & 255 for j in range(cols))
        prev = row
    return bytes(out)


# ---------- variants ----------
def V(name, filters, parms, encode, kind='valid'):
    return {'name': name, 'filters': filters, 'parms': parms, 'encode': encode, 'kind': kind}


def pred(cols, inner, early=None):
    return lambda d: inner(predict_up(d, cols))


def variants(cols):
    parm_pred = '<< /Predictor 12 /Columns %d >>' % cols
    return [
        V('flate', ['FlateDecode'], [None], enc_flate),
        V('flate-predictor', ['FlateDecode'], [parm_pred], pred(cols, enc_flate)),
        V('ahx', ['ASCIIHexDecode'], [None], enc_ahx),
        V('a85', ['ASCII85Decode'], [None], enc_a85),
        V('lzw', ['LZWDecode'], [None], enc_lzw),
        V('lzw-early0', ['LZWDecode'], ['<< /EarlyChange 0 >>'], lambda d: enc_lzw(d, 0)),
        V('lzw-predictor', ['LZWDecode'], [parm_pred], pred(cols, enc_lzw)),
        V('rl', ['RunLengthDecode'], [None], enc_rl),
        V('abbr-fl', ['Fl'], [None], enc_flate),
        V('abbr-ahx', ['AHx'], [None], enc_ahx),
        V('abbr-a85', ['A85'], [None], enc_a85),
        V('abbr-lzw', ['LZW'], [None], enc_lzw),
        V('abbr-rl', ['RL'], [None], enc_rl),
        V('chain-a85-lzw', ['ASCII85Decode', 'LZWDecode'], [None, None], lambda d: enc_a85(enc_lzw(d))),
        V('chain-ahx-flate', ['ASCIIHexDecode', 'FlateDecode'], [None, None], lambda d: enc_ahx(enc_flate(d))),
        V('chain-a85-rl', ['ASCII85Decode', 'RunLengthDecode'], [None, None], lambda d: enc_a85(enc_rl(d))),
    ]


def malformed(plain_len):
    """Corruptions of a correct encoding of the plain data. Each returns the bytes to store."""
    def corrupt(fn, enc):
        return lambda d: fn(enc(d))
    mid = lambda b: len(b) // 2
    return [
        V('ahx-badchar', ['ASCIIHexDecode'], [None], corrupt(lambda b: b[:mid(b)] + b'G' + b[mid(b):], enc_ahx), 'malformed'),
        V('ahx-odd-digit', ['ASCIIHexDecode'], [None], corrupt(lambda b: b[:-2] + b'>', enc_ahx), 'malformed'),
        V('ahx-no-eod', ['ASCIIHexDecode'], [None], corrupt(lambda b: b[:-1], enc_ahx), 'malformed'),
        V('a85-badchar', ['ASCII85Decode'], [None], corrupt(lambda b: b[:mid(b)] + b'v' + b[mid(b):], enc_a85), 'malformed'),
        V('a85-no-eod', ['ASCII85Decode'], [None], corrupt(lambda b: b[:-2], enc_a85), 'malformed'),
        V('a85-overflow-group', ['ASCII85Decode'], [None], corrupt(lambda b: b'uuuuu' + b, enc_a85), 'malformed'),
        V('a85-single-trailing-char', ['ASCII85Decode'], [None], corrupt(lambda b: b[:-2] + b'!' + b'~>', enc_a85), 'malformed'),
        V('lzw-bad-code', ['LZWDecode'], [None], lambda d: lzw_bad_code(), 'malformed'),
        V('lzw-truncated', ['LZWDecode'], [None], corrupt(lambda b: b[:len(b) // 2], enc_lzw), 'malformed'),
        V('lzw-no-clear-first', ['LZWDecode'], [None], corrupt(lambda b: b'\x80' + b[2:] if False else b[:0] + b, lambda d: lzw_no_clear(d)), 'malformed'),
        V('rl-truncated-run', ['RunLengthDecode'], [None], corrupt(lambda b: b[:len(b) // 2], enc_rl), 'malformed'),
        V('rl-no-eod', ['RunLengthDecode'], [None], corrupt(lambda b: b[:-1], enc_rl), 'malformed'),
        V('rl-trailing-after-eod', ['RunLengthDecode'], [None], corrupt(lambda b: b + b'\x00Z\x00Y', enc_rl), 'malformed'),
        V('flate-truncated', ['FlateDecode'], [None], corrupt(lambda b: b[:len(b) // 2], enc_flate), 'malformed'),
        V('flate-bad-adler', ['FlateDecode'], [None], corrupt(lambda b: b[:-1] + bytes([b[-1] ^ 1]), enc_flate), 'malformed'),
        V('unsupported-dct', ['DCTDecode'], [None], lambda d: d, 'unsupported'),
        V('unsupported-unknown', ['FooDecode'], [None], lambda d: d, 'unsupported'),
        V('unsupported-in-chain', ['ASCIIHexDecode', 'DCTDecode'], [None, None], enc_ahx, 'unsupported'),
    ]


def lzw_bad_code():
    # clear, then a first code of 300 (> next free code 258): never valid.
    bits = format(256, '09b') + format(300, '09b') + format(257, '09b')
    bits += '0' * (-len(bits) % 8)
    return int(bits, 2).to_bytes(len(bits) // 8, 'big')


def lzw_no_clear(d):
    b = enc_lzw(d)
    # drop the leading clear code (first 9 bits): the stream is re-encoded without it.
    out, buf, nbits = bytearray(), 0, 0
    bits = ''.join(format(x, '08b') for x in b)[9:]
    bits += '0' * (-len(bits) % 8)
    return int(bits, 2).to_bytes(len(bits) // 8, 'big') if bits else b''


# ---------- documents ----------
def obj(n, body):
    return ('%d 0 obj\n' % n).encode() + body + b'\nendobj\n'


def stream_obj(n, entries, data):
    return ('%d 0 obj\n<< %s /Length %d >>\nstream\n' % (n, entries, len(data))).encode() + data + b'\nendstream\nendobj\n'


def filter_entries(filters, parms):
    f = '/Filter /%s' % filters[0] if len(filters) == 1 else '/Filter [%s]' % ' '.join('/' + x for x in filters)
    if all(p is None for p in parms):
        return f
    if len(filters) == 1:
        return f + ' /DecodeParms ' + parms[0]
    return f + ' /DecodeParms [%s]' % ' '.join(p or 'null' for p in parms)


HEADER = b'%PDF-1.5\n%\xe2\xe3\xcf\xd3\n'
PAGES = b'<< /Type /Pages /Kids [3 0 R] /Count 1 >>'
PAGE = b'<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] /Resources << >> >>'


def classic(parts, root_extra=''):
    body, offs = HEADER, {}
    for n, data in parts:
        offs[n] = len(body)
        body += data
    size = max(offs) + 1
    xo = len(body)
    table = b'xref\n0 %d\n0000000000 65535 f \n' % size + b''.join(b'%010d 00000 n \n' % offs[i] for i in range(1, size))
    return body + table + b'trailer\n<< /Size %d /Root 1 0 R >>\nstartxref\n%d\n%%%%EOF\n' % (size, xo)


def payload(n):
    r = random.Random(209)
    out = bytearray()
    while len(out) < n:
        if r.random() < 0.3:
            out += bytes([r.randrange(256)]) * r.randrange(3, 200)
        elif r.random() < 0.2:
            out += bytes(r.randrange(1, 40))  # zero runs exercise A85 'z'
        else:
            out += bytes(r.randrange(256) for _ in range(r.randrange(1, 60)))
    return bytes(out[:n])


def xmp_plain(pad_to=16):
    text = ('<?xpacket begin="﻿" id="W5M0MpCehiHzreSzNTczkc9d"?>\n<x:xmpmeta xmlns:x="adobe:ns:meta/">'
            '<rdf:RDF xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#"><rdf:Description rdf:about="" '
            'xmlns:dc="http://purl.org/dc/elements/1.1/"><dc:title><rdf:Alt><rdf:li xml:lang="x-default">W-209 é中</rdf:li>'
            '</rdf:Alt></dc:title></rdf:Description></rdf:RDF></x:xmpmeta>').encode('utf-8')
    text += b' ' * (-(len(text) + len(b'\n<?xpacket end="w"?>')) % pad_to) + b'\n<?xpacket end="w"?>'
    return text


def doc_xref(var, plain_unused=None):
    # Cross-reference stream (obj 4) is the variable; objects 1-3 are plain.
    parts = [(1, obj(1, b'<< /Type /Catalog /Pages 2 0 R >>')), (2, obj(2, PAGES)), (3, obj(3, PAGE))]
    prefix = HEADER + b''.join(p[1] for p in parts)
    offs = [len(HEADER)]
    for _, d in parts[:-1]:
        offs.append(offs[-1] + len(d))
    entries = b'\x00\x00\x00\xff' + b''.join(bytes([1]) + o.to_bytes(2, 'big') + b'\x00' for o in offs) + bytes([1]) + len(prefix).to_bytes(2, 'big') + b'\x00'
    entries = entries  # 5 rows x 4 bytes
    enc = var['encode'](entries)
    dict_ = '/Type /XRef /Size 5 /W [1 2 1] /Root 1 0 R ' + filter_entries(var['filters'], var['parms'])
    xo = len(prefix)
    data = prefix + stream_obj(4, dict_, enc) + b'startxref\n%d\n%%%%EOF\n' % xo
    return data, {'site': 'xref', 'expect': {'pageCount': 1}}


def doc_objstm(var, _=None):
    bodies = [PAGES, PAGE]
    header, pos = b'', 0
    for n, b in zip((2, 3), bodies):
        header += b'%d %d ' % (n, pos)
        pos += len(b) + 1
    plain = header + b'\n'.join(bodies) + b'\n'
    plain += b' ' * (-len(plain) % 16)
    first = len(header)
    cat = obj(1, b'<< /Type /Catalog /Pages 2 0 R >>')
    prefix = HEADER + cat
    objstm_off = len(prefix)
    enc = var['encode'](plain)
    stm = stream_obj(4, '/Type /ObjStm /N 2 /First %d ' % first + filter_entries(var['filters'], var['parms']), enc)
    xo = objstm_off + len(stm)
    rows = b'\x00\x00\x00\xff' + bytes([1]) + len(HEADER).to_bytes(2, 'big') + b'\x00' + bytes([2]) + (4).to_bytes(2, 'big') + b'\x00' + bytes([2]) + (4).to_bytes(2, 'big') + b'\x01'
    rows += bytes([1]) + objstm_off.to_bytes(2, 'big') + b'\x00' + bytes([1]) + xo.to_bytes(2, 'big') + b'\x00'
    xs = stream_obj(5, '/Type /XRef /Size 6 /W [1 2 1] /Root 1 0 R /Filter /FlateDecode', zlib.compress(rows))
    return prefix + stm + xs + b'startxref\n%d\n%%%%EOF\n' % xo, {'site': 'objstm', 'expect': {'pageCount': 1}}


def doc_xmp(var, _=None):
    plain = xmp_plain()
    enc = var['encode'](plain)
    parts = [(1, obj(1, b'<< /Type /Catalog /Pages 2 0 R /Metadata 4 0 R >>')), (2, obj(2, PAGES)), (3, obj(3, PAGE)),
             (4, stream_obj(4, '/Type /Metadata /Subtype /XML ' + filter_entries(var['filters'], var['parms']), enc))]
    return classic(parts), {'site': 'xmp', 'expect': {'sha256': hashlib.sha256(plain).hexdigest(), 'bytes': len(plain)}}


def doc_attachment(var, _=None):
    plain = payload(4096)
    enc = var['encode'](plain)
    parts = [(1, obj(1, b'<< /Type /Catalog /Pages 2 0 R /Names << /EmbeddedFiles << /Names [(a.bin) 5 0 R] >> >> >>')), (2, obj(2, PAGES)),
             (3, obj(3, PAGE)),
             (4, stream_obj(4, '/Type /EmbeddedFile /Params << /Size 4096 >> ' + filter_entries(var['filters'], var['parms']), enc)),
             (5, obj(5, b'<< /Type /Filespec /F (a.bin) /UF (a.bin) /EF << /F 4 0 R >> >>'))]
    return classic(parts), {'site': 'attachment', 'expect': {'sha256': hashlib.sha256(plain).hexdigest(), 'bytes': len(plain)}}


SITES = {'xref': (doc_xref, 4), 'objstm': (doc_objstm, 16), 'xmp': (doc_xmp, 16), 'attachment': (doc_attachment, 16)}
manifest = []


def write(case_id, data, meta):
    path = os.path.join(OUT, case_id + '.pdf')
    open(path, 'wb').write(data)
    meta.update({'id': case_id, 'file': case_id + '.pdf', 'fileBytes': len(data), 'fileSha256': hashlib.sha256(data).hexdigest()})
    manifest.append(meta)


# self-check the encoders with an independent decoder (pypdf)
def selfcheck():
    from pypdf.filters import ASCII85Decode, ASCIIHexDecode, LZWDecode, RunLengthDecode
    sample = payload(5000)
    assert ASCIIHexDecode.decode(enc_ahx(sample)) == sample
    assert ASCII85Decode.decode(enc_a85(sample)) == sample
    assert RunLengthDecode.decode(enc_rl(sample)) == sample
    assert LZWDecode.decode(enc_lzw(sample)) == sample
    big = (bytes(100000) + payload(60000)) * 2
    assert LZWDecode.decode(enc_lzw(big)) == big  # table-full clear path
    return {'pypdf-roundtrip': 'ok'}


check = selfcheck()
for site, (builder, cols) in SITES.items():
    for var in variants(cols) + malformed(0):
        data, meta = builder(var)
        meta.update({'variant': var['name'], 'kind': var['kind'], 'filters': var['filters']})
        write('%s__%s' % (site, var['name']), data, meta)

if BOMBS:
    def bomb_doc(site, filt_name, filters, enc, plain_len, cap):
        var = V(filt_name, filters, [None] * len(filters), lambda d: enc)
        if site == 'xmp':
            parts = [(1, obj(1, b'<< /Type /Catalog /Pages 2 0 R /Metadata 4 0 R >>')), (2, obj(2, PAGES)), (3, obj(3, PAGE)),
                     (4, stream_obj(4, '/Type /Metadata /Subtype /XML ' + filter_entries(filters, var['parms']), enc))]
            data = classic(parts)
        elif site == 'attachment':
            parts = [(1, obj(1, b'<< /Type /Catalog /Pages 2 0 R /Names << /EmbeddedFiles << /Names [(a.bin) 5 0 R] >> >> >>')), (2, obj(2, PAGES)),
                     (3, obj(3, PAGE)), (4, stream_obj(4, '/Type /EmbeddedFile ' + filter_entries(filters, var['parms']), enc)),
                     (5, obj(5, b'<< /Type /Filespec /F (a.bin) /EF << /F 4 0 R >> >>'))]
            data = classic(parts)
        elif site == 'objstm':
            cat = obj(1, b'<< /Type /Catalog /Pages 2 0 R >>')
            prefix = HEADER + cat
            stm = stream_obj(4, '/Type /ObjStm /N 2 /First 8 ' + filter_entries(filters, var['parms']), enc)
            xo = len(prefix) + len(stm)
            row = lambda t, a, b: bytes([t]) + a.to_bytes(4, 'big') + bytes([b])
            rows = row(0, 0, 255) + row(1, len(HEADER), 0) + row(2, 4, 0) + row(2, 4, 1) + row(1, len(prefix), 0) + row(1, xo, 0)
            xs = stream_obj(5, '/Type /XRef /Size 6 /W [1 4 1] /Root 1 0 R /Filter /FlateDecode', zlib.compress(rows))
            data = prefix + stm + xs + b'startxref\n%d\n%%%%EOF\n' % xo
        else:
            raise ValueError(site)
        write('bomb-%s__%s' % (site, filt_name), data, {'site': site, 'variant': filt_name, 'kind': 'limit', 'filters': filters,
                                                       'decodedBytesIfUnbounded': plain_len, 'capBytes': cap, 'encodedBytes': len(enc)})

    n_big = STRUCT_CAP + 1024
    zeros = bytes(n_big)
    flate_big = zlib.compress(zeros, 9)
    rl_big = b'\x81\x00' * ((n_big + 127) // 128) + b'\x80'  # 129 -> 128 copies of one byte
    a85_big = b'z' * ((n_big + 3) // 4) + b'~>'  # 'z' is four zero bytes
    lzw_big = enc_lzw(zeros)
    for site in ('xmp', 'objstm'):
        for name, filters, enc in (('flate', ['FlateDecode'], flate_big), ('rl', ['RunLengthDecode'], rl_big),
                                   ('a85', ['ASCII85Decode'], a85_big), ('lzw', ['LZWDecode'], lzw_big)):
            bomb_doc(site, name, filters, enc, n_big, STRUCT_CAP)
    n_small = ATTACH_CAP + 4096
    z = bytes(n_small)
    for name, filters, enc in (('flate', ['FlateDecode'], zlib.compress(z, 9)), ('rl', ['RunLengthDecode'], b'\x81\x00' * ((n_small + 127) // 128) + b'\x80'),
                               ('a85', ['ASCII85Decode'], b'z' * ((n_small + 3) // 4) + b'~>'), ('lzw', ['LZWDecode'], enc_lzw(z)),
                               ('ahx', ['ASCIIHexDecode'], enc_ahx(z))):
        bomb_doc('attachment', name, filters, enc, n_small, ATTACH_CAP)

json.dump({'selfcheck': check, 'structuralCap': STRUCT_CAP, 'attachmentCapUsed': ATTACH_CAP, 'cases': manifest}, open(os.path.join(OUT, 'manifest.json'), 'w'), indent=1)
print(json.dumps({'cases': len(manifest), 'selfcheck': check, 'totalBytes': sum(m['fileBytes'] for m in manifest)}))
