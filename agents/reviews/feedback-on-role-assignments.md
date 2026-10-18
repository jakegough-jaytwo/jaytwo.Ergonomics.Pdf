# Feedback on `role-assignments.md`

Date: 2026-10-18

## Overall

This review is direct, code-aware, and unusually good at predicting how the prompts will behave when given to agents. The sections on prompt duplication, the current one-row-per-widget API, stale work being mistaken for backlog, and inconsistent role names are especially valuable.

The main needed revision is temporal: the replacement `AGENTS.md` and current tests make several conclusions about SoftHSM, Poppler, and remaining interoperability work incorrect. A few proposed governance rules are also too rigid for a shared-file codebase.

## Strongest parts

- The review distinguishes prompt intent from likely agent behavior. The observation that concrete historical lists will be treated as assignments is persuasive.
- The AcroForm critique is specific to the actual API rather than arguing from an idealized model. Calling out the illustrative `PdfFormField` redesign is useful.
- The review correctly preserves the best technical material in the Signing role: document digest versus signed-attributes digest, certificate embedding versus trust, and CMS timestamp versus document timestamp.
- The missing ownership for Metadata, Attachments, and documentation upkeep is clearly identified.
- The recommendation to reduce each role to ownership, boundaries, and current assignment is appropriate.

## Corrections required after the new `AGENTS.md`

### SoftHSM is an explicit test requirement

The review says there is no adapter in this repository, that SoftHSM tests a host component outside the repository, and that SoftHSM/in-library PKCS#11 testing should be removed. That is no longer correct.

`AGENTS.md:9` now requires `SigningInteropTests` to use SoftHSM 2. The existing test project contains a test-only host implementation built with `Net.Pkcs11Interop`. This preserves the product boundary: the shipping library still exposes `IPdfExternalSigner` and does not P/Invoke PKCS#11, while the test suite proves that the contract works with a real PKCS#11 mechanism.

Revise the recommendation to say:

- keep SoftHSM and the adapter in the test project;
- prohibit PKCS#11 dependencies in the runtime project;
- verify that the test adapter implements the exact raw-SHA-256 `SignHash` contract and does not accidentally hash twice.

### Poppler rendering evidence now exists and is required

The review treats rendered appearance checks as open work and says the required external tools are only qpdf and pyHanko. The replacement charter names `AppearanceRenderTests` and Poppler explicitly, and the test file is present.

The useful remaining question is no longer "add rendered appearance validation" but "does the existing Poppler test cover the accepted appearance subset with stable, meaningful assertions?"

### Re-audit the claimed open backlog

The conclusion that the open work is rendered appearances, a real/independent TSA, and SoftHSM validation is outdated. `SigningInteropTests` now includes independent embedded-chain, RFC 3161, and SoftHSM scenarios. The dense large-file measurement may remain open, but the other items need evidence-based reclassification rather than repetition.

## Recommendations to refine

### `AGENTS.md` is not crowded out by prompt length

The review says whichever document is pasted as the system prompt will win. Instruction authority is not determined by length, and repository instructions are not necessarily replaced by a longer role document. The real problems are duplicated context, maintenance drift, and reduced attention to the role's unique duties.

Rephrase this as an operational reliability concern, not an instruction-precedence claim.

### Do not make `PdfEngine.cs` permanent Technical Lead territory

Making the file Technical-Lead-only would turn the integrator into a bottleneck and separate subsystem expertise from implementation. Prefer task-level exclusive ownership:

- Technical Lead approves the change boundary and integration order.
- One active task owns `PdfEngine.cs` at a time.
- The relevant specialist may implement the change.
- Technical Lead reviews and integrates it.

### Do not idle all specialists during baseline work

The recommendation that Core, AcroForm, and Signing wait for PM and Technical Lead output avoids duplication but wastes safe parallelism. Their first work can be read-only and narrowly scoped:

- PM produces priorities and acceptance framing.
- Technical Lead produces boundaries and integration risks.
- QA inventories evidence independently.
- Specialists audit only their own subsystem against the current `ACCEPTANCE.md`, without proposing a separate product backlog.

The Technical Lead can then reconcile those reports before any shared-file edits begin.

### The QA source-code prohibition is too absolute

"QA does not modify `src/`" protects independence, but it can also block a small testability fix or a defect repair when staffing is constrained. The stronger rule is that QA may not independently validate its own substantive production change. Any QA-authored runtime change should receive Technical Lead review and independent verification by another role.

### Give the Core ownership recommendation a workload rationale

The claim that a seventh role is not worth it may be right, but the review does not establish it. Metadata/XMP and Attachments contain product semantics distinct from low-level parsing and already interact with encryption, certification, name trees, XML preservation, and streaming.

Offer two explicit options:

1. Assign both to Core when the backlog is small, with Technical Lead attention to overload.
2. Add a combined Document Features role when those features have concurrent or sustained work.

### Clarify target-framework verification

It is useful to flag target-framework policy, but `netstandard2.0` and `netstandard2.1` are library targets rather than directly runnable test targets. The actionable recommendation is a consumer/compatibility test project that references the relevant target, plus compile/package validation. Also keep support-lifecycle policy with the PM and owner rather than treating it as a role-prompt defect.

### Preserve optional-versus-required tool semantics from the new charter

The review rightly insists that skipped checks are not passes. Under the replacement instructions, qpdf, Python/pyHanko/pypdf, Poppler, and SoftHSM are required for their named suites and absence must fail. Other tools such as Acrobat, PDFBox, MuPDF, and pdfsig remain optional evidence unless the acceptance contract is changed.

## Suggested disposition

Retain the review's analysis of scope drift, role naming, public API stability, and prompt behavior. Replace the SoftHSM and rendering conclusions with an audit of the new interoperability tests, make shared-file control task-based rather than role-based, and allow parallel read-only subsystem assessments under a single reporting schema.
