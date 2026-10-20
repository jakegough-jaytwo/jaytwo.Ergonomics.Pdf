# Signing matrix

Keep these properties separate. A test or external verifier establishes only the rows it actually checks; name them in the evidence record.

| Property | What establishes it |
|---|---|
| `/ByteRange` covers everything except the `/Contents` placeholder, and the placeholder is the whole hex string | byte arithmetic on the output; pyHanko integrity check |
| CMS signed attributes: content type, message digest of the byte range, signing time, signing certificate v2 | DER inspection; pyHanko |
| Signed attributes are DER-encoded as a SET for signing and `[0] IMPLICIT` in SignerInfo | DER inspection |
| The signature value verifies under the embedded signer certificate | `System.Security.Cryptography.Pkcs` `CheckSignature(true)`; pyHanko; OpenSSL `cms -verify -noverify` |
| Signer certificate matches the key the host signed with | the test signer's public key |
| Chain contents: `AdditionalCertificates` are embedded | DER inspection |
| Revision coverage: the signature covers the latest revision | pyHanko `coverage` |
| Trust: the chain reaches a configured root | pyHanko with a trust root. Never inferred from the rows above |
| DocMDP `P` and FieldMDP `All`, `Include`, `Exclude` | library tests; pyHanko modification analysis |
| `/SV` seed-value constraints and `/Lock` on a reused signature field | library tests against ISO 32000-1 12.7.4.5 and 12.8 |
| CMS signature timestamp versus document timestamp | the unsigned attribute (`id-aa-timeStampToken`) versus a `/DocTimeStamp` revision |
| Timestamp imprint over the signature value, nonce echo, digest algorithm, `certReq` and certificate inclusion | `RequireMatch`; OpenSSL `ts -verify`; pyHanko |
| Encrypted PDF: signature dictionary strings stay plaintext; the rest of the revision is encrypted | library tests; pyHanko with the password |
| Failure: provider throws, appendix overflow, TSA timeout or rejection | destination unchanged or as documented; `PdfSignException`, `PdfSignatureTooLargeException`, `PdfTimestampException` |

Where ISO 32000 is ambiguous, the evidence record names the reference interpretation it follows, such as pyHanko or Acrobat.

Qualification work needs a second PDF-aware verifier beyond pyHanko (Poppler `pdfsig` or PDFBox; see B-14). A CMS-only verifier such as OpenSSL does not fill that role because it cannot check the byte range or revision coverage.
