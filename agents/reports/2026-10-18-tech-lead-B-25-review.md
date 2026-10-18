# B-25 review

## 1. Task and owner

Technical Lead. Review of the B-25 test-only diagnostic checks. No code, test, or state edit. G9 is not passed.

## 2. Verified facts

The two revised tests stay inside the assigned files. They inspect the exception message, `ToString()`, source, help link, data, and inner exceptions. The open test checks the rejected, user, and owner passwords and the fixture file key. The sign test checks the user and owner passwords and the file key held after a successful encrypted open. Assertion failure text names only the secret kind.

The scoped run passed 33/33 on `net8.0` and 33/33 on `net6.0`. This included all `OpenTests` plus both revised B-25 tests. The four library targets built with zero warnings and zero errors, and `git diff --check` passed.

## 4. Defects confirmed

Approval is refused because the assigned acceptance is incomplete. The board requires failing-open and failing-sign evidence for passwords, a PIN, and key material. The revised B-25 sign test uses `ThrowingSigner`, which receives only the hash and throws a fixed message; no PIN reaches that failure path. The implementer report correctly withdraws the PIN and private-key claim, and QA signs off only the narrower password and file-key evidence.

The separate existing `external_signer_diagnostic_is_sanitized_and_does_not_extend_the_file` test is not identified as a B-25 change or evidence in either B-25 report and depends on production sanitization outside B-25's test-only boundary. It does not make the submitted B-25 change meet its assigned acceptance.

## 5. Evidence missing

A boundary-compliant disposition for the PIN requirement is missing. The task must either cite valid pre-existing evidence through the Product Manager's accepted scope or be re-scoped; the revised tests cannot claim that a value never supplied to the operation was omitted.

## 7. Verification run

SDK `10.0.100-rc.1.25451.107`.

- Scoped `OpenTests` and both B-25 facts: 33 passed on `net8.0`; 33 passed on `net6.0`.
- Library build: `net8.0`, `net6.0`, `netstandard2.1`, and `netstandard2.0` succeeded, zero warnings and errors.
- `git diff --check`: passed.

## 9. Handoff

Product Manager and QA Engineer reconcile the PIN acceptance with the test-only boundary. G9 remains unpassed.
