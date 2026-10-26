# R4 DSS classification (W-103, G4 evidence)

This record covers independent-tool properties of the current engine and workflow signing paths. It does not resume release qualification, pass a release gate, establish public identity, or claim production qualification.

## Reproducible configuration

- EU DSS 6.3, official [demonstration release](https://github.com/esig/dss-demonstrations/releases/tag/6.3), bundle SHA-256 `60452c53eb395106655340890a207885f165bf844485046e3a1f8535ef8fa944`. Only `WEB-INF/lib/*.jar` is extracted from `ROOT.war`; Tomcat is never run.
- Temurin JDK 21.0.9+10. Windows archive SHA-256 `1c67df516e9795c0b09f5714bfe151da2e3cc988082042f5bbb60d75e4e63fb5`; Linux amd64 archive SHA-256 `810d3773df7e0d6c4394e4e244b264c8b30e0b05a0acf542d065fd78a6b65c2f`. The wrapper reads DSS's Maven version resource and checks both runtime pins.
- Unmodified DSS `policy/constraint.xml`, named `QES AdESQC TL based`, SHA-256 `842c4a877b8ab8c125abf89bb3a4d39599d489ed0cdf75f7e57c13f701e70d4b`. Profile detection, cryptographic results and policy indication are retained separately; qualification errors concerning absent trusted lists are retained in the original reports.
- The documentation shipped inside this exact bundle, `ROOT.war!/doc/dss-documentation.html`, references ETSI EN 319 142-1 V1.2.1 (2024-01), R03, and ETSI EN 319 102-1 V1.4.1 (2024-06), R09. API fields were checked against the pinned classes with `javap`, including `SimpleReport.getSignatureFormat/getIndication/getSubIndication`, `SignatureWrapper.getMessageDigest/isSignatureIntact/isSignatureValid`, and `TimestampWrapper.isMessageImprintDataIntact`. The [DSS documentation](https://ec.europa.eu/digital-building-blocks/DSS/webapp-demo/doc/dss-documentation.html) describes these separate report layers; its live edition may advance.
- Each test creates a local CA and a signer leaf; B-T also uses the existing OpenSSL-authority fixture and its separate CA. Only those roots are trust anchors. Local empty CRLs cover the signer and TSA certificates. No AIA, OCSP, online CRL or trusted-list source is configured. The original policy is not weakened. Each invocation records its explicit validation instant, anchor/CRL hashes and the producing commit. No private key enters evidence.

## Cases and expected properties

Both direct engine and workflow tests exercise the same fixture construction through their respective public signing APIs. Each path has nine B-B cases: RSA 2048, P-256 and P-384, each with SHA-256/384/512; two B-T cases: RSA/SHA-256 and P-256/SHA-384; and B-B on qpdf AES-256 input opened under each of the owner/user passwords. Positive profiles must be `PAdES_BASELINE_B` or `PAdES_BASELINE_T`, with intact digest and signature, a trusted test chain, and signature indication `TOTAL_PASSED`. B-T additionally requires intact/valid timestamp signature, matching imprint, trusted TSA chain and timestamp indication `PASSED` (DSS uses that building-block value, distinct from `TOTAL_PASSED`).

Each path also exercises B-B and four controls: legacy basic output must be `PKCS7_B` with valid cryptography; changing a signed byte must fail its digest; a corrupt signature value must fail signature verification; and an otherwise valid token from another signature must fail its timestamp imprint. No negative profile label is counted as successful verification.

## Finding DSS-6.3-T-label: timestamp label survives a mismatched imprint

**Observed:** replacing only the unsigned signature-timestamp attribute in a valid B-T fixture with a valid token from a different signature preserves the original PDF/CMS signature. DSS 6.3 reports `profile=PAdES_BASELINE_T` and base-signature `TOTAL_PASSED`, while its timestamp has `signatureIntact=true`, `signatureValid=false`, `imprintIntact=false`, `FAILED/HASH_FAILURE`.

**Expected:** a claimed, valid B-T result needs a matching timestamp imprint. The label and base-signature conclusion alone cannot establish that property. This deliberate negative fixture demonstrates a disagreement between the profile label and timestamp verification, not a defect in the library's positive output.

**Impact/severity:** medium test-tool limitation: consumers relying on the label alone could overstate a B-T result. **Disposition:** retain the fixture and all original reports, explicitly assert the mismatch and its exact indication/subindication, and require successful timestamp verification in every positive B-T assertion. The label is preserved verbatim rather than normalized or hidden. No product behavior or profile expectation for valid output changes.

## Execution and retained evidence

Source implementation and preliminary Windows runs are in progress. Revision-bound Windows/Linux results, original reports, fixture hashes and report checksum inventories will be recorded here after the source checkpoint and tool-install integration gate. Pending checks are not passes.
