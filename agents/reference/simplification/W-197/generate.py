"""Original MIT-licensed, disposable admission fixtures for W-197; outside G2 and the corpus.

Every fixture has two pages. A feature is placed on the catalog (whole input), on page 1 (selected by
extract [1]) or on page 2 (discarded by extract [1]). Signature dictionaries are structural: their
ByteRange/Contents are placeholders, because assembly discovers signatures, it does not validate them.
The probe adds one real engine-signed CMS input of its own.
"""
import hashlib
import json
import sys
from pathlib import Path
from pypdf import PdfWriter
from pypdf.generic import (DictionaryObject as D, NameObject as N, ArrayObject as A, NumberObject as I,
                           TextStringObject as T, ByteStringObject as B, BooleanObject, IndirectObject,
                           StreamObject, DecodedStreamObject)


def d(**kw):
    return D({N('/' + k): v for k, v in kw.items()})


def nums(*xs):
    return A([I(x) for x in xs])


def stream(w, data, **kw):
    s = DecodedStreamObject()
    s.set_data(data)
    for k, v in kw.items():
        s[N('/' + k)] = v
    return w._add_object(s)


def js(w, code='app.alert("W197");'):
    return w._add_object(d(S=N('/JavaScript'), JS=T(code)))


def sigdict(w, kind='approval', reference=None):
    sd = d(Type=N('/DocTimeStamp' if kind == 'timestamp' else '/Sig'), Filter=N('/Adobe.PPKLite'),
           SubFilter=N('/ETSI.RFC3161' if kind == 'timestamp' else '/adbe.pkcs7.detached'))
    if kind != 'no-byterange':
        sd[N('/ByteRange')] = nums(0, 0, 0, 0)
        sd[N('/Contents')] = B(b'\0' * 32)
    if reference is not None:
        sd[N('/Reference')] = A([reference])
    return w._add_object(sd)


def transform(method, **params):
    return d(Type=N('/SigRef'), TransformMethod=N('/' + method),
             TransformParams=d(Type=N('/TransformParams'), **params))


class Doc:
    def __init__(self, painted=False):
        self.w = w = PdfWriter()
        self.font = w._add_object(d(Type=N('/Font'), Subtype=N('/Type1'), BaseFont=N('/Helvetica')))
        self.pages = []
        for n in (1, 2):
            text = b'Digitally signed by J. Doe 2026.10.26' if painted and n == 1 else b'Page %d' % n
            page = w.add_blank_page(200, 120)
            page[N('/Resources')] = d(Font=d(F1=self.font))
            page[N('/Contents')] = stream(w, b'BT /F1 9 Tf 10 50 Td (' + text + b') Tj ET')
            self.pages.append(page)
        self.fields = A()
        self.root = w._root_object

    def annot(self, page, **kw):
        a = self.w._add_object(d(Type=N('/Annot'), Rect=nums(10, 10, 60, 40), P=self.pages[page - 1].indirect_reference, **kw))
        p = self.pages[page - 1]
        if N('/Annots') not in p:
            p[N('/Annots')] = A()
        p[N('/Annots')].append(a)
        return a

    def field(self, page, ft, name, rect=(10, 10, 60, 40), **kw):
        f = self.w._add_object(d(FT=N('/' + ft), T=T(name), **kw))
        widget = self.annot(page, Subtype=N('/Widget'), Parent=f, F=I(4))
        widget.get_object()[N('/Rect')] = nums(*rect)
        f.get_object()[N('/Kids')] = A([widget])
        self.fields.append(f)
        return f

    def finish(self, path):
        if len(self.fields):
            self.root[N('/AcroForm')] = self.w._add_object(d(Fields=self.fields, DA=T('/F1 9 Tf 0 g'),
                                                             DR=d(Font=d(F1=self.font))))
        self.w.write(path)


def build(fid):
    doc = Doc(painted=fid == 'painted-mark')
    w, root = doc.w, doc.root
    if fid.startswith('approval-'):
        page = 1 if fid.endswith('p1') or fid.endswith('invisible') else 2
        rect = (0, 0, 0, 0) if fid.endswith('invisible') else (10, 10, 60, 40)
        doc.field(page, 'Sig', 'Approval', rect, V=sigdict(w), SigFlags=I(3))
    elif fid.startswith('certified-p'):
        level = int(fid[-1])
        sd = sigdict(w, reference=transform('DocMDP', P=I(level), V=N('/1.2')))
        doc.field(2, 'Sig', 'Certification', (0, 0, 0, 0), V=sd)
        root[N('/Perms')] = d(DocMDP=sd)
    elif fid == 'certified-dangling':
        doc.field(2, 'Sig', 'Approval', V=sigdict(w))
        root[N('/Perms')] = d(DocMDP=IndirectObject(9999, 0, w))
    elif fid == 'timestamp-p2':
        doc.field(2, 'Sig', 'Timestamp', (0, 0, 0, 0), V=sigdict(w, 'timestamp'))
    elif fid == 'fieldmdp-p2':
        doc.field(1, 'Tx', 'Name', V=T('Locked value'))
        lock = w._add_object(d(Type=N('/SigFieldLock'), Action=N('/Include'), Fields=A([T('Name')])))
        ref = transform('FieldMDP', Action=N('/Include'), Fields=A([T('Name')]), V=N('/1.2'))
        doc.field(2, 'Sig', 'Approval', V=sigdict(w, reference=ref), Lock=lock)
    elif fid == 'ur3':
        doc.field(1, 'Tx', 'Name', V=T('Value'))
        ur = sigdict(w, reference=transform('UR3', Form=A([N('/FillIn')]), V=N('/2.2')))
        root[N('/Perms')] = d(UR3=ur)
    elif fid == 'sig-dangling-v':
        doc.field(1, 'Sig', 'Approval', V=IndirectObject(9998, 0, w))
    elif fid == 'sig-no-byterange':
        doc.field(1, 'Sig', 'Approval', V=sigdict(w, 'no-byterange'))
    elif fid == 'doc-js':
        root[N('/Names')] = d(JavaScript=d(Names=A([T('w197'), js(w)])))
    elif fid == 'openaction-js':
        root[N('/OpenAction')] = js(w)
    elif fid == 'catalog-aa':
        root[N('/AA')] = d(WC=js(w))
    elif fid in ('page-aa-p1', 'page-aa-p2'):
        doc.pages[int(fid[-1]) - 1][N('/AA')] = d(O=js(w))
    elif fid == 'annot-aa-p1':
        doc.annot(1, Subtype=N('/Text'), Contents=T('note'), AA=d(E=js(w)))
    elif fid == 'annot-js-p1':
        doc.annot(1, Subtype=N('/Square'), A=js(w))
    elif fid == 'link-js-p1':
        doc.annot(1, Subtype=N('/Link'), A=js(w))
    elif fid == 'link-launch-p1':
        doc.annot(1, Subtype=N('/Link'), A=d(S=N('/Launch'), F=T('calc.exe')))
    elif fid == 'widget-aa-p1':
        doc.field(1, 'Tx', 'Name', V=T('Value'), AA=d(K=js(w, 'event.rc = true;')))
    elif fid in ('embedded-files', 'fileattach-p1', 'fileattach-p2', 'catalog-af'):
        ef = stream(w, b'attachment payload', Type=N('/EmbeddedFile'))
        spec = w._add_object(d(Type=N('/Filespec'), F=T('data.txt'), UF=T('data.txt'), EF=d(F=ef)))
        if fid == 'embedded-files':
            root[N('/Names')] = d(EmbeddedFiles=d(Names=A([T('data.txt'), spec])))
        elif fid == 'catalog-af':
            root[N('/AF')] = A([spec])
        else:
            doc.annot(int(fid[-1]), Subtype=N('/FileAttachment'), FS=spec, Name=N('/Paperclip'))
    elif fid in ('ocprops', 'oc-annot-no-catalog'):
        ocg = w._add_object(d(Type=N('/OCG'), Name=T('Hidden layer')))
        if fid == 'ocprops':
            root[N('/OCProperties')] = d(OCGs=A([ocg]), D=d(OFF=A([ocg])))
            doc.pages[0][N('/Resources')][N('/Properties')] = d(L1=ocg)
        else:
            doc.annot(1, Subtype=N('/Square'), OC=ocg)
    elif fid == 'hybrid-xfa':
        doc.field(1, 'Tx', 'Name', V=T('Value'))
        doc.finish_xfa = stream(w, b'<xdp:xdp xmlns:xdp="http://ns.adobe.com/xdp/"/>')
    elif fid == 'budget-deep-fields':
        # An unsigned field hierarchy deeper than the engine's 256-level traversal limit, on discarded page 2.
        parent = None
        for depth in range(300):
            node = w._add_object(d(T=T('n%d' % depth)))
            if parent is None:
                doc.fields.append(node)
            else:
                parent.get_object()[N('/Kids')] = A([node])
                node.get_object()[N('/Parent')] = parent
            parent = node
        leaf = parent.get_object()
        leaf[N('/FT')] = N('/Tx')
        widget = doc.annot(2, Subtype=N('/Widget'), Parent=parent)
        leaf[N('/Kids')] = A([widget])
    elif fid not in ('control', 'painted-mark'):
        raise ValueError(fid)
    return doc


FIXTURES = [
    'control', 'painted-mark', 'approval-p1', 'approval-p2', 'approval-invisible', 'certified-p1', 'certified-p2',
    'certified-p3', 'certified-dangling', 'timestamp-p2', 'fieldmdp-p2', 'ur3', 'sig-dangling-v', 'sig-no-byterange',
    'doc-js', 'openaction-js', 'catalog-aa', 'page-aa-p1', 'page-aa-p2', 'annot-aa-p1', 'annot-js-p1', 'link-js-p1',
    'link-launch-p1', 'widget-aa-p1', 'embedded-files', 'fileattach-p1', 'fileattach-p2', 'catalog-af', 'ocprops',
    'oc-annot-no-catalog', 'hybrid-xfa', 'budget-deep-fields',
]

out = Path(sys.argv[1])
out.mkdir(parents=True, exist_ok=True)
rows = []
for fid in FIXTURES:
    doc = build(fid)
    path = out / (fid + '.pdf')
    if hasattr(doc, 'finish_xfa'):
        doc.finish(path)
        doc.root[N('/AcroForm')].get_object()[N('/XFA')] = doc.finish_xfa
        doc.w.write(path)
    else:
        doc.finish(path)
    rows.append(dict(id=fid, sha256=hashlib.sha256(path.read_bytes()).hexdigest(), rights='MIT (repository)',
                     producer='pypdf original generate.py'))
(out / 'manifest.json').write_text(json.dumps(rows, indent=2) + '\n')
print(f'Generated {len(rows)} original fixtures')
