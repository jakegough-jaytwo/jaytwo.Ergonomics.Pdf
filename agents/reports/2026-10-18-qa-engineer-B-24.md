# B-24. Record the B-23 reader on the G3 inventory rows

## Task and owner

QA Engineer. Edited only the G3 row and the metadata and attachment capability rows in `agents/state/status.md`. The release-readiness note, every other section, `ACCEPTANCE.md`, and product code were not edited. No commit.

**G3 is not passed.** No qualification gate is passed.

## Verified facts

The three rows now name pypdf 6.21.1 and the B-23 properties:

- G3: Info title `InfoEdited`, XMP `dc:title` `x-default` `XmpEdited`, and `notes.bin` bytes `00 01 02 7F 80 FE FF` followed by ASCII `B-23`. The missing Info, XMP, and attachment-reader sentences are gone. The row still says G3 is not passed.
- Metadata: the same Info title and XMP title. "No independent round trip" is gone.
- Attachments: the same `notes.bin` bytes. "No independent extraction of the bytes" is gone.

## Changes made or proposed

`agents/state/status.md`: those three rows only, on top of the B-22 inventory.

## Handoff

Technical Lead reviews. The Product Manager is not asked to pass G3 from this report.
