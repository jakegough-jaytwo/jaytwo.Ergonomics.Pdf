"""Original MIT-licensed research fixtures, not G2 corpus or runtime tests."""
import hashlib
import json
import sys
from pathlib import Path
from pypdf import PdfWriter
from pypdf.generic import (DictionaryObject as D, NameObject as N, ArrayObject as A,
                           NumberObject as I, TextStringObject as T, BooleanObject as B,
                           DecodedStreamObject as S, NullObject)


def d(**kw): return D({N('/' + k): v for k, v in kw.items()})
def nums(*xs): return A([I(x) for x in xs])


def build(mode, path):
    w = PdfWriter()
    w.pdf_header = '%PDF-1.7'
    pages = [w.add_blank_page(200, 120) for _ in range(2)]
    font = w._add_object(d(Type=N('/Font'), Subtype=N('/Type1'), BaseFont=N('/Helvetica')))
    resources = d(Font=d(F1=font))
    tree = w._add_object(d(Type=N('/StructTreeRoot')))
    doc = w._add_object(d(Type=N('/StructElem'), S=N('/Document'), P=tree))
    tree.get_object()[N('/K')] = A([doc])
    parents = []
    blocks = []
    labels = []
    for index, page in enumerate(pages):
        page[N('/Resources')] = resources
        page[N('/StructParents')] = I(index)
        label = f'Page{index + 1}'
        labels.append(label)
        content = S()
        # Explicit known MCID permits offline semantic checking without a runtime content parser.
        content.set_data(f'/P <</MCID 0>> BDC BT /F1 12 Tf 10 80 Td ({label}) Tj ET EMC\n'.encode())
        page[N('/Contents')] = w._add_object(content.flate_encode())
        block = w._add_object(d(Type=N('/StructElem'), S=N('/P'), P=doc,
                                Pg=page.indirect_reference, K=I(0), Lang=T('en-US')))
        blocks.append(block)
        parents.extend([I(index), A([block])])
    doc.get_object()[N('/K')] = A(blocks)
    tree.get_object()[N('/ParentTree')] = w._add_object(d(Nums=A(parents)))
    tree.get_object()[N('/ParentTreeNextKey')] = I(2)
    w._root_object.update(d(StructTreeRoot=tree, MarkInfo=d(Marked=B(True)), Lang=T('en-US')))
    if mode in ('mappings', 'conflicting-mappings'):
        tree.get_object().update(d(RoleMap=d(BusinessPara=N('/H1' if mode == 'conflicting-mappings' else '/P')),
                                   ClassMap=d(BusinessStyle=d(O=N('/Layout'), Placement=N('/Block')))))
        blocks[0].get_object().update(d(S=N('/BusinessPara'), C=N('/BusinessStyle'),
                                       Alt=T('First page alternate description'), ActualText=T('First page replacement text')))
    if mode in ('annotation', 'tagged-form'):
        page = pages[0]
        if mode == 'tagged-form':
            appearance = S(); appearance.set_data(b'BT /F1 12 Tf 2 8 Td (Original) Tj ET')
            appearance.update(d(Type=N('/XObject'), Subtype=N('/Form'), BBox=nums(0, 0, 120, 30), Resources=resources))
            annotation = w._add_object(d(Type=N('/Annot'), Subtype=N('/Widget'), FT=N('/Tx'),
                T=T('Name'), TU=T('Customer name'), V=T('Original'), Rect=nums(10, 10, 130, 40),
                F=I(4), P=page.indirect_reference, StructParent=I(2), AP=d(N=w._add_object(appearance.flate_encode()))))
            w._root_object[N('/AcroForm')] = w._add_object(d(Fields=A([annotation]), DR=resources, DA=T('/F1 12 Tf 0 g')))
        else:
            annotation = w._add_object(d(Type=N('/Annot'), Subtype=N('/Text'), Rect=nums(10, 10, 30, 30),
                Contents=T('Check invoice'), P=page.indirect_reference, StructParent=I(2)))
        page[N('/Annots')] = A([annotation])
        elem = w._add_object(d(Type=N('/StructElem'), S=N('/Form' if mode == 'tagged-form' else '/Annot'),
            P=doc, Pg=page.indirect_reference, Alt=T('Customer name' if mode == 'tagged-form' else 'Invoice note'),
            K=d(Type=N('/OBJR'), Obj=annotation, Pg=page.indirect_reference)))
        doc.get_object()[N('/K')] = A([blocks[0], elem, blocks[1]])
        parents.extend([I(2), elem])
        tree.get_object()[N('/ParentTree')].get_object()[N('/Nums')] = A(parents)
        tree.get_object()[N('/ParentTreeNextKey')] = I(3)
    if mode == 'cross-page':
        doc.get_object()[N('/K')] = A([blocks[0]])
        blocks[0].get_object()[N('/K')] = A([
            d(Type=N('/MCR'), Pg=pages[1].indirect_reference, MCID=I(0)),
            d(Type=N('/MCR'), Pg=pages[0].indirect_reference, MCID=I(0))])
        # Deliberately logical order Page2, Page1; physical order alone is insufficient.
        del blocks[0].get_object()[N('/Pg')]
        parents[3] = A([blocks[0]])
        tree.get_object()[N('/ParentTree')].get_object()[N('/Nums')] = A(parents)
    if mode == 'bad-page': blocks[0].get_object()[N('/Pg')] = font
    if mode == 'bad-parent':
        tree.get_object()[N('/ParentTree')].get_object()[N('/Nums')] = A([I(0), A([NullObject()]), I(1), A([blocks[1]])])
    if mode == 'cycle': doc.get_object()[N('/K')] = A([doc])
    if mode == 'unmarked-tree': w._root_object[N('/MarkInfo')] = d(Marked=B(False))
    w.write(path)
    return {'id': mode, 'sha256': hashlib.sha256(path.read_bytes()).hexdigest(),
            'bytes': path.stat().st_size, 'physicalOrder': labels,
            'expectedLogicalOrder': ['Page2', 'Page1'] if mode == 'cross-page' else labels,
            'malformed': mode in ('bad-page', 'bad-parent', 'cycle'),
            'provenance': 'Original synthetic W-191 generator; MIT repository license; no third-party document content'}


if __name__ == '__main__':
    out = Path(sys.argv[1]); out.mkdir(parents=True, exist_ok=True)
    modes = ['minimal', 'mappings', 'conflicting-mappings', 'annotation', 'tagged-form',
             'cross-page', 'bad-page', 'bad-parent', 'cycle', 'unmarked-tree']
    manifest = [build(mode, out / (mode + '.pdf')) for mode in modes]
    (out / 'manifest.json').write_text(json.dumps(manifest, indent=2) + '\n')
    print(f'Generated {len(manifest)} original tagged fixtures')
