"""Validate one PDF document timestamp with pyHanko.

Usage: validate_doc_timestamp.py pdf root.pem tsa.pem

root.pem is the only trust anchor. tsa.pem is the TSA certificate, supplied
out of band: the library omits certReq (decision D-14), so an RFC 3161 TSA
leaves the token's certificates field empty, and pyHanko only looks for the
signer certificate there. The certificate is added to the parsed SignedData in
memory, not to the PDF. That field is unsigned. pyHanko still checks the signed
ESS signing-certificate attribute against it.

Prints the status lines. The caller asserts on them.
"""

import sys
from pathlib import Path

from asn1crypto import cms, pem, x509
from pyhanko.pdf_utils.reader import PdfFileReader
from pyhanko.sign.validation import validate_pdf_timestamp
from pyhanko_certvalidator import ValidationContext


def load_cert(path: str) -> x509.Certificate:
    data = Path(path).read_bytes()
    if pem.detect(data):
        _, _, data = pem.unarmor(data)
    return x509.Certificate.load(data)


def main() -> None:
    if len(sys.argv) != 4:
        sys.exit("usage: validate_doc_timestamp.py pdf root.pem tsa.pem")
    root = load_cert(sys.argv[2])
    tsa = load_cert(sys.argv[3])
    with open(sys.argv[1], "rb") as handle:
        reader = PdfFileReader(handle, strict=False)
        signatures = reader.embedded_signatures
        if len(signatures) != 1:
            sys.exit("expected one signature, found " + str(len(signatures)))
        signature = signatures[0]
        embedded = signature.signed_data["certificates"]
        print("type=" + str(signature.sig_object_type))
        print("embedded_certs=" + str(0 if embedded.native is None else len(embedded)))
        signature.signed_data["certificates"] = cms.CertificateSet(
            [cms.CertificateChoices(name="certificate", value=tsa)]
        )
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
