"""Disposable reference model of the two outline projection rules (no product implementation).

For every case it reads each source outline with pypdf (iterative traversal, strict classification),
applies rule A (retain necessary ancestor headings) and rule B (remove the item, promote survivors),
and writes the resulting outline onto the engine's real Concatenate output so independent readers can inspect it.
"""
import json
import sys
from pathlib import Path
from pypdf import PdfReader, PdfWriter
from pypdf.generic import (DictionaryObject as D, ArrayObject as A, NameObject as N, NumberObject as I,
                           FloatObject as F, TextStringObject as T, NullObject, IndirectObject, ByteStringObject)

MAX_DEPTH = 256        # PdfObjectStore.MaxTraversalDepth (read from source; model uses the same value)
MAX_ITEMS = 1_000_000  # PdfNewFileLimits.DefaultMappedObjects
MODES = {'XYZ': 3, 'Fit': 0, 'FitH': 1, 'FitV': 1, 'FitR': 4, 'FitB': 0, 'FitBH': 1, 'FitBV': 1}


class Refusal(Exception):
    def __init__(self, kind, reason):
        super().__init__(f'{kind}:{reason}')
        self.kind, self.reason = kind, reason


def is_num(v):
    return isinstance(v, (I, F))


def name_tree_lookup(node, key, depth=0, budget=None):
    budget = budget if budget is not None else [10000]
    budget[0] -= 1
    if depth > 32 or budget[0] < 0:
        raise Refusal('limit', 'name-tree')
    node = node.get_object()
    if not isinstance(node, D):
        raise Refusal('malformed', 'name-tree')
    if '/Names' in node:
        names = node['/Names']
        for i in range(0, len(names) - 1, 2):
            if str(names[i].get_object()) == key:
                return names[i + 1].get_object()
    for kid in node.get('/Kids', []):
        v = name_tree_lookup(kid, key, depth + 1, budget)
        if v is not None:
            return v
    return None


def resolve_dest(value, catalog, page_index):
    """Return (source page index 1-based, mode, params) or raise Refusal."""
    value = value.get_object() if hasattr(value, 'get_object') else value
    if isinstance(value, (N, T, ByteStringObject)):
        key = str(value)
        found = None
        if isinstance(value, N):
            legacy = catalog.get('/Dests')
            if legacy is not None and isinstance(legacy.get_object(), D):
                found = legacy.get_object().get(str(value))
                found = found.get_object() if found is not None else None
        if found is None and '/Names' in catalog and '/Dests' in catalog['/Names'].get_object():
            found = name_tree_lookup(catalog['/Names'].get_object()['/Dests'], key.lstrip('/') if isinstance(value, N) else key)
        if found is None:
            raise Refusal('malformed', 'name-missing')
        if isinstance(found, D):
            found = found.get('/D')
            found = found.get_object() if found is not None else None
        value = found
    if not isinstance(value, A) or len(value) < 2:
        raise Refusal('malformed', 'dest-shape')
    page = value[0]
    if not isinstance(page, IndirectObject) or page.idnum not in page_index:
        raise Refusal('malformed', 'dest-not-page')
    mode = value[1].get_object()
    if not isinstance(mode, N) or mode[1:] not in MODES:
        raise Refusal('malformed', 'dest-mode')
    params = [x.get_object() for x in value[2:]]
    if len(params) != MODES[mode[1:]]:
        raise Refusal('malformed', 'dest-params')
    for p in params:
        if not (is_num(p) or (isinstance(p, NullObject) and mode[1:] != 'FitR')):
            raise Refusal('malformed', 'dest-param-type')
    return page_index[page.idnum], mode[1:], [None if isinstance(p, NullObject) else float(p) for p in params]


def read_outline(reader):
    """Strict iterative traversal over /First and /Next; /Prev, /Last and /Count are derived and ignored."""
    catalog = reader.trailer['/Root'].get_object()
    page_index = {p.indirect_reference.idnum: i + 1 for i, p in enumerate(reader.pages)}
    if '/Outlines' not in catalog:
        return []
    root_ref = catalog.raw_get('/Outlines')
    root = root_ref.get_object()
    if not isinstance(root, D):
        raise Refusal('malformed', 'outlines-not-dictionary')
    if '/First' not in root:
        return []
    seen, top, count = set(), [], 0
    stack = [(root.raw_get('/First'), top, root_ref, 1)]
    while stack:
        cur, siblings, parent, depth = stack.pop()
        if depth > MAX_DEPTH:
            raise Refusal('limit', 'depth')
        while cur is not None:
            if not isinstance(cur, IndirectObject):
                raise Refusal('malformed', 'item-not-indirect')
            obj = cur.get_object()
            if not isinstance(obj, D):
                raise Refusal('malformed', 'item-not-dictionary')
            if cur.idnum in seen:
                raise Refusal('malformed', 'cycle-or-shared-item')
            seen.add(cur.idnum)
            count += 1
            if count > MAX_ITEMS:
                raise Refusal('limit', 'items')
            if not isinstance(obj.raw_get('/Parent') if '/Parent' in obj else None, IndirectObject) or obj.raw_get('/Parent').idnum != parent.idnum:
                raise Refusal('malformed', 'parent-mismatch')
            title = obj.get('/Title')
            if not isinstance(title, (T, ByteStringObject)):
                raise Refusal('malformed', 'title')
            node = dict(title=str(title), page=None, mode=None, params=None, open=True, children=[], c=None, f=None, heading=True)
            has_dest, has_a = '/Dest' in obj, '/A' in obj
            if has_dest and has_a:
                raise Refusal('malformed', 'dest-and-action')
            if has_dest:
                node['page'], node['mode'], node['params'] = resolve_dest(obj['/Dest'], catalog, page_index)
                node['heading'] = False
            elif has_a:
                act = obj['/A']
                if not isinstance(act, D) or not isinstance(act.get('/S'), N):
                    raise Refusal('malformed', 'action-shape')
                if act['/S'] != '/GoTo' or '/Next' in act:
                    raise Refusal('unsupported', 'action:' + ('Next' if act['/S'] == '/GoTo' else str(act['/S'])[1:]))
                if '/D' not in act:
                    raise Refusal('malformed', 'action-no-dest')
                node['page'], node['mode'], node['params'] = resolve_dest(act['/D'], catalog, page_index)
                node['heading'] = False
            if '/C' in obj:
                c = obj['/C']
                if not isinstance(c, A) or len(c) != 3 or not all(is_num(x.get_object()) and 0 <= float(x) <= 1 for x in c):
                    raise Refusal('malformed', 'color')
                node['c'] = [float(x) for x in c]
            if '/F' in obj:
                f = obj['/F']
                if not isinstance(f, I) or not 0 <= int(f) <= 3:
                    raise Refusal('malformed', 'flags')
                node['f'] = int(f)
            if '/First' in obj:
                cnt = obj.get('/Count', 0)
                node['open'] = not (is_num(cnt) and int(cnt) < 0)
                stack.append((obj.raw_get('/First'), node['children'], cur, depth + 1))
            siblings.append(node)
            cur = obj.raw_get('/Next') if '/Next' in obj else None
    return top


def project(nodes, live, rule):
    """live(node) -> output page index or None. Returns new node list; node ids stay in source order."""
    out = []
    for n in nodes:
        kids = project(n['children'], live, rule)
        target = live(n) if n['page'] is not None else None
        dead = n['page'] is not None and target is None
        if not dead:
            if target is None and not kids:
                continue  # heading that has no surviving child
            out.append(dict(n, out_page=target, children=kids, dead_ancestor=False))
        elif rule == 'A':
            if kids:
                out.append(dict(n, out_page=None, children=kids, dead_ancestor=True))
        else:  # rule B: promote survivors in place
            out.extend(kids)
    return out


def render(nodes, depth=0, key='out_page'):
    lines = []
    for n in nodes:
        mark = ('+' if n['open'] else '-') if n['children'] else ''
        p = n.get(key)
        lines.append('  ' * depth + n['title'] + ('@%d' % p if p else '') + mark)
        lines.extend(render(n['children'], depth + 1, key))
    return lines


def render_source(nodes, depth=0):
    lines = []
    for n in nodes:
        mark = ('+' if n['open'] else '-') if n['children'] else ''
        lines.append('  ' * depth + n['title'] + (f"@s{n['page']}" if n['page'] else '') + mark)
        lines.extend(render_source(n['children'], depth + 1))
    return lines


def visible(nodes):
    return sum(1 + (visible(n['children']) if n['open'] else 0) for n in nodes)


def write_items(w, pages, nodes, parent):
    refs = []
    for n in nodes:
        d = D({N('/Title'): T(n['title']), N('/Parent'): parent})
        if n['out_page']:
            d[N('/Dest')] = A([pages[n['out_page'] - 1].indirect_reference, N('/' + n['mode'])] +
                               [NullObject() if x is None else F(x) for x in n['params']])
        if n['c'] is not None:
            d[N('/C')] = A([F(x) for x in n['c']])
        if n['f'] is not None:
            d[N('/F')] = I(n['f'])
        refs.append((w._add_object(d), n))
    for i, (r, n) in enumerate(refs):
        o = r.get_object()
        if i:
            o[N('/Prev')] = refs[i - 1][0]
        if i + 1 < len(refs):
            o[N('/Next')] = refs[i + 1][0]
        if n['children']:
            first, last = write_items(w, pages, n['children'], r)
            o[N('/First')], o[N('/Last')] = first, last
            v = visible(n['children'])
            o[N('/Count')] = I(v if n['open'] else -v)
    return refs[0][0], refs[-1][0]


def main():
    scratch, record = Path(sys.argv[1]), Path(sys.argv[2])
    sys.setrecursionlimit(5000)
    cases = json.loads((scratch / 'inputs' / 'cases.json').read_text())
    engine = {r['name']: r for r in json.loads((record / 'results.json').read_text(encoding='utf-8-sig'))}
    (scratch / 'projected').mkdir(exist_ok=True)
    rows = []
    for case in cases:
        name = case['name']
        row = dict(name=name, inputs=case['inputs'], engine_error=engine[name]['error'], engine_omissions=engine[name]['omissions'])
        try:
            sources, offsets, total = [], [], 0
            for occ in case['inputs']:
                reader = PdfReader(scratch / 'inputs' / (occ['file'] + '.pdf'))
                sources.append(read_outline(reader))
                first_copy = {}
                for k, src in enumerate(occ['pages']):
                    first_copy.setdefault(src, total + k + 1)
                offsets.append(first_copy)
                total += len(occ['pages'])
            row['before'] = [render_source(s) for s in sources]
            row['source_items'] = [sum(1 for _ in iter_nodes(s)) for s in sources]
            row['outcome'] = 'accepted'
            for rule in ('A', 'B'):
                merged = []
                for occ_nodes, first_copy in zip(sources, offsets):
                    merged.extend(project(occ_nodes, lambda n, fc=first_copy: fc.get(n['page']), rule))
                row['after' + rule] = render(merged)
                row['items' + rule] = sum(1 for _ in iter_nodes(merged))
                row['dead_ancestors' + rule] = sum(1 for n in iter_nodes(merged) if n.get('dead_ancestor'))
                if merged:
                    w = PdfWriter(clone_from=str(scratch / 'outputs' / (name + '.pdf')))
                    pages = list(w.pages)
                    root = D({N('/Type'): N('/Outlines')})
                    rref = w._add_object(root)
                    first, last = write_items(w, pages, merged, rref)
                    root[N('/First')], root[N('/Last')] = first, last
                    root[N('/Count')] = I(visible(merged))
                    w._root_object[N('/Outlines')] = rref
                    with open(scratch / 'projected' / f'{name}-{rule}.pdf', 'wb') as fh:
                        w.write(fh)
        except Refusal as e:
            row['outcome'] = 'refused'
            row['refusal'] = dict(kind=e.kind, reason=e.reason)
        rows.append(row)
    (record / 'projection.json').write_text(json.dumps(rows, indent=2) + '\n', encoding='utf-8')


def iter_nodes(nodes):
    stack = list(nodes)
    while stack:
        n = stack.pop()
        yield n
        stack.extend(n['children'])


if __name__ == "__main__":
    main()
