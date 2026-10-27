"""Independent-reader and structural checks for W-195. No candidate implementation is claimed.

Reader 1: pypdf walks each projected output with its own checker (links, counts, open state, destinations).
Reader 2: MuPDF `mutool show outline` is parsed into the same notation.
qpdf --check validates every engine output and projected output. Hand-authored oracles come from generate.py.
"""
import importlib.util
import json
import re
import subprocess
import sys
from collections import Counter
from pathlib import Path
from pypdf import PdfReader
from pypdf.generic import DictionaryObject, IndirectObject

scratch, qpdf, record, poppler, mutool = sys.argv[1:]
scratch, record = Path(scratch), Path(record)
repo = scratch.parent.parent


def run(*args):
    p = subprocess.run([str(a) for a in args], capture_output=True, text=True)
    return p.returncode, p.stdout + p.stderr


def tool_version(*cmd):
    return run(*cmd)[1].strip().splitlines()[0]


def walk_pypdf(path):
    """Independent checker. Returns (tree lines, problems, stats)."""
    r = PdfReader(path)
    page_of = {p.indirect_reference.idnum: i + 1 for i, p in enumerate(r.pages)}
    problems, lines, seen = [], [], set()
    root_ref = r.trailer['/Root'].raw_get('/Outlines')
    root = root_ref.get_object()

    def chain(first_ref, parent_ref, depth):
        items, prev, cur = [], None, first_ref
        out_lines, total = [], 0
        while cur is not None:
            o = cur.get_object()
            if cur.idnum in seen:
                problems.append('revisited item %d' % cur.idnum)
                break
            seen.add(cur.idnum)
            if o.raw_get('/Parent').idnum != parent_ref.idnum:
                problems.append('bad parent on %s' % o['/Title'])
            if (o.raw_get('/Prev').idnum if '/Prev' in o else None) != (prev.idnum if prev is not None else None):
                problems.append('bad prev on %s' % o['/Title'])
            for banned in ('/A', '/SE'):
                if banned in o:
                    problems.append('unexpected %s on %s' % (banned, o['/Title']))
            page = None
            if '/Dest' in o:
                d = o['/Dest']
                if not isinstance(d, list) or not isinstance(d[0], IndirectObject) or d[0].idnum not in page_of:
                    problems.append('dest not a direct page array on %s' % o['/Title'])
                else:
                    page = page_of[d[0].idnum]
            sub_lines, sub_total = [], 0
            if '/First' in o:
                sub_lines, sub_total, last = chain(o.raw_get('/First'), cur, depth + 1)
                if o.raw_get('/Last').idnum != last.idnum:
                    problems.append('bad last on %s' % o['/Title'])
                cnt = int(o['/Count'])
                is_open = cnt > 0
                if abs(cnt) != sub_total:
                    problems.append('bad count on %s: %d vs %d' % (o['/Title'], cnt, sub_total))
                mark = '+' if is_open else '-'
            else:
                is_open, mark = False, ''
                if '/Count' in o or '/Last' in o:
                    problems.append('leaf with Count/Last: %s' % o['/Title'])
            out_lines.append('  ' * depth + str(o['/Title']) + ('@%d' % page if page else '') + mark)
            out_lines.extend(sub_lines)
            total += 1 + (sub_total if is_open else 0)
            prev, last_ref = cur, cur
            cur = o.raw_get('/Next') if '/Next' in o else None
        return out_lines, total, last_ref

    if '/First' not in root:
        return [], ['empty root'], {}
    out_lines, total, last = chain(root.raw_get('/First'), root_ref, 0)
    if root.raw_get('/Last').idnum != last.idnum:
        problems.append('bad root last')
    if int(root['/Count']) != total:
        problems.append('bad root count %s vs %d' % (root['/Count'], total))
    return out_lines, problems, dict(items=len(seen))


def walk_mutool(path):
    code, text = run(mutool, 'show', path, 'outline')
    if code:
        return None
    lines = []
    for raw in text.splitlines():
        m = re.match(r'^([+\-|])\t(\t*)"(.*)"\t(.*)$', raw)
        if not m:
            return ['UNPARSED: ' + raw]
        flag, tabs, title, uri = m.groups()
        page = re.search(r'#page=(\d+)', uri)
        lines.append('  ' * len(tabs) + title + ('@' + page.group(1) if page else '') + {'-': '+', '+': '-', '|': ''}[flag])
    return lines


projection = json.loads((record / 'projection.json').read_text())
cases = json.loads((scratch / 'inputs' / 'cases.json').read_text())
oracle = {c['name']: c for c in cases}
results = []
failures = []
for row in projection:
    name = row['name']
    rec = dict(name=name, outcome=row['outcome'], engine_error=row['engine_error'], engine_omissions=row['engine_omissions'])
    engine_path = scratch / 'outputs' / (name + '.pdf')
    code, text = run(qpdf, '--check', engine_path)
    rec['engine_qpdf_ok'] = code == 0
    rec['engine_has_outlines'] = '/Outlines' in PdfReader(engine_path).trailer['/Root']
    if not rec['engine_qpdf_ok'] or rec['engine_has_outlines'] or row['engine_error']:
        failures.append(f'{name}: baseline engine output unexpected')
    expected_refuse = oracle[name]['refuse']
    if expected_refuse and row['outcome'] != 'refused':
        failures.append(f'{name}: expected refusal')
    if row['outcome'] == 'refused':
        rec['refusal'] = row['refusal']
        if not expected_refuse and name != 'deep':
            failures.append(f'{name}: unexpected refusal')
        results.append(rec)
        continue
    rec['before'] = row['before']
    rec['source_items'] = row['source_items']
    for rule in ('A', 'B'):
        lines = row['after' + rule]
        rec['after' + rule] = lines
        rec['items' + rule] = row['items' + rule]
        path = scratch / 'projected' / f'{name}-{rule}.pdf'
        if not lines:
            if path.exists():
                failures.append(f'{name}-{rule}: file written for empty outline')
            continue
        qcode, qtext = run(qpdf, '--check', path)
        py_lines, problems, stats = walk_pypdf(path)
        mu_lines = walk_mutool(path)
        rec[f'{rule}_qpdf_ok'] = qcode == 0
        rec[f'{rule}_pypdf_problems'] = problems
        rec[f'{rule}_pypdf_equals_model'] = py_lines == lines
        rec[f'{rule}_mutool_equals_model'] = mu_lines == lines
        want = oracle[name].get('oracle' + rule)
        if want is not None:
            rec[f'{rule}_oracle_equals_model'] = want.split('\n') == lines if want else lines == []
        for key, ok in ((f'{rule}_qpdf_ok', qcode == 0), (f'{rule}_pypdf_clean', not problems),
                        (f'{rule}_pypdf', py_lines == lines), (f'{rule}_mutool', mu_lines == lines),
                        (f'{rule}_oracle', want is None or rec[f'{rule}_oracle_equals_model'])):
            if not ok:
                failures.append(f'{name}: {key} failed')
    # Both rules must retain exactly the same live (titled, targeted) items.
    live = lambda ls: Counter(re.sub(r'[+-]$', '', l.strip()) for l in ls if '@' in l)
    if live(row['afterA']) != live(row['afterB']):
        failures.append(f'{name}: live item sets differ between rules')
    rec['live_items'] = sum(live(row['afterA']).values())
    rec['dead_ancestor_headings_A'] = row['dead_ancestorsA']
    if row['itemsA'] - row['itemsB'] != row['dead_ancestorsA']:
        failures.append(f'{name}: A-B count != dead ancestors')
    results.append(rec)

# Baseline versus counterfactual over the research matrix. Not a prevalence claim.
accepted = [r for r in results if r['outcome'] == 'accepted']
refused = [r for r in results if r['outcome'] == 'refused']
with_outline = [r for r in accepted if r.get('source_items') and sum(r['source_items'])]
summary = dict(
    operations=len(results), engine_baseline_accepted=sum(1 for r in results if not r['engine_error']),
    engine_baseline_with_outlines_in_output=sum(1 for r in results if r['engine_has_outlines']),
    proposal_accepted=len(accepted), proposal_refused=len(refused),
    proposal_accepted_with_projected_outline=sum(1 for r in accepted if r['itemsA']),
    rules_differ=sum(1 for r in accepted if r['itemsA'] != r['itemsB']),
    refusal_reasons=dict(Counter(f"{r['refusal']['kind']}:{r['refusal']['reason']}" for r in refused)),
)

# Real-file scan of the repository's tracked PDFs (synthetic and derived fixtures; not customer prevalence).
spec = importlib.util.spec_from_file_location('project', record / 'project.py')
project = importlib.util.module_from_spec(spec)
spec.loader.exec_module(project)
files = subprocess.run(['git', '-C', str(repo), 'ls-files', '*.pdf'], capture_output=True, text=True).stdout.split()
corpus = Counter()
corpus_rows = []
for f in files:
    corpus['files'] += 1
    try:
        reader = PdfReader(repo / f)
        if reader.is_encrypted:
            corpus['encrypted_skipped'] += 1
            continue
        if '/Outlines' not in reader.trailer['/Root']:
            corpus['no_outlines'] += 1
            continue
        corpus['with_outlines'] += 1
        try:
            tree = project.read_outline(reader)
            n = sum(1 for _ in project.iter_nodes(tree))
            corpus['classified_supported' if n else 'empty_outline'] += 1
            row = dict(file=f, items=n, pages=len(reader.pages), selections=0, rules_differ=0,
                       dead_ancestors_total=0, max_items_A=0, max_items_B=0, empty_A=0)
            if n:
                for p in range(1, len(reader.pages) + 1):
                    # Single-page and every-other-page selections over every page of a bounded sample.
                    for sel in ([p], list(range(p, len(reader.pages) + 1, 2))):
                        fc = {}
                        for k, src in enumerate(sel):
                            fc.setdefault(src, k + 1)
                        a = project.project(tree, lambda x: fc.get(x['page']), 'A')
                        b = project.project(tree, lambda x: fc.get(x['page']), 'B')
                        na, nb = sum(1 for _ in project.iter_nodes(a)), sum(1 for _ in project.iter_nodes(b))
                        row['selections'] += 1
                        row['rules_differ'] += na != nb
                        row['dead_ancestors_total'] += na - nb
                        row['max_items_A'] = max(row['max_items_A'], na)
                        row['max_items_B'] = max(row['max_items_B'], nb)
                        row['empty_A'] += na == 0
            corpus_rows.append(row)
        except project.Refusal as e:
            corpus[f'refused_{e.kind}_{e.reason}'] += 1
            corpus_rows.append(dict(file=f, refusal=f'{e.kind}:{e.reason}'))
    except Exception as e:  # unreadable or deliberately malformed fixtures
        corpus['unreadable_' + type(e).__name__] += 1

real_examples = []
for f, sel in (('test/jaytwo.Pdf.Tests/Fixtures/corpus/faa-8710-1.pdf', [3]), ('test/jaytwo.Pdf.Tests/Fixtures/corpus/opm-of306.pdf', [2]),
               ('test/jaytwo.Pdf.Tests/Fixtures/corpus/opm-of306.pdf', [3, 1])):
    tree = project.read_outline(PdfReader(repo / f))
    fc = {}
    for k, src in enumerate(sel):
        fc.setdefault(src, k + 1)
    real_examples.append(dict(file=f, pages=sel, before=project.render_source(tree),
                              afterA=project.render(project.project(tree, lambda x: fc.get(x['page']), 'A')),
                              afterB=project.render(project.project(tree, lambda x: fc.get(x['page']), 'B'))))

record_out = dict(real_examples=real_examples, 
    tools=dict(pypdf=__import__('pypdf').__version__, qpdf=tool_version(qpdf, '--version'),
               mutool=tool_version(mutool, '--version')),
    summary=summary, corpus=dict(corpus), corpus_files_with_outlines=corpus_rows, failures=failures, results=results)
(record / 'inspection.json').write_text(json.dumps(record_out, indent=2) + '\n', encoding='utf-8')
print(json.dumps(dict(summary=summary, corpus=dict(corpus), failures=failures), indent=2))
if failures:
    sys.exit(1)
