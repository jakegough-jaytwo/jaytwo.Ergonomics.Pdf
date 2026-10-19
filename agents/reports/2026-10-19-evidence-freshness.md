# Evidence freshness step 3

The 46-row evidence inventory now records date, commit, and dirty-tree state. Rows affected by F-2 through F-5 are distinguished from the B-41 checkpoint. G10 no longer reports the closed B-34 `net48` failure: `B-41` passed 309/309 on every maintained Windows and Linux runtime and built all four library targets in Release with no warnings.

G10 remains unpassed because the B-20 package record predates later source changes, reproducible repeat-build and compiled-example evidence is absent, versioning/compatibility expectations are not documented, and no consumer test directly exercises `netstandard2.1`.

The shared dirty tree passed `make docker-builder docker-test`: 314/314 on Linux `net8.0` and `net6.0`; one test is concurrent work outside F-2 through F-5. The 35 `AcroFormTests` passed on Windows `net8.0`, `net6.0`, and `net48`.
