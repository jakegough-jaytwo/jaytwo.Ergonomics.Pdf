# Technical baseline

Migrated from `agents/archive/state-v1/status.md` under D-33; D-36 and W-56 record its removal, and its pre-removal state is [squashed].

Maintained through `pdf-task-workflow`. As of 2026-10-18. Inventory only. B-02 is accepted. On 2026-10-18 the suite ran on `net8.0` and on `net6.0`: 145 executed, 145 passed, and 0 failed on each. TRX files are `%LOCALAPPDATA%\Temp\jaytwo-b02\b02-net8.trx` and `b02-net6.trx`.

- The test project declares 107 `[Fact]` methods, plus `[Theory]` methods with 38 `[InlineData]` rows.
- These test files cover what the original role files called open gaps:
  - `AppearanceRenderTests`: Poppler text extraction and raster check of generated appearances.
  - `SigningInteropTests`: pyHanko trust of an embedded chain, an RFC 3161 token from the local TSA in `Fixtures/tsa_server.py`, and a SoftHSM signer through `IPdfExternalSigner`.
  - `DenseMemoryTests`: managed-memory peak on dense files, including a qpdf object-stream rewrite.
- B-04 added timestamp imprint, digest-algorithm, and nonce checks. B-29's proposed negative-status and overflowing-DER-length fixes and tests are present in the mixed working tree but are not boarded or accepted.
