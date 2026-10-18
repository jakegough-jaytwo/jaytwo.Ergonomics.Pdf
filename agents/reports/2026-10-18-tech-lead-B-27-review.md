# B-27 review

## 1. Task and owner

Technical Lead. Review of the two signer findings. No `src/` edit and no signer fix is boarded. The nonce, imprint, and throwing-signer work is not reopened. G8 is not passed.

## 2. Verified facts

Both findings are confirmed from the code. Neither input was executed.

1. Confirmed. `ExtractToken` throws only when `ReadInteger` returns a value greater than 1. `ReadInteger` accepts at most four content bytes and folds them into a signed 32-bit int. The INTEGER `02 04 80 00 00 00` decodes as `int.MinValue`. That value is not greater than 1, so extraction continues and returns the following ContentInfo. A negative status is not granted or grantedWithMods. Status 0 and status 1 still pass that comparison. `RequireMatch` still checks the imprint and the nonce.

2. Confirmed. `ReadTlv` rejects a length when `length < 0` or `index + length > data.Length`. The addition is unchecked. A 4-byte length of `0x7FFFFFFF` makes `index + length` wrap negative, so the comparison does not throw. `ReadOnlySpan<byte>.Slice` then throws `ArgumentOutOfRangeException` instead of `PdfTimestampException`. The slice fails before a buffer of that length is allocated. On the sign path this is inside `CreateContents`, before the destination write.

## 6. Changes made or proposed

This report. B-27 stays on `agents/state/board.md` until the Product Manager accepts the review. No signer fix is boarded. B-28 is boarded as the parser fix. It does not change the signer.

## 9. Handoff

The Product Manager accepts the review. This review does not pass G8.
