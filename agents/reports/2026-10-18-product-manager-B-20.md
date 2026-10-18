# B-20 acceptance

## Task and owner

Product Manager. Accepted the B-20 build and package record. Edited `agents/state/backlog.md` only. G10 is not passed. B-12 and the remaining B-21 clauses are not closed.

## Verified facts

QA recorded the builds in `agents/reports/2026-10-18-qa-engineer-B-20.md`. Debug builds of `netstandard2.0` and `netstandard2.1` each succeeded with 0 warnings and 0 errors. `dotnet pack` rebuilt `net8.0`, `net6.0`, `netstandard2.1`, and `netstandard2.0` and wrote `jaytwo.Ergonomics.Pdf.0.1.0.nupkg`. The package's only dependency is `System.Memory` 4.5.5 on `netstandard2.0`. The other three target frameworks have an empty dependency group. qpdf, pyHanko, Poppler, SoftHSM, and Pkcs11Interop are not in the package. No target was dropped. The test suite was not run on either netstandard target.

The Technical Lead approved the record in `agents/reports/2026-10-18-tech-lead-B-20-review.md`. That review did not rebuild the package, and it does not pass G10. The QA report says the same.

## Handoff

B-20 is closed as a record. G10 stays unpassed. B-12 and the remaining B-21 clauses stay open.
