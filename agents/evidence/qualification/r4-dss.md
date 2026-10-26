# R4 DSS classification (W-103, G4 evidence)

This record covers independent-tool properties of the current engine and workflow signing paths. It does not resume release qualification, pass a release gate, establish public identity, or claim production qualification.

## Reproducible configuration

- EU DSS 6.3, official [demonstration release](https://github.com/esig/dss-demonstrations/releases/tag/6.3), bundle SHA-256 `60452c53eb395106655340890a207885f165bf844485046e3a1f8535ef8fa944`. Only `WEB-INF/lib/*.jar` is extracted from `ROOT.war`; Tomcat is never run.
- Temurin JDK 21.0.9+10. Windows archive SHA-256 `1c67df516e9795c0b09f5714bfe151da2e3cc988082042f5bbb60d75e4e63fb5`; Linux amd64 archive SHA-256 `810d3773df7e0d6c4394e4e244b264c8b30e0b05a0acf542d065fd78a6b65c2f`. The wrapper reads DSS's Maven version resource and checks both runtime pins.
- Unmodified DSS `policy/constraint.xml`, named `QES AdESQC TL based`, SHA-256 `842c4a877b8ab8c125abf89bb3a4d39599d489ed0cdf75f7e57c13f701e70d4b`. Profile detection, cryptographic results and policy indication are retained separately; qualification errors concerning absent trusted lists are retained in the original reports.
- The documentation shipped inside this exact bundle, `ROOT.war!/doc/dss-documentation.html`, references ETSI EN 319 142-1 V1.2.1 (2024-01), R03, and ETSI EN 319 102-1 V1.4.1 (2024-06), R09. API fields were checked against the pinned classes with `javap`, including `SimpleReport.getSignatureFormat/getIndication/getSubIndication`, `SignatureWrapper.getMessageDigest/isSignatureIntact/isSignatureValid`, and `TimestampWrapper.isMessageImprintDataIntact`. The [DSS documentation](https://ec.europa.eu/digital-building-blocks/DSS/webapp-demo/doc/dss-documentation.html) describes these separate report layers; its live edition may advance.
- Each test creates a local CA and a signer leaf; B-T also uses the existing OpenSSL-authority fixture and its separate CA. Only those roots are trust anchors. Local empty CRLs cover the signer and TSA certificates. No AIA, OCSP, online CRL or trusted-list source is configured. The original policy is not weakened. Each invocation records its explicit validation instant, anchor/CRL hashes and the producing commit. No private key enters evidence.

## Cases and expected properties

Both direct engine and workflow tests exercise the same fixture construction through their respective public signing APIs. Each path has nine B-B cases: RSA 2048, P-256 and P-384, each with SHA-256/384/512; two B-T cases: RSA/SHA-256 and P-256/SHA-384; and B-B on qpdf AES-256 input opened under each of the owner/user passwords. Positive profiles must be `PAdES_BASELINE_B` or `PAdES_BASELINE_T`, with intact digest and signature, a trusted test chain, and signature indication `TOTAL_PASSED`. B-T additionally requires intact/valid timestamp signature, matching imprint, trusted TSA chain and timestamp indication `PASSED` (DSS uses that building-block value, distinct from `TOTAL_PASSED`).

Each path also exercises B-B and four controls: legacy basic output must be `PKCS7_B` with valid cryptography; changing a signed byte must fail its digest; a corrupt signature value must fail signature verification; and an otherwise valid token from another signature must fail its timestamp imprint. No negative profile label is counted as successful verification.

## Finding DSS-6.3-T-label: timestamp label survives a mismatched imprint

**Observed:** replacing only the unsigned signature-timestamp attribute in a valid B-T fixture with a valid token from a different signature preserves the original PDF/CMS signature. DSS 6.3 reports `profile=PAdES_BASELINE_T` and base-signature `TOTAL_PASSED`, while its timestamp has `signatureIntact=true`, `signatureValid=false`, `imprintIntact=false`, `FAILED/HASH_FAILURE`.

**Expected:** a claimed, valid B-T result needs a matching timestamp imprint. The label and base-signature conclusion alone cannot establish that property. This deliberate negative fixture demonstrates a disagreement between the profile label and timestamp verification, not a defect in the library's positive output.

**Impact/severity:** medium test-tool limitation: consumers relying on the label alone could overstate a B-T result. **Disposition:** retain the fixture and all original reports, explicitly assert the mismatch and its exact indication/subindication, and require successful timestamp verification in every positive B-T assertion. The label is preserved verbatim rather than normalized or hidden. No product behavior or profile expectation for valid output changes.

## Execution and retained evidence

Source implementation: `71f9971dc3f48742c0984ef48f7e9778f2e4bb53`. CI host selection: `32897fdd7430393c6333dfe86ec416cf2592d033` (only Makefile/work-item changes; identical product, wrapper and test sources).

Windows 11, .NET net10.0: `make quiet-test TEST_FRAMEWORK=net10.0 TEST_FILTER=FullyQualifiedName~DssInteropTests` passed 14/14; the same command with `TEST_PROJECT=./test/jaytwo.Ergonomics.Pdf.Tests` passed 14/14. Both revision-bound runs set `JAYTWO_EVIDENCE_COMMIT=71f9971` and `JAYTWO_DSS_EVIDENCE=out/dss/final-windows`. The Windows installer exercised both DSS extraction and the pinned JDK extraction branch. `make quiet-build` passed the full solution with warnings as errors. External input tools: qpdf 12.3.2, OpenSSL 3.6.5.

[Windows original reports and public fixtures](r4-dss-windows.zip), archive SHA-256 `f3d24bd24db54a3c317ddfd7957b4134be31dba394a73a2235d750660f59fdf8`; [searchable result/checksum index](r4-dss-windows.json). All 36 invocations completed; 24 positive B-B, four positive B-T and two legacy controls passed their respective properties. Four negative byte/signature controls failed as expected. The two mismatched-timestamp controls have the explicitly recorded failed timestamp result despite the T label and base-signature pass. There were no unsupported or not-run Windows cases. Each case retains its PDF, test roots/CRLs, exact validation time, signature identity, algorithms, policy, indications, four original XML reports, process exit status and stdout/stderr. The archive inventory hashes every retained file, including reports.

Recreate an archive with `python tools/test-tools/dss/collect.py <evidence-directory> <archive.zip>`. Private keys are generated only in a temporary fixture directory and deleted after the case; they are not retained.

Linux focused runs used the separate worktree at `d2cbbc7c83b976134722a5a1f05a5d0aeb2cbe35` (the committed bounded-download recovery; product, wrapper and test sources are unchanged from `71f9971`). Ubuntu 26.04.1 LTS, Temurin 21.0.9+10, qpdf 12.3.2 and OpenSSL 3.5.5: the same focused net10.0 filter passed 14/14 in each project. `JAYTWO_EVIDENCE_COMMIT=d2cbbc7` and `JAYTWO_DSS_EVIDENCE=/build/out/dss/linux-focused` were passed through make. The running image's Makefile, wrapper and downloader hashes matched the worktree exactly.

[Linux focused original reports and fixtures](r4-dss-linux-focused.zip), archive SHA-256 `b95538331216765e69a3f1a3b988b68661e962b969a77af622aabbce7b2be423`, and [result/checksum index](r4-dss-linux-focused.json) retain another 36 completed invocations with the same expected properties and no unsupported/not-run cases. All 498 retained file hashes were verified. The 36 case sets agree across Windows/Linux on profile, algorithms, digest/signature results, trust-policy indication/subindication and timestamp results; signature identities and fixture hashes differ as expected for freshly generated keys.

The separate-worktree integration command `make quiet-docker-test DOCKER_TAG=jaytwo_ergonomics_pdf_w103_d2cbbc7 "DOCKER_RUN_MAKE_TARGETS=test JAYTWO_DSS_EVIDENCE=/build/out/dss/linux JAYTWO_EVIDENCE_COMMIT=d2cbbc7"` passed at `d2cbbc7` in 13m38s. This wraps the required `make docker-builder docker-test`. All builds passed with warnings as errors; fuzz smoke passed. Framework results, including the serial clock hosts:

| Project | Framework | Passed | Platform skips | Failed |
|---|---|---:|---:|---:|
| Engine | net10.0 | 4674 | 0 | 0 |
| Engine | net8.0 | 4200 | 0 | 0 |
| Engine | net6.0 | 4200 | 0 | 0 |
| Engine netstandard2.0 lane | net8.0 | 4200 | 0 | 0 |
| Workflow | net10.0 | 166 | 2 | 0 |
| Workflow | net8.0 | 152 | 2 | 0 |
| Workflow | net6.0 | 152 | 2 | 0 |

Total: 17,744 passed, six existing platform skips, zero failures. The two workflow skips per framework are `WorkflowFileSaveTests.a_destination_that_differs_from_the_input_only_by_case_is_refused` (Windows/OSX only) and `WorkflowExportTests.export_reads_no_bytes_that_the_engine_does_not` (Windows only). Neither is a tool-availability skip. Both net10.0 project TRX files explicitly contain all 14 DSS cases as Passed; none of W-103's checks was skipped, unsupported or not run.

[Linux integration original reports, fixtures, full gate log and TRX results](r4-dss-linux-integration.zip), archive SHA-256 `0f131b87a310963c8a14ad91a035e08a0854f5c6c4c0086c151d029f9fedbf48`, and [result/checksum index](r4-dss-linux-integration.json) retain the 36 full-gate DSS invocations. All 511 retained file hashes were verified. Every archive contains the full expected 36-case set, completed execution status, and the original report files. The hashes in each external JSON index also identify every fixture and report inside its ZIP.

The first Docker attempt was interrupted in the single-connection EU bundle download before tests. The replacement downloader was executed against the official server and verified the same complete archive hash, then the Linux image built successfully with it. A separate Windows launcher attempt selected `cmd.exe` for the POSIX make recipe and ran no tests; selecting Git Bash explicitly corrected the launcher. Neither attempt is counted as a pass. On Windows, Docker make recipes were run with `SHELL=C:/Program Files/Git/bin/bash.exe`, `MSYS_NO_PATHCONV=1`, and `MSYS2_ARG_CONV_EXCL=*`.
