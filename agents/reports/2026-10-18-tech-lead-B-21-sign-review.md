# B-21 clauses 8 and 9 review

## 1. Task and owner

Technical Lead. Review of the cross-stream sign test and the `SignedCms` source boundary. No edit to `SignatureTests.cs`, `backlog.md`, or product code. The task is not accepted. QA still signs off.

## 2. Verified facts

Clause 8 is approved. No defects.

`sign_to_another_stream_leaves_the_opened_stream_unchanged` opens a writable stream and calls `Sign(Stream, PdfSignatureOptions, IPdfSignatureProvider, leaveOpen)` with a different destination. The opened stream's bytes stay equal to the input. The destination is longer than the input, and its prefix equals the input. A different destination is copied, then the appendix is written there. The opened stream is not that destination.

The Signing Engineer recorded 1 passed and 0 failed on `net8.0`. This review did not rerun it. `net6.0` was not run.

Clause 9 is a source fact. No new test and no project-file change.

`cms_signature_verifies_and_covers_the_byte_range` and `cms_embeds_the_certificate_chain_and_a_timestamp_over_the_signature_value` each call `SignedCms.CheckSignature(verifySignatureOnly: true)` inside `#if NET8_0_OR_GREATER`. The `using System.Security.Cryptography.Pkcs` directive and `MessageDigest(SignedCms)` are inside that same symbol. `jaytwo.Ergonomics.Pdf.Tests.csproj` references `System.Security.Cryptography.Pkcs` 10.0.13 only when the target is `net8.0`. The test project targets `net8.0` and `net6.0`. The `net6.0` build does not get that package, and those calls are not compiled for it.

## 6. Changes made or proposed

This report. B-12 is removed from `agents/state/board.md`. G1 stays unpassed. Clauses 8 and 9 stay until QA signs off and the Product Manager accepts them. The Core and AcroForm rows stay because `agents/reports/2026-10-18-product-manager-B-21-core-acroform.md` does not exist.

## 9. Handoff

QA Engineer signs off. The Product Manager accepts after that. This review does not accept the task.
