"""W-218 closure, concern 3: measure the specification set (read-only; no opinion).

python -I w218-closure-measure.py --repo REPO --out OUT.json

Reads docs/spec/*.md, docs/spec/drafting/*.md and the completeness-review table in drafting/reconciliation.md and git
history (git log --grep=W-218). Counts are mechanical proxies and are labelled as such:
  gap_phrases  = comma/semicolon-separated phrases in the table column "Remaining limits, refusal and failure/cancellation gaps"
  unexecuted   = case-insensitive occurrences of the word 'unexecuted' (a draft self-reports unrun scenarios this way)
  owner_marks  = occurrences of 'owner' (any context); a crude indicator of owner-facing text, not of decisions pending
"""
import argparse
import hashlib
import json
import re
import subprocess
import sys
from pathlib import Path


def sha256(path):
    h = hashlib.sha256()
    h.update(Path(path).read_bytes())
    return h.hexdigest()


def git(repo, *args):
    p = subprocess.run(['git', '-C', str(repo)] + list(args), capture_output=True, text=True, encoding='utf-8', errors='replace')
    return p.stdout


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--repo', required=True)
    ap.add_argument('--out', required=True)
    a = ap.parse_args()
    repo = Path(a.repo)
    spec = repo / 'docs' / 'spec'
    rec = {'script': Path(__file__).name, 'script_sha256': sha256(__file__), 'python': sys.version.split()[0],
           'git_head': git(repo, 'rev-parse', 'HEAD').strip(), 'git_branch': git(repo, 'rev-parse', '--abbrev-ref', 'HEAD').strip(),
           'subjects': [], 'drafting': [], 'research': []}
    status = {}
    for line in git(repo, 'status', '--porcelain', '--', 'docs').splitlines():
        status[line[3:].strip().replace('\\', '/')] = line[:2]

    for p in sorted(spec.glob('*.md')):
        text = p.read_text(encoding='utf-8')
        rel = p.relative_to(repo).as_posix()
        rec['subjects'].append({'file': p.name, 'bytes': p.stat().st_size, 'lines': text.count('\n') + 1,
                                'sha256': sha256(p), 'git_status': status.get(rel, 'clean-or-tracked'),
                                'headings': len(re.findall(r'^#{1,6} ', text, flags=re.M)),
                                'unexecuted': len(re.findall(r'unexecuted', text, flags=re.I)),
                                'owner_marks': len(re.findall(r'owner', text, flags=re.I)),
                                'requirement_ids': len(set(re.findall(r'\b[A-Z]{3,}(?:-[A-Z0-9]+)+\b', text)))})
    for p in sorted((spec / 'drafting').glob('*.md')):
        text = p.read_text(encoding='utf-8')
        rec['drafting'].append({'file': p.name, 'bytes': p.stat().st_size, 'lines': text.count('\n') + 1})
    for p in sorted((spec / 'drafting' / 'research').iterdir()):
        if p.is_file():
            rec['research'].append({'file': p.name, 'bytes': p.stat().st_size})
    rec['totals'] = {'spec_bytes': sum(s['bytes'] for s in rec['subjects']), 'drafting_bytes': sum(s['bytes'] for s in rec['drafting']),
                     'research_bytes': sum(s['bytes'] for s in rec['research'])}
    forms = next(s for s in rec['subjects'] if s['file'] == 'forms.md')
    rec['forms_share_of_spec_bytes'] = round(forms['bytes'] / rec['totals']['spec_bytes'], 4)
    rec['forms_share_excluding_readme'] = round(forms['bytes'] / (rec['totals']['spec_bytes'] - next(s for s in rec['subjects'] if s['file'] == 'README.md')['bytes']), 4)

    # completeness-review table
    recon = (spec / 'drafting' / 'reconciliation.md').read_text(encoding='utf-8')
    start = recon.index('## Completeness review by subject')
    end = recon.index('\n## ', start + 5)
    rows = []
    for line in recon[start:end].splitlines():
        if line.startswith('| [') or line.startswith('| '):
            cells = [c.strip() for c in line.strip().strip('|').split('|')]
            if len(cells) == 5 and not cells[0].startswith('---') and not cells[0].startswith('Subject'):
                gaps = [g.strip() for g in re.split(r'[;,]', cells[3]) if g.strip()]
                rows.append({'subject': re.sub(r'\]\(.*?\)', ']', cells[0]), 'gap_phrases': len(gaps), 'gap_text': cells[3]})
    rec['completeness_review_rows'] = rows

    # git history
    log = git(repo, 'log', '--grep=W-218', '--pretty=format:%h|%ad|%s', '--date=short').splitlines()
    rec['git_w218_commits'] = [l.split('|', 2) for l in log]
    rec['git_total_commits'] = int(git(repo, 'rev-list', '--count', 'HEAD').strip())
    rec['git_status_docs'] = status
    Path(a.out).write_text(json.dumps(rec, indent=1, ensure_ascii=False), encoding='utf-8')
    print('subjects', len(rec['subjects']), 'rows', len(rows), 'w218 commits', len(log))
    for s in rec['subjects']:
        print('%-26s %8d bytes %5d lines unexec=%-4d' % (s['file'], s['bytes'], s['lines'], s['unexecuted']))
    for r in rows:
        print(r['subject'][:30], r['gap_phrases'])
    print('forms share', rec['forms_share_of_spec_bytes'], rec['forms_share_excluding_readme'])


if __name__ == '__main__':
    main()
