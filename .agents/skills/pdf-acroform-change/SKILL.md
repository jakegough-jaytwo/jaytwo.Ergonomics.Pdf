---
name: pdf-acroform-change
description: Change AcroForm discovery, values, appearances, signature-field discovery, or widget geometry. Use when implementation work touches form behavior.
---

# PDF AcroForm change

Follow the fixed field shape and appearance policy in `AGENTS.md`. Read only the applicable `ACCEPTANCE.md` and `README.md` sections and the current task record.

For field topology, inheritance, button, choice, or appearance changes, read `references/field-matrix.md`. It lists the producer variants and the per-setter invariants a fill must keep.

## Working rules

- Keep field dictionaries, widget annotations, nonterminal fields, merged field/widgets, and repeated widgets for one field distinct. The public field model has a fixed shape. Do not reshape it for elegance.
- Filling changes more than `/V`: value, widget state, and appearance must agree.
- Discover check-box on-states from their appearance dictionaries, not by assuming `Yes`. Radio widgets share one value and select by export value. Choice fields separate export values from display text.
- Require unambiguous field identity. Report malformed or duplicate names instead of guessing.
- Generate appearances only for the supported subset, with the specified font metrics. A generated stream carries its own resources, bounding box, and transform, and must not depend on page graphics state.
- Outside the subset, apply the `/NeedAppearances` policy exactly, report the outcome to the caller, and let signing refuse. Do not grow a font, shaping, or subsetting engine. Widening the subset is an owner decision.
- Report raw user space with the page boxes and rotation. A different coordinate system is a product change, not a fix.
- Resolve widgets without `/P` through page annotations lazily, mindful of large documents. Discovery on a large form must not decode unrelated streams.
- Bound `/Kids` and `/Parent` traversal, reject cycles, and refuse oversized or broken values explicitly.
- Use the core parsing primitives. Do not duplicate parsing in form code. Call the certification gate before a fill. Do not build a second interpretation of permissions.
- Report an unsupported case as a limitation instead of producing a plausible wrong result.

Use `pdf-core-change` only when the parser, page tree, writer, or encryption must change. Use `pdf-signing-change` only when the certification gate or signature revision changes. A change that affects appearances needs rendered evidence from an independent renderer, not only a structural check. Finish with `pdf-change-verification`.
