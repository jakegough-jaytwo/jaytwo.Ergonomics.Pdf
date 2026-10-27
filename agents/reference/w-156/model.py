"""W-156 reference model: a disposable executable form of the proposed admission-inspection contract.

This is NOT the engine and not product code. It reads PDFs with pypdf (an independent parser) and implements
the traversal, budget, gap, coverage and policy rules of proposal.md, so the contract can be exercised against
representative documents before anything is built. Names mirror the proposed C# API but are illustrative.
"""
from dataclasses import dataclass, field
from pypdf import PdfReader
from pypdf.generic import IndirectObject, DictionaryObject, ArrayObject, StreamObject

# Categories reported as findings. Internal navigation without /Next is counted, not reported.
CATEGORIES = ['JavaScript', 'Launch', 'ExternalReference', 'FormAction', 'EmbeddedFile', 'RichMedia',
              'Xfa', 'OtherAction', 'UnknownAction']
ACTION_CATEGORY = {
    'JavaScript': 'JavaScript', 'Launch': 'Launch',
    'URI': 'ExternalReference', 'GoToR': 'ExternalReference', 'GoToE': 'ExternalReference',
    'SubmitForm': 'FormAction', 'ResetForm': 'FormAction', 'ImportData': 'FormAction',
    'Named': 'OtherAction', 'Hide': 'OtherAction', 'SetOCGState': 'OtherAction', 'Rendition': 'OtherAction',
    'Trans': 'OtherAction', 'GoTo3DView': 'OtherAction', 'Movie': 'RichMedia', 'Sound': 'RichMedia', 'RichMediaExecute': 'RichMedia',
    'Thread': 'OtherAction', 'GoToDp': 'OtherAction',
}
INERT = {'GoTo'}
RICH_ANNOTS = {'Sound', 'Movie', 'Screen', 'RichMedia', '3D'}
SCOPES = ['Catalog', 'NameTrees', 'Outlines', 'Pages', 'AcroForm', 'Resources', 'ContentStreams',
          'StructureTree', 'PriorRevisions', 'UnreachableObjects']
NEVER_INSPECTED = {'Resources', 'ContentStreams', 'StructureTree', 'PriorRevisions', 'UnreachableObjects'}


@dataclass
class Budget:
    max_visits: int = 100_000      # structure visits (indirect resolutions + direct containers entered)
    max_depth: int = 256           # nesting depth of trees and chains
    max_findings: int = 1_000      # findings retained; further matches are counted


@dataclass
class Finding:
    category: str
    trigger: str
    location: str
    page: int = 0
    field: str = ''
    via_indirect: bool = False
    chain_depth: int = 0


@dataclass
class Gap:
    kind: str            # Unresolvable | BudgetExhausted | DepthExceeded | Unsupported
    scope: str
    location: str
    affects: tuple = ('*',)


class NotOpened(Exception):
    pass


@dataclass
class Report:
    findings: list = field(default_factory=list)
    gaps: list = field(default_factory=list)
    coverage: dict = field(default_factory=dict)
    visits: int = 0
    suppressed_findings: int = 0
    inert_actions: int = 0
    revisions: int = 1


class _Out(Exception):
    pass


class Inspector:
    def __init__(self, reader, budget, revisions):
        self.r, self.b = reader, budget
        self.rep = Report(revisions=revisions)
        self.seen = set()
        self.partial = set()
        self.scope = 'Catalog'

    # -- primitives -----------------------------------------------------------------------------
    def _visit(self):
        self.rep.visits += 1
        if self.rep.visits > self.b.max_visits:
            raise _Out

    def _gap(self, kind, location, affects=('*',)):
        self.rep.gaps.append(Gap(kind, self.scope, location, tuple(affects)))
        self.partial.add(self.scope)

    def _resolve(self, v, location, relevant=True):
        """Return (object, via_indirect). Dangling references in a policy-relevant slot are a gap, not null."""
        indirect = isinstance(v, IndirectObject)
        if indirect:
            self._visit()
            try:
                v = v.get_object()
            except Exception:
                v = None
            if v is None and relevant:
                self._gap('Unresolvable', location)
        return v, indirect

    def _finding(self, category, trigger, location, **kw):
        if len(self.rep.findings) >= self.b.max_findings:
            self.rep.suppressed_findings += 1
            return
        self.rep.findings.append(Finding(category, trigger, location, **kw))

    # -- actions --------------------------------------------------------------------------------
    def action(self, v, trigger, location, page=0, fld='', depth=0, indirect_in=False):
        a, ind = self._resolve(v, location)
        ind = ind or indirect_in
        if a is None:
            return
        if depth > self.b.max_depth:
            self._gap('DepthExceeded', location)
            return
        if isinstance(a, ArrayObject):          # action arrays are not valid; some readers accept them
            for i, item in enumerate(a):
                self.action(item, trigger, f'{location}[{i}]', page, fld, depth + 1, ind)
            return
        if not isinstance(a, DictionaryObject):
            return
        if id(a) in self.seen:
            return                               # cycle or shared action: already reported
        self.seen.add(id(a))
        self._visit()
        s = str(a.get('/S', '')).lstrip('/')
        if s in INERT:
            self.rep.inert_actions += 1
        elif s in ACTION_CATEGORY:
            self._finding(ACTION_CATEGORY[s], trigger, location, page=page, field=fld, via_indirect=ind, chain_depth=depth)
        else:
            self._finding('UnknownAction', trigger, location, page=page, field=fld, via_indirect=ind, chain_depth=depth)
        if '/Next' in a:
            nxt, nind = self._resolve(a.raw_get('/Next'), location + '/Next')
            if isinstance(nxt, ArrayObject):
                for i, item in enumerate(nxt):
                    self.action(item, trigger, f'{location}/Next[{i}]', page, fld, depth + 1, ind or nind)
            elif nxt is not None:
                self.action(a.raw_get('/Next'), trigger, location + '/Next', page, fld, depth + 1, ind)

    def aa(self, v, trigger, location, page=0, fld=''):
        d, _ = self._resolve(v, location)
        if isinstance(d, DictionaryObject):
            for k in d.keys():
                self.action(d.raw_get(k), trigger, f'{location}/{k.lstrip("/")}', page, fld)

    # -- scopes ---------------------------------------------------------------------------------
    def run(self):
        root = self.r.trailer['/Root']
        try:
            self.catalog(root)
        except _Out:
            self._gap('BudgetExhausted', 'traversal')
            self.partial.update(s for s in SCOPES if s not in NEVER_INSPECTED and s not in self.done)
        for s in SCOPES:
            if s in NEVER_INSPECTED or s == 'PriorRevisions' and self.rep.revisions == 1:
                self.rep.coverage[s] = 'NotInspected' if s in NEVER_INSPECTED and not (s == 'PriorRevisions' and self.rep.revisions == 1) else 'Complete'
            else:
                self.rep.coverage[s] = 'Partial' if s in self.partial or s not in self.done else 'Complete'
        return self.rep

    done = set()

    def catalog(self, root):
        self.done = set()
        self.scope = 'Catalog'
        cat = root.get_object()
        if '/OpenAction' in cat:
            oa, _ = self._resolve(cat.raw_get('/OpenAction'), 'Catalog/OpenAction')
            if isinstance(oa, DictionaryObject):
                self.action(cat.raw_get('/OpenAction'), 'OpenAction', 'Catalog/OpenAction')
        if '/AA' in cat:
            self.aa(cat.raw_get('/AA'), 'DocumentAction', 'Catalog/AA')
        if '/AF' in cat:
            af, _ = self._resolve(cat.raw_get('/AF'), 'Catalog/AF')
            if isinstance(af, ArrayObject) and len(af):
                self._finding('EmbeddedFile', 'AssociatedFile', 'Catalog/AF')
        self.done.add('Catalog')
        self.scope = 'NameTrees'
        names, _ = self._resolve(cat.raw_get('/Names'), 'Catalog/Names') if '/Names' in cat else (None, False)
        if isinstance(names, DictionaryObject):
            if '/JavaScript' in names:
                self.name_tree(names.raw_get('/JavaScript'), 'JavaScript', 'Names/JavaScript', 'NamedScript')
            if '/EmbeddedFiles' in names:
                self.name_tree(names.raw_get('/EmbeddedFiles'), 'EmbeddedFile', 'Names/EmbeddedFiles', 'EmbeddedFile')
        self.done.add('NameTrees')
        self.scope = 'Outlines'
        if '/Outlines' in cat:
            self.outline(cat.raw_get('/Outlines'), 'Outlines')
        self.done.add('Outlines')
        self.scope = 'AcroForm'
        if '/AcroForm' in cat:
            self.acroform(cat.raw_get('/AcroForm'))
        self.done.add('AcroForm')
        self.scope = 'Pages'
        self.pages(cat.raw_get('/Pages'))
        self.done.add('Pages')

    def name_tree(self, v, category, location, trigger, depth=0):
        n, _ = self._resolve(v, location)
        if not isinstance(n, DictionaryObject):
            return
        if depth > self.b.max_depth:
            self._gap('DepthExceeded', location, (category,))
            return
        if id(n) in self.seen:
            return
        self.seen.add(id(n))
        self._visit()
        for k in ('/Names', '/Kids'):
            if k in n:
                arr, _ = self._resolve(n.raw_get(k), f'{location}/{k[1:]}')
                if isinstance(arr, ArrayObject) and len(arr):
                    if k == '/Names':
                        for i in range(1, len(arr), 2):
                            self._visit()
                            self._finding(category, trigger, f'{location}/Names[{i}]')
                    else:
                        for i, kid in enumerate(arr):
                            self.name_tree(kid, category, f'{location}/Kids[{i}]', trigger, depth + 1)

    def outline(self, v, location, depth=0):
        o, _ = self._resolve(v, location)
        if not isinstance(o, DictionaryObject) or id(o) in self.seen:
            return
        if depth > self.b.max_depth:
            self._gap('DepthExceeded', location)
            return
        self.seen.add(id(o))
        self._visit()
        if '/A' in o:
            self.action(o.raw_get('/A'), 'OutlineItem', location + '/A')
        if '/First' in o:
            self.outline(o.raw_get('/First'), location + '/First', depth + 1)
        if '/Next' in o:
            self.outline(o.raw_get('/Next'), location + '/Next', depth)

    def acroform(self, v):
        af, _ = self._resolve(v, 'Catalog/AcroForm')
        if not isinstance(af, DictionaryObject):
            return
        if '/XFA' in af:
            self._finding('Xfa', 'AcroForm', 'AcroForm/XFA')
        fields, _ = self._resolve(af.raw_get('/Fields'), 'AcroForm/Fields') if '/Fields' in af else (None, False)
        if isinstance(fields, ArrayObject):
            for i, f in enumerate(fields):
                self.field(f, f'AcroForm/Fields[{i}]', '', 0)

    def field(self, v, location, inherited_name, depth):
        f, _ = self._resolve(v, location)
        if not isinstance(f, DictionaryObject) or id(f) in self.seen:
            return
        if depth > self.b.max_depth:
            self._gap('DepthExceeded', location)
            return
        self.seen.add(id(f))
        self._visit()
        name = str(f.get('/T', inherited_name))
        if '/A' in f:
            self.action(f.raw_get('/A'), 'FieldAction', location + '/A', fld=name)
        if '/AA' in f:
            self.aa(f.raw_get('/AA'), 'FieldAction', location + '/AA', fld=name)
        if '/Kids' in f:
            kids, _ = self._resolve(f.raw_get('/Kids'), location + '/Kids')
            if isinstance(kids, ArrayObject):
                for i, k in enumerate(kids):
                    self.field(k, f'{location}/Kids[{i}]', name, depth + 1)

    def pages(self, v, depth=0):
        node, _ = self._resolve(v, 'Pages')
        if not isinstance(node, DictionaryObject):
            return
        if depth > self.b.max_depth:
            self._gap('DepthExceeded', 'Pages')
            return
        if id(node) in self.seen:
            return
        self.seen.add(id(node))
        self._visit()
        if '/Kids' in node:
            kids, _ = self._resolve(node.raw_get('/Kids'), 'Pages/Kids')
            for k in kids or []:
                self.pages(k, depth + 1)
            return
        self.page_no = getattr(self, 'page_no', 0) + 1
        n = self.page_no
        loc = f'Page {n}'
        if '/AA' in node:
            self.aa(node.raw_get('/AA'), 'PageAction', loc + '/AA', page=n)
        if '/AF' in node:
            af, _ = self._resolve(node.raw_get('/AF'), loc + '/AF')
            if isinstance(af, ArrayObject) and len(af):
                self._finding('EmbeddedFile', 'AssociatedFile', loc + '/AF', page=n)
        if '/Annots' in node:
            annots, _ = self._resolve(node.raw_get('/Annots'), loc + '/Annots')
            if isinstance(annots, ArrayObject):
                for i, a in enumerate(annots):
                    self.annotation(a, f'{loc}/Annots[{i}]', n)

    def annotation(self, v, location, n):
        a, _ = self._resolve(v, location)
        if not isinstance(a, DictionaryObject) or id(a) in self.seen:
            return
        self.seen.add(id(a))
        self._visit()
        sub = str(a.get('/Subtype', '')).lstrip('/')
        if sub in RICH_ANNOTS:
            self._finding('RichMedia', 'Annotation', location, page=n)
        elif sub == 'FileAttachment':
            self._finding('EmbeddedFile', 'Annotation', location, page=n)
        if '/A' in a:
            self.action(a.raw_get('/A'), 'AnnotationAction', location + '/A', page=n)
        if '/AA' in a:
            self.aa(a.raw_get('/AA'), 'AnnotationAction', location + '/AA', page=n)


def inspect(path, budget=None):
    """Open and inspect. A file that cannot be opened produces no report (parse state is separate)."""
    try:
        reader = PdfReader(path, strict=True)
        if reader.is_encrypted:
            if not reader.decrypt(''):
                raise NotOpened('password required')
            filt = str(reader.trailer['/Encrypt'].get('/Filter', ''))
            if filt != '/Standard':
                raise NotOpened('unsupported security handler ' + filt)
        _ = reader.trailer['/Root']
    except NotOpened:
        raise
    except Exception as e:
        raise NotOpened(type(e).__name__)
    data = open(path, 'rb').read()
    revisions = data.count(b'%%EOF') or 1
    return Inspector(reader, budget or Budget(), revisions).run()


# -- policy ---------------------------------------------------------------------------------------
@dataclass
class Policy:
    allow: frozenset = frozenset()          # categories the host accepts; every other category rejects

    def evaluate(self, rep):
        """Return (verdict, reasons, exclusions). Within policy needs zero rejecting findings AND no gap that
        could hide one: coverage of every scope that can carry a rejected category must be Complete."""
        rejected = [f for f in rep.findings if f.category not in self.allow]
        relevant_cats = [c for c in CATEGORIES if c not in self.allow]
        blocking = [g for g in rep.gaps if '*' in g.affects or any(c in relevant_cats for c in g.affects)]
        suppressed_relevant = rep.suppressed_findings > 0 and bool(relevant_cats)
        if rejected:
            return 'Rejected', sorted({f.category for f in rejected}), []
        if relevant_cats and (blocking or suppressed_relevant):
            return 'Indeterminate', sorted({g.kind for g in blocking}) or ['FindingsSuppressed'], []
        excl = [s for s, st in rep.coverage.items() if st != 'Complete']
        return 'WithinPolicy', [], excl
