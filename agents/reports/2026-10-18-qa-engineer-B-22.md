# B-22. Refresh the evidence inventory

## Task and owner

QA Engineer. Edited only the evidence inventory and the release-readiness note in `agents/state/status.md`. No product code, `ACCEPTANCE.md`, `README.md`, or `backlog.md`. No commit.

**No qualification gate is passed.** No capability is production-qualified.

## Verified facts

Stale statements replaced:

- B-04: both providers reject a mismatched imprint, digest algorithm, or nonce, and the file is not extended. pyHanko 0.38.0 trusted the token on `net8.0`. The `net6.0` interop run stays not run.
- B-09: an info edit that does not set `ModificationDate` writes `ModDate` in UTC.
- B-10: the metadata appendix is 886 bytes, under 8 KiB.
- B-11: a revision 4 RC4 stream decrypts beside an AES string.
- B-12: the support matrix is in `README.md` and separates read-only from modification compatibility. G1 is not passed.
- B-16: Poppler 26.09.0 checks visibility, WinAnsi, alignment with center XMin 174–180, long values, clipping, rotation, CropBox, and font size. G6 is not passed. No second renderer.
- B-19: a throwing external signer and a throwing timestamp authority do not extend the file.
- B-20: `netstandard2.0` and `netstandard2.1` built and packed. The only package dependency is `System.Memory` 4.5.5 on `netstandard2.0`. The suite was not run on those targets. G10 is not passed.
- B-21: header window, revision 2 fill-in bit, repeated `/ID`, inherited `/DA`, `Yes`/`Off`, radio curves, cross-stream `Sign`, and `SignedCms` on `net8.0` only. The unfilled `Blue` absence is not a pass.

Still open: fewer than 50 fixtures and fewer than four producers, no pdfsig, no timestamp authority besides pyHanko, no approved memory budget, no fuzz harness, and no independent security review.

The release-readiness note no longer says the bar has not been assessed. It says no gate is passed and no capability is production-qualified.

## Changes made or proposed

`agents/state/status.md`: evidence inventory, including the qualification-gate table, and the release-readiness note.

## Handoff

Technical Lead reviews. The Product Manager is not asked to accept a gate from this report.
