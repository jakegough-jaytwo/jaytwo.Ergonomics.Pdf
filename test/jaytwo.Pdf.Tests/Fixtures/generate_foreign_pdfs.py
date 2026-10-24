"""Build the foreign PDF fixtures used by ForeignPdfTests.

These files are produced by pypdf, qpdf, and pyHanko. The library under test
does not write them. Re-run this script when those tools change and replace
the committed PDFs. The private signing key is not written next to the PDFs.
"""

import shutil
import subprocess
import sys
from pathlib import Path

from cryptography import x509
from cryptography.hazmat.primitives import hashes, serialization
from cryptography.hazmat.primitives.asymmetric import rsa
from cryptography.x509.oid import NameOID
from datetime import datetime, timedelta, timezone

from pypdf import PdfWriter
from pypdf.generic import (
    ArrayObject,
    BooleanObject,
    DictionaryObject,
    FloatObject,
    IndirectObject,
    NameObject,
    NumberObject,
    TextStringObject,
)

from pyhanko.pdf_utils.incremental_writer import IncrementalPdfFileWriter
from pyhanko.sign import signers
from pyhanko.sign.fields import SigFieldSpec, append_signature_field


def require_qpdf() -> str:
    found = shutil.which("qpdf")
    if found:
        return found
    portable = (
        Path.home()
        / "AppData"
        / "Local"
        / "Temp"
        / "qpdf-bin"
        / "qpdf-12.4.2-msvc64"
        / "bin"
        / "qpdf.exe"
    )
    if portable.exists():
        return str(portable)
    sys.exit("qpdf was not found on PATH.")


def write_base(path: Path) -> None:
    writer = PdfWriter()
    writer.add_blank_page(width=612, height=792)
    writer.add_metadata({"/Title": "Foreign base", "/Author": "pypdf"})
    with path.open("wb") as handle:
        writer.write(handle)


def write_form(path: Path) -> None:
    writer = PdfWriter()
    writer.add_blank_page(width=612, height=792)
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
    box = DictionaryObject()
    box.update(
        {
            NameObject("/Type"): NameObject("/Annot"),
            NameObject("/Subtype"): NameObject("/Widget"),
            NameObject("/FT"): NameObject("/Btn"),
            NameObject("/T"): TextStringObject("Agree"),
            NameObject("/V"): NameObject("/Off"),
            NameObject("/AS"): NameObject("/Off"),
            NameObject("/Rect"): ArrayObject(
                [FloatObject(72), FloatObject(660), FloatObject(90), FloatObject(678)]
            ),
            NameObject("/F"): NumberObject(4),
        }
    )
    box_ref = writer._add_object(box)
    choice = DictionaryObject()
    choice.update(
        {
            NameObject("/Type"): NameObject("/Annot"),
            NameObject("/Subtype"): NameObject("/Widget"),
            NameObject("/FT"): NameObject("/Ch"),
            NameObject("/T"): TextStringObject("Color"),
            NameObject("/Opt"): ArrayObject([TextStringObject("Red"), TextStringObject("Blue")]),
            NameObject("/Rect"): ArrayObject(
                [FloatObject(72), FloatObject(620), FloatObject(220), FloatObject(644)]
            ),
            NameObject("/DA"): TextStringObject("/Helv 12 Tf 0 g"),
            NameObject("/F"): NumberObject(4),
        }
    )
    choice_ref = writer._add_object(choice)
    radio = DictionaryObject()
    radio.update(
        {
            NameObject("/Type"): NameObject("/Annot"),
            NameObject("/Subtype"): NameObject("/Widget"),
            NameObject("/FT"): NameObject("/Btn"),
            NameObject("/Ff"): NumberObject(32768),
            NameObject("/T"): TextStringObject("Station"),
            NameObject("/Rect"): ArrayObject(
                [FloatObject(72), FloatObject(580), FloatObject(98), FloatObject(598)]
            ),
            NameObject("/F"): NumberObject(4),
        }
    )
    radio_ref = writer._add_object(radio)
    page_ref = page.indirect_reference
    text[NameObject("/P")] = page_ref
    box[NameObject("/P")] = page_ref
    choice[NameObject("/P")] = page_ref
    radio[NameObject("/P")] = page_ref
    page[NameObject("/Annots")] = ArrayObject([text_ref, box_ref, choice_ref, radio_ref])
    form = DictionaryObject()
    form.update(
        {
            NameObject("/Fields"): ArrayObject([text_ref, box_ref, choice_ref, radio_ref]),
            NameObject("/NeedAppearances"): BooleanObject(False),
        }
    )
    writer._root_object[NameObject("/AcroForm")] = writer._add_object(form)
    with path.open("wb") as handle:
        writer.write(handle)


def write_dangling(path: Path) -> None:
    """A form whose /Fields and /Annots keep references that resolve to null.

    pypdf writes the deleted field as a free xref entry. The other two entries name
    an object number with no xref entry and the live field at generation 1.
    """
    writer = PdfWriter()
    writer.add_blank_page(width=612, height=792)
    page = writer.pages[0]

    def widget(name: str, top: int) -> DictionaryObject:
        field = DictionaryObject()
        field.update(
            {
                NameObject("/Type"): NameObject("/Annot"),
                NameObject("/Subtype"): NameObject("/Widget"),
                NameObject("/FT"): NameObject("/Tx"),
                NameObject("/T"): TextStringObject(name),
                NameObject("/Rect"): ArrayObject(
                    [FloatObject(72), FloatObject(top), FloatObject(300), FloatObject(top + 24)]
                ),
                NameObject("/DA"): TextStringObject("/Helv 12 Tf 0 g"),
                NameObject("/F"): NumberObject(4),
                NameObject("/P"): page.indirect_reference,
            }
        )
        return field

    live_ref = writer._add_object(widget("Name", 700))
    deleted_ref = writer._add_object(widget("Deleted", 660))
    undefined_ref = IndirectObject(len(writer._objects) + 50, 0, writer)
    stale_ref = IndirectObject(live_ref.idnum, 1, writer)
    refs = [deleted_ref, live_ref, stale_ref, undefined_ref]
    page[NameObject("/Annots")] = ArrayObject(refs)
    form = DictionaryObject()
    form.update(
        {
            NameObject("/Fields"): ArrayObject(refs),
            NameObject("/NeedAppearances"): BooleanObject(False),
        }
    )
    writer._root_object[NameObject("/AcroForm")] = writer._add_object(form)
    writer._objects[deleted_ref.idnum - 1] = None
    with path.open("wb") as handle:
        writer.write(handle)


# Print, annotate, and fill allowed; modify, copy, and assemble refused (W-99). pypdf writes /P as the unsigned
# 32-bit value, 4294966244, on every revision.
UNSIGNED_P_FLAGS = -1052
UNSIGNED_P_ALGORITHMS = {
    2: "RC4-40",
    3: "RC4-128",
    4: "AES-128",
    5: "AES-256-R5",
    6: "AES-256",
}


def write_unsigned_p(form: Path, directory: Path) -> None:
    for revision, algorithm in UNSIGNED_P_ALGORITHMS.items():
        writer = PdfWriter(clone_from=str(form))
        writer.encrypt(
            "user-secret",
            "owner-secret",
            permissions_flag=UNSIGNED_P_FLAGS,
            algorithm=algorithm,
        )
        path = directory / f"pypdf-unsigned-p-r{revision}.pdf"
        with path.open("wb") as handle:
            writer.write(handle)
        data = path.read_bytes()
        if b"/P 4294966244" not in data or f"/R {revision}".encode("ascii") not in data:
            sys.exit(f"pypdf no longer writes an unsigned /P for {algorithm}.")


def write_signing_key(directory: Path) -> tuple[Path, Path]:
    key = rsa.generate_private_key(public_exponent=65537, key_size=2048)
    name = x509.Name([x509.NameAttribute(NameOID.COMMON_NAME, "pyHanko fixture")])
    now = datetime.now(timezone.utc)
    cert = (
        x509.CertificateBuilder()
        .subject_name(name)
        .issuer_name(name)
        .public_key(key.public_key())
        .serial_number(x509.random_serial_number())
        .not_valid_before(now - timedelta(days=1))
        .not_valid_after(now + timedelta(days=30))
        .sign(key, hashes.SHA256())
    )
    key_path = directory / "pyhanko-key.pem"
    cert_path = directory / "pyhanko-cert.pem"
    key_path.write_bytes(
        key.private_bytes(
            serialization.Encoding.PEM,
            serialization.PrivateFormat.PKCS8,
            serialization.NoEncryption(),
        )
    )
    cert_path.write_bytes(cert.public_bytes(serialization.Encoding.PEM))
    return key_path, cert_path


def write_signature_field(base: Path, path: Path) -> None:
    shutil.copyfile(base, path)
    with path.open("rb+") as handle:
        writer = IncrementalPdfFileWriter(handle)
        append_signature_field(
            writer,
            SigFieldSpec(sig_field_name="ExistingSig", on_page=0, box=(72, 100, 240, 140)),
        )
        writer.write_in_place()
    original = base.read_bytes()
    if not path.read_bytes().startswith(original):
        sys.exit("pyHanko signature field was not an incremental append.")


def write_signed(base: Path, path: Path, key_path: Path, cert_path: Path) -> None:
    signer = signers.SimpleSigner.load(str(key_path), str(cert_path), ca_chain_files=())
    with base.open("rb") as handle:
        writer = IncrementalPdfFileWriter(handle)
        with path.open("wb") as output:
            signers.PdfSigner(
                signers.PdfSignatureMetadata(field_name="PyHankoSig"),
                signer=signer,
                new_field_spec=SigFieldSpec(sig_field_name="PyHankoSig", on_page=0, box=(72, 40, 240, 80)),
            ).sign_pdf(writer, output=output)
    original = base.read_bytes()
    if not path.read_bytes().startswith(original):
        sys.exit("pyHanko signature was not an incremental append.")


def main() -> None:
    root = Path(__file__).resolve().parent
    qpdf = require_qpdf()
    base = root / "pypdf-base.pdf"
    write_base(base)
    subprocess.check_call([qpdf, "--object-streams=generate", str(base), str(root / "qpdf-object-streams.pdf")])
    subprocess.check_call(
        [
            qpdf,
            "--encrypt",
            "user-secret",
            "owner-secret",
            "256",
            "--modify=all",
            "--",
            str(base),
            str(root / "qpdf-encrypted.pdf"),
        ]
    )
    note = root / "note.txt"
    note.write_text("foreign-attachment", encoding="ascii")
    subprocess.check_call(
        [
            qpdf,
            "--add-attachment",
            str(note),
            "--key=note.txt",
            "--filename=note.txt",
            "--",
            str(base),
            str(root / "qpdf-attachment.pdf"),
        ]
    )
    note.unlink()
    write_form(root / "pypdf-acroform.pdf")
    write_unsigned_p(root / "pypdf-acroform.pdf", root)
    write_dangling(root / "pypdf-dangling.pdf")
    key_path, cert_path = write_signing_key(root)
    try:
        write_signature_field(base, root / "pyhanko-sigfield.pdf")
        write_signed(base, root / "pyhanko-signed.pdf", key_path, cert_path)
    finally:
        key_path.unlink(missing_ok=True)
        cert_path.unlink(missing_ok=True)
    for pdf in sorted(root.glob("*.pdf")):
        command = [qpdf, "--check", str(pdf)]
        if pdf.name == "qpdf-encrypted.pdf" or pdf.name.startswith("pypdf-unsigned-p-"):
            command = [qpdf, "--password=user-secret", "--check", str(pdf)]
        subprocess.check_call(command)
        print(pdf.name, pdf.stat().st_size)


if __name__ == "__main__":
    main()
