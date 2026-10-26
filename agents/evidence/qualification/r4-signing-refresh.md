# R4 signing refresh (W-181)

This record refreshes named signing properties for the engine and public workflow layer. It does not pass a release gate or claim production qualification. W-182 owns the full framework and Docker integration checkpoint.

## Verification

Windows net10.0, SDK 10.0.402, runtime .NET 10.0.13: the focused engine selection passed 178/178 cases, and workflow signing/interop passed 48/48 plus two focused certification cases. The latter adds real pyHanko DocMDP P=1/P=2 and FieldMDP inputs: allowed workflow fills retain the exact signed source prefix and pass pyHanko modification analysis and pdfsig signature checks; forbidden metadata, locked-field fills and P=1 signing leave the destination empty. All new workflow evidence tests use only public library APIs. Release solution build passed with StyleCop warnings as errors.

Commands (all coverage off):

```text
python tools/agents/quiet_run.py --name w181-engine --tests -- dotnet test test/jaytwo.Pdf.Tests -f net10.0 -p:CollectCoverage=false --filter "FullyQualifiedName~SigningInteropTests|FullyQualifiedName~SignatureTests|FullyQualifiedName~SigningAlgorithmTests|FullyQualifiedName~OpenSslTimestampTests|FullyQualifiedName~HttpTimestamp|FullyQualifiedName~DocMdp|FullyQualifiedName~FieldMdp|FullyQualifiedName~SeedValue|FullyQualifiedName~CmsFuzzTests" --logger "trx;LogFileName=w181-engine.trx" --results-directory out/w181/test-results
python tools/agents/quiet_run.py --name w181-workflow --tests -- dotnet test test/jaytwo.Ergonomics.Pdf.Tests -f net10.0 -p:CollectCoverage=false --filter "FullyQualifiedName~WorkflowSigningInteropTests|FullyQualifiedName~WorkflowSigningTests" --logger "trx;LogFileName=w181-workflow.trx" --results-directory out/w181/test-results
python tools/agents/quiet_run.py --name w181-certification --tests -- dotnet test test/jaytwo.Ergonomics.Pdf.Tests -f net10.0 -p:CollectCoverage=false --filter FullyQualifiedName~certified_workflow_fills_obey_docmdp_and_fieldmdp --logger "trx;LogFileName=w181-certification.trx" --results-directory out/w181/test-results
make quiet-build CONFIGURATION=Release
make fuzz-smoke
```

Observed tools: pyHanko 0.38.0, pypdf 6.21.1, pdfsig 26.08.0, OpenSSL 3.6.5, qpdf 12.3.2, SoftHSM 2.5.0. Each verifier uses only the explicit fixture anchor, not public identity trust. Earlier attempts that failed NuGet restore or compilation ran no tests and are not passes. An owner-password verifier invocation was corrected to supply this fixture's user password to pdfsig's `-upw`; both signing password roles then passed.

| Property | Evidence and limits |
|---|---|
| Key/digest/format | Engine and workflow cover RSA-2048, P-256 and P-384 with SHA-256/384/512, for basic detached CMS and PAdES B-B; pyHanko verifies the signature and reports the actual mechanism/digest. pdfsig checks signature, exact byte ranges, end-of-file coverage and explicitly anchored trust. |
| Embedded signature timestamp | Existing engine RSA/ECDSA/OpenSSL/HTTP-TSA cases plus workflow new/existing fields: pyHanko checks token integrity, validity and TSA-anchor trust. pdfsig establishes base-PDF signature properties only; it does not verify the timestamp attribute. |
| Document timestamp | Engine and workflow new/existing timestamp fields: pyHanko and OpenSSL validate the token over exact signed ranges. Workflow edits and timestamp occupy separate revisions. Poppler does not validate ETSI.RFC3161 document timestamps. |
| Encryption | Existing engine qpdf RC4/AES/password matrix is preserved; workflow AES-128 fill-and-sign passes under user and owner roles. Signature integrity, trust, and prefix preservation are separate assertions. |
| Later revisions and tamper | Permitted fills preserve earlier signature bytes; pyHanko reports ENTIRE_REVISION and FORM_FILLING with policy permitted, pdfsig reports valid signature and partial-file coverage. Both detect the modified signed byte control. |
| Failure guarantees | Existing engine and workflow tests cover host/TSA failure, reservation overflow, cancellation, certification refusal, destination copy failure and staging cleanup. No callback or algorithm contract changed. |
| DSS classification | Incorporates W-103's [DSS record](r4-dss.md), original Windows/Linux archives and result indices. DSS profile labels, cryptographic results and trust-policy indications remain separate. The recorded DSS-6.3 mismatched-timestamp T-label limitation remains; a T label is not a timestamp verification pass. |

## Release resource observations

Each new workload runs alone in its own Release test process with coverage disabled, using real file-backed data. Windows 10.0.26100, .NET 10.0.13, X64, 8 logical CPUs. The existing 256 MiB simple-operation budget is the assertion ceiling. Fixture generation and verification are outside the measured operation. Managed heap is sampled about every millisecond plus a final sample, including uncollected garbage; native memory and filesystem cache are not measured. These are observations of the named workloads, not a proof for every PDF or a before/after performance comparison.

| Workload | Input/payload bytes | Output bytes | Peak managed growth | Largest input read | Staging bytes at signer callback | Total logical disk bytes at callback |
|---|---:|---:|---:|---:|---:|---:|
| Dense 64-page RGB PDF + metadata + sign | 1,073,916,019 | 1,073,951,819 | 4,435,776 | 1,048,576 | 1,073,917,637 | 2,147,833,656 |
| 1 GiB random attachment + metadata + sign | 488 + 1,073,741,824 | 1,073,777,488 | 1,794,744 | 65,536 | 1,073,743,806 | 2,147,486,118 |

Measured operation times were 6.016 and 10.375 seconds, respectively, with 410 and 698 heap samples. Timing is descriptive, not a threshold. Both verified the complete source prefix through 64 KiB comparison buffers, reopened the signature and metadata, and observed no temporary siblings after publication. Attachment extraction's SHA-256 equals the original file's SHA-256. The destination does not exist while the signer is called. Disk figures are logical file lengths, not allocated blocks or a continuously sampled peak; at this callback the private staging file is the eventual published output, so it is not an additional permanent output copy. The measured managed growth and read bounds support absence of whole-document managed buffering in these workloads; staging uses disk in proportion to output size.

Reproduce each workload separately:

```text
dotnet test test/jaytwo.Ergonomics.Pdf.Tests -c Release -f net10.0 -p:CollectCoverage=false --filter FullyQualifiedName~dense_gibibyte_workflow_signing_uses_bounded_memory_and_file_staging
dotnet test test/jaytwo.Ergonomics.Pdf.Tests -c Release -f net10.0 -p:CollectCoverage=false --filter FullyQualifiedName~gibibyte_attachment_workflow_signing_uses_bounded_memory_and_file_staging
```

Set `JAYTWO_R4_EVIDENCE` to retain JSON observations and public workflow verifier artifacts. The dense test itself exited 0 and reported one passed test; its quiet wrapper incorrectly reported failure because the detailed console format did not match its summary parser. This is not relabeled as a wrapper pass. The attachment test and wrapper both passed.

## Fuzz campaign

The deterministic nine-target smoke passed every seed and 2000 mutations per target without uncontrolled failure. The first instrumented campaign failed before mutations during seed generation. W-183 corrected runner ordering and added a real instrumented startup regression (old runner fails; fixed runner passes CMS and RFC 3161 with zero artifacts). The full allocation restarted from clean commit `704ba5c` with no engine changes and completed both targets: CMS 3680 seconds/29,971,180 reported executions; RFC 3161 3679 seconds/32,803,061 reported executions; both exits 0 and zero artifacts. The [campaign record](../../reports/2026-10-25-W-181-fuzz.md) records hashes, limits, durations, coverage observations, the first failure and its disposition, launcher limitations and claim boundaries. The completed allocation is W-181 evidence, not a full G8 pass.

Original reports, public PDF/anchor fixtures, TRX records, resource JSON, tool versions and campaign output are retained in [r4-signing-w181.zip](r4-signing-w181.zip), SHA-256 `ab4b067fe93dfb362b06c11df8d3b407fdafb4b0c8ba0c635ab5154d76313ac8`, with a [checksum/result index](r4-signing-w181.json). All 549 retained files were read back and their hashes verified. Tests first shipped in `88a57b1`; certification tests and this evidence are at `c54bfc0`. Engine runtime is unchanged from the W-104 checkpoint. Final record/archival closeout: 2026-10-26.
