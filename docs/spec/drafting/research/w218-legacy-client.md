# W-218 legacy compiled-client baseline, 2026-10-31

**Current-assembly baseline only.** This is separate from the independent-renderer research in `w218-appearance/`. No new opt-in API, requested validator, changed-assembly compatibility or supplied-font behavior was executed. No acceptance or qualification record is changed.

## Inputs and retained artifacts

Repository HEAD recorded by the incoming handoff: `509af167b4257646b330c61d95a381cfff2cf014`. The inherited workspace has unrelated edits. The handoff reports successful restore and a Release library build for net10.0/net8.0/net6.0/netstandard2.0 with zero warnings/errors, and identifies the copied DLLs under `tmp/w218-r3-client/lib/` as HEAD-built. Those restore/library-build commands were not rerun in this continuation; the hashes below bind the assemblies actually used without certifying a clean-checkout build. No runtime source was edited by this continuation.

Scratch source and project are `tmp/w218-r3-client/Program.cs` and `Client.csproj`. The project references the copied library DLLs by target-specific absolute Reference path. The retained `bin/Release/<tfm>/` contains the client DLL, dependency/runtime configuration and copied baseline libraries; preserve that directory and `retained-sha256.json` for later substitution. Program.cs and Client.csproj hashes identify the exact source/project. Rebuild only a separate candidate client after adoption, so the retained old client remains unchanged.

## Commands and observed results

Executed in this continuation using .NET SDK 10.0.402:

```powershell
dotnet build tmp/w218-r3-client/Client.csproj -c Release --no-restore
dotnet --fx-version 10.0.13 tmp/w218-r3-client/bin/Release/net10.0/Client.dll C:/jake/git/jaytwo.Ergonomics.Pdf
dotnet --fx-version 8.0.31 tmp/w218-r3-client/bin/Release/net8.0/Client.dll C:/jake/git/jaytwo.Ergonomics.Pdf
dotnet --fx-version 6.0.36 tmp/w218-r3-client/bin/Release/net6.0/Client.dll C:/jake/git/jaytwo.Ergonomics.Pdf
```

Build: succeeded, zero errors, one NETSDK1138 warning for net6.0 end of support; no analyzer warning/error or analyzer waiver. The inherited build's two warnings were not retained as an identifiable log; the fresh build identifies its own warning. Bare `dotnet <Client.dll>` initially failed to launch all three targets (exit -2147450730): the scratch project's RollForward=Disable requests exact 10.0.0/8.0.0/6.0.0 runtimes, whereas installed stable patches are 10.0.13/8.0.31/6.0.36. Explicit `--fx-version` selected those patches without rebuilding, editing binaries or enabling cross-major roll-forward. All three explicit runs exited 0, each reporting 13 PASS observations and ALL PASS (39 observations total). Logs are in the scratch directory, not independent-reader evidence.

The 13 observations per runtime were:

1. Engine single Fill method group: Ada -> Generated; Save/reopen preserves the exact value and false NeedAppearances.
2. Engine single Fill: Japanese value -> NeedsViewer; Save/reopen preserves exact characters and true NeedAppearances. This checks value/flag storage, not visible Japanese paint.
3. Bulk method group with Dictionary and implicit string/bool: LegalName Generated, Hazmat ExistingAppearanceSelected, FleetSize Generated.
4. Bulk List input: TaxId Generated.
5. Bulk array with FromText: Email Generated.
6. Empty bulk dictionary: empty result.
7. Export-shaped semantic value input: Notes accepted.
8. Missing engine field: PdfFieldNotFoundException at Fill.
9. Bulk with TaxId change then invalid Hazmat value: PdfFieldValueException; after Save/reopen TaxId is not changed and the earlier LegalName=Earlier edit remains. This proves that legacy call's value restoration, not every R4 pending structure or a post-AP failure injection.
10. Duplicate literal TaxId bulk entries: ArgumentException.
11. Workflow Fill then FillForm for LegalName: last wins, dictionary copied at description time, exact Japanese value saved, Modified=true and one FieldAppearances NeedsViewer result despite later mutation of the caller dictionary.
12. Workflow with no fill: empty FieldAppearances.
13. Workflow null name: ArgumentNullException at description; missing name accepted at description then PdfFieldNotFoundException at Save, destination length 0 on this path.

Method-group/source binding occurred in the baseline build, not against future API additions. No new assembly substitution has run. netstandard2.0 DLLs were retained/hashed but not exercised by a host-framework client (for example net48). Output was read by the target library only; no independent PDF reader/renderer was run in this continuation. New readiness validation, rich clear, font size/proportionality, layout, cancellation/throwing-font-source, detailed restoration and ready-terminal scenarios remain **unexecuted**. Docker was **not run**.

## SHA-256 manifest

Paths are relative to the repository root. The manifest includes every retained client output file (including copied libraries), input source/project, fixture and evidence logs. These hashes do not attest compatibility or rendering.

| Retained/input file | SHA-256 |
| --- | --- |
| `tmp/w218-r3-client/Program.cs` | `e5d9f2a28ae4a44c191e1b263202d711c9d76b0934340cb529912746186dcd42` |
| `tmp/w218-r3-client/Client.csproj` | `49434698050fd9e612590e71e8323072126305d4c58d8e549fbc8f34ce23b901` |
| `test/jaytwo.Pdf.Tests/Fixtures/corpus/libreoffice-form.pdf` | `cbef8f2cf3eb2ad547b26e9f6866812f978ec256fb10f4294c412f2300db2a72` |
| `tmp/w218-r3-client/lib/net10.0/jaytwo.Ergonomics.Pdf.dll` | `3e659f97d5576852ad870fb6fc1f4b494d8082dc875122d01ecdfa521cdad341` |
| `tmp/w218-r3-client/lib/net10.0/jaytwo.Pdf.dll` | `42a3c83e065921b3588d961b611519d8e1e417ff190debc69285c7b43b2d36a6` |
| `tmp/w218-r3-client/lib/net6.0/jaytwo.Ergonomics.Pdf.dll` | `621a4c9f5d18464c747645afd4a4eac686bc5d3e3a40cd1470d35981de05624d` |
| `tmp/w218-r3-client/lib/net6.0/jaytwo.Pdf.dll` | `a68661d986ee5e977d77d7f408b9f0bca7e8b31d61982a258be98db8a89ba3bf` |
| `tmp/w218-r3-client/lib/net8.0/jaytwo.Ergonomics.Pdf.dll` | `eb270637b2a8ef9cfdc34b594c11c3210ea35812c57e041177caf977650bd09a` |
| `tmp/w218-r3-client/lib/net8.0/jaytwo.Pdf.dll` | `d330351f9a794857a8500760785b447a428f15629e880c4524cd725121f44f06` |
| `tmp/w218-r3-client/lib/netstandard2.0/jaytwo.Ergonomics.Pdf.dll` | `2a28f541a0b91234b4911250a0f8b7d96b5134237ec65b2b2d56eea969b59a1e` |
| `tmp/w218-r3-client/lib/netstandard2.0/jaytwo.Pdf.dll` | `d914c1ea15c1943b1c2f369307faabc54b331454532a93fd272fa2a53f37deb2` |
| `tmp/w218-r3-client/bin/Release/net10.0/Client.deps.json` | `eb4a5c255e64f5e9c2867b04abea039d5a4288b72b22da3fabdbb7361d78092b` |
| `tmp/w218-r3-client/bin/Release/net10.0/Client.dll` | `8c983af70744b01fd15ddfe1d37ddc919a242a55673f2853a8b8ed0d5e23aa6c` |
| `tmp/w218-r3-client/bin/Release/net10.0/Client.exe` | `be4f1591cb70f2d93070f2f4206e1078d51d530431eaa12c317d6ec17ead8061` |
| `tmp/w218-r3-client/bin/Release/net10.0/Client.pdb` | `de77e194638ad239d18a1f3e317fe0e864656e5d62be6a599273466dc09fb121` |
| `tmp/w218-r3-client/bin/Release/net10.0/Client.runtimeconfig.json` | `0a8368fb133754ba732346f405754070c3ed56f457bb8323160c9e15080f4952` |
| `tmp/w218-r3-client/bin/Release/net10.0/jaytwo.Ergonomics.Pdf.dll` | `3e659f97d5576852ad870fb6fc1f4b494d8082dc875122d01ecdfa521cdad341` |
| `tmp/w218-r3-client/bin/Release/net10.0/jaytwo.Pdf.dll` | `42a3c83e065921b3588d961b611519d8e1e417ff190debc69285c7b43b2d36a6` |
| `tmp/w218-r3-client/bin/Release/net8.0/Client.deps.json` | `fed6a574bb84d5e3e769e39c4c09daefaae69e997d0636ee10601b4b353cde81` |
| `tmp/w218-r3-client/bin/Release/net8.0/Client.dll` | `16b3adcf28eb5ba98ec89677e732eb852402b8e62f2a938f16f5204381817c0e` |
| `tmp/w218-r3-client/bin/Release/net8.0/Client.exe` | `90bed2477a4ca9da41746bbad1877473654969aa111299146bf0c5ea6335785e` |
| `tmp/w218-r3-client/bin/Release/net8.0/Client.pdb` | `1d26a86ad9e8b58beefeeb1cc970fdd995f7fb2528a20db50c4181d516c362c8` |
| `tmp/w218-r3-client/bin/Release/net8.0/Client.runtimeconfig.json` | `6e4ad26f9d1d942aab81b37fd7deafbf4cde6ee4d1f12ec821261bc23a17765c` |
| `tmp/w218-r3-client/bin/Release/net8.0/jaytwo.Ergonomics.Pdf.dll` | `eb270637b2a8ef9cfdc34b594c11c3210ea35812c57e041177caf977650bd09a` |
| `tmp/w218-r3-client/bin/Release/net8.0/jaytwo.Pdf.dll` | `d330351f9a794857a8500760785b447a428f15629e880c4524cd725121f44f06` |
| `tmp/w218-r3-client/bin/Release/net6.0/Client.deps.json` | `0fa0fd3e9639e19915d25a243ae2a93e8836322749e4abe0b3f7441daae90a4d` |
| `tmp/w218-r3-client/bin/Release/net6.0/Client.dll` | `3c2445bbec59a0dbcbfbbb47d80c2fae4019fa2497511d0757544c422ec908f5` |
| `tmp/w218-r3-client/bin/Release/net6.0/Client.exe` | `6ed4a0bdb85a4d1a391a037da4c9eaa83fe2949bdf4ff8ff55ad9d57f20bfda9` |
| `tmp/w218-r3-client/bin/Release/net6.0/Client.pdb` | `f139828401ab5c157a178c57ac88bd4bcba756792003a1b217904325f0d47c66` |
| `tmp/w218-r3-client/bin/Release/net6.0/Client.runtimeconfig.json` | `4adadb4c6df750bb713aabdd02a238af35388756c997cb0167b58201b2829db9` |
| `tmp/w218-r3-client/bin/Release/net6.0/jaytwo.Ergonomics.Pdf.dll` | `621a4c9f5d18464c747645afd4a4eac686bc5d3e3a40cd1470d35981de05624d` |
| `tmp/w218-r3-client/bin/Release/net6.0/jaytwo.Pdf.dll` | `a68661d986ee5e977d77d7f408b9f0bca7e8b31d61982a258be98db8a89ba3bf` |
| `tmp/w218-r3-client/build.log` | `89cc1d21384291c54bb6a2437a4f734234509cb707976fda5455d5b6df1aaa8f` |
| `tmp/w218-r3-client/run-net10.0.log` | `b773f5f3c2e5e4f65fa4ce8ccdd7394a3d9490612c9c21611e8d87fb0d48aac7` |
| `tmp/w218-r3-client/run-net6.0.log` | `31522fb70d81719d430a3f072f01bb2d6e3f3d70189d146202dbfde403e66b8e` |
| `tmp/w218-r3-client/run-net8.0.log` | `fc2e063b78e915248c3441dc4fa13aa46f004858025cad1c8751191a2a8f6188` |
