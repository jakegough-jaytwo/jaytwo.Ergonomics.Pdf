# External CMS parser seeds

Generated on 2026-10-25 by `external-seeds.sh OUTPUT --cms-only` using the repository's Linux builder and OpenSSL 3. No private keys are retained.

- `document-timestamp.cms`: RFC 3161 token including its TSA certificate and genTime.
- `signature-timestamp.cms`: issuer/serial CMS with an unsigned signature-time-stamp attribute. The token imprint is over this CMS's signature value. Signed attributes retain their original DER bytes.
- `subject-key-identifier.cms`: OpenSSL detached CMS with a subject-key-identifier SignerInfo and matching certificate.
- `library-basic.cms` and `library-pades.cms`: emitted through the engine's editor and CMS provider at W-104. Regenerate explicitly with `dotnet run --project tools/fuzz/jaytwo.Ergonomics.Pdf.Fuzz.csproj -c Release -- cms-library-seeds tools/fuzz/seeds/cms`. The throwaway RSA key is not saved. Ordinary smoke runs read the fixed files rather than generating random keys or certificates.

The fixed files make timestamp coverage available in the ordinary smoke run without starting external tools. The script also generates fresh versions for campaigns. They exercise inspection, not trust or validation, so certificate expiry does not invalidate these seeds. The in-process seed builder adds library-generated basic/PAdES CMS, every hex signature in the registered fixture directories (top level, corpus, defects and focused sets), BER container re-encodings, and controlled-rejection variants. The existing strict-DER target is unchanged.
