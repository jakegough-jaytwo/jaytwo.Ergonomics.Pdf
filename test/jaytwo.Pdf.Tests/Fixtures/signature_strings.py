"""Read the text strings of every signature dictionary with pypdf, as an independent reader of an encrypted file.

Usage: signature_strings.py PATH [PASSWORD]

Prints one JSON object keyed by signature field name. Each entry maps /M, /Reason, /Location, /ContactInfo, and
/Name, without the slash, to the string pypdf reads, or null when the key is absent. pypdf decrypts every string of
the object with the document key (ISO 32000-1 7.6.1). /Contents is not read here: pypdf does not exempt it, and the
verifiers read it raw. Nothing here uses jaytwo.Ergonomics.Pdf.
"""

import json
import sys

from pypdf import PdfReader
from pypdf.generic import IndirectObject, NullObject

KEYS = ("M", "Reason", "Location", "ContactInfo", "Name")


def resolve(value):
    if not isinstance(value, IndirectObject):
        return value
    found = value.get_object()
    return None if isinstance(found, NullObject) else found


def text(value):
    value = resolve(value)
    if value is None:
        return None
    if isinstance(value, (bytes, bytearray)):
        return value.decode("latin-1")
    return str(value)


def main():
    reader = PdfReader(sys.argv[1], strict=False)
    if reader.is_encrypted and not reader.decrypt(sys.argv[2] if len(sys.argv) > 2 else ""):
        raise ValueError("pypdf could not decrypt")
    out = {}
    form = resolve(resolve(reader.trailer["/Root"]).get("/AcroForm"))
    for field in resolve(form.get("/Fields")) if form is not None else []:
        field = resolve(field)
        if text(field.get("/FT")) != "/Sig":
            continue
        signature = resolve(field.get("/V"))
        if signature is not None:
            out[text(field.get("/T"))] = {key: text(signature.get("/" + key)) for key in KEYS}
    json.dump(out, sys.stdout, indent=1, sort_keys=True)


if __name__ == "__main__":
    main()
