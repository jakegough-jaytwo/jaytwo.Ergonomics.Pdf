# B-18 audit disposition

## Task and owner

Technical Lead review of `2026-10-18-security-auditor-B-18.md` against committed checkpoint `d4b84dd`. The dirty B-34/B-35 worktree was not treated as product code.

## Verified facts

I inspected each cited method with `git show d4b84dd:<path>`. The report's locations and control-flow triggers are accurate. No finding relies on excluded signature validation, trust validation, rendering, or another out-of-charter capability.

| Finding | Disposition | Reason and ownership |
|---|---|---|
| SA-01 | Confirmed High; release blocker | The normal literal-string branch alone checks `StructuralCap`; escape and nested-parenthesis branches bypass it, and number, keyword, name, and hex readers have no token-size cap. Core Engineer. |
| SA-02 | Confirmed High; release blocker | `ParseDictionary` has no member/work bound and each distinct key takes a linear `PdfObject.Dict.Set` scan, producing unbounded quadratic work from untrusted input. Core Engineer. |
| SA-03 | Confirmed High; release blocker | A 64 MiB, one-byte xref-entry layout can drive about 67 million dictionary insertions. Object-stream header tokenization creates the full string, list, and substrings before `/N` constrains use. Core Engineer. |
| SA-04 | Confirmed High; release blocker | `WalkPages` detects repeated object IDs but has no depth check; a distinct acyclic `/Pages` chain recurses until process stack exhaustion. Core Engineer, with AcroForm Engineer consulted for page/field behavior. |
| SA-05 | Confirmed High; release blocker | CMS certificate, chain, signature, and non-HTTP timestamp callback bytes are repeatedly copied before the post-provider `ReservedContentsBytes` check. `AdditionalCertificates` enumeration is also unbounded. Signing Engineer. |
| SA-06 | Confirmed High; release blocker | Only `SignHash` is inside the sanitizing catch. Exceptions from host-supplied `CertificateDer` and `AdditionalCertificates` access escape unchanged. This is a real G9 diagnostic path even though the public API never receives a PIN directly. Signing Engineer. |
| SA-07 | Confirmed High; release blocker | The 8 MiB estimate runs before `BuildSignature` dirties an existing field and counts only already-dirty stream bodies. `WriteAppendix` later serializes the full cloned dictionary with no actual-length guard. Core Engineer owns the writer; Signing Engineer is consulted. |
| SA-08 | Confirmed Medium; blocks the documented certification behavior | The code implements `Include` as allowing named fields and `Exclude` as blocking named fields. PDF FieldMDP semantics are the reverse. README repeats the defect; acceptance covers only `All`. Signing Engineer owns FieldMDP, consulting AcroForm and Core. Because this corrects a public certification guarantee, Product Manager should obtain the narrow owner confirmation required by `team.md` section 9 before changing the README wording. |
| SA-09 | Confirmed Medium; blocks advertised revision-4 interoperability | Omitted revision-4 `StrF` and `StmF` are substituted with `StdCF` instead of `Identity`, contrary to the format default. Core Engineer, with Signing Engineer consulted for signature-dictionary encryption exemptions. |
| SA-10 | Confirmed Medium; absorbed by active B-35 | At `d4b84dd`, `ReadTlv` and the timestamp parser do not require full consumption at top-level or nested levels, and nonce matching returns before checking trailing values. B-35 already claims `Rfc3161TimeStampToken.cs` and malformed-token evidence; strict consumption and signer/attribute ambiguity belong in that task. Signing Engineer now, then QA. |
| SA-11 | Confirmed Medium; blocks predictable malformed-input behavior | `FiltersOf` drops a non-name scalar and non-name array elements, while `ParmsOf` retains positional entries. A malformed stream can therefore be returned raw or decoded with shifted parameters instead of being rejected. Core Engineer. |

No finding is returned or downgraded to Suspected. SA-01 through SA-07 meet the role's High definition and `QUALIFICATION.md` section 13 bars production qualification while they remain unresolved. SA-08, SA-09, and SA-11 are Medium, but each contradicts supported or publicly documented behavior and should be fixed rather than accepted as a limitation. SA-10 is not a separate task if B-35 closes it with regression evidence.

## Evidence missing

- Static inspection confirms reachability and missing bounds, but it does not establish safe numeric limits. QA and the owning engineer must agree on bounded allocation/work assertions before implementation is accepted.
- The fuzz harness and 24 aggregate CPU-hour run remain outstanding. Resource proofs must run in bounded child processes or use allocation/work assertions; QA should not deliberately destabilize the shared test process.
- SA-06 needs regression cases for failures from both certificate properties, not only `SignHash`.
- SA-07 needs an actual serialized-appendix measurement and a no-destination-extension assertion, not the current estimate alone.
- SA-08 needs independent PDF-aware verification of both `Include` and `Exclude` outcomes.
- SA-09 needs a valid revision-4 fixture with omitted defaults and a save/reopen check.

## Changes made or proposed

Changed only this report.

Recommended sequence, preserving the current board:

1. Do not interrupt B-34/B-35. Finish their implementation, QA, Technical Lead review, Product Manager acceptance, and checkpoint. B-35 must include SA-10's strict-DER cases.
2. Board the B-18 fuzz harness next, as already queued. Add the audit triggers as fixed, bounded seeds and regression specifications before starting the 24-hour campaign. Do not count a campaign over code with known High crash/OOM paths as qualification evidence.
3. Board remediation from the clean checkpoint. A Core batch owns SA-01, SA-02, SA-03, SA-04, SA-09, and SA-11. Keep SA-07 separate because it crosses writer/signing ownership and requires an exact append-size design. A Signing batch owns SA-05 and SA-06. SA-08 is a separate certification correction after owner confirmation. Give each task exact source and test claims before work begins.
4. Where separate worktrees are created from the accepted checkpoint, the Core batch and Signing batch may proceed in parallel because their production-file claims do not overlap. QA verification and shared test-file changes remain serialized. On the current dirty `wip` tree, keep editing one task at a time.
5. Run the fixed-seed suite, then the bounded 24 aggregate CPU-hour campaign against the remediated integration candidate. Continue B-14 and B-13 in the recorded order/background arrangement only under the board's claims.

## Verification run

Read-only commands included:

```text
Get-Content -Raw agents/reports/2026-10-18-security-auditor-B-18.md
git show d4b84dd:<cited source path>
git grep -n <cited method or contract> d4b84dd -- src test README.md ACCEPTANCE.md
```

No build, test, proof-of-concept, source edit, test edit, or state edit was performed.

## Handoff

Product Manager: treat SA-01 through SA-07 as release blockers, accept SA-10 into B-35, and seek the narrow owner confirmation for the SA-08 public certification correction. Technical Lead: create exact claims only after the current signing checkpoint. QA: turn all eleven triggers into bounded regression/fuzz seeds and withhold G8 sign-off until the High findings are remediated and the required fuzz run completes.
