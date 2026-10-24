"""Read every terminal form field's value with pypdf, as an independent reader of AcroForm data.

Usage: form_values.py PATH [PASSWORD]

Prints one JSON object keyed by dotted full name. Each entry has "kind" (text, password, checkbox, radio, push,
choice, multichoice, signature, or unknown) and "value": a string for text and radio, a Boolean for a check box,
a string or null for a single-select choice, a list for a multi-select choice, and null for no value. The field
tree is walked here from the raw objects (ISO 32000-1 12.7.3.1): a field is terminal when none of its /Kids has a
/T, and /FT, /Ff, and /V are inherited. Nothing here uses jaytwo.Ergonomics.Pdf.
"""

import json
import sys

from pypdf import PdfReader
from pypdf.generic import ArrayObject, IndirectObject, NameObject, NullObject

FLAG_PASSWORD = 1 << 13
FLAG_RADIO = 1 << 15
FLAG_PUSHBUTTON = 1 << 16
FLAG_MULTISELECT = 1 << 21


def resolve(value):
    # A reference to an undefined object is null (ISO 32000-1 7.3.10).
    if not isinstance(value, IndirectObject):
        return value
    try:
        found = value.get_object()
    except Exception:
        return None
    return None if isinstance(found, NullObject) else found


def name_text(value):
    value = resolve(value)
    return str(value)[1:] if isinstance(value, NameObject) else None


def string_text(value):
    value = resolve(value)
    if value is None or isinstance(value, NameObject):
        return None
    if isinstance(value, (bytes, bytearray)):
        return value.decode("latin-1")
    return str(value)


def entry(kind, value):
    return {"kind": kind, "value": value}


def classify(ft, ff, v):
    ft = name_text(ft)
    v = resolve(v)
    if ft == "Tx":
        if ff & FLAG_PASSWORD:
            return entry("password", None)
        return entry("text", string_text(v) or "")
    if ft == "Btn":
        if ff & FLAG_PUSHBUTTON:
            return entry("push", None)
        state = name_text(v)
        if ff & FLAG_RADIO:
            return entry("radio", None if state in (None, "Off") else state)
        return entry("checkbox", state not in (None, "Off"))
    if ft == "Ch":
        values = [string_text(item) for item in v] if isinstance(v, ArrayObject) else ([] if v is None else [string_text(v)])
        if ff & FLAG_MULTISELECT:
            return entry("multichoice", values)
        return entry("choice", values[0] if values else None)
    if ft == "Sig":
        return entry("signature", None)
    return entry("unknown", None)


def walk(node, parent, inherited, out, depth):
    if depth > 64:
        raise ValueError("field tree too deep")
    node = resolve(node)
    partial = node.get("/T")
    name = parent if partial is None else (string_text(partial) if not parent else parent + "." + string_text(partial))
    ft = node.get("/FT", inherited[0])
    ff = int(resolve(node.get("/Ff", inherited[1])) or 0)
    v = node.get("/V", inherited[2])
    kids = resolve(node.get("/Kids")) or []
    fields = [kid for kid in kids if resolve(kid) is not None and "/T" in resolve(kid)]
    if fields:
        for kid in fields:
            walk(kid, name, (ft, ff, v), out, depth + 1)
    elif name and name not in out:
        out[name] = classify(ft, ff, v)


def main():
    reader = PdfReader(sys.argv[1], strict=False)
    if reader.is_encrypted and not reader.decrypt(sys.argv[2] if len(sys.argv) > 2 else ""):
        raise ValueError("pypdf could not decrypt")
    out = {}
    form = resolve(resolve(reader.trailer["/Root"]).get("/AcroForm"))
    if form is not None:
        for field in resolve(form.get("/Fields")) or []:
            if resolve(field) is not None:
                walk(field, "", (None, 0, None), out, 0)
    json.dump(out, sys.stdout, indent=1, sort_keys=True)


if __name__ == "__main__":
    main()
