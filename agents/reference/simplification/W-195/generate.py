"""Original MIT-licensed, bounded synthetic outline research fixtures and case matrix."""
import hashlib
import json
import sys
from pathlib import Path
from pypdf import PdfWriter
from pypdf.generic import (DictionaryObject as D, NameObject as N, ArrayObject as A, NumberObject as I,
                           FloatObject as F, TextStringObject as T, IndirectObject, NullObject)


def vis(children):
    """Visible descendants if the owner were open (ISO 32000-1 12.3.3 /Count)."""
    return sum(1 + (vis(c['children']) if c['open'] else 0) for c in children)


def item(title, target=None, children=(), open=True, **extra):
    return dict(title=title, target=target, children=list(children), open=open, extra=extra)


def std_dest(w, pages, s, d):
    t = s['target']
    if t is None:
        return
    ref = lambda n: pages[n - 1].indirect_reference
    if isinstance(t, int):
        d[N('/Dest')] = A([ref(t), N('/Fit')])
    elif t[0] == 'xyz':
        d[N('/Dest')] = A([ref(t[1]), N('/XYZ'), I(10), I(20), F(1.5)])
    elif t[0] == 'mode':
        d[N('/Dest')] = A([ref(t[1]), N('/' + t[2])] + [I(x) if x is not None else NullObject() for x in t[3:]])
    elif t[0] == 'name':
        d[N('/Dest')] = N('/' + t[1]) if t[2] == 'nm' else T(t[1])
    elif t[0] == 'goto':
        d[N('/A')] = D({N('/S'): N('/GoTo'), N('/D'): A([ref(t[1]), N('/Fit')])})
    elif t[0] == 'gotoname':
        d[N('/A')] = D({N('/S'): N('/GoTo'), N('/D'): T(t[1])})


def make_items(w, pages, items, parent):
    """Create sibling items under parent; returns (first, last). Derived keys are computed from the spec."""
    refs = []
    for s in items:
        d = D()
        d[N('/Title')] = T(s['title'])
        d[N('/Parent')] = parent
        std_dest(w, pages, s, d)
        for k, v in s['extra'].items():
            d[N(k)] = v
        refs.append((w._add_object(d), s))
    for i, (r, s) in enumerate(refs):
        o = r.get_object()
        if i:
            o[N('/Prev')] = refs[i - 1][0]
        if i + 1 < len(refs):
            o[N('/Next')] = refs[i + 1][0]
        if s['children']:
            first, last = make_items(w, pages, s['children'], r)
            o[N('/First')], o[N('/Last')] = first, last
            v = vis(s['children'])
            o[N('/Count')] = I(v if s['open'] else -v)
    return refs[0][0], refs[-1][0]


def doc(specs, names=None, legacy=None, post=None):
    w = PdfWriter()
    pages = [w.add_blank_page(100, 100) for _ in range(4)]
    root = D({N('/Type'): N('/Outlines')})
    rref = w._add_object(root)
    if specs:
        first, last = make_items(w, pages, specs, rref)
        root[N('/First')], root[N('/Last')] = first, last
        root[N('/Count')] = I(vis(specs))
    w._root_object[N('/Outlines')] = rref
    if names:
        w._root_object[N('/Names')] = D({N('/Dests'): D({N('/Names'): A(
            [x for n, p in names for x in (T(n), A([pages[p - 1].indirect_reference, N('/Fit')]))])})})
    if legacy:
        w._root_object[N('/Dests')] = D({N('/' + n): A([pages[p - 1].indirect_reference, N('/Fit')]) for n, p in legacy})
    if post:
        post(w, pages, root)
    return w


def ch():
    return [item('Ch', 1, [item('Sub', 2)]), item('Other', 3)]


fixtures = {}
fixtures['book'] = lambda: doc([
    item('Ch1', 1, [item('1.1', 1), item('1.2', 2)]),
    item('Ch2', 3, [item('2.1', 3), item('2.2', 4)], open=False),
    item('Ch3', 4)])
fixtures['headings'] = lambda: doc([
    item('Part I', None, [item('A', 1), item('B', 2)]),
    item('Part II', None, [item('C', 3)]),
    item('Part III', None),
    item('Appendix', 4)])
fixtures['dupes'] = lambda: doc([
    item('Summary', 1), item('Summary', 2, [item('Notes', 1), item('Notes', 2)]), item('Summary', 3)])
fixtures['named'] = lambda: doc([
    item('N1', ('name', 'n1', 'str')),
    item('N2', ('gotoname', 'n2'), [item('N3', ('name', 'n3', 'nm'))]),
    item('Direct', ('goto', 4))], names=[('n1', 1), ('n2', 2)], legacy=[('n3', 3)])
fixtures['modes'] = lambda: doc([
    item('xyz-null', ('mode', 1, 'XYZ', None, None, None)), item('xyz', ('xyz', 2)),
    item('fit', 3), item('fith', ('mode', 4, 'FitH', 50)), item('fitv', ('mode', 1, 'FitV', 20)),
    item('fitr', ('mode', 2, 'FitR', 0, 0, 50, 50)), item('fitb', ('mode', 3, 'FitB')),
    item('fitbh', ('mode', 4, 'FitBH', None)), item('fitbv', ('mode', 1, 'FitBV', 5))])
fixtures['styled'] = lambda: doc([
    item('Colored', 1, **{'/C': A([F(1), F(0), F(0)]), '/F': I(3)}),
    item('Plain', 2)])


def mal(label, mutate):
    fixtures[label] = lambda: doc(ch(), post=mutate)


def objs(root):
    c = root['/First'].get_object()
    return c, c['/First'].get_object(), c['/Next'].get_object()


def m_cycle_next(w, p, r):
    objs(r)[2][N('/Next')] = r.raw_get('/First')


def m_cycle_first(w, p, r):
    s = objs(r)[1]
    s[N('/First')] = r.raw_get('/First')
    s[N('/Last')] = r.raw_get('/First')


def m_shared(w, p, r):
    c, s, o = objs(r)
    s[N('/Next')] = c.raw_get('/Next')


def m_parent(w, p, r):
    c, s, o = objs(r)
    s[N('/Parent')] = c.raw_get('/Next')


def m_dangling(w, p, r):
    objs(r)[0][N('/First')] = IndirectObject(9999, 0, w)


def m_notitle(w, p, r):
    del objs(r)[1][N('/Title')]


def m_dest_dangling(w, p, r):
    objs(r)[1][N('/Dest')] = A([IndirectObject(9999, 0, w), N('/Fit')])


def m_dest_nonpage(w, p, r):
    objs(r)[1][N('/Dest')] = A([w._root_object.indirect_reference, N('/Fit')])


def m_dest_int(w, p, r):
    objs(r)[1][N('/Dest')] = A([I(0), N('/Fit')])


def m_name_missing(w, p, r):
    objs(r)[1][N('/Dest')] = T('nosuch')


def m_both(w, p, r):
    objs(r)[1][N('/A')] = D({N('/S'): N('/GoTo'), N('/D'): A([p[0].indirect_reference, N('/Fit')])})


def m_mode_bad(w, p, r):
    objs(r)[1][N('/Dest')] = A([p[1].indirect_reference, N('/Foo')])


def m_mode_short(w, p, r):
    objs(r)[1][N('/Dest')] = A([p[1].indirect_reference, N('/FitR'), I(1), I(2)])


def m_countlie(w, p, r):
    objs(r)[0][N('/Count')] = I(99)
    r[N('/Count')] = I(-7)


def m_se(w, p, r):
    objs(r)[1][N('/SE')] = w._add_object(D({N('/Type'): N('/StructElem'), N('/S'): N('/P')}))


def m_act(action):
    def m(w, p, r):
        s = objs(r)[1]
        if '/Dest' in s:
            del s[N('/Dest')]
        s[N('/A')] = action(w, p)
    return m


for k, v in {'cycle-next': m_cycle_next, 'cycle-first': m_cycle_first, 'shared-item': m_shared,
             'parent-mismatch': m_parent, 'dangling-first': m_dangling, 'no-title': m_notitle,
             'dest-dangling': m_dest_dangling, 'dest-nonpage': m_dest_nonpage, 'dest-intpage': m_dest_int,
             'name-missing': m_name_missing, 'both-dest-action': m_both, 'mode-bad': m_mode_bad,
             'mode-short': m_mode_short, 'count-lie': m_countlie, 'se-present': m_se}.items():
    mal(k, v)
acts = {
    'act-uri': lambda w, p: D({N('/S'): N('/URI'), N('/URI'): T('https://example.com/x')}),
    'act-gotor': lambda w, p: D({N('/S'): N('/GoToR'), N('/F'): T('other.pdf'), N('/D'): A([I(0), N('/Fit')])}),
    'act-launch': lambda w, p: D({N('/S'): N('/Launch'), N('/F'): T('tool.exe')}),
    'act-named': lambda w, p: D({N('/S'): N('/Named'), N('/N'): N('/NextPage')}),
    'act-js': lambda w, p: D({N('/S'): N('/JavaScript'), N('/JS'): T('app.alert(1)')}),
    'act-next': lambda w, p: D({N('/S'): N('/GoTo'), N('/D'): A([p[1].indirect_reference, N('/Fit')]),
                                N('/Next'): D({N('/S'): N('/Named'), N('/N'): N('/NextPage')})}),
}
for k, a in acts.items():
    mal(k, m_act(a))


def deep():
    w = PdfWriter()
    pages = [w.add_blank_page(100, 100) for _ in range(2)]
    rref = w._add_object(D({N('/Type'): N('/Outlines')}))
    parent = rref
    for i in range(300):
        r = w._add_object(D({N('/Title'): T(f'L{i}'), N('/Parent'): parent,
                             N('/Dest'): A([pages[0].indirect_reference, N('/Fit')])}))
        parent.get_object()[N('/First')] = r
        parent.get_object()[N('/Last')] = r
        if parent is not rref:
            parent.get_object()[N('/Count')] = I(1)
        parent = r
    rref.get_object()[N('/Count')] = I(300)
    w._root_object[N('/Outlines')] = rref
    return w


def empty():
    w = PdfWriter()
    [w.add_blank_page(100, 100) for _ in range(2)]
    w._root_object[N('/Outlines')] = w._add_object(D({N('/Type'): N('/Outlines')}))
    return w


def none():
    w = PdfWriter()
    [w.add_blank_page(100, 100) for _ in range(2)]
    return w


fixtures['deep-chain'] = deep
fixtures['empty-root'] = empty
fixtures['no-outlines'] = none


def case(name, *inputs, a=None, b=None, refuse=None):
    return dict(name=name, inputs=[dict(file=f, pages=p) for f, p in inputs], oracleA=a, oracleB=b, refuse=refuse)


def S(*lines):
    return '\n'.join(lines)


book_all = S('Ch1@1+', '  1.1@1', '  1.2@2', 'Ch2@3-', '  2.1@3', '  2.2@4', 'Ch3@4')
book_rev = S('Ch1@4+', '  1.1@4', '  1.2@3', 'Ch2@2-', '  2.1@2', '  2.2@1', 'Ch3@1')
book_twice = S('Ch2@1-', '  2.1@1', '  2.2@2', 'Ch3@2', 'Ch2@3-', '  2.1@3', '  2.2@4', 'Ch3@4')
head_all = S('Part I+', '  A@1', '  B@2', 'Part II+', '  C@3', 'Appendix@4')
dup_twice = S('Summary@1', 'Summary@2+', '  Notes@1', '  Notes@2', 'Summary@3', 'Summary@4+', '  Notes@3', '  Notes@4')
dup_21 = S('Summary@2', 'Summary@1+', '  Notes@2', '  Notes@1')
cases = [
    case('book-all', ('book', [1, 2, 3, 4]), a=book_all, b=book_all),
    case('book-12', ('book', [1, 2]), a=S('Ch1@1+', '  1.1@1', '  1.2@2'), b=S('Ch1@1+', '  1.1@1', '  1.2@2')),
    case('book-2', ('book', [2]), a=S('Ch1+', '  1.2@1'), b=S('1.2@1')),
    case('book-23', ('book', [2, 3]), a=S('Ch1+', '  1.2@1', 'Ch2@2-', '  2.1@2'), b=S('1.2@1', 'Ch2@2-', '  2.1@2')),
    case('book-4', ('book', [4]), a=S('Ch2-', '  2.2@1', 'Ch3@1'), b=S('2.2@1', 'Ch3@1')),
    case('book-31', ('book', [3, 1]), a=S('Ch1@2+', '  1.1@2', 'Ch2@1-', '  2.1@1'), b=S('Ch1@2+', '  1.1@2', 'Ch2@1-', '  2.1@1')),
    case('book-reverse', ('book', [4, 3, 2, 1]), a=book_rev, b=book_rev),
    case('book-repeat', ('book', [1, 1, 2]), a=S('Ch1@1+', '  1.1@1', '  1.2@3'), b=S('Ch1@1+', '  1.1@1', '  1.2@3')),
    case('book-two-occurrences', ('book', [1]), ('book', [2]),
         a=S('Ch1@1+', '  1.1@1', 'Ch1+', '  1.2@2'), b=S('Ch1@1+', '  1.1@1', '1.2@2')),
    case('book-two-same-order', ('book', [3, 4]), ('book', [3, 4]), a=book_twice, b=book_twice),
    case('headings-23', ('headings', [2, 3]), a=S('Part I+', '  B@1', 'Part II+', '  C@2'), b=S('Part I+', '  B@1', 'Part II+', '  C@2')),
    case('headings-1', ('headings', [1]), a=S('Part I+', '  A@1'), b=S('Part I+', '  A@1')),
    case('headings-all', ('headings', [1, 2, 3, 4]), a=head_all, b=head_all),
    case('dupes-1', ('dupes', [1]), a=S('Summary@1', 'Summary+', '  Notes@1'), b=S('Summary@1', 'Notes@1')),
    case('dupes-21', ('dupes', [2, 1]), a=dup_21, b=dup_21),
    case('dupes-2', ('dupes', [2]), a=S('Summary@1+', '  Notes@1'), b=S('Summary@1+', '  Notes@1')),
    case('dupes-twice', ('dupes', [1, 2]), ('dupes', [1, 2]), a=dup_twice, b=dup_twice),
    case('named-all', ('named', [1, 2, 3, 4]), a=S('N1@1', 'N2@2+', '  N3@3', 'Direct@4'), b=S('N1@1', 'N2@2+', '  N3@3', 'Direct@4')),
    case('named-3', ('named', [3]), a=S('N2+', '  N3@1'), b=S('N3@1')),
    case('modes-all', ('modes', [1, 2, 3, 4])),
    case('modes-34', ('modes', [3, 4])),
    case('styled-2', ('styled', [2])),
    case('styled-all', ('styled', [1, 2])),
    case('mixed-inputs', ('book', [2]), ('no-outlines', [1]), ('empty-root', [2]), ('headings', [3]),
         a=S('Ch1+', '  1.2@1', 'Part II+', '  C@4'), b=S('1.2@1', 'Part II+', '  C@4')),
    case('none-only', ('no-outlines', [1, 2]), a='', b=''),
    case('deep', ('deep-chain', [1])),
]
for k in ['cycle-next', 'cycle-first', 'shared-item', 'parent-mismatch', 'dangling-first', 'no-title', 'dest-dangling',
          'dest-nonpage', 'dest-intpage', 'name-missing', 'both-dest-action', 'mode-bad', 'mode-short',
          'act-uri', 'act-gotor', 'act-launch', 'act-named', 'act-js', 'act-next']:
    cases.append(case('bad-' + k, (k, [1, 2, 3]), refuse=True))
cases.append(case('count-lie', ('count-lie', [1, 2, 3])))
cases.append(case('se-present', ('se-present', [1, 2, 3])))
# A malformed item refuses even when its sibling pages are the only ones selected: the defect is not a target choice.
cases.append(case('bad-dest-dangling-sub-unselected', ('dest-dangling', [1, 3]), refuse=True))

out = Path(sys.argv[1])
out.mkdir(parents=True, exist_ok=True)
manifest = []
for name, make in fixtures.items():
    w = make()
    path = out / (name + '.pdf')
    with open(path, 'wb') as fh:
        w.write(fh)
    data = path.read_bytes()
    manifest.append(dict(name=name, file=name + '.pdf', bytes=len(data), sha256=hashlib.sha256(data).hexdigest(),
                         license='MIT, original synthetic, generated by generate.py with pypdf'))
Path(sys.argv[2]).write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8')
(out / 'cases.json').write_text(json.dumps(cases, indent=2), encoding='utf-8')
Path(sys.argv[3]).write_text(json.dumps(cases, indent=2) + '\n', encoding='utf-8')
