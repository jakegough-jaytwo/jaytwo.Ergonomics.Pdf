"""Validate one PDF document timestamp with pyHanko.

Usage: validate_doc_timestamp.py pdf root.pem

root.pem is the only trust anchor. Nothing else is supplied: the library sets
certReq (decision D-21), so the TSA puts its signing certificate in the token,
and pyHanko finds it there. Including the certificate is not trust. Trust comes
only from root.pem.

Prints the status lines. The caller asserts on them.
"""

import sys
from pathlib import Path

from asn1crypto import pem, x509
from pyhanko.pdf_utils.reader import PdfFileReader
from pyhanko.sign.validation import validate_pdf_timestamp
from pyhanko_certvalidator import ValidationContext


def load_cert(path: str) -> x509.Certificate:
    data = Path(path).read_bytes()
    if pem.detect(data):
        _, _, data = pem.unarmor(data)
    return x509.Certificate.load(data)


def main() -> None:
    if len(sys.argv) != 3:
        sys.exit("usage: validate_doc_timestamp.py pdf root.pem")
    root = load_cert(sys.argv[2])
    with open(sys.argv[1], "rb") as handle:
        reader = PdfFileReader(handle, strict=False)
        signatures = reader.embedded_signatures
        if len(signatures) != 1:
            sys.exit("expected one signature, found " + str(len(signatures)))
        signature = signatures[0]
        embedded = signature.signed_data["certificates"]
        print("type=" + str(signature.sig_object_type))
        print("embedded_certs=" + str(0 if embedded.native is None else len(embedded)))
        status = validate_pdf_timestamp(
            signature,
            validation_context=ValidationContext(trust_roots=[root]),
        )
    print(status.summary())
    print("intact=" + str(bool(status.intact)).lower())
    print("valid=" + str(bool(status.valid)).lower())
    print("trusted=" + str(bool(status.trusted)).lower())
    print("coverage=" + status.coverage.name)
    print("time=" + status.timestamp.isoformat())


if __name__ == "__main__":
    main()
