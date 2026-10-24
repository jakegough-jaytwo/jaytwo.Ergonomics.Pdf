"""Signature inspection as pyHanko and asn1crypto read it (W-72, spec R9).

Usage: inspect_signatures.py PDF [PASSWORD]

Prints one JSON object: "fields" lists every signature field in pyHanko's order with whether it is signed, and
"signatures" maps each signed field name to what pyHanko reads from its signature dictionary and CMS. Nothing is
validated: no digest, signature, or trust check runs. Certificates are reported as SHA-256 hex of their DER bytes.
"""

import hashlib
import json
import sys

from asn1crypto import tsp
from pyhanko.pdf_utils.generic import parse_pdf_date
from pyhanko.pdf_utils.reader import PdfFileReader
from pyhanko.sign.fields import enumerate_sig_fields


def sha256(data):
    return hashlib.sha256(data).hexdigest()


def iso(value):
    return None if value is None else value.isoformat(timespec="microseconds")


def text(sig_object, key):
    # Indexing resolves the value, which decrypts a string in an encrypted file; get() returns the lazy proxy.
    return str(sig_object[key]) if key in sig_object else None


def gen_time(signed_data):
    content = signed_data["encap_content_info"]["content"].parsed
    if not isinstance(content, tsp.TSTInfo):
        content = tsp.TSTInfo.load(content.contents)
    return content["gen_time"].native


def describe(sig, revisions):
    sig_object = sig.sig_object
    signed_data = sig.signed_data
    signer_info = sig.signer_info
    signed_attrs = signer_info["signed_attrs"]
    cms_signing_time = None
    for attribute in signed_attrs:
        if attribute["type"].native == "signing_time":
            cms_signing_time = attribute["values"][0].native

    timestamp = None
    if sig.sig_object_type == "/DocTimeStamp":
        timestamp = gen_time(signed_data)
    else:
        for attribute in signer_info["unsigned_attrs"] or []:
            if attribute["type"].native == "signature_time_stamp_token":
                timestamp = gen_time(attribute["values"][0]["content"])
                break

    certificates = [sha256(choice.chosen.dump()) for choice in signed_data["certificates"] if choice.name == "certificate"]
    modified = text(sig_object, "/M")
    docmdp = sig.docmdp_level
    return {
        "subFilter": str(sig_object["/SubFilter"])[1:],
        "signerCertificate": None if sig.signer_cert is None else sha256(sig.signer_cert.dump()),
        "certificates": certificates,
        "digestAlgorithm": signer_info["digest_algorithm"]["algorithm"].dotted,
        "signatureAlgorithm": signer_info["signature_algorithm"]["algorithm"].dotted,
        "signingTime": None if modified is None else iso(parse_pdf_date(modified)),
        "cmsSigningTime": iso(cms_signing_time),
        "name": text(sig_object, "/Name"),
        "reason": text(sig_object, "/Reason"),
        "location": text(sig_object, "/Location"),
        "contactInfo": text(sig_object, "/ContactInfo"),
        "byteRange": [int(x) for x in sig_object["/ByteRange"]],
        "laterRevisions": sig.signed_revision < revisions - 1,
        "timestampTime": iso(timestamp),
        "certificationLevel": None if docmdp is None else int(docmdp.value),
    }


def main():
    path = sys.argv[1]
    password = sys.argv[2] if len(sys.argv) > 2 else None
    with open(path, "rb") as stream:
        reader = PdfFileReader(stream, strict=False)
        if password is not None:
            reader.decrypt(password)
        revisions = reader.xrefs.total_revisions
        fields = [{"name": name, "signed": value is not None} for name, value, _ in enumerate_sig_fields(reader)]
        signatures = {sig.field_name: describe(sig, revisions) for sig in reader.embedded_signatures}
    print(json.dumps({"fields": fields, "signatures": signatures}, indent=1))


if __name__ == "__main__":
    main()
