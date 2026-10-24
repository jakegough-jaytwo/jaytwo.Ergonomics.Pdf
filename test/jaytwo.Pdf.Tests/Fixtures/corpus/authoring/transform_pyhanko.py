"""Transform corpus files with pyHanko (a repository test generator; outputs are recorded as transformed).

Usage:
  transform_pyhanko.py approve  IN OUT FIELD   approval signature in FIELD (an existing empty field, or a new invisible one)
  transform_pyhanko.py certify  IN OUT FIELD   certification signature in FIELD, DocMDP P 2 (form filling allowed)
  transform_pyhanko.py pubkey   IN OUT         public-key (Adobe.PubSec) encryption, AES-128, for one recipient

Each run makes a throwaway self-signed RSA-2048 key and certificate; neither is kept.
"""

import datetime
import sys

from cryptography import x509
from cryptography.hazmat.primitives import hashes, serialization
from cryptography.hazmat.primitives.asymmetric import rsa
from cryptography.x509.oid import NameOID
from pyhanko.keys import load_cert_from_pemder, load_private_key_from_pemder_data
from pyhanko.pdf_utils.incremental_writer import IncrementalPdfFileWriter
from pyhanko.pdf_utils.reader import PdfFileReader
from pyhanko.pdf_utils.writer import copy_into_new_writer
from pyhanko.sign import fields, signers
from pyhanko.sign.signers import SimpleSigner
from asn1crypto import x509 as asn1x509


def make_identity():
    key = rsa.generate_private_key(public_exponent=65537, key_size=2048)
    name = x509.Name([x509.NameAttribute(NameOID.COMMON_NAME, "jaytwo corpus throwaway signer")])
    now = datetime.datetime.now(datetime.timezone.utc)
    cert = (x509.CertificateBuilder().subject_name(name).issuer_name(name).public_key(key.public_key())
            .serial_number(x509.random_serial_number()).not_valid_before(now - datetime.timedelta(days=1))
            .not_valid_after(now + datetime.timedelta(days=3650))
            .add_extension(x509.KeyUsage(True, True, True, False, False, False, False, False, False), critical=True)
            .sign(key, hashes.SHA256()))
    key_der = key.private_bytes(serialization.Encoding.DER, serialization.PrivateFormat.PKCS8, serialization.NoEncryption())
    cert_der = cert.public_bytes(serialization.Encoding.DER)
    return asn1x509.Certificate.load(cert_der), load_private_key_from_pemder_data(key_der, None)


def sign(source, target, field, certify):
    cert, key = make_identity()
    signer = SimpleSigner(signing_cert=cert, signing_key=key, cert_registry=None)
    with open(source, "rb") as handle:
        writer = IncrementalPdfFileWriter(handle, strict=False)
        meta = signers.PdfSignatureMetadata(
            field_name=field,
            certify=certify,
            docmdp_permissions=fields.MDPPerm.FILL_FORMS if certify else None,
            reason="jaytwo.Ergonomics.Pdf corpus fixture",
        )
        existing = {name for name, _, _ in fields.enumerate_sig_fields(writer)}
        spec = None if field in existing else fields.SigFieldSpec(field)
        with open(target, "wb") as out:
            signers.sign_pdf(writer, meta, signer=signer, new_field_spec=spec, output=out)


def pubkey(source, target):
    cert, _ = make_identity()
    with open(source, "rb") as handle:
        writer = copy_into_new_writer(PdfFileReader(handle, strict=False))
        writer.encrypt_pubkey([cert])
        with open(target, "wb") as out:
            writer.write(out)


def main():
    mode = sys.argv[1]
    if mode in ("approve", "certify"):
        sign(sys.argv[2], sys.argv[3], sys.argv[4], mode == "certify")
    elif mode == "pubkey":
        pubkey(sys.argv[2], sys.argv[3])
    else:
        sys.exit("unknown mode " + mode)


if __name__ == "__main__":
    main()
