# W-218 scope reconciliation: proposed wording

For owner approval. This proposal is not adopted authority and does not authorize implementation. It resolves the three identified scope conflicts without changing current API guarantees or adopting a new rewrite strategy.

## 1. AGENTS.md

In the Product charter's `Never in scope` paragraph, replace:

> stamping and watermarks, layout or creating a PDF from scratch

with:

> general document authoring and layout, except for the limited image operations below

Immediately after that paragraph, add:

> Intended product scope also includes JPEG/PNG image placement on existing pages for image stamping and watermarking, and image-to-PDF conversion using limited blank-page creation. These are explicit exceptions to the former stamping and from-scratch creation exclusions; they do not introduce general authoring, layout, or a public raw object model. Security-permission changes are limited to permission bits on an already password-encrypted document, authenticated with the owner password. They do not include adding encryption, changing passwords, or removing encryption. These capabilities are intended scope, not current support or adopted implementation requirements. Their detailed specifications and delivery order require owner adoption before implementation; no new full-document rewrite strategy is approved here. N5 password removal remains a separate R5 evaluation.

Immediately before `Scope is defined in docs/spec/`, add:

> Read `docs/CHARTER.md` and `docs/SUPPORTED.md` before proposing or implementing work. The former records enduring purpose and principles; the latter records intended capabilities and boundaries regardless of implementation status. Adopted specifications define implementation requirements. The named additions below are approved scope reservations awaiting detailed specifications, not contradictions with the older adopted phase coverage. Other binding constraints in this file remain in force until explicitly reconciled by the owner.

## 2. docs/spec/phase-1.md section 11

In the `Never` list, replace `stamping and watermarks, layout or creating PDFs from scratch` with `general document authoring and layout, except for the limited image operations identified below`.

After that list, add:

> **Intended additions outside the adopted Phase 1 requirements:** JPEG/PNG image placement on existing pages for stamping and watermarking; image-to-PDF conversion using limited blank-page creation; and changes to permission bits on an already password-encrypted document with owner authentication. These scope reservations do not adopt detailed requirements, assign delivery stages, or approve a new rewrite strategy. Image-to-PDF requires new-file writing. Permission changes do not include adding encryption, changing passwords, or removing encryption. N5 password removal remains a separate R5 evaluation. General authoring and layout remain excluded.

## 3. docs/spec/phase-2.md

After the opening status paragraph, add:

> Image-to-PDF conversion is also intended product scope, using limited blank-page creation and JPEG/PNG placement. It is outside the adopted N1/N2 assembly requirements and has no adopted detailed requirements or delivery stage. This reservation does not change N1/N2 preservation or unencrypted-output rules, adopt N3 to N5, or approve a new rewrite strategy. Image placement on existing pages and owner-authenticated changes to permission bits on existing password-encrypted documents likewise await separate detailed specifications. Permission changes do not include adding encryption, changing passwords, or removing encryption; N5 remains the separate password-removal evaluation.

In the final `Never` list, replace `stamping and watermarks, layout or creating PDFs from scratch` with `general document authoring and layout, except for the limited image operations reserved above`.

Section 4's incorporation of Phase 1's exclusions then uses the revised exclusions. All existing N1/N2 requirements, the writer rules, and N3-to-N5 scheduling remain unchanged.

## 4. README.md

Replace the paragraph beginning `This README is the project's anchor` with:

> The [Project Charter](docs/CHARTER.md) defines purpose and principles. [Supported capabilities](docs/SUPPORTED.md) defines intended product scope, whether implemented or not. Use them to assess whether a change serves the project. Current behavior is in [docs/behavior.md](docs/behavior.md).

Rename `What it will never do` to `Product boundaries`. In its exclusion list, replace `stamping and watermarks, layout or creating a PDF from scratch` with `general document authoring and layout`.

After that exclusion list, add:

> Intended additions are JPEG/PNG image stamping and watermarking, image-to-PDF conversion with limited blank-page creation, and owner-authenticated permission-bit changes on already password-encrypted documents. These are not current APIs. General authoring and layout remain excluded. Permission changes do not include adding encryption, changing passwords, or removing encryption. Detailed specifications and delivery order remain to be adopted.

In `Does a change belong here?`, replace the first two bullets with:

> - It serves a capability in the intended product scope.
> - Its output strategy follows an adopted operation contract; a new full-document rewrite strategy requires owner approval under the charter.

Prefix commitment 2 with `For current editing and assembly APIs:` so its existing append/copy guarantees remain descriptions of those APIs rather than a prohibition on the newly reserved image-to-PDF writer. Update the final AGENTS.md link label to `binding contributor rules and adopted implementation constraints`.

## 5. Companion document alignment

In SUPPORTED.md, replace the Security permissions coverage with:

> Read document security permissions; change permission bits on already password-encrypted PDFs with owner authentication. Adding encryption, changing passwords, and removing encryption are separate capabilities and are not included in this commitment.

In behavior.md's Planned additions table, replace the Permission changes intended-behavior cell with:

> Change permission bits on already password-encrypted PDFs with owner authentication, without adding encryption, changing passwords, or removing encryption.

In that section's introduction, replace `Detailed specification adoption and reconciliation with the older exclusions in AGENTS.md are still required before implementation` with `Detailed specifications and delivery order still require owner adoption before implementation; no new rewrite strategy is approved by the scope reservation`.

Correct behavior.md's introduction to say SUPPORTED.md defines `intended capabilities and boundaries` rather than delivery priorities. Replace the obsolete MVP/lower-priority paragraph with:

> Intended scope is independent of delivery priorities. Existing capabilities keep the current contracts below until an approved change explicitly updates them.

## Approval boundary

Approval adopts only the exact scope and document-role amendments above. It does not schedule implementation, adopt image/security acceptance requirements, change existing permission/signature enforcement, alter N1/N2 output, or approve a rewrite strategy. Broader charter reconciliation remains a separate task.
