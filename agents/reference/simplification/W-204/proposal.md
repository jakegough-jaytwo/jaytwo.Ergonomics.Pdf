# W-204 exact wording proposal — not adopted

## Disposition for PLAN 15 / O 09

Proposed owner decision:

> Retain the workflow ordering and duplicate-fill contract in phase-1 §6.7 F5. A workflow is a deferred description, not an imperative edit log. Fills are collected into one all-or-nothing engine bulk fill containing the last declared value for each dotted field name, matched ordinally. Earlier assignments to that name are not sent to the engine or validated against the document. Flatten runs once after filling, across the union of named requests, or across every eligible field when any request requests all fields. All other operations execute afterwards in their own declaration order, without inferred final-state reduction or suppression of intermediate engine exceptions. Do not add duplicate/contradictory-fill rejection or switch to global declaration order as cleanup.

F5 itself needs no edit to retain the decision. No AGENTS.md or QUALIFICATION.md change is proposed. The text below is optional future explanation for `docs/behavior.md`, subject to the owner's adoption; this research does not change it or the spec.

## Optional explanatory paragraph and example

> Repeating a field name is an override: the workflow sends only its final value to the engine, whether repeated in one `FillForm` enumerable or across several `Fill` and `FillForm` calls. A final invalid value still fails the entire fill before output. For example, `Fill("Code", "TOOLONG").Fill("Code", "OK")` validates `OK`, not the discarded draft. Field names are matched ordinally. Validate duplicate business inputs in the caller if they must be treated as errors. Describing a flatten before a fill still fills first; metadata, attachment and rotation calls retain their order relative to each other after the form stages.

```csharp
PdfWorkflow.FromFile("order.pdf")
    .FillForm(defaults)
    .FlattenForm("Customer")
    .Fill("Customer", customerName)
    .WithTitle("Executed Order")
    .Save("order-final.pdf");
// The final Customer value is filled before flatten; title runs after form operations.
```

## Retained lifecycle and safety wording

> Each run opens the original input once and applies operations through one editor. Ordinary saves append at most one revision. Description is deferred and copies caller collections; caller-supplied streams and sources remain caller-owned. A terminal consumes the builder even on failure or cancellation. No output is written until the edits apply successfully; later output failures retain the existing best-effort restoration and publication limits. This does not promise physical destination safety after a failed restoration.
>
> Direct engine edits remain pending on the document until saved or signed; disposing an editor neither saves nor discards them. A failed engine bulk fill leaves the preceding pending state intact, which differs from replaying single fills that may have already succeeded. A failed workflow disposes its private document/session; it does not expose a partially edited document for reuse. Build a new workflow for retry.
>
> Signing and document timestamps remain dedicated terminals, not queued ordinary operations. They stage ordinary edits and then sign/timestamp through the same editor, add a separate signing/timestamp revision, and publish only after staging completes. Existing permission, certification, field-lock, appearance, host-exception and no-automatic-retry guarantees remain unchanged.

## Adoption and follow-up

Recommendation: retain; no runtime or test-suite change required. Optional documentation clarification can be scoped in a future owner-approved documentation item. If the owner instead selects duplicate rejection or global declaration order, exact equality/error/state rules, migration examples, regression expectations and affected F5/F12/F13 wording must be approved first. These changes are behavior changes, with the losses and uncertainty in `findings.md`; they are not authorized by this item. W-205/W-206 retain their separate rollback/alias questions, and W-202/W-203 retain their signing-provider questions.
