# Contributing

Contributor and agent rules are in [`AGENTS.md`](AGENTS.md).

## Build and test

```bash
dotnet test jaytwo.Ergonomics.Pdf.sln
```

The interop tests check the library's output with external tools: qpdf, Poppler (including `pdfsig` with NSS), MuPDF `mutool`, SoftHSM 2, OpenSSL 3, Python with pyHanko and pypdf, and pinned EU DSS 6.3 on Temurin JDK 21.0.9+10. DSS checks PAdES profile classification separately from cryptography and trust-policy indication, using local test CA/TSA anchors and CRLs without network retrieval. A test fails when its tool is missing. `make unit-test` runs the interop tests on `net10.0` only, because the library writes the same bytes on every target; a test on every target checks that. `make quiet-test LANE=fast` leaves out the tests that need tools, the corpus, or large data.

- **Windows:** run `powershell -ExecutionPolicy Bypass -File tools/test-tools/install.ps1` once. It installs pinned, hash-checked copies of the tools into the git-ignored `.tools/` folder without administrator rights, and writes `.tools/test.runsettings`, which `dotnet test` and IDE test runners load. No environment variables or `PATH` changes are needed. Rerunning it after a pin changes updates only that tool. The tests need the .NET 6, 8, and 10 runtimes; the script reports any that are missing.
- **Linux or any machine with Docker:** `make docker-builder docker-test` runs the suite in the image the `Dockerfile` builds, which has every tool.
- Tools installed elsewhere can be named with the `JAYTWO_*` variables listed in `AGENTS.md`.

For DSS, `JAYTWO_JAVA` names the Java executable and `JAYTWO_DSS` names the installed `DssValidate.java` wrapper with its pinned jars in the adjacent `lib/` directory. Rerun the installer after updating the wrapper. Focused checks use `make quiet-test TEST_FRAMEWORK=net10.0 TEST_FILTER=FullyQualifiedName~DssInteropTests`; add `TEST_PROJECT=./test/jaytwo.Ergonomics.Pdf.Tests` for workflows. Reports and public fixtures stay in each test output's `dss-evidence/` folder, or in `JAYTWO_DSS_EVIDENCE` when set. `JAYTWO_EVIDENCE_COMMIT` records the producing commit for an evidence run. The reports include the exact validation time and unchanged DSS policy. These are test trust results, not public identity or production-qualification claims.

The PDF engine is the `jaytwo.Pdf` project and namespace, which `jaytwo.Ergonomics.Pdf` references. While the release track is suspended, `jaytwo.Pdf` is not packaged and no package is pushed. Both projects target `net10.0`, `net8.0`, `net6.0`, and `netstandard2.0`. The `netstandard2.0` build is tested on `net8.0` by `test/jaytwo.Pdf.NetStandard.Tests` and on `net48` on Windows. `netstandard2.1` consumers use the `netstandard2.0` build. The `netstandard2.0` build depends on `System.Memory` and `Microsoft.Bcl.AsyncInterfaces`, which brings `System.Threading.Tasks.Extensions`. Acceptance criteria for the tests live in `ACCEPTANCE.md`.
