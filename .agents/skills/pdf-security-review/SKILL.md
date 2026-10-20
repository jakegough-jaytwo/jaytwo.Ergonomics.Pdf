---
name: pdf-security-review
description: Perform a read-only hostile-input and cryptographic security review. Use when explicitly requested or when collecting QUALIFICATION gate G8 review evidence, not for ordinary changes.
---

# PDF security review

Review only. Do not modify product code, tests, fixtures, or state unless the user separately requests remediation or documentation updates.

## Independence

A persona label does not create independence. If the reviewer implemented the affected code in the same working context, label the result an internal review. To claim independent-review evidence, use a human reviewer or a fresh agent context that did not implement the code. Delegation requires the user's explicit request.

## Scope

Review the requested subset of parser and xref handling, object and stream decoding, decompression limits, standard security, CMS and DER encoding, signature construction, RFC 3161 processing, DocMDP and FieldMDP enforcement, and failure paths. Consider allocation timing, destination-stream state, and sensitive data in diagnostics.

Assume the PDF, TSA response, and signer callback are hostile. Assume the caller and host process are trusted unless the task says otherwise.

## Findings

Confirm findings from code and, when cheap and authorized, a bounded proof of concept kept outside the repository. Mark each finding Confirmed or Suspected. A suspected finding is not a defect until confirmed. The disposition of each finding (fix, limitation, or release blocker) belongs to `pdf-task-workflow`, not to the review. Each finding includes:

- severity and confidence;
- exact location;
- concrete trigger;
- impact on a supported operation or guarantee;
- evidence and reproduction notes;
- the applicable charter or acceptance boundary.

Critical means code execution or a forged or misleading accepted signature/certification. High means unbounded resource use, a reachable crash from supported untrusted input, or secret disclosure. Medium means bounded excessive resource use or a broken guarantee without security impact. Low covers lesser issues.

Do not record keys, PINs, passwords, or secret material. Treat fixes requiring an excluded feature as limitations or scope proposals, not defects in the current charter.
