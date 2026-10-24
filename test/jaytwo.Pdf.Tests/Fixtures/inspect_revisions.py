"""List logical PDF revisions as pyHanko 0.38.0 reads their cross-reference sections (W-73, spec R10).

Usage: inspect_revisions.py PDF [PASSWORD]

The script reports exclusive revision end offsets, collapses the decreasing-offset main/first-page xref pair of a
linearized file, and matches terminal signature fields to revisions by /ByteRange end. It does not validate signatures.
"""

import json
import sys

from pyhanko.pdf_utils.reader import PdfFileReader


WHITESPACE = b"\x00\x09\x0a\x0c\x0d\x20"


def revision_end(data, section_end):
    startxref = data.find(b"startxref", section_end)
    if startxref < 0:
        raise ValueError("xref section has no startxref")
    eof = data.find(b"%%EOF", startxref)
    if eof < 0:
        raise ValueError("xref section has no %%EOF")
    end = eof + len(b"%%EOF")
    while end < len(data) and data[end] in WHITESPACE:
        end += 1
    return end


def main():
    path = sys.argv[1]
    password = sys.argv[2] if len(sys.argv) > 2 else None
    with open(path, "rb") as stream:
        data = stream.read()
        stream.seek(0)
        reader = PdfFileReader(stream, strict=False)
        if reader.encrypted:
            reader.decrypt(password or "")

        groups = []
        previous_offset = None
        for index in range(reader.xrefs.total_revisions):
            meta = reader.xrefs.get_xref_container_info(index)
            end = revision_end(data, meta.end_location)
            linearized_first_page = index == 1 and meta.declared_startxref < previous_offset
            if linearized_first_page:
                groups[0] = max(groups[0], end)
            else:
                groups.append(end)
            previous_offset = meta.declared_startxref

        covered_ends = {
            int(signature.byte_range[2]) + int(signature.byte_range[3])
            for signature in reader.embedded_signatures
        }
        revisions = [
            {"number": number, "endOffset": end, "covered": end in covered_ends}
            for number, end in enumerate(groups, 1)
        ]
        by_end = {end: number for number, end in enumerate(groups, 1)}
        signature_revisions = {
            signature.field_name: by_end.get(int(signature.byte_range[2]) + int(signature.byte_range[3]))
            for signature in reader.embedded_signatures
        }
    print(json.dumps({"revisions": revisions, "signatureRevisions": signature_revisions}, indent=1))


if __name__ == "__main__":
    main()
