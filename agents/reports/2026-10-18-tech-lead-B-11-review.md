# B-11 review

## 1. Task and owner

Technical Lead. Review of the revision 4 stream-cipher evidence. No product code and no `backlog.md` edit. The task is not accepted.

## 2. Verified facts

Approved. No defects.

`revision4_rc4_stream_decrypts_beside_an_aes_string` builds revision 4 with `/StrF /Aes` (`AESV2`) and `/StmF /Rc4` (`V2`) and no `/EFF`. The title string is AES. The embedded-file stream is RC4 from `EncryptRc4`, whose object key has no `sAlT`. The same plaintext and object number encrypted with AES differ from that RC4 ciphertext. `Open` with password `user` returns title `Secret`, and `Attachments["notes.bin"].Open()` returns the 40 plaintext bytes. A missing `/EFF` uses the stream cipher, so the attachment is the RC4 case.

`revision4_can_mix_aes_strings_with_rc4_streams` still checks the title only. The new test is the stream assertion. The opening row in `ACCEPTANCE.md` still says revision 4 follows each crypt filter, so strings and streams can use different ciphers. QA reported that `git diff` of that file does not contain that sentence.

QA signed off: the new test passed on `net8.0` and `net6.0`, 1 passed and 0 failed each time. This review did not rerun it. No `src/` change belongs to this task.

## 6. Changes made or proposed

This report. B-11 stays on `agents/state/board.md` until the Product Manager accepts it.

## 9. Handoff

The Product Manager accepts the task. This review does not.
