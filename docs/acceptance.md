# Acceptance record

Enforced test outcomes, mapped to the adopted contracts' acceptance scenarios. A row appears only for an outcome a test enforces. Scenarios not listed here remain unexecuted targets.

| Scenario | Enforced outcome | Tests |
| --- | --- | --- |
| META-A7 (partial) | A touched mapped property defined more than once across descriptions, as both attribute and element, or with duplicate `xml:lang` refuses with `PdfXmpException`; Info, ModDate and the packet are unchanged. A duplicate ModifyDate refuses an edit that refreshes ModDate. An unrelated duplicate neither refuses nor changes. A property defined once in a later description is edited in place. Not covered: several descriptions with non-empty `rdf:about` beyond these fixtures, and other-root or malformed packets, which still leave the packet unchanged while Info updates. | `MetadataTests.duplicate_touched_definition_refuses_with_no_change`, `duplicate_modify_date_refuses_an_edit_that_refreshes_moddate`, `unrelated_duplicate_does_not_refuse_and_survives`, `property_defined_in_a_later_description_is_edited_in_place` |
