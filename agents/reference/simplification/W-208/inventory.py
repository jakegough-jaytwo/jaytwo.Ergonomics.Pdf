"""W-208 static inventory of the asynchronous surface. Counts only; run from any directory."""
import json, os, re, subprocess

root = os.path.abspath(os.path.join(os.path.dirname(__file__), '../../../..'))
rev = subprocess.check_output(['git', '-C', root, 'rev-parse', 'HEAD'], text=True).strip()

def files(sub):
    for d, _, fs in os.walk(os.path.join(root, sub)):
        if os.sep + 'obj' in d or os.sep + 'bin' in d:
            continue
        for f in fs:
            if f.endswith('.cs'):
                yield os.path.join(d, f)

def read(p):
    return open(p, encoding='utf-8-sig').read()

def rel(p):
    return os.path.relpath(p, root).replace(os.sep, '/')

out = {'revision': rev}
pdf = list(files('src/jaytwo.Pdf'))
ergo = list(files('src/jaytwo.Ergonomics.Pdf'))

def count(paths, pat):
    r = re.compile(pat)
    hits = {}
    for p in paths:
        n = len(r.findall(read(p)))
        if n:
            hits[rel(p)] = n
    return hits

# Public async-shaped members in each library (declaration lines only).
pub = re.compile(r'^\s*public [^=;{]*\b(\w+Async)\s*\(', re.M)
for name, paths in (('jaytwo.Pdf', pdf), ('jaytwo.Ergonomics.Pdf', ergo)):
    members = {}
    for p in paths:
        for m in pub.finditer(read(p)):
            members.setdefault(rel(p), []).append(m.group(1))
    out['public_async_members_' + name] = {k: v for k, v in members.items()}
    out['public_async_member_count_' + name] = sum(len(v) for v in members.values())

# Async-shaped internals: ValueTask / Task returning or awaiting members, by file.
out['valuetask_mentions'] = count(pdf, r'\bValueTask\b')
out['useAsync_mentions'] = count(pdf, r'\buseAsync\b')
out['await_mentions'] = count(pdf, r'\bawait\b')
out['pdfsync_result_mentions'] = count(pdf, r'\bPdfSync\.Result\b')
out['totals'] = {
    'valuetask_mentions': sum(out['valuetask_mentions'].values()),
    'useAsync_mentions': sum(out['useAsync_mentions'].values()),
    'await_mentions': sum(out['await_mentions'].values()),
    'pdfsync_result_mentions': sum(out['pdfsync_result_mentions'].values()),
}

# Signer / timestamp / output async that is independent of source inspection.
sign = count(pdf + ergo, r'\b(IPdfSignatureProvider|ITimestampAuthority|IPdfExternalSigner)\b')
out['signer_timestamp_interface_mentions'] = sign
out['signer_async_signatures'] = {
    rel(p): re.findall(r'(?:ValueTask|Task)<?[^\n(]*>?\s+(\w+Async)\(', read(p))
    for p in pdf + ergo if re.search(r'ISignatureProvider|IPdfSignatureProvider|ITimestampAuthority|IPdfExternalSigner', read(p)) and 'interface' in read(p)
}

# Tests that name asynchronous behaviour.
tests = list(files('test'))
t = {}
for p in tests:
    s = read(p)
    n = len(re.findall(r'\bAsync\s*\(', s)) + len(re.findall(r'\bReadAsync\b|\bOpenAsync\b|\bSaveAsync\b|\bSignAsync\b', s))
    if n:
        t[rel(p)] = {'asyncMentions': n, 'lines': s.count('\n') + 1}
out['tests_with_async_mentions'] = t
ap = os.path.join(root, 'test/jaytwo.Pdf.Tests/AsyncTests.cs')
if os.path.exists(ap):
    s = read(ap)
    out['AsyncTests'] = {'lines': s.count('\n') + 1, 'facts_and_theories': len(re.findall(r'\[(?:Fact|Theory)', s))}

# Public members that read the source (engine-backed) in the document-side API, by file.
members = {}
for rp in ['src/jaytwo.Pdf/PdfDocument.cs', 'src/jaytwo.Pdf/PdfDocumentEditor.cs', 'src/jaytwo.Pdf/AcroForm/PdfAcroForm.cs',
           'src/jaytwo.Pdf/AcroForm/PdfAcroFormEditor.cs', 'src/jaytwo.Pdf/Metadata/PdfMetadata.cs',
           'src/jaytwo.Pdf/Metadata/PdfMetadataEditor.cs', 'src/jaytwo.Pdf/Attachments/PdfAttachmentCollection.cs',
           'src/jaytwo.Pdf/Attachments/PdfAttachmentCollectionEditor.cs', 'src/jaytwo.Pdf/PdfPageEditor.cs']:
    s = read(os.path.join(root, rp))
    members[rp] = sorted(set(re.findall(r'^\s*public [^=;{(\n]*?\b(\w+)\s*(?:=>|\(|$)', s, re.M)) - {'Dispose'})
out['public_members_by_file_not_verified_by_probe'] = members
json.dump(out, open(os.path.join(os.path.dirname(__file__), 'inventory.json'), 'w'), indent=2)
print(json.dumps({k: out[k] for k in ('revision', 'totals', 'public_async_member_count_jaytwo.Pdf', 'public_async_member_count_jaytwo.Ergonomics.Pdf', 'AsyncTests')}, indent=1))
