"""Sign Reader-extended test forms with pyHanko before the library writes to them (UsageRightsTests, W-88, W-90).

Usage:
  usage_rights_samples.py MODE IN OUT [IN OUT ...]

MODE is certify-p2 (DocMDP P 2, form filling), certify-p3 (DocMDP P 3, form filling and annotation), or approve
(an approval signature). Each signature goes in a new invisible field, Certify or Approve. pyHanko keeps the existing
catalog /Perms entries, so /UR3 is in the signed revision. Each run makes a throwaway self-signed RSA-2048 key and
certificate; neither is kept.
"""

import datetime
import sys

from asn1crypto import x509 as asn1x509
from cryptography import x509
from cryptography.hazmat.primitives import hashes, serialization
from cryptography.hazmat.primitives.asymmetric import rsa
from cryptography.x509.oid import NameOID
from pyhanko.keys import load_private_key_from_pemder_data
from pyhanko.pdf_utils.incremental_writer import IncrementalPdfFileWriter
from pyhanko.sign import fields, signers
from pyhanko.sign.signers import SimpleSigner

MODES = {
    "certify-p2": ("Certify", fields.MDPPerm.FILL_FORMS),
    "certify-p3": ("Certify", fields.MDPPerm.ANNOTATE),
    "approve": ("Approve", None),
}


def make_signer():
    key = rsa.generate_private_key(public_exponent=65537, key_size=2048)
    name = x509.Name([x509.NameAttribute(NameOID.COMMON_NAME, "jaytwo usage-rights throwaway signer")])
    now = datetime.datetime.now(datetime.timezone.utc)
    cert = (x509.CertificateBuilder().subject_name(name).issuer_name(name).public_key(key.public_key())
            .serial_number(x509.random_serial_number()).not_valid_before(now - datetime.timedelta(days=1))
            .not_valid_after(now + datetime.timedelta(days=30))
            .add_extension(x509.KeyUsage(True, True, True, False, False, False, False, False, False), critical=True)
            .sign(key, hashes.SHA256()))
    key_der = key.private_bytes(serialization.Encoding.DER, serialization.PrivateFormat.PKCS8, serialization.NoEncryption())
    certificate = asn1x509.Certificate.load(cert.public_bytes(serialization.Encoding.DER))
    return SimpleSigner(signing_cert=certificate, signing_key=load_private_key_from_pemder_data(key_der, None), cert_registry=None)


def main():
    field, permission = MODES[sys.argv[1]]
    signer = make_signer()
    pairs = sys.argv[2:]
    for source, target in zip(pairs[0::2], pairs[1::2]):
        with open(source, "rb") as handle:
            writer = IncrementalPdfFileWriter(handle, strict=False)
            meta = signers.PdfSignatureMetadata(field_name=field, certify=permission is not None, docmdp_permissions=permission)
            with open(target, "wb") as out:
                signers.sign_pdf(writer, meta, signer=signer, new_field_spec=fields.SigFieldSpec(field), output=out)


if __name__ == "__main__":
    main()
