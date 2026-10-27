"""Read-only symbol inventory; run from the repository root."""
import hashlib
import json
from pathlib import Path
import re
import subprocess

symbols = ['IPdfSignatureProvider', 'PdfSignatureContext',
           'CmsDetachedSignatureProvider', 'DocTimeStampSignatureProvider']
paths = subprocess.check_output(['git', 'ls-files'], text=True).splitlines()
hits = []
implementations = []
for name in paths:
    if not (name.startswith(('src/', 'test/', 'tools/fuzz/', 'docs/'))
            or name in ('ACCEPTANCE.md', 'README.md', 'reset-plan.md')):
        continue
    p = Path(name)
    if p.suffix not in ('.cs', '.md'):
        continue
    for line, value in enumerate(p.read_text(encoding='utf-8-sig').splitlines(), 1):
        found = [s for s in symbols if s in value]
        if found:
            hits.append(dict(path=name, line=line, symbols=found, text=value.strip()))
        if re.search(r'class\s+\w+\s*:\s*IPdfSignatureProvider\b', value):
            implementations.append(dict(path=name, line=line, text=value.strip()))
files = sorted({h['path'] for h in hits})
result = dict(revision=subprocess.check_output(['git', 'rev-parse', 'HEAD'], text=True).strip(),
              matched_files=len(files), matching_lines=len(hits),
              implementations=implementations,
              files=[dict(path=p, sha256=hashlib.sha256(Path(p).read_bytes()).hexdigest()) for p in files],
              hits=hits)
Path('agents/reference/simplification/W-202/inventory.json').write_text(
    json.dumps(result, indent=2) + '\n', encoding='utf-8')
print(json.dumps({k: result[k] for k in ('revision', 'matched_files', 'matching_lines', 'implementations')}, indent=2))
