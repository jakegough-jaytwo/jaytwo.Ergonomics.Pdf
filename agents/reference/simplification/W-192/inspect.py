"""Independent pypdf destination/URI inspection and qpdf structural validation; never execute actions."""
import hashlib
import json
import logging
import subprocess
import sys
from pathlib import Path
import pypdf
from pypdf import PdfReader

logging.getLogger('pypdf').setLevel(logging.ERROR)
scratch, qpdf, evidence = Path(sys.argv[1]), sys.argv[2], Path(sys.argv[3])
rows = json.loads((evidence / 'results.json').read_text(encoding='utf-8-sig'))
report = []
kept = {'ismap-false', 'page-aa', 'catalog-aa', 'open-goto', 'open-goto-action', 'open-uri', 'uri-mailto',
        'uri-relative', 'uri-query', 'uri-network-path'}
expected_uri = {'uri-mailto': 'mailto:a@example.org', 'uri-relative': 'https://example.org/c?x=1#f',
                'uri-query': 'https://example.org/a/b?q=1', 'uri-network-path': 'https://other.org/x'}
def inspect(path):
    r = PdfReader(path)
    ids = {p.indirect_reference.idnum: i + 1 for i, p in enumerate(r.pages)}
    pages = []
    for page in r.pages:
        links = []
        for ref in page.get('/Annots', []):
            a = ref.get_object()
            dest = a.get('/Dest')
            links.append({'target': ids.get(dest[0].idnum) if isinstance(dest, list) and hasattr(dest[0], 'idnum') else None,
                          'named_destination': str(dest) if isinstance(dest, str) else None,
                          'uri': str(a.get('/A', {}).get('/URI', '')),
                          'keys': sorted(a.keys())})
        pages.append(links)
    return {'pages': pages, 'page_keys': [sorted(p.keys()) for p in r.pages],
            'catalog_keys': sorted(r.trailer['/Root'].keys()),
            'sha256': hashlib.sha256(path.read_bytes()).hexdigest()}

for row in rows:
    assert row['error'] is None, row
    path = scratch / 'outputs' / (row['name'] + '-' + row['operation'] + '.pdf')
    check = subprocess.run([qpdf, '--check', str(path)], capture_output=True, text=True)
    assert check.returncode == 0, (path.name, check.stdout, check.stderr)
    data = inspect(path)
    pages = data['pages']
    if row['name'] == 'navigation':
        if row['operation'] == 'exclude':
            assert [[l['target'] for l in p] for p in pages] == [[1], [2]]
            assert row['omissions'] == {'MissingDestination': 6}
        else:
            expected = [[1, 3, 3, 3], [2, 3, 3, 3], [], []]
            if row['operation'] == 'twice': expected += [[5, 7, 7, 7], [6, 7, 7, 7], [], []]
            assert [[l['target'] for l in p] for p in pages] == expected
            assert not row['omissions']
    else:
        assert len(pages[0]) == (1 if row['name'] in kept else 0), row
        missing = {'missing-name', 'dangling-page', 'nonpage-target', 'bad-mode', 'bad-coordinate'}
        expected = {} if row['name'] in kept else {'MissingDestination' if row['name'] in missing else 'UnsupportedLink': 1}
        assert row['omissions'] == expected, row
        if row['name'] in expected_uri: assert pages[0][0]['uri'] == expected_uri[row['name']]
    assert '/OpenAction' not in data['catalog_keys'] and '/AA' not in data['catalog_keys']
    assert all('/AA' not in keys for keys in data['page_keys'])
    data.update(name=row['name'], operation=row['operation'], qpdf_check=check.returncode)
    report.append(data)

# Same navigation and open-action fixtures with qpdf's two document-selection modes.
comparisons = []
for name, selection in [('navigation', '1,1,2,2'), ('open-goto', '1')]:
    source = str(scratch / 'inputs' / (name + '.pdf'))
    for mode in ['primary', 'empty']:
        path = scratch / (name + '-qpdf-' + mode + '.pdf')
        command = ([qpdf, source, '--pages', '.', selection, '--', str(path)] if mode == 'primary'
                   else [qpdf, '--empty', '--pages', source, selection, '--', str(path)])
        result = subprocess.run(command, capture_output=True, text=True)
        assert result.returncode == 0, result.stderr
        data = inspect(path)
        data.update(name=name, mode=mode)
        comparisons.append(data)
versions = {'python': sys.version.split()[0], 'pypdf': pypdf.__version__,
            'qpdf': subprocess.check_output([qpdf, '--version'], text=True).splitlines()[0]}
(evidence / 'inspection.json').write_text(json.dumps({'versions': versions, 'engine_outputs': report,
    'qpdf_comparisons': comparisons}, indent=2) + '\n', encoding='utf-8')
refused = {'missing-name', 'dangling-page', 'nonpage-target', 'bad-mode', 'bad-coordinate',
           'ambiguous-dest-action', 'named-nextpage', 'remote-goto', 'uri-chain', 'uri-next-null',
           'link-aa', 'ismap-true', 'uri-no-base', 'uri-bad-escape', 'uri-space',
           'catalog-aa', 'page-aa', 'open-uri'}
widened = {'uri-userinfo', 'uri-file', 'uri-custom', 'uri-file-base', 'uri-userinfo-base'}
candidate = [{'name': r['name'], 'operation': r['operation'], 'baseline': 'accepted',
              'proposed': 'refused' if r['name'] in refused else 'accepted',
              'newly_preserved_uri': r['name'] in widened,
              'initial_view_count_added': r['name'] in {'open-goto', 'open-goto-action'},
              'evidence_kind': 'counterfactual structural inference; candidate not implemented'} for r in rows]
assert sum(r['proposed'] == 'refused' for r in candidate) == 18
(evidence / 'candidate.json').write_text(json.dumps(candidate, indent=2) + '\n', encoding='utf-8')
print(f'{len(rows)} engine operations: pypdf semantic assertions and qpdf checks passed; {len(comparisons)} qpdf comparisons inspected.')
