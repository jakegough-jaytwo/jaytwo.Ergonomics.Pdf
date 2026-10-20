---
id: D-O4
title: "Caller-facing coordinates"
status: rejected
---

# D-O4. Caller-facing coordinates


The charter reports raw user space, the page boxes, and rotation. The original AcroForm role asked for top-left viewport coordinates and `/UserUnit` normalization.

Recommendation: keep the charter. Revisit only with a named consumer that needs it.

Rejected by the owner on 2026-10-19. This is an ergonomics library over the PDF format, not a translator into coordinates a caller might find easier. Geometry follows the PDF specification: user space with the standard bottom-left origin and 72 units per inch, read from the file as stored. Page boxes and rotation are reported, not applied, and `/UserUnit` is not applied. `README.md` states this contract (W-58).
