# W-208 proposal (draft; nothing here is approved, shipped, or applied)

Companion to `findings.md`. Protected files (`AGENTS.md`, `docs/spec/`, `QUALIFICATION.md`, `.agents/skills/`), W-138, W-109 and the queue in `agents/state/current.md` are **not edited**. This file holds the proposed dispositions and wording for the owner's Gate 2 decision.

## 1. Disposition

| Item | Proposed disposition | Reason |
| --- | --- | --- |
| Asynchronous source path (`OpenAsync`, `IPdfSource.ReadAsync`, async stream adapter) | **Retain as shipped.** Do not remove. | Removal loses cancellation (opens cancel in under 1 ms at 50 ms reads), makes 64 concurrent opens 4 to 5 times slower with 23 to 28 pool threads blocked, contradicts Exists rows R13 and W20, and reverses D-55; the savings are mostly public surface. |
| W-138 | **Stay `proposed`; remains the sole inspection API-design owner.** Add the evidence note in §2. | 15 of 17 measured document-side members and every mutating editor member read synchronously; no option here designs the fix. |
| W-109 | **Stay `proposed`, ordered after W-138.** Add the cross-reference in §3. | No decode-count evidence was gathered; the only new fact is the shared-retention constraint. |
| Extraction read amplification (findings §3.6) | **Route to the owner**, for W-211 or a page-extraction item. | 12,523 reads for two pages of a 12,000-page file; unexplained, one fixture. |

No change to `depends_on`, `blocked_by`, status or queue order is proposed for any item.

## 2. Proposed note for W-138 (append to its Notes after approval)

> - 2026-10-26: W-208 supplied measurements (`agents/reference/simplification/W-208/`, baseline `0b042d7`, 52 MB / 12,000-page fixture, simulated per-read latency). After `OpenAsync`, 15 of 17 measured document members (`PageCount`, `Pages`, `Signatures`, `Revisions`, six `AcroForm` members, `AcroForm.NeedAppearances`, `Metadata.Title`, `Keys`, `HasXmp`, `Attachments.Count`) read the source synchronously, 1 to 31 reads and 64 KiB to 1.97 MB each, in sequence; `Permissions` and `AuthenticationRole` do not read. An asynchronous save makes one synchronous appendix read; an asynchronous workflow save made 32 of 92 reads synchronously. Constraints for the design: a load step cannot be a window prefetch because reads exceed the 1 MiB window ceiling; it needs a retention rule beside D-49 item 4; cancellation and ownership must match `OpenAsync`; `OpenAsync(Stream)` is only as asynchronous as the stream's `ReadAsync`. Removing the asynchronous source path was measured and is not recommended.

## 3. Proposed note for W-109 (append to its Notes after approval)

> - 2026-10-26: W-208 found that any asynchronous load step and this item's object-stream cache would both sit above the source and would need one retention rule under D-49 item 4. No decode counts were measured there. Ordering is unchanged: after W-138.

## 4. Optional documentation wording (not proposed for adoption now)

`docs/behavior.md` line 22 and the `IPdfSource` remarks already say that synchronous members call `Read`. If the owner wants the blocking trade-off stated for remote-source hosts, one sentence could be added to the same bullet with the next documentation change:

> Opening with `OpenAsync` awaits every read; the document's inspection members and synchronous saves still call `Read`, so a source whose `Read` blocks blocks the calling thread there. A seekable `Stream` is only as asynchronous as its own `ReadAsync`.

This restates measured behavior, adds no promise, and would need no `ACCEPTANCE.md` row. It is a candidate for W-211 or W-138's closeout, not a standalone item.

## 5. Costs and risks of the recommendation

- **Business use kept:** non-blocking open, cancellation, async save and sign on slow storage.
- **Loss accepted:** inspection and synchronous saves keep blocking on a blocking source until W-138 decides.
- **Credential burden:** none; no source or signing credential is involved.
- **Responsibility deleted / added:** none / none.
- **Overlapping savings:** none; the Remove option's public-surface savings would not combine with W-138.
- **Residual risk:** the R5 remote adapters cannot rely on non-blocking inspection until W-138 is implemented.
- **Effort:** this recommendation 0. Public-only removal, if the owner overrode it, about 1 to 2 days (inferred); reversing D-55, multi-day to multi-week (inferred), not recommended.

## 6. What a follow-up would need

If the owner chooses a W-138 candidate, it gets its own item with its own allowed paths. The measurements here are reproducible with `run.ps1`; a gigabyte-scale or real-remote spike is a separate approval.
