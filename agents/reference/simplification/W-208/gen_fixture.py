"""Deterministic large classic-xref PDF for the W-208 probe: PAGES pages each with an valid raw content stream,
a two-level page tree, and FIELDS text fields on the first pages. Usage: gen_fixture.py OUT [PAGES] [FIELDS]."""
import hashlib, random, sys

out = sys.argv[1]
pages = int(sys.argv[2]) if len(sys.argv) > 2 else 12000
fields = int(sys.argv[3]) if len(sys.argv) > 3 else 400
fanout = 100
rng = random.Random(208)
objs = {}
nxt = [1]

def new():
    n = nxt[0]; nxt[0] += 1; return n

catalog, root = new(), new()
info = new()
inner = [new() for _ in range((pages + fanout - 1) // fanout)]
page_ids = [new() for _ in range(pages)]
content_ids = [new() for _ in range(pages)]
field_ids = [new() for _ in range(fields)]
acro = new()
font = new()

objs[font] = b"<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>"
objs[info] = b"<< /Title (W-208 fixture) /Producer (w208 gen) >>"
objs[root] = b"<< /Type /Pages /Count %d /Kids [%s] >>" % (pages, b" ".join(b"%d 0 R" % i for i in inner))
for i, iid in enumerate(inner):
    kids = page_ids[i * fanout:(i + 1) * fanout]
    objs[iid] = b"<< /Type /Pages /Parent %d 0 R /Count %d /Kids [%s] >>" % (root, len(kids), b" ".join(b"%d 0 R" % k for k in kids))
for p in range(pages):
    iid = inner[p // fanout]
    annots = b""
    if p < fields:
        annots = b" /Annots [%d 0 R]" % field_ids[p]
    objs[page_ids[p]] = (b"<< /Type /Page /Parent %d 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 %d 0 R >> >> /Contents %d 0 R%s >>"
                         % (iid, font, content_ids[p], annots))
    lines = bytearray()
    while len(lines) < 4090:
        lines += b"%d %d m %d %d l S\n" % (rng.randrange(612), rng.randrange(792), rng.randrange(612), rng.randrange(792))
    body = bytes(lines[:4090]).rsplit(b"\n", 1)[0] + b"\n"
    # a raw (unfiltered) valid content stream; never decoded by the library
    objs[content_ids[p]] = (b"<< /Length %d >>\nstream\n" % len(body)) + body + b"\nendstream"
for f in range(fields):
    objs[field_ids[f]] = (b"<< /Type /Annot /Subtype /Widget /FT /Tx /T (f%d) /Rect [10 10 110 30] /P %d 0 R /F 4 /DA (/Helv 0 Tf 0 g) >>"
                          % (f, page_ids[f]))
objs[acro] = b"<< /Fields [%s] /DA (/Helv 0 Tf 0 g) /DR << /Font << /Helv %d 0 R >> >> >>" % (b" ".join(b"%d 0 R" % i for i in field_ids), font)
objs[catalog] = b"<< /Type /Catalog /Pages %d 0 R /AcroForm %d 0 R >>" % (root, acro)

buf = bytearray(b"%PDF-1.7\n%\xe2\xe3\xcf\xd3\n")
offsets = {}
for n in sorted(objs):
    offsets[n] = len(buf)
    buf += b"%d 0 obj\n" % n + objs[n] + b"\nendobj\n"
xref = len(buf)
size = nxt[0]
buf += b"xref\n0 %d\n" % size + b"0000000000 65535 f \n"
for n in range(1, size):
    buf += b"%010d 00000 n \n" % offsets[n]
buf += b"trailer\n<< /Size %d /Root %d 0 R /Info %d 0 R /ID [<%s> <%s>] >>\nstartxref\n%d\n%%%%EOF\n" % (
    size, catalog, info, b"0" * 32, b"1" * 32, xref)
open(out, "wb").write(buf)
print("bytes", len(buf), "pages", pages, "fields", fields, "sha256", hashlib.sha256(buf).hexdigest())
