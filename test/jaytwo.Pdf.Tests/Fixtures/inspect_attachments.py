"""Report document-level embedded files as pypdf 6.21.1 reads them (W-75, spec R8 and W9 to W12).

Usage: inspect_attachments.py PDF [PASSWORD]

Prints JSON: the catalog /PageMode and, for each attachment pypdf lists, its name, /AFRelationship, /Params dates as
UTC ISO 8601, /CheckSum as hex, /Params /Size, the decoded content length, and the SHA-256 and MD5 of the decoded
content. pypdf decodes the stream's filters and decrypts strings and streams itself.
"""

import datetime
import hashlib
import json
import sys

from pypdf import PdfReader


def utc(value):
    if value is None:
        return None
    if value.tzinfo is None:
        value = value.replace(tzinfo=datetime.timezone.utc)
    return value.astimezone(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def main():
    reader = PdfReader(sys.argv[1])
    if reader.is_encrypted:
        reader.decrypt(sys.argv[2] if len(sys.argv) > 2 else "")
    attachments = []
    for item in reader.attachment_list:
        content = item.content
        checksum = item.checksum
        attachments.append(
            {
                "name": item.name,
                "relationship": str(item.associated_file_relationship),
                "creationDate": utc(item.creation_date),
                "modDate": utc(item.modification_date),
                # A decrypted string that decodes as text is a TextStringObject; original_bytes is the string's bytes.
                "checksum": None if checksum is None else bytes(checksum.original_bytes).hex(),
                "size": None if item.size is None else int(item.size),
                "length": len(content),
                "sha256": hashlib.sha256(content).hexdigest(),
                "md5": hashlib.md5(content).hexdigest(),
            }
        )
    page_mode = reader.page_mode
    print(json.dumps({"pageMode": None if page_mode is None else str(page_mode), "attachments": attachments}))


if __name__ == "__main__":
    main()
