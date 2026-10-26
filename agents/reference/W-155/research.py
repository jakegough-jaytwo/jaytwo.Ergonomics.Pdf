"""Bounded W-155 fixture generation and independent checks. No production code imports.

Run with the pinned .tools Python and --qpdf from the test-tool settings.
Fixtures are deliberately tiny; regex surgery supports only these generated files,
not arbitrary PDFs. Rebuild offsets after surgery; never rely on parser repair.
"""
import argparse
import base64
import hashlib
import io
import json
import re
import subprocess
import unicodedata
from pathlib import Path

import pypdf
from pypdf import PdfReader, PdfWriter
from pypdf.generic import DictionaryObject, NameObject, NumberObject

ROOT = Path(__file__).resolve().parent
FIXTURES = ROOT / "fixtures"
CASES = []
PROVENANCE = []


def encrypted(algorithm, password="user", title="probe"):
    writer = PdfWriter()
    writer.pdf_header = b"%PDF-2.0" if algorithm == "AES-256" else b"%PDF-1.7"
    if algorithm == "AES-256-R5":
        writer.root_object[NameObject("/Extensions")] = DictionaryObject({NameObject("/ADBE"): DictionaryObject({NameObject("/BaseVersion"): NameObject("/1.7"), NameObject("/ExtensionLevel"): NumberObject(3)})})
    writer.add_blank_page(width=72, height=72)
    writer.add_metadata({"/Title": title})
    writer.add_attachment("probe.bin", b"payload")
    writer.encrypt(password, "owner", algorithm=algorithm)
    output = io.BytesIO()
    writer.write(output)
    return output.getvalue()


def surgery(data, change, trailer_change=lambda value: value):
    objects = {int(m[1]): m[2] for m in re.finditer(rb"(?m)^(\d+) 0 obj\r?\n(.*?)\r?\nendobj", data, re.S)}
    trailer = re.search(rb"trailer\r?\n(.*?)\r?\nstartxref", data, re.S)[1]
    change(objects)
    out = bytearray(data[:data.index(b"\n") + 1])
    offsets = {}
    for number, body in objects.items():
        offsets[number] = len(out)
        out.extend(str(number).encode() + b" 0 obj\n" + body + b"\nendobj\n")
    start = len(out)
    size = max(objects) + 1
    out.extend(f"xref\n0 {size}\n0000000000 65535 f \n".encode())
    for i in range(1, size):
        out.extend(f"{offsets[i]:010d} 00000 n \n".encode())
    out.extend(b"trailer\n" + trailer_change(trailer) + f"\nstartxref\n{start}\n%%EOF\n".encode())
    return bytes(out)


def modify_encrypt(data, fn):
    def change(objects):
        for number, body in objects.items():
            if b"/Filter /Standard" in body:
                objects[number] = fn(body)
    return surgery(data, change)


def case(name, data, password="user", source="pypdf", save=False):
    filename = name + ".pdf"
    (FIXTURES / filename).write_bytes(data)
    CASES.append("\t".join([name, base64.b64encode(password.encode()).decode(), filename, "save" if save else "read"]))
    PROVENANCE.append({"case": name, "producer": source, "sha256": hashlib.sha256(data).hexdigest()})


def generate(qpdf):
    FIXTURES.mkdir(exist_ok=True)
    for algorithm in ["RC4-40", "RC4-128", "AES-128", "AES-256-R5", "AES-256"]:
        tag = algorithm.lower()
        baseline = encrypted(algorithm)
        case(tag, baseline, save=True)
        if algorithm in ["AES-256-R5", "AES-256"]:
            for key in ["O", "U", "OE", "UE"]:
                for delta in [-1, 1, 79]:
                    def alter(body, key=key, delta=delta):
                        pattern = rb"/" + key.encode() + rb" <([0-9a-fA-F]+)>"
                        return re.sub(pattern, lambda m: b"/" + key.encode() + b" <" + (m[1][:delta * 2] if delta < 0 else m[1] + b"00" * delta) + b">", body)
                    case(f"{tag}-{key}-{delta}", modify_encrypt(baseline, alter), source="pypdf envelope + synthetic length mutation")
        if algorithm == "AES-128":
            for name, before, after in [("top-omitted", b"/Length 128\n", b""), ("top-16", b"/Length 128\n", b"/Length 16\n"), ("cf-omitted", b"/Length 16\n", b""), ("cf-bits", b"/Length 16\n", b"/Length 128\n"), ("cf-bad", b"/Length 16\n", b"/Length 15\n")]:
                case(name, modify_encrypt(baseline, lambda body: body.replace(before, after)), source="pypdf envelope + synthetic Length mutation")
        if algorithm.startswith("AES"):
            for n in [0, 16]:
                def empty(objects, n=n):
                    objects[1] = re.sub(rb"/Title <[^>]*>", b"/Title <" + b"00" * n + b">", objects[1])
                case(f"{tag}-empty-{n}", surgery(baseline, empty), source="pypdf envelope + malformed AES string")
                def empty_stream(objects, n=n):
                    for number, body in objects.items():
                        if b"/Type /EmbeddedFile" in body:
                            objects[number] = b"<< /Type /EmbeddedFile /Length " + str(n).encode() + b" >>\nstream\n" + bytes(n) + b"\nendstream"
                case(f"{tag}-empty-stream-{n}", surgery(baseline, empty_stream), source="pypdf envelope + malformed AES stream")
            case(f"{tag}-empty-valid", encrypted(algorithm, title=""), save=True)
            for mode in ["identity", "default-name", "stdcf"]:
                def crypt(objects, mode=mode):
                    for number, body in objects.items():
                        if b"/Type /EmbeddedFile" not in body:
                            continue
                        if mode == "stdcf":
                            objects[number] = body.replace(b"/Type /EmbeddedFile", b"/Type /EmbeddedFile /Filter [/Crypt] /DecodeParms [<< /Name /StdCF >>]")
                        else:
                            params = b"/Name /Identity" if mode == "identity" else b""
                            objects[number] = b"<< /Type /EmbeddedFile /Length 7 /Filter [/Crypt] /DecodeParms [<< " + params + b" >>] >>\nstream\npayload\nendstream"
                case(f"{tag}-crypt-{mode}", surgery(baseline, crypt), source="pypdf envelope + explicit Crypt stream")
            def inline(trailer):
                return re.sub(rb"/Info \d+ 0 R", b"/Info << /Title (probe) >>", trailer)
            case(f"{tag}-inline-info", surgery(baseline, lambda objects: None, inline), source="pypdf envelope + malformed direct Info")
    # Unicode boundary splits a 2-byte character at byte 127. R5 has no SASLprep.
    for algorithm in ["AES-256-R5", "AES-256"]:
        for label, password in [("split127", "a" * 126 + "é"), ("nfkc", "a\ufb01"), ("space", "a\u00a0b"), ("mapped", "a\u00adb"), ("bidi", "a\u05d0"), ("post32", "\u1d2c"), ("supp-private", "\U000f0000"), ("noncharacter", "\U0001fffe")]:
            case(algorithm.lower() + "-" + label, encrypted(algorithm, password), password)
    # A Unicode 3.2 query string keeps U+1D2C (then unassigned); modern NFKC maps it to A.
    password = "\u1d2c"
    prepared32 = unicodedata.ucd_3_2_0.normalize("NFKC", password).encode("utf-8")
    case("r6-unicode32-query", encrypted("AES-256", prepared32), password, "pypdf crypto with explicit Unicode 3.2 query bytes")
    # R5 input is UTF-8, without R6 preprocessing. Bytes bypass pypdf's R5 normalization.
    case("r5-raw-nfkc", encrypted("AES-256-R5", "a\ufb01".encode()), "a\ufb01", "pypdf crypto with explicit R5 UTF-8 bytes")
    # Direct Info has no standards-defined containing indirect object identifier.
    raw = encrypted("RC4-40")
    case("r2-inline-info", surgery(raw, lambda objects: None, lambda trailer: re.sub(rb"/Info \d+ 0 R", b"/Info << /Title (probe) >>", trailer)), source="pypdf envelope + malformed direct Info")
    # Actual independent CLI-produced legacy encodings and Unicode R6, no surgery.
    base = ROOT / "base.pdf"
    writer = PdfWriter()
    writer.add_blank_page(width=72, height=72)
    writer.add_metadata({"/Title": "probe"})
    writer.add_attachment("probe.bin", b"payload")
    with base.open("wb") as stream:
        writer.write(stream)
    case("plain-inline-info", surgery(base.read_bytes(), lambda objects: None, lambda trailer: re.sub(rb"/Info \d+ 0 R", b"/Info << /Title (probe) >>", trailer)), save=True, source="pypdf plaintext + malformed direct Info")
    for bits, option in [(40, []), (128, ["--use-aes=n"]), (128, ["--use-aes=y"]), (256, []), (256, ["--force-R5"])]:
        for label, password in [("euro", "€100"), ("latin", "café"), ("bullet", "•100"), ("long", "x" * 40), ("split127", "a" * 126 + "é"), ("mapped", "a\u00adb")]:
            if label == "mapped" and bits != 256:
                continue  # Soft hyphen is not PDFDocEncoding; qpdf correctly refuses creation.
            name = f"qpdf-{bits}-{'r5' if '--force-R5' in option else 'aes' if '--use-aes=y' in option else 'default'}-{label}"
            target = FIXTURES / (name + ".pdf")
            subprocess.run([qpdf, "--allow-weak-crypto", "--password-mode=unicode", "--encrypt", password, "owner", str(bits), *option, "--", str(base), str(target)], check=True, capture_output=True)
            case(name, target.read_bytes(), password, "qpdf CLI (pypdf plaintext input)")
    (ROOT / "cases.tsv").write_text("\n".join(CASES) + "\n", encoding="utf-8")
    (ROOT / "manifest.json").write_text(json.dumps({"pypdf": pypdf.__version__, "qpdf": subprocess.check_output([qpdf, "--version"], text=True).strip(), "cases": PROVENANCE}, indent=2) + "\n")


def check(qpdf):
    results = []
    for line in (ROOT / "cases.tsv").read_text().splitlines():
        name, encoded, filename, _ = line.split("\t")
        password = base64.b64decode(encoded).decode()
        path = FIXTURES / filename
        result = {"case": name}
        try:
            reader = PdfReader(path, password=password)
            result["pypdf-title"] = reader.metadata.title
            result["pypdf-payload"] = base64.b64encode(reader.attachments["probe.bin"][0]).decode()
        except Exception as error:
            result["pypdf-error"] = type(error).__name__ + ": " + str(error)
        run = subprocess.run([qpdf, "--password=" + password, "--check", str(path)], capture_output=True, text=True, encoding="utf-8", errors="replace")
        result["qpdf-exit"] = run.returncode
        result["qpdf-output"] = run.stdout + run.stderr
        payload = subprocess.run([qpdf, "--password=" + password, str(path), "--show-attachment=probe.bin"], capture_output=True)
        result["qpdf-attachment-exit"] = payload.returncode
        result["qpdf-payload"] = base64.b64encode(payload.stdout).decode()
        result["qpdf-attachment-error"] = payload.stderr.decode("utf-8", errors="replace")
        results.append(result)
    for path in sorted((ROOT / "saved").glob("*.pdf")):
        result = {"case": "saved/" + path.stem}
        try:
            reader = PdfReader(path)
            if reader.is_encrypted:
                reader.decrypt("user")
            result["pypdf-title"] = reader.metadata.title
            result["pypdf-payload"] = base64.b64encode(reader.attachments["probe.bin"][0]).decode()
            result["pypdf-empty-payload"] = base64.b64encode(reader.attachments["empty.bin"][0]).decode()
        except Exception as error:
            result["pypdf-error"] = type(error).__name__ + ": " + str(error)
        results.append(result)
    (ROOT / "independent.json").write_text(json.dumps(results, indent=2, ensure_ascii=True) + "\n")
    print(f"Checked {len(results)} inputs/outputs")


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("action", choices=["generate", "check"])
    parser.add_argument("--qpdf", required=True)
    options = parser.parse_args()
    (generate if options.action == "generate" else check)(options.qpdf)
