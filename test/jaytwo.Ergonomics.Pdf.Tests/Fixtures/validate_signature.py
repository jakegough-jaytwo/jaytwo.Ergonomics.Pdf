"""Validate one PDF signature with pyHanko.

Usage: validate_signature.py pdf cert.pem|- password|- entire|revision [checks]

checks is a comma-separated list:
  chain                 cert.pem is a trust anchor and the CMS carries intermediates
  timestamp             a CMS time-stamp token must be intact, valid, and trusted
  tsa=cert.pem          trust anchor for that token
  docmdp-ok             post-signature changes are allowed by the modification policy
  docmdp-illegal        post-signature changes violate the modification policy
  modification=NAME     NONE, FORM_FILLING, ANNOTATIONS, OTHER, or ABSENT
  broken                the byte range must fail the integrity check
  fieldmdp=A|B          print pyHanko's FieldMDP verdict for each named field,
                        as fieldmdp:NAME=locked or fieldmdp:NAME=open

Without checks, the signature must be intact, cryptographically valid, and trusted.
"""

import sys
from pathlib import Path

from asn1crypto import x509 as asn1x509
from cryptography.hazmat.primitives.serialization import Encoding
from cryptography.x509 import load_pem_x509_certificate
from pyhanko.pdf_utils.reader import PdfFileReader
from pyhanko.sign.validation import validate_pdf_signature
from pyhanko.sign.validation.status import SignatureCoverageLevel
from pyhanko_certvalidator import ValidationContext


def load_trust(path: str):
    cert = load_pem_x509_certificate(Path(path).read_bytes())
    return asn1x509.Certificate.load(cert.public_bytes(Encoding.DER))


def parse_checks(raw: str) -> tuple[list[str], str | None]:
    checks = []
    tsa = None
    if not raw:
        return checks, tsa
    for item in raw.split(","):
        if item.startswith("tsa="):
            tsa = item[4:]
        else:
            checks.append(item)
    return checks, tsa


def main() -> None:
    if len(sys.argv) < 5 or len(sys.argv) > 6:
        sys.exit(
            "usage: validate_signature.py pdf cert.pem|- password|- entire|revision [checks]"
        )
    pdf_path = sys.argv[1]
    cert_path = None if sys.argv[2] == "-" else sys.argv[2]
    password = None if sys.argv[3] == "-" else sys.argv[3]
    coverage = sys.argv[4]
    checks, tsa_path = parse_checks(sys.argv[5] if len(sys.argv) == 6 else "")
    with open(pdf_path, "rb") as handle:
        reader = PdfFileReader(handle, strict=False)
        if password:
            reader.decrypt(password)
        signatures = reader.embedded_signatures
        if len(signatures) != 1:
            sys.exit("expected one signature, found " + str(len(signatures)))
        signature = signatures[0]
        trust = load_trust(cert_path) if cert_path else signature.signer_cert
        timestamp_context = ValidationContext(trust_roots=[load_trust(tsa_path)]) if tsa_path else None
        status = validate_pdf_signature(
            signature,
            signer_validation_context=ValidationContext(trust_roots=[trust]),
            ts_validation_context=timestamp_context,
        )
    level = "ABSENT" if status.modification_level is None else status.modification_level.name
    print(status.summary())
    print("intact=" + str(bool(status.intact)).lower())
    print("valid=" + str(bool(status.valid)).lower())
    print("trusted=" + str(bool(status.trusted)).lower())
    print("docmdp_ok=" + str(bool(status.docmdp_ok)).lower())
    print("modification=" + level)
    print("coverage=" + str(status.coverage))
    for item in checks:
        if item.startswith("fieldmdp="):
            spec = signature.fieldmdp
            if spec is None:
                sys.exit("the signature has no FieldMDP reference")
            print("fieldmdp_action=" + spec.action.name)
            for name in item.split("=", 1)[1].split("|"):
                print("fieldmdp:" + name + "=" + ("locked" if spec.is_locked(name) else "open"))
    if "broken" in checks:
        if status.intact:
            sys.exit("expected the byte range integrity check to fail")
        return
    if not status.intact:
        sys.exit("signature byte range is not intact")
    if not status.valid:
        sys.exit("signature is not cryptographically valid")
    if not status.trusted:
        sys.exit("signature is not trusted")
    expected = (
        SignatureCoverageLevel.ENTIRE_FILE
        if coverage == "entire"
        else SignatureCoverageLevel.ENTIRE_REVISION
    )
    if status.coverage != expected:
        sys.exit("coverage is " + str(status.coverage) + ", expected " + str(expected))
    if "chain" in checks and len(list(signature.other_embedded_certs)) < 2:
        sys.exit(
            "expected the CMS to carry an intermediate and a root, found "
            + str(len(list(signature.other_embedded_certs)))
        )
    if "timestamp" in checks:
        token = status.timestamp_validity
        if token is None:
            sys.exit("the CMS signature has no time-stamp token")
        print("timestamp_intact=" + str(bool(token.intact)).lower())
        print("timestamp_valid=" + str(bool(token.valid)).lower())
        print("timestamp_trusted=" + str(bool(token.trusted)).lower())
        if not token.intact or not token.valid or not token.trusted:
            sys.exit("time-stamp token was not accepted: " + token.summary())
    if "docmdp-ok" in checks and not status.docmdp_ok:
        sys.exit("modification policy rejected the changes after signing")
    if "docmdp-illegal" in checks and status.docmdp_ok:
        sys.exit("modification policy allowed the changes after signing")
    for item in checks:
        if item.startswith("modification="):
            expected_level = item.split("=", 1)[1]
            if level != expected_level:
                sys.exit("modification is " + level + ", expected " + expected_level)


if __name__ == "__main__":
    main()
