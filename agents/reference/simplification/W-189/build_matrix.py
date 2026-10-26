"""W-189 matrix builder. Usage: python -I build_matrix.py (run from the W-189 directory)

Inputs, all produced at the pinned revision: features.json (classify.py over the 51 tracked fixtures),
combine-probe.json (a disposable probe over the public PdfWorkflow.Combine API) and output-features.json
(classify.py over the probe's saved outputs). Outputs: matrix.csv (one row per fixture and operation) and
summary.json. Candidate rules are COUNTERFACTUALS evaluated by read-only classification; no runtime was changed.
"""
import collections
import csv
import itertools
import json
import os

MANIFEST = "../../../../test/jaytwo.Pdf.Tests/Fixtures/corpus/manifest.json"
manifest = {e["file"]: e for e in json.load(open(MANIFEST, encoding="utf-8"))["entries"]}
feat = json.load(open("features.json", encoding="utf-8"))
probe = json.load(open("combine-probe.json", encoding="utf-8-sig"))
outf = {os.path.basename(x["path"]): x for x in json.load(open("output-features.json", encoding="utf-8-sig"))}
TRACKED_PRESERVATION = {"tagged", "outlines", "page-labels", "named-destinations", "xmp", "acroform", "signature-field",
                        "signed", "usage-rights", "embedded-files", "javascript", "xfa", "certified"}
KEPT_CHECK = {"tagged", "outlines", "page-labels", "named-destinations", "xmp", "acroform", "signature-field"}


def provenance(path):
    key = path.split("Fixtures/", 1)[1]
    entry = manifest.get(key)
    if entry:
        return "manifest:" + entry["provenance"]["method"], entry["license"]["terms"][:80]
    if "/defects/" in path:
        return "repo-generated (defects/fixtures.json, generate_foreign_pdfs.py)", "repo-authored; no per-file terms recorded"
    if "focused-assembly" in path:
        return "third-party test file (focused assembly fixtures)", "terms not recorded here: gap"
    return "repo-generated (generate_foreign_pdfs.py)", "repo-authored; no per-file terms recorded"


CATS = {
    "tagged": lambda f: "tagged" in f,
    "signed-or-certified": lambda f: bool({"signed", "certified", "document-timestamp"} & f),
    "usage-rights": lambda f: "usage-rights" in f,
    "xfa": lambda f: "xfa" in f,
    "active-content": lambda f: bool({"javascript", "field-actions", "catalog-actions"} & f),
    "attachments-or-AF": lambda f: bool({"embedded-files", "associated-files", "file-attachment-annotation"} & f),
    "optional-content": lambda f: "optional-content" in f,
    "field-lock": lambda f: "field-lock" in f,
    "outlines-labels-dests": lambda f: bool({"outlines", "page-labels", "named-destinations"} & f),
}
PREP = {"remove-rights": {"usage-rights"}, "abandon-xfa": {"xfa"}, "unsigned-derivative": {"signed-or-certified", "field-lock"}}
ALL_PREP = set().union(*PREP.values())
# Scenario B additionally models an explicitly authorized untagged derivative (charter option); scenario A does not.
ALL_PREP_B = ALL_PREP | {"tagged"}

by = collections.defaultdict(dict)
for p in probe:
    by[p["path"]][p["op"]] = p
fmap = {x["path"]: x for x in feat}
tracked = [x["path"] for x in feat]
OPS = ("combine-all", "combine-page1", "combine-all-consent")


def cats_of(path):
    f = set(fmap[path]["features"])
    return {c for c, fn in CATS.items() if fn(f)}


rows = []
for x in feat:
    prov, rights = provenance(x["path"])
    cats = sorted(cats_of(x["path"]))
    for op in OPS:
        p = by[x["path"]][op]
        reason = p["detail"].split(": ", 1)[-1] if p["result"] == "refused" else ""
        preservation = ""
        if p["result"] == "accepted":
            out = outf.get(os.path.basename(x["path"]))
            if out:
                lost = sorted(set(x["features"]) & TRACKED_PRESERVATION - set(out["features"]))
                kept = sorted(set(x["features"]) & set(out["features"]) & KEPT_CHECK)
                preservation = "lost:" + "|".join(lost) + ";kept:" + "|".join(kept)
        current = p["result"] if p["result"] == "refused" else ("accepted-with-reported-omissions" if p["omissions"] else "accepted")
        rows.append({
            "sha256": x["sha256"], "path": x["path"], "provenance": prov, "rights": rights, "operation": op,
            "selection": "page 1 of %s" % x.get("pages") if op == "combine-page1" else "all pages",
            "occurrences": 1, "credential": p["credential"], "pages": x.get("pages"),
            "features": "|".join(x["features"]),
            "stream_filters": "|".join(f for f in x["filters"] if f.endswith("Decode")),
            "current_result": current, "current_reason": reason, "reported_omissions": p["omissions"],
            "output_preservation_consent_run": preservation if op == "combine-all-consent" else "",
            "candidate_blocking_categories": "|".join(cats), "classification_errors": "|".join(x["errors"]),
        })
with open("matrix.csv", "w", newline="", encoding="utf-8") as fh:
    writer = csv.DictWriter(fh, fieldnames=list(rows[0]))
    writer.writeheader()
    writer.writerows(rows)

summary = {"denominators": {"tracked_pdfs": len(tracked), "unclassifiable": [x["path"] for x in feat if x["errors"]]}}
for op in OPS:
    acc = [p for p in tracked if by[p][op]["result"] == "accepted"]
    newly = {c: sorted(p for p in acc if c in cats_of(p)) for c in CATS}
    union = sorted(set().union(*map(set, newly.values())))
    core = [p for p in union if cats_of(p) - {"outlines-labels-dests"}]
    prepared = [p for p in core if (cats_of(p) - {"outlines-labels-dests"}) <= ALL_PREP]
    summary[op] = {
        "accepted_now": len(acc), "refused_now": len(tracked) - len(acc), "of": len(tracked),
        "newly_refused_by_category_of_accepted": {c: len(v) for c, v in newly.items()},
        "pairwise_intersections": {a + " & " + b: n for a, b in itertools.combinations(CATS, 2)
                                  if (n := len(set(newly[a]) & set(newly[b])))},
        "union_all_categories": len(union),
        "union_excluding_outlines_labels_dests": len(core),
        "core_accepted_after_all_named_preparations": len(prepared),
        "core_accepted_after_all_named_preparations_files": sorted(os.path.basename(p) for p in prepared),
        "core_still_blocked": sorted(os.path.basename(p) for p in core if p not in prepared),
        "scenarioB_with_untagged_derivative_accepted": len([p for p in core if (cats_of(p) - {"outlines-labels-dests"}) <= ALL_PREP_B]),
        "scenarioB_still_blocked": sorted(os.path.basename(p) for p in core if not (cats_of(p) - {"outlines-labels-dests"}) <= ALL_PREP_B),
        "files_by_category": {c: [os.path.basename(p) for p in v] for c, v in newly.items()},
    }
summary["current_refusal_reasons_combine_all"] = dict(collections.Counter(
    r["current_reason"][:90] for r in rows if r["operation"] == "combine-all" and r["current_result"] == "refused"))
summary["page1_result_equals_all_pages_result_for_every_fixture"] = all(
    by[p]["combine-all"]["result"] == by[p]["combine-page1"]["result"] for p in tracked)
loss = collections.Counter()
accepted_consent = 0
for r in rows:
    if r["operation"] == "combine-all-consent" and r["current_result"].startswith("accepted"):
        accepted_consent += 1
        for token in r["output_preservation_consent_run"].split(";")[0][5:].split("|"):
            if token:
                loss[token] += 1
summary["silent_feature_loss_on_accepted_consent_run"] = {"accepted": accepted_consent, "feature_dropped_count": dict(loss)}
count = lambda items: dict(collections.Counter(items))
summary["feature_counts_51"] = count(f for x in feat for f in x["features"])
summary["feature_counts_41_manifest_entries"] = count(
    f for x in feat if x["path"].split("Fixtures/", 1)[1] in manifest for f in x["features"])
summary["decode_filter_file_counts_51"] = count(f for x in feat for f in set(x["filters"]) if f.endswith("Decode"))
summary["files_with_any_decode_filter_51"] = sum(1 for x in feat if any(f.endswith("Decode") for f in x["filters"]))
json.dump(summary, open("summary.json", "w"), indent=1)
print(json.dumps(summary, indent=1))
