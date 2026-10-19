# QA report: B-39 FieldMDP follows ISO 32000

## 1. Task and owner

QA Engineer. Independent verification of B-39 (D-24, audit SA-08), implemented by the Technical Lead in `4ffb169`. Baseline: branch `wip`, HEAD `4ffb169`, clean tree.

**QA sign-off: given for B-39 as scoped.** `Include` locks the named fields, `Exclude` locks every field except them, `README.md` and `ACCEPTANCE.md` say so, and pyHanko 0.38.0 independently confirms both outcomes. One related defect, QA-B39-01, is filed below. It predates B-39 and is outside D-24's wording, but it blocks qualifying FieldMDP.

## 2. Verified facts

- Gate: `PdfEngine.FieldMdpBlocks` (`src/jaytwo.Ergonomics.Pdf/Internal/PdfEngine.cs` ~2329). It collects `/Fields` text strings into a set and compares them exactly (ordinal) with the fully qualified name of the terminal field being filled. `Include` locks `names.Contains(fullName)`. `Exclude` locks `!names.Contains(fullName)`. A missing `/Action` or `TransformParams` is treated as `All`. `EnsureFieldChange` applies the DocMDP `P` 1 check first, then FieldMDP from every signed signature field's `/Reference`. It is called by `SetText`, the check box, the radio, and the choice setters.
- Edge cases, now tested in `AcroFormTests` (form with `Locked`, `Open`, and the nested `Group.Child`):
  - A nested full name in `/Fields`: `Include [(Group.Child)]` locks only `Group.Child`. `Exclude [(Group.Child)]` leaves only `Group.Child` fillable. Pass.
  - Partial and near names do not match: `(Child)`, `(Gro)`, `(Group.Chil)`, and `(group.child)` neither lock `Group.Child` under `Include` nor free it under `Exclude`. Pass.
  - Empty `/Fields []`: `Include` locks nothing, and `Exclude` locks everything. Pass.
  - DocMDP `P` 2 plus FieldMDP (`Include [(Locked)]`, `Exclude [(Open) (Group.Child)]`, `All`): the FieldMDP result applies per field, metadata stays blocked, and `Sign` is allowed. Pass.
  - Missing `/Fields`: not tested, because ISO 32000 requires the key but does not say what a reader does without it. From the code, `Include` without `/Fields` locks nothing and `Exclude` without it locks everything. An unknown `/Action` locks nothing. See O1.
- Mutation check: with `PdfEngine.cs` reverted to `4ffb169^` in a scratch copy of HEAD, every new Include/Exclude case and the new interop test fail on net8.0. Only the `/All` row passes, as expected. The new tests catch the SA-08 defect.
- pyHanko 0.38.0, used through `certify_samples.py <dir> fieldmdp`, produced three signed files over `Locked`, `Open`, and `Group.Child`: an approval signature whose field `/Lock` is `Include [Locked]`, an approval signature whose lock is `Exclude [Open, Group.Child]`, and a P 2 certification with the lock `Include [Locked]`. pyHanko writes a `/FieldMDP` SigRef in the signature's `/Reference`. For the certification, it also writes the DocMDP SigRef and `/Perms`.
  - On each file, pyHanko's own `FieldMDPSpec.is_locked` (from `EmbeddedPdfSignature.fieldmdp`) reports `Locked`=locked, `Open`=open, and `Group.Child`=open. The library throws `PdfCertificationLockedException` exactly for pyHanko's locked fields and fills the open ones.
  - After the library fills `Open` and `Group.Child` and calls `SaveIncremental`, the original bytes are a prefix, and on reopening `Locked` is still empty and the title survives. pyHanko validates the original signature: intact, valid, trusted, `docmdp_ok`, modification level `FORM_FILLING`, and coverage `ENTIRE_REVISION`.
  - Property established: for fully qualified terminal names, the library's `Include` and `Exclude` lock decisions match pyHanko's, and the library's allowed fills are accepted modifications under pyHanko's diff policy for both actions. Level: independently validated.

## 4. Defects confirmed

### QA-B39-01: a non-terminal name in FieldMDP `/Fields` does not cover its descendants

- **Summary:** The gate matches only an exact terminal full name. pyHanko treats a listed non-terminal field as covering its whole subtree (`fields.py`, `FieldMDPSpec.is_locked`: `field_name.startswith(scoped + ".")`).
- **Impact:** Under `Include [(Group)]`, the library allows a fill of `Group.Child`, and pyHanko then rejects the signed revision as an illegal modification. This is the same failure class as SA-08: a fill allowed against the signer's lock. Under `Exclude [(Group)]`, the library refuses `Group.Child`, which pyHanko leaves fillable. That is an over-strict refusal and does not break a guarantee.
- **Reproduction:** Run `certify_samples.write_fieldmdp_form` with `sign(..., lock=FieldMDPSpec(INCLUDE, ["Group"]))`. Open the result with `PdfDocument`, call `AcroForm.SetText("Group.Child", "Ada")` (it succeeds), then call `SaveIncremental`. Validate the result with `validate_signature.py <file> - - revision`.
- **Expected:** ISO 32000-1 12.8.2.4 and Table 256 say only "an array of text strings containing field names". They do not say whether a non-terminal name covers its kids. pyHanko 0.38.0 treats it as covering the subtree.
- **Actual:** pyHanko reports `SuspiciousModification: Update of Reference(idnum=8) is not allowed because the form field Group.Child is locked`, `INTACT:TRUSTED,EXTENDED_WITH_OTHER,ILLEGAL_MODIFICATIONS`, `docmdp_ok=false`, `modification=OTHER`. For the `Exclude [Group]` sample, the library refuses `Locked`, `Open`, and `Group.Child`, while pyHanko reports `Group.Child=open`.
- **Severity:** High under the role rubric, because a supported operation fails independent verification. The trigger is narrow: a lock that names a parent field.
- **Suggested owner:** Signing Engineer, with the AcroForm Engineer consulted. ISO is silent, so the Product Manager should confirm subtree semantics before the fix.
- No regression test was added, because it would fail on the current `src/`. The fix should add the `Include [(Group)]` and `Exclude [(Group)]` rows to `fieldmdp_matches_fully_qualified_field_names_only`.

## 5. Evidence missing

- No second independent validator, such as Acrobat, was run. The subtree reading rests on pyHanko alone.
- O1 (Low, observation): an `Include` with no `/Fields`, or an unknown `/Action`, locks nothing. ISO requires `/Fields` for Include and Exclude. The library treats a certification that does not resolve as `P` 1, so failing closed here would be the consistent choice. This is for the Signing Engineer and the Product Manager. No test pins either behavior.

## 6. Changes made

- `test/jaytwo.Ergonomics.Pdf.Tests/AcroFormTests.cs`: adds `fieldmdp_matches_fully_qualified_field_names_only` (6 rows) and `docmdp_form_fill_permission_with_fieldmdp_applies_both` (3 rows), and the helpers `NestedFieldMdpForm` and `AssertFill`.
- `test/jaytwo.Ergonomics.Pdf.Tests/SigningInteropTests.cs`: adds `pyhanko_fieldmdp_include_and_exclude_lock_the_same_fields_and_allowed_fills_keep_the_signature`.
- `Fixtures/certify_samples.py`: adds a `fieldmdp` mode and an optional `lock` parameter on `sign`. The default output is unchanged.
- `Fixtures/validate_signature.py`: adds a `fieldmdp=A|B` check that prints pyHanko's lock verdict per field.
- No `src/` changes, and nothing was committed.

## 7. Verification run

- Tools: pyHanko 0.38.0, pypdf 6.21.1, cryptography 50.0.2, Python 3.13, Windows 11, with the environment from the task brief.
- `dotnet build test/jaytwo.Ergonomics.Pdf.Tests`: 0 warnings, 0 errors.
- `dotnet test -f <tfm> --filter "FullyQualifiedName~AcroFormTests|FullyQualifiedName~SigningInteropTests"`: net8.0 33/33, net6.0 33/33, net48 33/33 passed.
- The mutation check against the `4ffb169^` engine and the QA-B39-01 reproduction both ran in a scratch copy of HEAD, which has been deleted.
- Linux was not run. The new test uses `python` and `ExternalTool.Fixture` only, with no `.exe` names or absolute paths.

## 8. Decisions needed

- Product Manager: whether a non-terminal name in FieldMDP `/Fields` locks its descendants (QA-B39-01), and whether to board it. QA recommends subtree semantics to match pyHanko.
- Product Manager and Signing Engineer: O1, failing closed on a malformed FieldMDP.

## 9. Handoff

Technical Lead: review the test changes and run the checkpoint suite, including Docker. Product Manager: accept B-39, and board QA-B39-01 and O1.

## Re-verification (QA-B39-01 and O1 folded into B-39)

- **Change reviewed:** the Technical Lead's uncommitted `PdfEngine.cs` diff. The new `Names` helper matches the full name, or any prefix of it that ends at a dot, against `/Fields`. This is the same rule as pyHanko's `startswith(scoped + ".")`. A partial segment such as `Gro` still does not match. Any `/Action` other than `Include` or `Exclude` now locks, so an unknown action fails closed. One piece of O1 remains: `Include` with no `/Fields` still locks nothing, and `Exclude` with no `/Fields` locks everything. This is Low, and it is recorded only.
- **pyHanko repro, re-run as part of the interop test.** `certify_samples.py fieldmdp` now also writes `fieldmdp-include-parent.pdf` (`Include [Group]`) and `fieldmdp-exclude-parent.pdf` (`Exclude [Group]`). `pyhanko_fieldmdp_include_and_exclude_lock_the_same_fields_and_allowed_fills_keep_the_signature` now covers all five samples and checks pyHanko's verdict for each field against an expected set:
  - Under `Include [Group]`, the library throws `PdfCertificationLockedException` on `Group.Child` and fills `Locked` and `Open`.
  - Under `Exclude [Group]`, the library fills `Group.Child` and refuses `Locked` and `Open`.
  - In both cases, pyHanko 0.38.0 reports the original signature intact, valid, and trusted, with `docmdp_ok`, `FORM_FILLING`, and `ENTIRE_REVISION`. The original bytes are a prefix of the output. On reopening, the locked fields are empty and the filled fields read `Ada`.
- **Property established**, at the independently validated level: for terminal names and names of non-terminal fields, the library's lock decisions match pyHanko's for `Include` and `Exclude`, and the fills it allows are accepted modifications.
- **QA-B39-01:** resolved.
- **Files changed:**
  - `Fixtures/certify_samples.py`: two more samples in `fieldmdp` mode.
  - `SigningInteropTests.cs`: the test now uses a table of expected locks per sample.
- **Verification run:**
  - `dotnet build`: 0 warnings, 0 errors.
  - `AcroFormTests` plus `SigningInteropTests`: 36/36 passed on each of net8.0, net6.0, and net48.
  - Linux was not run.
- **QA sign-off for B-39: given.**
