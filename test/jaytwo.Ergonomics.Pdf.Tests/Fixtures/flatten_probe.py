"""Read what a flatten leaves on each page with pypdf, as an independent reader of the page tree.

Usage: flatten_probe.py PATH [PASSWORD]

Prints one JSON object: "pages" lists, per page, the full names of the widget annotations in /Annots ("widgets"),
the number of other annotations ("other"), and the number of content streams ("contents"); "acroform" lists the
AcroForm keys and "fields" the number of /Fields entries. Nothing here uses jaytwo.Ergonomics.Pdf.
"""

import json
import sys

from pypdf import PdfReader
from pypdf.generic import ArrayObject


def full_name(annot):
    parts = []
    node = annot
    seen = set()
    while node is not None and id(node) not in seen:
        seen.add(id(node))
        if "/T" in node:
            parts.append(str(node["/T"]))
        parent = node.get("/Parent")
        node = parent.get_object() if parent is not None else None
    return ".".join(reversed(parts))


def main():
    reader = PdfReader(sys.argv[1])
    if reader.is_encrypted:
        reader.decrypt(sys.argv[2] if len(sys.argv) > 2 else "")
    pages = []
    for page in reader.pages:
        widgets = []
        other = 0
        annots = page.get("/Annots")
        for item in (annots.get_object() if annots is not None else []):
            annot = item.get_object()
            if annot.get("/Subtype") == "/Widget":
                widgets.append(full_name(annot))
            else:
                other += 1
        contents = page.get("/Contents")
        contents = contents.get_object() if contents is not None else None
        count = len(contents) if isinstance(contents, ArrayObject) else (0 if contents is None else 1)
        pages.append({"widgets": widgets, "other": other, "contents": count})
    root = reader.trailer["/Root"]
    acro = root.get("/AcroForm")
    acro = acro.get_object() if acro is not None else {}
    fields = acro.get("/Fields")
    print(json.dumps({
        "pages": pages,
        "acroform": sorted(str(key) for key in acro.keys()),
        "fields": len(fields.get_object()) if fields is not None else 0,
    }))


if __name__ == "__main__":
    main()
