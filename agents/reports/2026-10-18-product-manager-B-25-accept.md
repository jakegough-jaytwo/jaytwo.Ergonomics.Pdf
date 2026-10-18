# B-25 acceptance

## 1. Task and owner

Product Manager. Accept or return B-25 under the re-scoped wording in `2026-10-18-product-manager-B-28-B-25.md`. G9 is not passed.

## 2. Verified facts

- QA concurred with the re-scope and signed off in `2026-10-18-qa-engineer-B-25-rescope.md`. Both B-25 tests passed 2/2 on `net8.0` and on `net6.0`. The logs had 0 matches for the passwords, the file-key hex, or the file-key Base64.
- The Technical Lead approved in `2026-10-18-tech-lead-B-25-re-review.md`. No defects were found. Both test targets compiled with no warnings.
- `team.md` section 8:
  1. The criteria are met at the internally tested level.
  2. There is no `src/` change. The library built on four targets with 0 warnings in the first review.
  3. No defect was confirmed.
  4. The failure path is unchanged.
  5. `README.md` and `ACCEPTANCE.md` were not changed, and no criterion was weakened. The PIN clause was corrected because it could not be tested.
  6. The limitations are recorded.
  7. QA signed off, the Technical Lead approved, and the Product Manager accepts here.

**Accepted.**

## 5. Evidence missing

G9 needs its own assessment, covering:
- the PIN line;
- a host signer exception that carries its own PIN;
- host-configured `HttpClient` credentials.

The unaccepted `external_signer_diagnostic_is_sanitized_and_does_not_extend_the_file` is candidate evidence for that assessment. It is not B-25 evidence.

## 6. Changes made

`agents/state/backlog.md`:
- Header line updated.
- B-25 closed.
- B-29 now records that `SignatureTests.cs` is released and that it waits only on Technical Lead boarding.

## 9. Handoff

Technical Lead: update the board, release the B-25 claims, and board B-29.
