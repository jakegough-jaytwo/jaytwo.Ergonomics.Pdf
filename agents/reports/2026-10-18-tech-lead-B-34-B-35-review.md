# Technical Lead review: B-34 and B-35

## Task and owner

B-34 (header-phase timeout on .NET Framework) and B-35 (certReq under D-21, including SA-10's strict-DER cases). Implemented by the Signing Engineer (`2026-10-18-signing-engineer-B-34.md`, `-B-35.md`) after the reconstruction in `2026-10-18-tech-lead-B-34-B-35-reconstruction.md`. QA signed off both (`2026-10-18-qa-engineer-B-34.md`, `-B-35.md`). Reviewer: Technical Lead.

## Verified facts

- Charter: there is no new public type, member, or option, and no runtime package. The only public surface change is XML documentation of the existing exceptions. The library does no trust or chain validation. Inclusion of the identified certificate is an input check during creation (D-10).
- B-34: `HttpTimestampAuthority` now owns one deadline token for headers and body, armed `min(50 ms, Timeout/10)` ahead of `HttpClient`'s timer, and passes it to `SendAsync`. Only cancellation caused by that deadline becomes `TaskCanceledException` with a `TimeoutException` inner exception. Any other cancellation keeps its original exception. Response disposal still aborts a token-ignoring body. An infinite timeout stays infinite. The caller's `HttpClient` is not mutated. The effective timeout is up to 50 ms shorter than `HttpClient.Timeout`, which is acceptable.
- B-35: both request overloads write `certReq` as `01 01 FF`. DER omits the FALSE default, so the vector grows from 56 to 59 bytes, and `ACCEPTANCE.md` says so. `Rfc3161TimeStampToken` caps the token at 8 MiB before parsing. It walks a fixed structure iteratively, with no recursion on input nesting. It requires full consumption at each level and minimal DER lengths (`DerCodec.ReadTlv` already rejects indefinite and lengths over 4 bytes). It requires one SignerInfo and one ESS signing-certificate attribute, and that the identified certificate (hash, plus issuer and serial when present) is in the SignedData certificates. This closes SA-10.
- Docs: `README.md` and `ACCEPTANCE.md` replace "no certReq" with certReq true and the inclusion check, and state that inclusion is not trust. The B-31 cap sentences are kept, and the timeout sentence is extended to both phases on every target. No criterion was weakened. The 56-byte vector row was updated in the same change, not removed.
- QA evidence: `SignatureTests` 44/44, `OpenSslTimestampTests` 2/2, and `SigningInteropTests` 4/4 on each of `net8.0`, `net6.0`, and `net48`. pyHanko 0.38.0 validates the OpenSSL token with only the trust root. Release builds for all four library targets have 0 warnings.

## Defects confirmed

None in the change.

## Evidence missing

SA-05 (copies before the reserved-size check) and SA-06 (certificate-property exceptions) are separate Signing tasks. B-35 does not address them.

## Handoff

Approved both. Product Manager to accept.
