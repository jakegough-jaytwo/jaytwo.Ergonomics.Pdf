"""Generate W-156 fixtures, run the reference model, and check expectations.

Usage: python -I run.py <repo-root> <scratch-dir>
Fixtures are original, disposable and MIT-licensed; they are not corpus files.
"""
import json
import re
import subprocess
import sys
from pathlib import Path
from pypdf import PdfWriter
from pypdf.generic import (DictionaryObject as D, NameObject as N, ArrayObject as A, NumberObject as I,
                           TextStringObject as T, DecodedStreamObject, IndirectObject)

sys.path.insert(0, str(Path(__file__).parent))
import model  # noqa: E402


def d(**kw):
    return D({N('/' + k): v for k, v in kw.items()})


class Doc:
    def __init__(self, pages=1):
        self.w = PdfWriter()
        self.pages = [self.w.add_blank_page(200, 120) for _ in range(pages)]
        self.root = self.w._root_object

    def add(self, obj):
        return self.w._add_object(obj)

    def js(self, code='app.alert(1);'):
        s = DecodedStreamObject()
        s.set_data(code.encode())
        return self.add(d(S=N('/JavaScript'), JS=self.add(s)))

    def annot(self, page, **kw):
        a = self.add(d(Type=N('/Annot'), Rect=A([I(10), I(10), I(60), I(40)]), **kw))
        p = self.pages[page - 1]
        p.setdefault(N('/Annots'), A()).append(a)
        return a

    def save(self, path):
        self.w.write(path)


def fixtures():
    out = {}

    def make(name, fn, pages=1):
        doc = Doc(pages)
        fn(doc)
        out[name] = doc

    make('clean', lambda x: x.annot(1, Subtype=N('/Link'), A=d(S=N('/GoTo'), D=A([x.pages[0].indirect_reference, N('/Fit')]))))
    make('openaction-js', lambda x: x.root.__setitem__(N('/OpenAction'), x.js()))
    make('names-js', lambda x: x.root.__setitem__(N('/Names'), d(JavaScript=d(Names=A([T('s'), x.js()])))))

    def chain(x):  # OpenAction GoTo -> Next [GoTo -> Next Launch]  (nested, direct then indirect)
        launch = x.add(d(S=N('/Launch'), F=T('calc.exe')))
        inner = x.add(d(S=N('/GoTo'), D=A([x.pages[0].indirect_reference, N('/Fit')]), Next=launch))
        x.root[N('/OpenAction')] = d(S=N('/GoTo'), D=A([x.pages[0].indirect_reference, N('/Fit')]), Next=A([inner]))
    make('nested-next-launch', chain)

    def field_aa(x):  # field -> indirect /AA -> indirect action -> /Next -> JavaScript
        js = x.js()
        mid = x.add(d(S=N('/ResetForm'), Next=js))
        f = x.add(d(FT=N('/Tx'), T=T('Name'), AA=x.add(d(K=mid))))
        x.root[N('/AcroForm')] = x.add(d(Fields=A([f])))
    make('indirect-field-aa', field_aa)

    make('link-uri', lambda x: x.annot(1, Subtype=N('/Link'), A=d(S=N('/URI'), URI=T('https://example.invalid/'))))
    make('link-gotor', lambda x: x.annot(1, Subtype=N('/Link'), A=d(S=N('/GoToR'), F=T('other.pdf'), D=A([I(0), N('/Fit')]))))

    def submit(x):
        f = x.add(d(FT=N('/Btn'), Ff=I(65536), T=T('Go'), A=d(S=N('/SubmitForm'), F=d(FS=N('/URL'), F=T('https://example.invalid/post')))))
        x.root[N('/AcroForm')] = x.add(d(Fields=A([f])))
    make('submit-form', submit)

    def embedded(x):
        ef = DecodedStreamObject()
        ef.set_data(b'payload')
        spec = x.add(d(Type=N('/Filespec'), F=T('a.txt'), EF=d(F=x.add(ef))))
        x.root[N('/Names')] = d(EmbeddedFiles=d(Names=A([T('a.txt'), spec])))
    make('embedded-files', embedded)
    make('fileattachment-annot', lambda x: x.annot(1, Subtype=N('/FileAttachment'), Contents=T('x')))
    make('richmedia-annot', lambda x: x.annot(1, Subtype=N('/RichMedia')))
    make('unknown-action', lambda x: x.annot(1, Subtype=N('/Link'), A=d(S=N('/VendorExtension'))))
    make('xfa', lambda x: x.root.__setitem__(N('/AcroForm'), d(Fields=A(), XFA=A([T('template'), x.add(DecodedStreamObject())]))))
    make('page2-aa', lambda x: x.pages[1].__setitem__(N('/AA'), d(O=x.js())), pages=3)

    def cycle(x):  # Named -> Next -> back to itself; reported once, no gap
        a = x.add(d(S=N('/Named'), N=N('/NextPage')))
        a.get_object()[N('/Next')] = a
        x.root[N('/OpenAction')] = a
    make('next-cycle', cycle)

    def dangling(x):
        x.annot(1, Subtype=N('/Link'), A=IndirectObject(9999, 0, x.w))
    make('dangling-action', dangling)

    def wide(x):  # 400 annotations, each an indirect Link/GoTo: exercised with a deliberately small budget
        for _ in range(400):
            x.annot(1, Subtype=N('/Link'), A=d(S=N('/GoTo'), D=A([x.pages[0].indirect_reference, N('/Fit')])))
    make('wide-budget', wide)

    def deep(x):  # nested outlines deeper than the (reduced) depth limit
        parent = x.add(d(Type=N('/Outlines')))
        first = parent
        for i in range(30):
            item = x.add(d(Title=T('t%d' % i), Parent=first))
            first.get_object()[N('/First')] = item
            first = item
        first.get_object()[N('/A')] = x.js()
        x.root[N('/Outlines')] = parent
    make('deep-outline', deep)

    def pubsec(x):
        x.w._add_object(d())
    make('encrypted-user', lambda x: None)
    return out


def write_revisioned(base, path):
    """Prior revision holds an OpenAction JavaScript; the final revision removes it."""
    data = bytearray(base.read_bytes())
    m = re.search(rb'/Root (\d+) 0 R', data)
    root = int(m.group(1))
    sx = int(re.search(rb'startxref\s+(\d+)', data[::-1][:0] or data).group(1)) if False else None
    start = int(re.findall(rb'startxref\s+(\d+)', data)[-1])
    size = int(re.search(rb'/Size (\d+)', data).group(1))
    cat = re.search(rb'%d 0 obj\s*<<(.*?)>>\s*endobj' % root, data, re.S).group(1)
    cat = re.sub(rb'/OpenAction\s*\d+ 0 R', b'', cat)
    off = len(data)
    data += b'\n%d 0 obj\n<<%s>>\nendobj\n' % (root, cat)
    xref = len(data)
    data += b'xref\n%d 1\n%010d 00000 n \ntrailer\n<< /Size %d /Root %d 0 R /Prev %d >>\nstartxref\n%d\n%%%%EOF\n' % (
        root, off, size, root, start, xref)
    path.write_bytes(bytes(data))


EXPECT = {  # name -> (strict verdict, expected categories, expected gap kinds) at default budget
    'clean': ('WithinPolicy', [], []),
    'openaction-js': ('Rejected', ['JavaScript'], []),
    'names-js': ('Rejected', ['JavaScript'], []),
    'nested-next-launch': ('Rejected', ['Launch'], []),
    'indirect-field-aa': ('Rejected', ['FormAction', 'JavaScript'], []),
    'link-uri': ('Rejected', ['ExternalReference'], []),
    'link-gotor': ('Rejected', ['ExternalReference'], []),
    'submit-form': ('Rejected', ['FormAction'], []),
    'embedded-files': ('Rejected', ['EmbeddedFile'], []),
    'fileattachment-annot': ('Rejected', ['EmbeddedFile'], []),
    'richmedia-annot': ('Rejected', ['RichMedia'], []),
    'unknown-action': ('Rejected', ['UnknownAction'], []),
    'xfa': ('Rejected', ['Xfa'], []),
    'page2-aa': ('Rejected', ['JavaScript'], []),
    'next-cycle': ('Rejected', ['OtherAction'], []),
    'dangling-action': ('Indeterminate', [], ['Unresolvable']),
    'wide-budget': ('WithinPolicy', [], []),
    'deep-outline': ('Rejected', ['JavaScript'], []),
    'revisioned-removed-js': ('WithinPolicy', [], []),
}


def main():
    repo, scratch = Path(sys.argv[1]), Path(sys.argv[2])
    scratch.mkdir(parents=True, exist_ok=True)
    docs = fixtures()
    paths = {}
    for name, doc in docs.items():
        if name == 'encrypted-user':
            continue
        p = scratch / (name + '.pdf')
        doc.save(p)
        paths[name] = p
    p = scratch / 'revisioned-removed-js.pdf'
    base = Doc()
    base.root[N('/OpenAction')] = base.js()
    base.save(scratch / '_base.pdf')
    write_revisioned(scratch / '_base.pdf', p)
    paths['revisioned-removed-js'] = p
    # encrypted with a user password: cannot be opened with the empty password -> no report at all
    enc = Doc()
    enc.w.encrypt('secret')
    enc.save(scratch / 'password-required.pdf')
    # unsupported (public-key) handler would likewise be refused at open; pypdf cannot write one, so it is inferred.

    results, failures = {}, []
    strict = model.Policy()
    for name, p in paths.items():
        rep = model.inspect(p)
        verdict, reasons, excl = strict.evaluate(rep)
        cats = sorted({f.category for f in rep.findings})
        gaps = sorted({g.kind for g in rep.gaps})
        results[name] = dict(verdict=verdict, categories=cats, gaps=gaps, findings=len(rep.findings),
                             visits=rep.visits, revisions=rep.revisions, coverage=rep.coverage,
                             exclusions=excl, locations=[f.location for f in rep.findings][:6])
        exp = EXPECT[name]
        if (verdict, cats, gaps) != (exp[0], exp[1], exp[2]):
            failures.append(f'{name}: got {(verdict, cats, gaps)} expected {exp}')

    # Budget cases: same documents, deliberately small budgets.
    small = model.Budget(max_visits=50)
    rep = model.inspect(paths['wide-budget'], small)
    v, r, _ = strict.evaluate(rep)
    results['wide-budget@visits=50'] = dict(verdict=v, gaps=sorted({g.kind for g in rep.gaps}), coverage=rep.coverage)
    if (v, results['wide-budget@visits=50']['gaps']) != ('Indeterminate', ['BudgetExhausted']):
        failures.append(f'wide-budget small: {v} {r}')
    # Same incomplete report cannot be admitted by a policy that allows only some categories either.
    v2, _, _ = model.Policy(allow=frozenset({'JavaScript'})).evaluate(rep)
    if v2 != 'Indeterminate':
        failures.append('partial-allow admitted an incomplete report')
    # Incomplete but a policy that allows every category cannot be affected by the gap.
    v3, _, ex = model.Policy(allow=frozenset(model.CATEGORIES)).evaluate(rep)
    results['wide-budget@visits=50,allow-all'] = dict(verdict=v3, exclusions=ex)
    if v3 != 'WithinPolicy':
        failures.append('allow-all should be unaffected by gaps')
    rep = model.inspect(paths['deep-outline'], model.Budget(max_depth=10))
    v, _, _ = strict.evaluate(rep)
    results['deep-outline@depth=10'] = dict(verdict=v, gaps=sorted({g.kind for g in rep.gaps}), categories=sorted({f.category for f in rep.findings}))
    if v != 'Indeterminate' or 'DepthExceeded' not in results['deep-outline@depth=10']['gaps']:
        failures.append(f'deep-outline depth: {results["deep-outline@depth=10"]}')
    rep = model.inspect(paths['openaction-js'], model.Budget(max_findings=0))
    v, _, _ = strict.evaluate(rep)
    results['openaction-js@max_findings=0'] = dict(verdict=v, suppressed=rep.suppressed_findings)
    if v != 'Indeterminate':
        failures.append('suppressed findings must not admit (and cannot hide a rejection)')
    # Policy allowing the category of a present finding accepts, with exclusions listed.
    v, _, ex = model.Policy(allow=frozenset({'ExternalReference'})).evaluate(model.inspect(paths['link-uri']))
    results['link-uri@allow=ExternalReference'] = dict(verdict=v, exclusions=ex)
    if v != 'WithinPolicy' or 'PriorRevisions' not in ex and 'ContentStreams' not in ex:
        failures.append('allow policy should admit with explicit exclusions')
    # Parse failure is not a verdict.
    try:
        model.inspect(scratch / 'password-required.pdf')
        failures.append('password-required opened')
        results['password-required'] = dict(opened=True)
    except model.NotOpened as e:
        results['password-required'] = dict(opened=False, reason=str(e))
    # The prior revision's JavaScript is not seen: coverage says so.
    if results['revisioned-removed-js']['coverage']['PriorRevisions'] != 'NotInspected' or results['revisioned-removed-js']['revisions'] != 2:
        failures.append('prior revision coverage not reported')

    # Independent parser sanity: every fixture is a valid PDF for qpdf.
    qpdf = None
    for line in (repo / '.tools' / 'test.runsettings').read_text().splitlines():
        m = re.search(r'<JAYTWO_QPDF>(.*?)</JAYTWO_QPDF>', line)
        if m:
            qpdf = m.group(1)
    qpdf_status = {}
    if qpdf:
        for name, p in paths.items():
            rc = subprocess.run([qpdf, '--check', str(p)], capture_output=True).returncode
            qpdf_status[name] = rc
    results['_qpdf_check_exit_codes'] = qpdf_status
    (Path(__file__).parent / 'results.json').write_text(json.dumps(results, indent=1, sort_keys=True))
    print(f'{len(paths)} fixtures, {len(results)} result rows, qpdf exit codes {sorted(set(qpdf_status.values()))}')
    if failures:
        print('FAILURES')
        print('\n'.join(failures))
        sys.exit(1)
    print('all expectations met')


if __name__ == '__main__':
    main()
