"""Bounded synthetic page-label research; original probe material, MIT.

generate: pypdf creates fixtures and hand-authored displayed-label oracles.
inspect: read engine output; project the preservation alternative with one range
per selected page, then independently evaluate labels using pypdf's reader.
This is disposable modeling, not a proposed production implementation.
"""
import hashlib
import json
import subprocess
import sys
from pathlib import Path
import pypdf
from pypdf import PdfReader, PdfWriter
from pypdf.generic import DictionaryObject as D, ArrayObject as A, NameObject as N, NumberObject as I, TextStringObject as T

mode, scratch, record = sys.argv[1:4]
scratch, record = Path(scratch), Path(record)


def save_json(name, value):
    (record / name).write_text(json.dumps(value, indent=2, ensure_ascii=False) + '\n', encoding='utf-8')


def label(style=None, prefix=None, start=1):
    d = D()
    if style is not None:
        d[N('/S')] = N('/' + style)
    if prefix is not None:
        d[N('/P')] = T(prefix)
    if start != 1:
        d[N('/St')] = I(start)
    return d


if mode == 'generate':
    (scratch / 'inputs').mkdir(parents=True, exist_ok=True)
    fixtures = {
        'mixed': (10, [(0, label('r')), (3, label('D')), (6, label('D', 'A-', 8)), (8, label('R', 'Part-', 4))],
                  ['i', 'ii', 'iii', '1', '2', '3', 'A-8', 'A-9', 'Part-IV', 'Part-V']),
        'restart': (5, [(0, label('D', 'Ex-', 7)), (2, label('D', 'Ex-')), (4, label(None, 'Contents'))],
                    ['Ex-7', 'Ex-8', 'Ex-1', 'Ex-2', 'Contents']),
        'plain': (5, [], ['1', '2', '3', '4', '5']),
        'prefix': (4, [(0, label(None, 'Résumé-')), (2, label())], ['Résumé-', 'Résumé-', '', '']),
        'letters': (4, [(0, label('A', start=26)), (2, label('a', start=26))], ['Z', 'AA', 'z', 'aa']),
    }
    manifest = {}
    for name, (count, ranges, expected) in fixtures.items():
        w = PdfWriter()
        for _ in range(count):
            w.add_blank_page(100, 100)
        if ranges:
            nums = A([v for index, d in ranges for v in (I(index), d)])
            tree = D({N('/Nums'): nums})
            w._root_object[N('/PageLabels')] = tree
        path = scratch / 'inputs' / (name + '.pdf')
        w.write(path)
        assert PdfReader(path).page_labels == expected, name
        manifest[name] = dict(labels=expected, sha256=hashlib.sha256(path.read_bytes()).hexdigest())
    cases = []

    def case(name, selections, mode='concat'):
        cases.append(dict(name=name, mode=mode, inputs=[dict(file=f, pages=p) for f, p in selections],
                          preserve=[manifest[f]['labels'][p - 1] for f, pages in selections for p in pages]))

    case('all-mixed', [('mixed', list(range(1, 11)))])
    case('cross-ranges', [('mixed', [2, 3, 4, 7, 8, 9])], 'extract')
    case('reorder-repeat', [('mixed', [10, 2, 2, 7, 4, 1])], 'extract')
    case('restart-prefix', [('restart', [1, 2, 3, 4, 5])])
    case('plain-subset', [('plain', [3, 5, 3])], 'extract')
    case('two-inputs', [('mixed', [2, 4, 7]), ('restart', [3, 5]), ('plain', [3, 4])])
    case('repeated-input', [('restart', [3, 4]), ('restart', [3, 4])])
    case('prefix-only-empty', [('prefix', [1, 2, 3, 4, 1])])
    case('alphabetic', [('letters', [1, 2, 3, 4])])
    case('unlabeled-first', [('plain', [4, 5]), ('mixed', [1, 7])])
    save_json('inputs.json', manifest)
    save_json('cases.json', cases)
    (scratch / 'inputs' / 'cases.json').write_text(json.dumps(cases), encoding='utf-8')
else:
    qpdf = sys.argv[4]
    cases = json.loads((record / 'cases.json').read_text(encoding='utf-8'))
    results = json.loads((record / 'results.json').read_text(encoding='utf-8-sig'))
    assert all(r['error'] is None for r in results), results
    rows = []
    for c in cases:
        path = scratch / 'outputs' / (c['name'] + '.pdf')
        r = PdfReader(path)
        new = [str(i + 1) for i in range(len(c['preserve']))]
        assert r.page_labels == new
        assert '/PageLabels' not in r.trailer['/Root']
        # The model copies each source descriptor and adjusts its numeric start.
        # It does not use the reader's generated strings to construct the output.
        nums = A()
        index = 0
        for selection in c['inputs']:
            source = PdfReader(scratch / 'inputs' / (selection['file'] + '.pdf'))
            def flatten(tree):
                if '/Nums' in tree:
                    values = tree['/Nums']
                    return [(int(values[i]), values[i+1].get_object()) for i in range(0, len(values), 2)]
                return [pair for kid in tree['/Kids'] for pair in flatten(kid.get_object())]
            ranges = flatten(source.trailer['/Root']['/PageLabels']) if '/PageLabels' in source.trailer['/Root'] else [(0, label('D'))]
            for page in selection['pages']:
                begin, descriptor = max((pair for pair in ranges if pair[0] <= page - 1), key=lambda p: p[0])
                d = D(descriptor)
                if '/S' in d:
                    d[N('/St')] = I(int(d.get('/St', 1)) + page - 1 - begin)
                nums.extend([I(index), d])
                index += 1
        w = PdfWriter()
        w.clone_document_from_reader(r)
        w._root_object[N('/PageLabels')] = D({N('/Nums'): nums})
        modeled = path.with_name(c['name'] + '-preserve.pdf')
        w.write(modeled)
        observed = PdfReader(modeled).page_labels
        assert observed == c['preserve'], (c['name'], observed, c['preserve'])
        for pdf in (path, modeled):
            check = subprocess.run([qpdf, '--check', str(pdf)], capture_output=True, text=True)
            assert check.returncode == 0, check.stdout + check.stderr
        rows.append(dict(name=c['name'], selection=c['inputs'], new_document=new, preserve=observed,
                         engine_has_labels=False, model_ranges=len(nums)//2, qpdf='passed-both'))
    version = subprocess.run([qpdf, '--version'], capture_output=True, text=True).stdout.splitlines()[0]
    save_json('inspection.json', dict(pypdf=pypdf.__version__, python=sys.version.split()[0], qpdf=version, cases=rows))
    repo = scratch.parent.parent
    baseline = json.loads((record.parent / 'W-189/features.json').read_text(encoding='utf-8'))
    corpus = []
    for item in baseline:
        if 'page-labels' not in item['features']:
            continue
        path = repo / item['path']
        reader = PdfReader(path)
        labels = reader.page_labels
        corpus.append(dict(path=item['path'], hash_matches_W189=hashlib.sha256(path.read_bytes()).hexdigest() == item['sha256'],
                           pages=len(labels), first_labels=labels[:16], differs_from_ordinal=sum(v != str(i+1) for i, v in enumerate(labels))))
    save_json('corpus-labels.json', corpus)
    assert all(item['hash_matches_W189'] for item in corpus)
    print(f'{len(rows)} engine outputs and {len(rows)} preservation models verified; zero failures')
