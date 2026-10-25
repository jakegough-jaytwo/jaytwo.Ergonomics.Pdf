# Factur-X hybrid invoice

Unmodified `library/src/test/resources/cii/X19_01_Warenrechnung_fx.pdf` from Mustangproject commit `1d633ab612856527d536a709ed53bedd2a4ea98c`, acquired 2026-10-25.

Source: https://github.com/ZUGFeRD/mustangproject/blob/1d633ab612856527d536a709ed53bedd2a4ea98c/library/src/test/resources/cii/X19_01_Warenrechnung_fx.pdf

The upstream repository distributes this test invoice under Apache-2.0. LICENSE and NOTICE are retained; trailing whitespace is removed from LICENSE. This is a focused A15 refusal fixture, not a G2 corpus member or a PDF/A conformance assertion. The test independently verifies the embedded factur-x.xml invoice and catalog association with pypdf, then checks typed refusal before output for extraction and concatenation.
