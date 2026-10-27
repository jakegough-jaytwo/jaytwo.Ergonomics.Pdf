"""W-197 counterfactual admission classifier. Usage: python -I classify.py REPO INPUTS OUTPUTS

Read-only. Applies the proposed bounded assembly preflight (proposal.md, rule set R197) to the W-197 fixtures
(selections [1] and [2]) and to the 51 tracked W-188 corpus files (selections [1] and all pages). Unlike W-189's
whole-graph scan (its limit L1), it inspects only the structures the proposal names: catalog entries, /Perms, the
AcroForm field tree, presence of the JavaScript and EmbeddedFiles name trees, and the selected pages with their
annotations and resource /Properties. It never decodes content streams. It is not the engine's implementation.
"""
import csv
import json
import logging
import os
import sys
from collections import Counter
from pathlib import Path
from pypdf import PdfReader
from pypdf.generic import IndirectObject, DictionaryObject, ArrayObject

MAX_FIELD_DEPTH = 256          # the engine's PdfObjectStore.MaxTraversalDepth
MAX_FIELD_NODES = 100_000      # proposed per-input whole-input field budget
PASSWORDS = {
    'test/jaytwo.Pdf.Tests/Fixtures/corpus/libreoffice-memo-rc4.pdf': 'corpus-user',
    'test/jaytwo.Pdf.Tests/Fixtures/qpdf-encrypted.pdf': 'user-secret',
}
for r in (2, 3, 4, 5, 6):
    PASSWORDS[f'test/jaytwo.Pdf.Tests/Fixtures/defects/pypdf-unsigned-p-r{r}.pdf'] = 'user-secret'

# Category -> (scope, candidate disposition). 'refuse' has no consent path; 'derivative' is refused by default and
# admitted only by the proposed explicit unsigned derivative; 'owner-item' is decided by the named research item.
CATEGORIES = {
    'certification': ('input', 'refuse'),
    'certification-unresolved': ('input', 'refuse'),
    'field-lock-signature': ('input', 'refuse'),
    'signature-malformed': ('input', 'refuse'),
    'approval-signature': ('input', 'derivative'),
    'document-timestamp': ('input', 'derivative'),
    'usage-rights': ('input', 'derivative'),
    'document-javascript': ('input', 'refuse'),
    'open-action': ('input', 'refuse'),
    'catalog-additional-actions': ('input', 'refuse'),
    'embedded-files': ('input', 'refuse'),
    'associated-files': ('input', 'refuse'),
    'optional-content': ('input', 'refuse'),
    'xfa-dynamic': ('input', 'refuse'),
    'xfa-hybrid': ('input', 'owner-item:W-198'),
    'tagged': ('input', 'owner-item:W-191'),
    'inspection-incomplete': ('input', 'refuse'),
    'field-action': ('input', 'refuse'),  # existing whole-form rule (W-190), kept unchanged
    'page-additional-actions': ('page', 'refuse'),
    'annotation-action': ('page', 'refuse'),
    'file-attachment-annotation': ('page', 'refuse'),
    'media-annotation': ('page', 'refuse'),
    'optional-content-reference': ('page', 'refuse'),
    'link-action': ('page', 'owner-item:W-192'),
}
MEDIA = {'/Sound', '/Movie', '/Screen', '/RichMedia', '/3D'}


def r(v):
    return v.get_object() if isinstance(v, IndirectObject) else v


def g(dic, key):
    return r(dic.get(key)) if isinstance(dic, DictionaryObject) else None


def dangling(dic, key):
    raw = dic.get(key) if isinstance(dic, DictionaryObject) else None
    if raw is None:
        return False
    try:
        return r(raw) is None or r(raw).__class__.__name__ == 'NullObject'
    except Exception:
        return True


def nonempty_tree(node):
    node = r(node)
    return isinstance(node, DictionaryObject) and (len(g(node, '/Names') or []) > 0 or len(g(node, '/Kids') or []) > 0)


def inspect_input(reader):
    found = set()
    cat = reader.trailer['/Root']
    perms = g(cat, '/Perms') or {}
    if '/DocMDP' in perms:
        found.add('certification-unresolved' if dangling(perms, '/DocMDP') else 'certification')
    if g(perms, '/UR3') is not None or g(perms, '/UR') is not None:
        found.add('usage-rights')
    names = g(cat, '/Names') or {}
    if nonempty_tree(names.get('/JavaScript')):
        found.add('document-javascript')
    if nonempty_tree(names.get('/EmbeddedFiles')):
        found.add('embedded-files')
    action = g(cat, '/OpenAction')
    if isinstance(action, DictionaryObject) and g(action, '/S') != '/GoTo':
        found.add('open-action')
    if len(g(cat, '/AA') or {}) > 0:
        found.add('catalog-additional-actions')
    if len(g(cat, '/AF') or []) > 0:
        found.add('associated-files')
    if len(g(cat, '/OCProperties') or {}) > 0:
        found.add('optional-content')
    if g(cat, '/StructTreeRoot') is not None or (g(cat, '/MarkInfo') or {}).get('/Marked') is True:
        found.add('tagged')
    form = g(cat, '/AcroForm')
    if form is not None:
        if g(form, '/XFA') is not None:
            fields = g(form, '/Fields') or []
            found.add('xfa-dynamic' if g(cat, '/NeedsRendering') is True or len(fields) == 0 else 'xfa-hybrid')
        budget = [0]
        try:
            for f in g(form, '/Fields') or []:
                walk_field(f, None, 0, budget, found)
        except OverflowError:
            found.add('inspection-incomplete')
    return found


def walk_field(ref, inherited, depth, budget, found):
    budget[0] += 1
    if depth > MAX_FIELD_DEPTH or budget[0] > MAX_FIELD_NODES:
        raise OverflowError
    field = r(ref)
    if not isinstance(field, DictionaryObject):
        found.add('inspection-incomplete')
        return
    ft = g(field, '/FT') or inherited
    if '/A' in field or '/AA' in field:
        found.add('field-action')
    if ft == '/Sig' and '/V' in field:
        value = g(field, '/V')
        if not isinstance(value, DictionaryObject):
            found.add('signature-malformed')
        elif g(value, '/Type') == '/DocTimeStamp' or g(value, '/SubFilter') == '/ETSI.RFC3161':
            found.add('document-timestamp')
        else:
            methods = {g(r(x), '/TransformMethod') for x in g(value, '/Reference') or []}
            if '/FieldMDP' in methods or g(field, '/Lock') is not None:
                found.add('field-lock-signature')
            if '/DocMDP' in methods:
                found.add('certification')
            found.add('approval-signature')
    for kid in g(field, '/Kids') or []:
        if '/Parent' in r(kid) and g(r(kid), '/Subtype') == '/Widget' and '/T' not in r(kid):
            continue
        walk_field(kid, ft, depth + 1, budget, found)


def inspect_pages(reader, numbers, has_ocproperties):
    found = set()
    for n in sorted(set(numbers)):
        page = reader.pages[n - 1]
        if len(g(page, '/AA') or {}) > 0:
            found.add('page-additional-actions')
        props = g(g(page, '/Resources') or {}, '/Properties') or {}
        if not has_ocproperties and any(g(r(v), '/Type') in ('/OCG', '/OCMD') for v in props.values()):
            found.add('optional-content-reference')
        for a in g(page, '/Annots') or []:
            a = r(a)
            if not isinstance(a, DictionaryObject):
                continue
            st = g(a, '/Subtype')
            if st == '/FileAttachment':
                found.add('file-attachment-annotation')
            elif st in MEDIA:
                found.add('media-annotation')
            elif st == '/Link':
                act = g(a, '/A')
                if (isinstance(act, DictionaryObject) and g(act, '/S') not in ('/GoTo', '/URI')) or '/AA' in a:
                    found.add('link-action')
            elif st == '/Widget':
                pass  # field actions are part of the whole-input field walk
            elif '/A' in a or '/AA' in a:
                found.add('annotation-action')
            if '/OC' in a and not has_ocproperties:
                found.add('optional-content-reference')
    return found


def classify(path, password, selections):
    reader = PdfReader(path)
    if reader.is_encrypted and not (password and reader.decrypt(password)):
        return {name: dict(error='cannot decrypt for classification') for name in selections}
    whole = inspect_input(reader)
    count = len(reader.pages)
    has_oc = len(g(reader.trailer['/Root'], '/OCProperties') or {}) > 0
    out = {}
    for name, numbers in selections.items():
        numbers = list(range(1, count + 1)) if numbers == 'all' else numbers
        cats = sorted(whole | inspect_pages(reader, numbers, has_oc))
        dispositions = {c: CATEGORIES[c][1] for c in cats}
        if any(v == 'refuse' for v in dispositions.values()):
            verdict = 'refuse'
        elif any(v == 'derivative' for v in dispositions.values()):
            verdict = 'refuse-unless-unsigned-derivative'
        else:
            verdict = 'admit'
        if verdict == 'admit' and any(v.startswith('owner-item') for v in dispositions.values()):
            verdict = 'admit-pending-' + '+'.join(sorted(v[11:] for v in dispositions.values() if v.startswith('owner-item')))
        out[name] = dict(categories=cats, verdict=verdict)
    return out


logging.getLogger('pypdf').setLevel(logging.ERROR)  # dangling references in malformed fixtures are deliberate
repo, inputs, outputs = map(Path, sys.argv[1:])
probe = json.loads((outputs / 'probe-results.json').read_text())
current = {(x['id'], x['op']): x for x in probe}
fixtures = []
for path in sorted(inputs.glob('*.pdf')):
    result = classify(path, None, {'extract1': [1], 'extract2': [2]})
    for op, row in result.items():
        now = current[(path.stem, op)]
        fixtures.append(dict(id=path.stem, op=op, current=now['status'], currentException=now['exception'],
                             omissions=now['omissions'], **row))

matrix = {}
with open(repo / 'agents/reference/simplification/W-189/matrix.csv', newline='', encoding='utf-8') as fh:
    for row in csv.DictReader(fh):
        matrix[(row['path'], row['operation'])] = row
corpus = []
with open(repo / 'agents/reference/simplification/W-188/fixture-manifest.csv', newline='', encoding='utf-8') as fh:
    for item in csv.DictReader(fh):
        rel = item['path']
        try:
            result = classify(repo / rel, PASSWORDS.get(rel), {'combine-page1': [1], 'combine-all': 'all'})
        except Exception as e:  # recorded, never hidden
            result = {op: dict(error=f'{type(e).__name__}: {e}') for op in ('combine-page1', 'combine-all')}
        for op, row in result.items():
            base = matrix[(rel, op)]
            corpus.append(dict(path=rel, op=op, current=base['current_result'], currentReason=base['current_reason'], **row))


def summarize(rows):
    s = {}
    for op in sorted({x['op'] for x in rows}):
        sel = [x for x in rows if x['op'] == op and 'verdict' in x]
        accepted = [x for x in sel if x['current'].startswith('accepted')]
        s[op] = dict(
            denominator=len([x for x in rows if x['op'] == op]),
            classified=len(sel),
            unclassified=len([x for x in rows if x['op'] == op and 'error' in x]),
            currentlyAccepted=len(accepted),
            verdictsAmongCurrentlyAccepted=dict(Counter(x['verdict'] for x in accepted)),
            newlyRefusedCategoryHits=dict(Counter(c for x in accepted for c in x['categories']
                                                   if CATEGORIES[c][1] in ('refuse', 'derivative'))),
            newlyRefusedFiles=sorted(Path(x.get('path', x.get('id', ''))).stem for x in accepted
                                     if x['verdict'].startswith('refuse')),
            recoverableByDerivative=sorted(Path(x.get('path', x.get('id', ''))).stem for x in accepted
                                           if x['verdict'] == 'refuse-unless-unsigned-derivative'),
        )
    return s


result = dict(rules=CATEGORIES, budgets=dict(fieldDepth=MAX_FIELD_DEPTH, fieldNodes=MAX_FIELD_NODES),
              fixtureSummary=summarize(fixtures), corpusSummary=summarize(corpus), fixtures=fixtures, corpus=corpus)
(outputs / 'candidate.json').write_text(json.dumps(result, indent=2) + '\n')
print(f'Classified {len(fixtures)} fixture and {len(corpus)} corpus operations')
