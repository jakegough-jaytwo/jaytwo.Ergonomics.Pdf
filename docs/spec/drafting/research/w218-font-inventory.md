# W-218 cross-script font input inventory

**Measured static input evidence, 2026-10-31; not target resource evidence.** Run `python docs/spec/drafting/research/w218-font-inventory.py` with fontTools 4.67.0. The [script](w218-font-inventory.py) reads locally installed Windows fonts without copying font binaries; [raw results](w218-font-inventory.json) retain SHA-256, environment, family, face, byte extent, table-byte sum, glyph count, fsType and selected cmap. Local installation does not establish redistribution rights.

| Installed family | Container bytes | Faces | Selected-map sample coverage | Format boundary |
| --- | --- | --- | --- | --- |
| Arial | 1,045,720 | 1 | Latin, Greek, Cyrillic, Arabic, Hebrew | Standalone TrueType outline input candidate; not validated eligible. |
| Times New Roman | 962,864 | 1 | Latin, Greek, Cyrillic, Hebrew | Standalone TrueType outline input candidate; not validated eligible. |
| Tahoma | 919,260 | 1 | Latin, Greek, Cyrillic, Arabic, Hebrew, Thai | Standalone TrueType outline input candidate; not validated eligible. |
| Sylfaen | 260,108 | 1 | Latin, Greek, Cyrillic | Standalone TrueType outline input candidate; not validated eligible. |
| Leelawadee UI | 390,004 | 1 | Latin, Thai | Standalone TrueType outline input candidate; not validated eligible. |
| Malgun Gothic | 13,459,196 | 1 | Latin, Greek, Cyrillic, Han, Hangul | Standalone TrueType outline input candidate; not validated eligible. |
| Nirmala UI | 5,304,988 | 6 | Latin, Devanagari | Collection comparator only; TTC is not admitted by FORM-FONT-03. |
| Microsoft YaHei | 19,704,352 | 2 | Latin, Greek, Cyrillic, Han | Collection comparator only; TTC is not admitted by FORM-FONT-03. |

Coverage means only that the selected cmap maps each recorded sample scalar to a non-.notdef glyph. It proves no shaping, language completeness, glyph correctness, P1 expansion or accepted appearance. Samples and exact map triples are in the raw results. The inventory spans Latin/Greek/Cyrillic, Arabic/Hebrew, Thai and Han/Hangul in standalone inputs; Devanagari currently appears only in a collection comparator. Collection byte extent includes shared faces and is not the size of a standalone extracted face. No collection extraction or support was added.

**Inference:** these input sizes show why two Latin fonts cannot justify a global resource default. They do not determine the appropriate threshold, memory coefficient or work bound. No size limit is proposed from this inventory.

**Still unmeasured/unexecuted:** standalone Indic and broader Southeast Asian candidates; legally usable portable fixture acquisition; representative business-PDF/font pairing; versioned established-tool embedding comparison; whole-font/no-subsetting and multiple-font workloads; target allocations, retained memory, CPU/work, output-size ratios, hostile composite controls, size-boundary refusal, cancellation and restoration. Follow [R4 measurement protocol](../../forms.md#r4-measurement-protocol-single-size-limit-and-proportional-growth). This inventory does not satisfy the complete A1 resource-evidence exit and triggers no A4 default decision.

Execution note: the first script run failed because closing a lazy collection face closed its shared source; the script now closes the collection after all faces. An initial console-input encoding issue replaced sample literals; samples now use explicit Unicode escapes and the recorded output was regenerated. Only the final raw results are evidence.

Independent follow-up: [fontTools processing diagnostic](w218-font-processing.md) now records six standalone inputs, two isolated ASCII-subsetting runs each. It is not PDF embedding, shaping or target resource evidence and selects no limit. The remaining target measurements and portable/business-PDF corpus gaps above stay open.
