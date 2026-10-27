"""Original MIT-licensed synthetic navigation probes. No action is executed."""
import hashlib
import json
import sys
from pathlib import Path
from pypdf import PdfWriter
from pypdf.generic import (DictionaryObject as D, NameObject as N, ArrayObject as A,
                          NumberObject as I, TextStringObject as T, BooleanObject as B,
                          NullObject, IndirectObject)

def d(**kw):
    return D({N('/' + k): v for k, v in kw.items()})

def link(**kw):
    return d(Type=N('/Annot'), Subtype=N('/Link'), Rect=A([I(0), I(0), I(10), I(10)]), **kw)

out = Path(sys.argv[1])
out.mkdir(parents=True, exist_ok=True)
cases = ['navigation', 'missing-name', 'dangling-page', 'nonpage-target', 'bad-mode',
         'bad-coordinate', 'ambiguous-dest-action', 'named-nextpage', 'remote-goto',
         'uri-chain', 'uri-next-null', 'link-aa', 'page-aa', 'catalog-aa', 'open-goto',
         'open-uri', 'open-goto-action', 'ismap-true', 'ismap-false', 'uri-userinfo', 'uri-file', 'uri-custom',
         'uri-mailto', 'uri-relative', 'uri-no-base', 'uri-file-base', 'uri-userinfo-base',
         'uri-bad-escape', 'uri-space', 'uri-query', 'uri-network-path']
manifest = []
for case in cases:
    w = PdfWriter()
    p1 = w.add_blank_page(100, 100)
    p2 = w.add_blank_page(100, 100)
    dest = A([p2.indirect_reference, N('/Fit')])
    uri = d(S=N('/URI'), URI=T('https://example.org/a'))
    annots = [link(A=uri)]
    if case == 'navigation':
        w._root_object[N('/Dests')] = d(old=dest)
        w._root_object[N('/Names')] = d(Dests=d(Names=A([T('new'), d(D=dest)])))
        annots = [link(Dest=A([p1.indirect_reference, N('/Fit')])), link(Dest=dest),
                  link(Dest=N('/old')), link(A=d(S=N('/GoTo'), D=T('new')))]
    elif case == 'missing-name': annots = [link(Dest=N('/missing'))]
    elif case == 'dangling-page': annots = [link(Dest=A([IndirectObject(999, 0, w), N('/Fit')]))]
    elif case == 'nonpage-target': annots = [link(Dest=A([w._add_object(d()), N('/Fit')]))]
    elif case == 'bad-mode': annots = [link(Dest=A([p1.indirect_reference, N('/Bogus')]))]
    elif case == 'bad-coordinate': annots = [link(Dest=A([p1.indirect_reference, N('/FitH'), T('bad')]))]
    elif case == 'ambiguous-dest-action': annots = [link(Dest=dest, A=uri)]
    elif case == 'named-nextpage': annots = [link(A=d(S=N('/Named'), N=N('/NextPage')))]
    elif case == 'remote-goto': annots = [link(A=d(S=N('/GoToR'), F=T('other.pdf'), D=A([I(0), N('/Fit')])))]
    elif case == 'uri-chain': uri[N('/Next')] = d(S=N('/URI'), URI=T('https://example.net/'))
    elif case == 'uri-next-null': uri[N('/Next')] = NullObject()
    elif case == 'link-aa': annots[0][N('/AA')] = d(E=uri)
    elif case == 'page-aa': p1[N('/AA')] = d(O=uri)
    elif case == 'catalog-aa': w._root_object[N('/AA')] = d(WC=uri)
    elif case == 'open-goto': w._root_object[N('/OpenAction')] = A([p1.indirect_reference, N('/Fit')])
    elif case == 'open-uri': w._root_object[N('/OpenAction')] = uri
    elif case == 'open-goto-action': w._root_object[N('/OpenAction')] = d(S=N('/GoTo'), D=dest)
    elif case.startswith('ismap-'): uri[N('/IsMap')] = B(case.endswith('true'))
    else:
        text = {'uri-userinfo': 'https://user@example.org/a', 'uri-file': 'file:///tmp/a',
                'uri-custom': 'business:invoice/42', 'uri-mailto': 'mailto:a@example.org',
                'uri-relative': '../c?x=1#f', 'uri-no-base': '../c', 'uri-file-base': 'child',
                'uri-userinfo-base': 'child', 'uri-bad-escape': 'https://example.org/%xx',
                'uri-space': 'https://example.org/a b', 'uri-query': '?q=1',
                'uri-network-path': '//other.org/x'}[case]
        uri[N('/URI')] = T(text)
        basis = {'uri-relative': 'https://example.org/a/b', 'uri-file-base': 'file:///tmp/',
                 'uri-userinfo-base': 'https://user@example.org/a/',
                 'uri-query': 'https://example.org/a/b?old=1',
                 'uri-network-path': 'https://example.org/a/'}.get(case)
        if basis: w._root_object[N('/URI')] = d(Base=T(basis))
    p1[N('/Annots')] = A(annots)
    path = out / (case + '.pdf')
    w.write(path)
    manifest.append({'case': case, 'sha256': hashlib.sha256(path.read_bytes()).hexdigest(),
                     'bytes': path.stat().st_size, 'provenance': 'W-192 generate.py; original MIT synthetic data'})
Path(sys.argv[2]).write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8')
