"""Write pyHanko-signed samples that differ only in modification policy.

approval.pdf has no DocMDP. locked.pdf certifies with P=1. fillable.pdf
certifies with P=2 over a text field named Name. The private key is removed
after signing; validators trust the embedded certificate.

With a second argument "fieldmdp", it instead writes FieldMDP samples over
text fields Locked, Open, and Group.Child: fieldmdp-include.pdf is an approval
signature whose lock is Include [Locked], fieldmdp-exclude.pdf is an approval
signature whose lock is Exclude [Open, Group.Child], and
fieldmdp-certified.pdf certifies with P=2 and the lock Include [Locked].
fieldmdp-include-parent.pdf and fieldmdp-exclude-parent.pdf are approval
signatures whose locks name the non-terminal field Group.
"""

import sys
from datetime import datetime, timedelta, timezone
from pathlib import Path

from cryptography import x509
from cryptography.hazmat.primitives import hashes, serialization
from cryptography.hazmat.primitives.asymmetric import rsa
from cryptography.x509.oid import NameOID
from pypdf import PdfWriter
from pypdf.generic import (
    ArrayObject,
    BooleanObject,
    DictionaryObject,
    FloatObject,
    NameObject,
    NumberObject,
    TextStringObject,
)
from pyhanko.pdf_utils.incremental_writer import IncrementalPdfFileWriter
from pyhanko.sign import signers
from pyhanko.sign.fields import FieldMDPAction, FieldMDPSpec, MDPPerm, SigFieldSpec


def write_form(path: Path) -> None:
    writer = PdfWriter()
    writer.add_blank_page(width=612, height=792)
    writer.add_metadata({"/Title": "Certified"})
    page = writer.pages[0]
    text = DictionaryObject()
    text.update(
        {
            NameObject("/Type"): NameObject("/Annot"),
            NameObject("/Subtype"): NameObject("/Widget"),
            NameObject("/FT"): NameObject("/Tx"),
            NameObject("/T"): TextStringObject("Name"),
            NameObject("/Rect"): ArrayObject(
                [FloatObject(72), FloatObject(700), FloatObject(300), FloatObject(724)]
            ),
            NameObject("/DA"): TextStringObject("/Helv 12 Tf 0 g"),
            NameObject("/F"): NumberObject(4),
        }
    )
    text_ref = writer._add_object(text)
    text[NameObject("/P")] = page.indirect_reference
    page[NameObject("/Annots")] = ArrayObject([text_ref])
    form = DictionaryObject()
    form.update(
        {
            NameObject("/Fields"): ArrayObject([text_ref]),
            NameObject("/NeedAppearances"): BooleanObject(False),
        }
    )
    writer._root_object[NameObject("/AcroForm")] = writer._add_object(form)
    with path.open("wb") as handle:
        writer.write(handle)


def text_widget(writer: PdfWriter, page, name: str, y: int) -> DictionaryObject:
    widget = DictionaryObject()
    widget.update(
        {
            NameObject("/Type"): NameObject("/Annot"),
            NameObject("/Subtype"): NameObject("/Widget"),
            NameObject("/FT"): NameObject("/Tx"),
            NameObject("/T"): TextStringObject(name),
            NameObject("/Rect"): ArrayObject(
                [FloatObject(72), FloatObject(y), FloatObject(300), FloatObject(y + 24)]
            ),
            NameObject("/DA"): TextStringObject("/Helv 12 Tf 0 g"),
            NameObject("/F"): NumberObject(4),
            NameObject("/P"): page.indirect_reference,
        }
    )
    return widget


def write_fieldmdp_form(path: Path) -> None:
    writer = PdfWriter()
    writer.add_blank_page(width=612, height=792)
    writer.add_metadata({"/Title": "FieldMDP"})
    page = writer.pages[0]
    locked_ref = writer._add_object(text_widget(writer, page, "Locked", 700))
    open_ref = writer._add_object(text_widget(writer, page, "Open", 650))
    group = DictionaryObject({NameObject("/T"): TextStringObject("Group")})
    group_ref = writer._add_object(group)
    child = text_widget(writer, page, "Child", 600)
    child[NameObject("/Parent")] = group_ref
    child_ref = writer._add_object(child)
    group[NameObject("/Kids")] = ArrayObject([child_ref])
    page[NameObject("/Annots")] = ArrayObject([locked_ref, open_ref, child_ref])
    form = DictionaryObject()
    form.update(
        {
            NameObject("/Fields"): ArrayObject([locked_ref, open_ref, group_ref]),
            NameObject("/NeedAppearances"): BooleanObject(False),
        }
    )
    writer._root_object[NameObject("/AcroForm")] = writer._add_object(form)
    with path.open("wb") as handle:
        writer.write(handle)


def write_key(directory: Path) -> tuple[Path, Path]:
    key = rsa.generate_private_key(public_exponent=65537, key_size=2048)
    name = x509.Name([x509.NameAttribute(NameOID.COMMON_NAME, "pyHanko policy")])
    now = datetime.now(timezone.utc)
    cert = (
        x509.CertificateBuilder()
        .subject_name(name)
        .issuer_name(name)
        .public_key(key.public_key())
        .serial_number(x509.random_serial_number())
        .not_valid_before(now - timedelta(days=1))
        .not_valid_after(now + timedelta(days=2))
        .sign(key, hashes.SHA256())
    )
    key_path = directory / "policy-key.pem"
    cert_path = directory / "policy-cert.pem"
    key_path.write_bytes(
        key.private_bytes(
            serialization.Encoding.PEM,
            serialization.PrivateFormat.PKCS8,
            serialization.NoEncryption(),
        )
    )
    cert_path.write_bytes(cert.public_bytes(serialization.Encoding.PEM))
    return key_path, cert_path


def sign(
    source: Path,
    dest: Path,
    key_path: Path,
    cert_path: Path,
    certify: bool,
    perm: MDPPerm,
    lock: FieldMDPSpec | None = None,
) -> None:
    signer = signers.SimpleSigner.load(str(key_path), str(cert_path), ca_chain_files=())
    with source.open("rb") as handle:
        writer = IncrementalPdfFileWriter(handle)
        with dest.open("wb") as output:
            signers.PdfSigner(
                signers.PdfSignatureMetadata(
                    field_name="PolicySig",
                    certify=certify,
                    docmdp_permissions=perm,
                ),
                signer=signer,
                new_field_spec=SigFieldSpec(
                    sig_field_name="PolicySig", on_page=0, box=(72, 40, 240, 80), field_mdp_spec=lock
                ),
            ).sign_pdf(writer, output=output)


def write_fieldmdp(root: Path) -> None:
    form = root / "fieldmdp-form.pdf"
    write_fieldmdp_form(form)
    key_path, cert_path = write_key(root)
    include = FieldMDPSpec(FieldMDPAction.INCLUDE, ["Locked"])
    exclude = FieldMDPSpec(FieldMDPAction.EXCLUDE, ["Open", "Group.Child"])
    try:
        sign(form, root / "fieldmdp-include.pdf", key_path, cert_path, False, MDPPerm.FILL_FORMS, include)
        sign(form, root / "fieldmdp-exclude.pdf", key_path, cert_path, False, MDPPerm.FILL_FORMS, exclude)
        sign(form, root / "fieldmdp-certified.pdf", key_path, cert_path, True, MDPPerm.FILL_FORMS, include)
        parent_include = FieldMDPSpec(FieldMDPAction.INCLUDE, ["Group"])
        parent_exclude = FieldMDPSpec(FieldMDPAction.EXCLUDE, ["Group"])
        sign(form, root / "fieldmdp-include-parent.pdf", key_path, cert_path, False, MDPPerm.FILL_FORMS, parent_include)
        sign(form, root / "fieldmdp-exclude-parent.pdf", key_path, cert_path, False, MDPPerm.FILL_FORMS, parent_exclude)
    finally:
        key_path.unlink(missing_ok=True)
        cert_path.unlink(missing_ok=True)
        form.unlink(missing_ok=True)


def main() -> None:
    root = Path(sys.argv[1])
    root.mkdir(parents=True, exist_ok=True)
    if len(sys.argv) > 2 and sys.argv[2] == "fieldmdp":
        write_fieldmdp(root)
        return
    form = root / "form.pdf"
    write_form(form)
    key_path, cert_path = write_key(root)
    try:
        sign(form, root / "approval.pdf", key_path, cert_path, False, MDPPerm.FILL_FORMS)
        sign(form, root / "locked.pdf", key_path, cert_path, True, MDPPerm.NO_CHANGES)
        sign(form, root / "fillable.pdf", key_path, cert_path, True, MDPPerm.FILL_FORMS)
    finally:
        key_path.unlink(missing_ok=True)
        cert_path.unlink(missing_ok=True)
        form.unlink(missing_ok=True)


if __name__ == "__main__":
    main()
