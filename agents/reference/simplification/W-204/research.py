import hashlib
import io
import json
import logging
from pathlib import Path
import subprocess
import sys
import pypdf
from pypdf import PdfReader, PdfWriter
from pypdf.generic import ArrayObject, BooleanObject, DictionaryObject, NameObject, NumberObject, TextStringObject

mode, scratch_arg, evidence_arg, qpdf = sys.argv[1:]
scratch, evidence = Path(scratch_arg), Path(evidence_arg)

def write(name, value):
    (evidence / name).write_text(json.dumps(value, indent=2) + "\n", encoding="utf-8")

if mode == "generate":
    writer = PdfWriter()
    page = writer.add_blank_page(612, 792)
    font = writer._add_object(DictionaryObject({NameObject("/Type"): NameObject("/Font"), NameObject("/Subtype"): NameObject("/Type1"), NameObject("/BaseFont"): NameObject("/Helvetica"), NameObject("/Encoding"): NameObject("/WinAnsiEncoding")}))
    fields = ArrayObject()
    for i, name in enumerate(["Name", "City", "Code"]):
        field = DictionaryObject({NameObject("/Type"): NameObject("/Annot"), NameObject("/Subtype"): NameObject("/Widget"), NameObject("/FT"): NameObject("/Tx"), NameObject("/T"): TextStringObject(name), NameObject("/V"): TextStringObject(""), NameObject("/DA"): TextStringObject("/Helv 12 Tf 0 g"), NameObject("/Rect"): ArrayObject([NumberObject(v) for v in [72, 700 - i * 40, 280, 724 - i * 40]]), NameObject("/P"): page.indirect_reference, NameObject("/F"): NumberObject(20 if name == "Name" else 4)})
        if name == "Code":
            field[NameObject("/MaxLen")] = NumberObject(3)
        fields.append(writer._add_object(field))
    page[NameObject("/Annots")] = fields
    writer._root_object[NameObject("/AcroForm")] = writer._add_object(DictionaryObject({NameObject("/Fields"): fields, NameObject("/DA"): TextStringObject("/Helv 12 Tf 0 g"), NameObject("/DR"): DictionaryObject({NameObject("/Font"): DictionaryObject({NameObject("/Helv"): font})}), NameObject("/NeedAppearances"): BooleanObject(False)}))
    writer.add_metadata({"/Title": "Original"})
    writer.write(scratch / "form.pdf")
    write("input.json", {"producer": "pypdf", "version": pypdf.__version__, "sha256": hashlib.sha256((scratch / "form.pdf").read_bytes()).hexdigest(), "purpose": "Three initially blank text widgets; Code MaxLen 3; Name NoRotate flag; no initial attachments or signatures."})
    print("Generated one independent synthetic form fixture.")

elif mode == "inspect":
    rows = json.loads((evidence / "observations.json").read_text())
    indexed = {r["name"]: r for r in rows}
    inspections = {}
    for path in sorted(scratch.glob("*.pdf")):
        if path.name.startswith("pypdf-"):
            continue
        checked = subprocess.run([qpdf, "--check", str(path)], capture_output=True, text=True)
        if checked.returncode != 0:
            raise AssertionError((path.name, checked.returncode, checked.stdout, checked.stderr))
        diagnostics = io.StringIO()
        handler = logging.StreamHandler(diagnostics)
        logger = logging.getLogger("pypdf")
        logger.addHandler(handler)
        logger.propagate = False
        reader = PdfReader(path, strict=True)
        fields = reader.get_fields() or {}
        form = reader.trailer["/Root"].get("/AcroForm")
        flag = form.get_object().get("/NeedAppearances", False) if form else False
        inspections[path.stem] = {"values": {k: str(v.get("/V", "")) for k, v in fields.items()}, "rotation": reader.pages[0].rotation, "title": reader.metadata.title, "attachments": list(reader.attachments), "text": reader.pages[0].extract_text(), "needAppearances": bool(getattr(flag, "value", flag)), "startxrefCount": path.read_bytes().count(b"startxref"), "qpdfExit": checked.returncode, "sha256": hashlib.sha256(path.read_bytes()).hexdigest(), "pypdfWarnings": diagnostics.getvalue()}
        logger.removeHandler(handler)
    assertions = []
    def check(label, condition):
        assert condition, label
        assertions.append(label)
    for name in ["same-value", "last-value", "within-batch", "across-batches", "invalid-kind-overridden", "snapshot", "engine-sequential"]:
        check(name + " final Name", inspections[name]["values"]["Name"] == "Ada")
    check("across-batches final City", inspections["across-batches"]["values"]["City"] == "Rome")
    check("invalid-length replaced", inspections["invalid-length-overridden"]["values"]["Code"] == "OK")
    for name in ["undrawable-overridden", "flatten-first", "rotation-before-flatten", "engine-staged-equivalent", "interleaved", "engine-interleaved-equivalent", "union-flatten", "all-flatten"]:
        check(name + " Name flattened", "Name" not in inspections[name]["values"] and "Ada" in inspections[name]["text"])
    check("union City flattened", "City" not in inspections["union-flatten"]["values"] and "Rome" in inspections["union-flatten"]["text"])
    check("all fields flattened", not inspections["all-flatten"]["values"])
    for name, expected in {"interleaved": 270, "engine-interleaved-equivalent": 270, "relative-order": 90, "cumulative-rotation": 180, "rotation-before-flatten": 90}.items():
        check(name + " rotation", inspections[name]["rotation"] == expected)
    check("interleaved title", inspections["interleaved"]["title"] == "Final")
    check("attachment removal applied", not inspections["attach-remove"]["attachments"])
    for name, expected in {"invalid-final": "PdfFieldValueException", "undrawable-final": "PdfUnsupportedFormException", "remove-before-attach": "PdfAttachmentNotFoundException", "form-error-first": "PdfFieldValueException", "atomic-append": "PdfFieldValueException", "ordinal-names": "PdfFieldNotFoundException", "engine-bulk-duplicate": "ArgumentException", "engine-bulk-identical": "ArgumentException", "engine-flatten-first": "PdfFieldNotFoundException", "engine-invalid-first": "PdfFieldValueException", "engine-atomic": "PdfFieldValueException", "engine-sequential-partial": "PdfFieldValueException", "engine-rotation-before-flatten": "PdfUnsupportedFormException", "engine-error-order": "PdfAttachmentNotFoundException"}.items():
        check(name + " exception", indexed[name]["error"] == expected)
    for row in rows:
        check(row["name"] + " source unchanged", row["sourceUnchanged"])
        if row["api"] == "workflow":
            check(row["name"] + " description no reads", row["descriptionReads"] == 0)
            check(row["name"] + " borrowed resources open", row["sourceOpen"] and row["destinationOpen"] and row["attachmentOpen"])
            check(row["name"] + " workflow consumed", row["consumed"] == "InvalidOperationException")
            if row["error"]:
                check(row["name"] + " output untouched", row["destinationBytes"] == (len((scratch / "form.pdf").read_bytes()) if row["name"] == "atomic-append" else 0))
            else:
                check(row["name"] + " prefix", row["prefix"])
                check(row["name"] + " one revision", inspections[row["name"]]["startxrefCount"] == 2)
    check("bulk failure preserved pending title", inspections["engine-atomic"]["title"] == "Pending")
    check("bulk failure no partial Name", inspections["engine-atomic"]["values"]["Name"] == "")
    check("sequential failure retains earlier Name", inspections["engine-sequential-partial"]["values"]["Name"] == "Ada")
    check("pending edits outlive editor", inspections["engine-sequential-partial"]["title"] == "Pending")
    check("overridden undrawable value never sets NeedAppearances", not inspections["undrawable-overridden"]["needAppearances"])
    check("sequential undrawable fill leaves sticky NeedAppearances", inspections["engine-undrawable-first"]["needAppearances"])
    for left, right in [("flatten-first", "engine-staged-equivalent"), ("interleaved", "engine-interleaved-equivalent")]:
        check(left + " direct byte identity", (scratch / (left + ".pdf")).read_bytes() == (scratch / (right + ".pdf")).read_bytes())
    # Analogous imperative API: repeated mapping updates are legal; a mapping itself has unique keys.
    precedent = []
    for name, batches, flatten in [("defaults-overrides", [{"Name": "Default"}, {"Name": "Ada"}], False), ("same-value", [{"Name": "Ada"}, {"Name": "Ada"}], False), ("fill-then-flatten", [{"Name": "Ada"}], True)]:
        writer = PdfWriter(clone_from=scratch / "form.pdf")
        for batch in batches:
            writer.update_page_form_field_values(None, batch, auto_regenerate=False, flatten=flatten)
        if flatten:
            writer.remove_annotations(subtypes="/Widget")
        dest = scratch / ("pypdf-" + name + ".pdf")
        writer.write(dest)
        reread = PdfReader(dest)
        checked = subprocess.run([qpdf, "--check", str(dest)], capture_output=True, text=True)
        check("pypdf " + name + " structural check disposition", checked.returncode == (3 if flatten else 0))
        if not flatten:
            check("pypdf " + name + " last value", reread.get_fields()["Name"]["/V"] == "Ada")
        else:
            check("pypdf flattened page text", "Ada" in reread.pages[0].extract_text())
        precedent.append({"name": name, "qpdfExit": checked.returncode, "qpdfStdout": checked.stdout, "qpdfStderr": checked.stderr, "values": {k: str(v.get("/V", "")) for k, v in (reread.get_fields() or {}).items()}, "text": reread.pages[0].extract_text()})
    write("inspection.json", inspections)
    write("precedent.json", {"pypdf": pypdf.__version__, "observations": precedent, "limits": "Mapping API, not a deferred builder; no claim of equal atomicity, permissions, signed-document behavior, or flatten cleanup."})
    write("summary.json", {"scenarioCount": len(rows), "workflowScenarios": sum(r["api"] == "workflow" for r in rows), "engineScenarios": sum(r["api"] == "engine" for r in rows), "inspectedPdfs": len(inspections), "precedentPdfs": len(precedent), "assertionCount": len(assertions), "assertions": assertions, "qpdf": subprocess.check_output([qpdf, "--version"], text=True).splitlines()[0], "python": sys.version, "pypdf": pypdf.__version__})
    print(f"Passed {len(assertions)} assertions across {len(rows)} scenarios; {len(inspections) + 2} clean qpdf checks; one pypdf flatten output has recorded qpdf warnings.")
