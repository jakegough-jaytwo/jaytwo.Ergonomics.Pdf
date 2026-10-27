"""W-209 lexical size of the candidate decoders (brace-matched line counts) and test/fuzz mentions. Run from any directory."""
import json, os, re

root = os.path.abspath(os.path.join(os.path.dirname(__file__), '../../../..'))
src = open(os.path.join(root, 'src/jaytwo.Pdf/Internal/PdfStreamDecoder.cs'), encoding='utf-8-sig').read().splitlines()


def block(pattern):
    for i, l in enumerate(src):
        if re.search(pattern, l):
            depth, seen = 0, False
            for j in range(i, len(src)):
                depth += src[j].count('{') - src[j].count('}')
                seen = seen or '{' in src[j]
                if seen and depth == 0:
                    return {'start': i + 1, 'end': j + 1, 'lines': j - i + 1}
    return None


items = {
    'AsciiHexDecodeStream (stream decoder, includes NextByte)': r'class AsciiHexDecodeStream\b',
    'Ascii85DecodeStream (stream decoder)': r'class Ascii85DecodeStream\b',
    'LzwDecodeStream (stream decoder)': r'class LzwDecodeStream\b',
    'RunLengthDecodeStream (stream decoder)': r'class RunLengthDecodeStream\b',
    'Lzw() array wrapper': r'private static byte\[\] Lzw\(',
    'RunLength() array wrapper': r'private static byte\[\] RunLength\(',
    'EarlyChangeOf()': r'private static int EarlyChangeOf\(',
    'AsciiHex() array decoder': r'static byte\[\] AsciiHex\(',
    'Ascii85() array decoder': r'static byte\[\] Ascii85\(',
    'WriteGroup()': r'static .* WriteGroup\(',
    'HexNibble()': r'static .* HexNibble\(',
}
out = {k: block(v) for k, v in items.items()}
total = sum(v['lines'] for v in out.values() if v)
files = {}
for d in ('test', 'tools/fuzz', 'src'):
    for base, _, fs in os.walk(os.path.join(root, d)):
        if os.sep + 'obj' in base or os.sep + 'bin' in base:
            continue
        for f in fs:
            if not f.endswith('.cs'):
                continue
            p = os.path.join(base, f)
            text = open(p, encoding='utf-8-sig').read()
            n = len(re.findall(r'LZW|ASCII85|ASCIIHex|RunLength|"A85"|"AHx"|"RL"|\bLzw\b', text, re.I))
            if n:
                files[os.path.relpath(p, root).replace(os.sep, '/')] = {'mentions': n, 'lines': text.count('\n') + 1,
                                                                        'factsAndTheories': len(re.findall(r'\[(?:Fact|Theory)', text))}
res = {'PdfStreamDecoder.cs total lines': len(src), 'blocks': out, 'candidateBlockLinesTotal': total,
       'fractionOfFile': round(total / len(src), 3), 'filesMentioningCandidates': files}
json.dump(res, open(os.path.join(os.path.dirname(__file__), 'loc.json'), 'w'), indent=1)
print(json.dumps({k: res[k] for k in ('PdfStreamDecoder.cs total lines', 'candidateBlockLinesTotal', 'fractionOfFile')}))
for k, v in files.items():
    print(k, v)
