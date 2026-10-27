# O 11 alias disposition for Gate 2

Recommendation: **keep** current file-alias safeguards. No implementation scope is proposed. No protected or shipped wording is edited by W-206.

Exact proposed decision wording for W-211's reconciliation:

> Retain the post-W-185 managed workflow file publication lifecycle and all current conservative alias safeguards: known normalized-path checks before opening inputs, intermediate and final link resolution on supported targets, refusal of reparse paths that cannot be resolved safely on older targets, final rechecks for every known file-backed input, and existing-destination length/last-write matching with refusal when input metadata is unavailable. Keep inputs open through publication and preserve borrowed-resource ownership. The matching-metadata check deliberately also refuses independent timestamp-preserving copies. It is a possible-alias safeguard, not proof of identity. Keep D-65's caller serialization and concurrent-filesystem limitations. No deletion of an alias safeguard, native identity layer, new dependency, cross-process coordination mechanism or stronger filesystem guarantee is approved by this research.

Proposed additions versus deletions: this reconciliation statement only; zero product code or public-contract changes. Current `docs/behavior.md` already describes the tradeoff and limitations; no replacement wording is needed there. D-65, F17/F20, ownership, flush policy, signing host-call ordering and supported algorithms remain binding.

Alternatives considered:

- Remove matching metadata: accepts timestamp-preserving independent copies, but also removes the documented possible-hard-link refusal; no safe equivalent was demonstrated. Owner contract approval and a separately scoped implementation/regression item would be required. Do not count it as a refactor.
- Rely on Windows sharing: measured Unix open-handle rename/unlink behavior prevents treating this as a portable substitute.
- Resolve only the last path segment: loses the intermediate-directory alias protection measured using a Windows junction and Linux symlink.
- Universal identity/publication abstraction: adds platform obligations prohibited by current D-65 scope; no measured business requirement or responsibility reduction supports reopening that decision.

Optional future evidence scope, only if requested: run the already existing workflow tests on Linux at the intended revision; run final/dangling/cyclic file-symlink cases in an authorized Windows environment; report filesystem/runtime and retain failures. Do not enable machine-wide privileges, change Developer Mode, add runtime dependencies, change test-suite files, or insert an implementation item as part of this packet. The evidence gaps support retention, not a claim that every supported filesystem has been qualified.
