"""Describe G2 corpus files with independent implementations: qpdf, pypdf, and pyHanko.

Usage: corpus_probe.py QPDF PATH[::PASSWORD][::FIELD=NAME] ...

Prints one JSON object keyed by PATH. Nothing here uses jaytwo.Ergonomics.Pdf; the manifest validator and the
corpus operation tests compare the library and the manifest against this output.

Definitions:
  revisions       pyHanko's cross-reference section count, less the first-page section of a linearized file.
  widgets         qpdf's /AcroForm field list (one entry per widget annotation).
  fieldNamesSha256  SHA-256 of the sorted widget full names, one per line, UTF-8.
  features        structural facts, each derived from the tools named in feature_list().
"""

import hashlib
import json
import re
import subprocess
import sys

from pypdf import PdfReader
from pypdf.generic import IndirectObject


def run(args):
    proc = subprocess.run(args, capture_output=True)
    return proc.returncode, proc.stdout.decode("utf-8", "replace"), proc.stderr.decode("utf-8", "replace")


def text(value):
    return None if value is None else str(value)


def qpdf_facts(qpdf, path, password):
    pw = ["--password=" + password] if password else []
    facts = {}
    code, out, err = run([qpdf] + pw + ["--check", path])
    facts["qpdfCheck"] = {"exit": code, "warnings": sorted({line for line in (out + err).splitlines() if "WARNING" in line})}
    code, out, err = run([qpdf] + pw + ["--show-npages", path])
    facts["pages"] = int(out.strip()) if code in (0, 3) and out.strip().isdigit() else None
    code, out, err = run([qpdf] + pw + ["--check-linearization", path])
    facts["linearizationValid"] = code == 0 and "no linearization errors" in out
    code, out, err = run([qpdf] + pw + ["--show-encryption", path])
    facts["encryption"] = None
    if code in (0, 3) and "File is not encrypted" not in out:
        lines = [line.strip() for line in out.splitlines()]
        revision = next((int(line[4:]) for line in lines if line.startswith("R = ")), None)
        perms = {}
        for line in lines:
            match = re.match(r"(.+): (allowed|not allowed)$", line)
            if match:
                perms[match.group(1)] = match.group(2) == "allowed"
        facts["encryption"] = {"revision": revision, "permissions": perms}
    code, out, err = run([qpdf] + pw + ["--json=2", "--json-key=acroform", path])
    names = []
    if code in (0, 3):
        form = json.loads(out).get("acroform", {})
        names = [field.get("fullname", "") for field in form.get("fields", [])]
    facts["widgets"] = len(names)
    facts["fieldNamesSha256"] = hashlib.sha256("\n".join(sorted(names)).encode("utf-8")).hexdigest()
    facts["attachments"] = []
    code, out, err = run([qpdf] + pw + ["--json=2", "--json-key=attachments", path])
    if code in (0, 3):
        # qpdf names the embedded files (name-tree keys) and gives each file specification's preferred name
        # (/UF, then /F); pypdf_facts adds each one's decoded bytes.
        found = json.loads(out).get("attachments", {})
        facts["attachments"] = [{"name": key, "fileName": found[key].get("preferredname"), "sha256": None} for key in sorted(found)]
    return facts


def resolve(value):
    return value.get_object() if isinstance(value, IndirectObject) else value


def pypdf_facts(path, password, data, wanted_fields, attachments):
    facts = {}
    reader = PdfReader(path, strict=False)
    if reader.is_encrypted:
        if not reader.decrypt(password or ""):
            raise ValueError("pypdf could not decrypt")
    contents = reader.attachments
    for attachment in attachments:
        # pypdf lists each file under its decoded key and its file name; a key that is not a text string (G2
        # finding Q1) is decoded differently from qpdf, so the file name finds it.
        values = contents.get(attachment["name"]) or contents.get(attachment["fileName"], [])
        if len(values) == 1:
            attachment["sha256"] = hashlib.sha256(values[0]).hexdigest()
    root = resolve(reader.trailer["/Root"])
    facts["catalogVersion"] = text(root.get("/Version"))[1:] if root.get("/Version") is not None else None
    info = reader.metadata or {}
    facts["info"] = {"title": text(info.get("/Title")), "producer": text(info.get("/Producer"))}
    page = reader.pages[0]
    facts["firstPage"] = {"mediaBox": [round(float(x), 4) for x in page.mediabox], "rotation": int(page.get("/Rotate", 0) or 0)}
    facts["xmp"] = "/Metadata" in root
    form = resolve(root.get("/AcroForm"))
    fields = {}
    if form is not None and wanted_fields:
        all_fields = reader.get_fields() or {}
        for name in wanted_fields:
            field = all_fields.get(name)
            fields[name] = text(field.get("/V")) if field is not None else None
    facts["fieldValues"] = fields
    perms = resolve(root.get("/Perms"))
    usage_rights = perms is not None and "/UR3" in perms
    docmdp = None
    if perms is not None and "/DocMDP" in perms:
        for ref in resolve(resolve(perms["/DocMDP"]).get("/Reference", [])):
            ref = resolve(ref)
            if ref.get("/TransformMethod") == "/DocMDP":
                docmdp = int(resolve(ref.get("/TransformParams", {})).get("/P", 2))

    def pages_node_attributes(node, seen):
        found = False
        node = resolve(node)
        if id(node) in seen:
            return False
        seen.add(id(node))
        if node.get("/Type") == "/Pages":
            if any(key in node for key in ("/MediaBox", "/CropBox", "/Resources", "/Rotate")):
                return True
            for kid in node.get("/Kids", []):
                found = found or pages_node_attributes(kid, seen)
        return found

    rotated = any(int(p.get("/Rotate", 0) or 0) % 360 for p in reader.pages)
    names = resolve(root.get("/Names"))
    features = set()
    if form is not None and len(form.get("/Fields", [])) > 0:
        features.add("acroform")
    if form is not None and "/XFA" in form:
        features.add("xfa")
    if form is not None and bool(form.get("/NeedAppearances", False)):
        features.add("need-appearances")
    if usage_rights:
        features.add("usage-rights")
    if docmdp is not None:
        features.add("certified")
    if facts["xmp"]:
        features.add("xmp")
    if names is not None and "/EmbeddedFiles" in names:
        features.add("embedded-files")
    if names is not None and "/JavaScript" in names:
        features.add("javascript")
    if "/AF" in root or re.search(rb"/AFRelationship", data):
        features.add("associated-files")
    if "/StructTreeRoot" in root:
        features.add("tagged")
    if "/Outlines" in root and resolve(root["/Outlines"]).get("/First") is not None:
        features.add("outlines")
    if pages_node_attributes(root["/Pages"], set()):
        features.add("inherited-page-attributes")
    if rotated:
        features.add("rotated-pages")
    if len(reader.pages) >= 1000:
        features.add("thousand-pages")
    facts["usageRights"] = usage_rights
    facts["docMdp"] = docmdp
    return facts, features


def pyhanko_facts(path, password):
    from pyhanko.pdf_utils.reader import PdfFileReader
    from pyhanko.sign.diff_analysis import SuspiciousModification
    from pyhanko.sign.validation import validate_pdf_signature, validate_pdf_timestamp
    from pyhanko_certvalidator import ValidationContext

    with open(path, "rb") as handle:
        reader = PdfFileReader(handle, strict=False)
        if reader.encrypted:
            reader.decrypt(password or "")
        sections = reader.xrefs.total_revisions
        signatures = []
        for signature in reader.embedded_signatures:
            # Trust the signer certificate itself: this establishes byte-range integrity and cryptographic validity,
            # not trust (the library performs no trust validation).
            context = ValidationContext(trust_roots=[signature.signer_cert], allow_fetching=False, revocation_mode="soft-fail")
            if signature.sig_object_type == "/DocTimeStamp":
                status = validate_pdf_timestamp(signature, validation_context=context)
            else:
                status = validate_pdf_signature(signature, signer_validation_context=context)
            facts = {
                "field": signature.field_name,
                "intact": bool(status.intact),
                "valid": bool(status.valid),
                "coverage": status.coverage.name,
                "modification": None if status.modification_level is None else status.modification_level.name,
                "docmdpOk": bool(status.docmdp_ok),
            }
            # Why difference analysis rejected a later revision, only when it did (W-90).
            if isinstance(status.diff_result, SuspiciousModification):
                facts["suspicious"] = str(status.diff_result)
            signatures.append(facts)
    return sections, signatures


def has_signature_field(path, password):
    reader = PdfReader(path, strict=False)
    if reader.is_encrypted:
        reader.decrypt(password or "")
    for field in (reader.get_fields() or {}).values():
        if field.get("/FT") == "/Sig":
            return True
    return False


def feature_list(data, facts, features):
    """Byte-level structure facts. Dictionary keys and stream dictionaries are never encrypted or compressed."""
    if re.search(rb"(^|[\r\n])xref[ \t]*[\r\n]", data):
        features.add("classic-xref")
    if re.search(rb"/Type\s*/XRef\b", data):
        features.add("xref-stream")
    if b"/XRefStm" in data:
        features.add("hybrid-xref")
    if re.search(rb"/Type\s*/ObjStm\b", data):
        features.add("object-streams")
    if b"/Linearized" in data[:1024]:
        features.add("linearized")
    if facts.get("encryption") is not None:
        features.add("encryption")
    for name, filt in (("ccitt", b"CCITTFaxDecode"), ("jbig2", b"JBIG2Decode"), ("jpx", b"JPXDecode"), ("dct", b"DCTDecode")):
        if re.search(rb"/" + filt + rb"\b", data):
            features.add(name)
    if facts.get("revisions", 1) > 1:
        features.add("multiple-revisions")
    if facts.get("signatures"):
        features.add("signed")
    return sorted(features)


def probe(qpdf, spec):
    parts = spec.split("::")
    path = parts[0]
    password = None
    wanted = []
    for part in parts[1:]:
        if part.startswith("FIELD="):
            wanted.append(part[6:])
        else:
            password = part
    data = open(path, "rb").read()
    facts = {"sha256": hashlib.sha256(data).hexdigest(), "size": len(data)}
    header = re.match(rb"%PDF-(\d\.\d)", data)
    facts["header"] = header.group(1).decode("ascii") if header else None
    facts.update(qpdf_facts(qpdf, path, password))
    try:
        more, features = pypdf_facts(path, password, data, wanted, facts["attachments"])
        facts.update(more)
        sections, signatures = pyhanko_facts(path, password)
        facts["xrefSections"] = sections
        facts["revisions"] = sections - (1 if b"/Linearized" in data[:1024] and sections > 1 else 0)
        facts["signatures"] = signatures
        if has_signature_field(path, password):
            features.add("signature-field")
        facts["features"] = feature_list(data, facts, features)
        facts["openError"] = None
    except Exception as error:  # an unsupported file: record what the independent tools say
        facts["openError"] = type(error).__name__ + ": " + str(error)[:200]
        facts["features"] = feature_list(data, facts, set())
    return path, facts


def main():
    qpdf = sys.argv[1]
    result = {}
    for spec in sys.argv[2:]:
        path, facts = probe(qpdf, spec)
        result[spec] = facts
    json.dump(result, sys.stdout, indent=1, sort_keys=True)


if __name__ == "__main__":
    main()
