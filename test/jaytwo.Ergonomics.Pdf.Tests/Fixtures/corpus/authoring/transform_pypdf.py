"""Rewrite a corpus file with pypdf (a repository test generator; the output is recorded as transformed).

Usage: transform_pypdf.py inherit IN OUT

inherit: moves /MediaBox and /Resources from every page to the root /Pages node and sets /Rotate 90 there, so each
page inherits all three (ISO 32000-1 7.7.3.4). Widget rectangles stay in default user space.
"""

import sys

from pypdf import PdfReader, PdfWriter
from pypdf.generic import NameObject, NumberObject


def inherit(source, target):
    writer = PdfWriter(clone_from=PdfReader(source))
    pages = writer.root_object["/Pages"].get_object()
    first = writer.pages[0]
    media_box = first["/MediaBox"]
    resources = first["/Resources"]
    for page in writer.pages:
        if page["/MediaBox"] != media_box or page["/Resources"] != resources:
            sys.exit("pages differ; cannot hoist a shared /MediaBox and /Resources")
    pages[NameObject("/MediaBox")] = media_box
    pages[NameObject("/Resources")] = resources
    pages[NameObject("/Rotate")] = NumberObject(90)
    for page in writer.pages:
        page_object = page.get_object()
        del page_object["/MediaBox"]
        del page_object["/Resources"]
        if "/Rotate" in page_object:
            del page_object["/Rotate"]
    writer.add_metadata({"/Title": "Carrier Onboarding Form (rotated, inherited attributes)"})
    with open(target, "wb") as out:
        writer.write(out)


def main():
    if sys.argv[1] != "inherit":
        sys.exit("unknown mode " + sys.argv[1])
    inherit(sys.argv[2], sys.argv[3])


if __name__ == "__main__":
    main()
