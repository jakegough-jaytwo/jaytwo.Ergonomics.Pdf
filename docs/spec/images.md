# Image placement and image-to-PDF

**Adopted subject contract, revision W-218-IMAGES-2026-10-31, approved by the owner on 2026-10-31 after review corrections.** Authority: [charter](../charter.md) > [supported capabilities](../supported.md) > adopted contracts. Apply [shared rules](shared.md). The engine owns placement/limited blank-page creation; [workflows](workflows.md) composes image-to-PDF and selected-page watermarking. These are planned capabilities, not current APIs or verified behavior.

## Input subset: IMAGE-INPUT-01

Identify format from bytes, not extension/MIME. The caller supplies a readable stream; read from current position without ownership transfer or a seek requirement. Successful preparation owns the accepted image data needed for save; the caller can close its image stream afterward. Position is not restored. Unsupported variants refuse explicitly; no external imaging dependency or conversion service is introduced.

| Format | Accepted subset and result |
| --- | --- |
| JPEG | Baseline sequential Huffman, 8-bit grayscale or three-component RGB/YCbCr with determinate standard color interpretation. Embed encoded samples without recompression. Refuse progressive, arithmetic, lossless, CMYK/YCCK and unestablishable color interpretation. |
| PNG | 8-bit non-interlaced RGB/RGBA, standard compression/filter methods, decoded internally. Preserve straight alpha through a PDF soft mask. RGB tRNS makes exact matching RGB samples transparent; malformed tRNS or tRNS on RGBA refuses. Refuse indexed, grayscale, interlaced and 16-bit PNG. |
| Color/profile/orientation metadata | Accept ordinary DeviceGray/DeviceRGB JPEG and default/sRGB PNG. Refuse embedded ICC profiles and PNG gAMA/cHRM declarations inconsistent with sRGB; never silently drop intended profiles or convert color. EXIF orientation/DPI do not transform samples or choose page size; dimensions/rotation are explicit. Ancillary comments/text may be discarded. |

Validate PNG chunk ordering, CRCs, dimensions, filter bytes and decompressed row extent. Reject truncated JPEG structure, inconsistent dimensions/components and invalid segment extents. JPEG pass-through does not promise full entropy-decoder validation or repair. Unsupported and malformed input have distinct failure categories. Output supports soft masks/transparency (at least PDF 1.4 when required); raise effective version incrementally if needed without rewriting input or promising PDF/A conformance.

## Coordinates and placement: IMAGE-PLACE-01

Pages are **1-based**. Use the visible CropBox (MediaBox if absent), after inherited page rotation, with origin at its displayed lower-left, x right and y up. Units are physical points (1/72 inch); apply UserUnit when mapping to PDF user space. Crop offsets and inherited rotation participate in this mapping. Refuse malformed/unsupported rotation, nonpositive UserUnit, invalid boxes or unestablishable visible area. This explicit placement coordinate system does not change ordinary inspection or signature-widget coordinates.

The caller supplies finite x/y, positive width/height, counterclockwise rotation in degrees, opacity in [0,1], and foreground/background. Normalize rotation modulo 360. The unrotated rectangle's lower-left is x/y; rotate about its center. Explicit width/height map the whole image to that rectangle, allowing stretching; DPI has no effect. Out-of-bounds painting clips to the visible crop rectangle, including after rotation; it does not move or resize existing content. Opacity multiplies pixel alpha for this image only; zero is a successful invisible placement.

Foreground paints after existing page content, background before it, with isolated graphics state. Neither overrides annotations, optional content, opaque backgrounds or viewer behavior; visibility is not guaranteed. Keep existing content streams encoded and unchanged; append/prepend placement streams and noncolliding resources. Preserve unrelated page geometry, annotations, metadata, attachments and document relationships. No general layout, text/stamp authoring or rendering API follows.

## Limited creation and fit: IMAGE-CREATE-01

Image-to-PDF creates one page with positive finite caller width/height in physical points, origin (0,0), matching MediaBox/CropBox, rotation 0 and UserUnit 1. Default placement contains the image within the page, preserves sample aspect ratio and centers it; unused area stays blank. Do not crop or infer paper size from DPI. Callers may instead choose an explicit placement rectangle/transform. Preserve transparent pixels without adding an opaque matte. Output is an unencrypted new PDF with minimal page/image structures, not an incremental copy or a PDF/A claim. Persistence/publication keeps its stream/file guarantees.

A watermark shortcut applies the same placement independently to selected pages. Omitted selection means all pages, empty means no placement, invalid page numbers refuse the whole call, and repeats apply once in first-occurrence order. This is image composition, not a general layout operation.

## Resource contract: IMAGE-LIMIT-01

Preparation requires caller-supplied positive **maxEncodedBytes** and **maxDecodedBytes**, per image, with no unmeasured numeric defaults. Measure actual encoded bytes consumed from the current position regardless of advertised length; over-limit input refuses. Exactly the limit permits a complete image, not silently truncated data. Retained data counts toward the encoded budget; arbitrary trailing data is not silently retained outside it.

Before raster allocation/placement, use checked arithmetic for nominal 8-bit sample extent: width * height * components (1/3 for accepted JPEG, 3/4 for PNG). This decoded-byte budget applies even to JPEG pass-through. Exactly the budget is accepted if other gates pass; exceeding it is a resource-limit refusal. PNG decompression must match calculated row extent including one filter byte per row, and stops on excess/truncation. Filter bytes are processing overhead, not pixels against the caller budget; alpha extraction is included in RGBA's four components.

Retained encoded samples, decoded pixels/mask storage and decoder work remain proportional to accepted encoded/raster extents, using checked allocation sizes and bounded chunk/row processing. Never allocate from unchecked headers. Each accepted image's data lives until its owning document/workflow releases it; aggregate storage can grow with submitted images. Callers control image count/aggregate budgets; no whole-document memory cap, automatic disk spill or pixel-rendering performance claim is made.

## Authorization, failure and cancellation: IMAGE-FAIL-01

Existing-page placement modifies page content: require general-modification authorization, refuse every certifying DocMDP level, and require explicit existing-signature-breaking opt-in after approval signatures/document timestamps. Opt-in never overrides permissions/certification/locks. Preserve field locks; refuse if protected structures cannot be preserved. Apply [security](security.md) Reader-rights rules including Q-33 signed removal-dependent refusal. Placement neither edits nor abandons XFA. PDF/A inputs receive normal checks without conformance guarantees.

One placement, image-to-PDF creation or watermark call is atomic in pending state. Bad image/page, decode/limit/authorization failure, I/O or observed cancellation commits none of that call, retains earlier edits and writes no destination. Watermark failure on a late page leaves no earlier selected page stamped. Check cancellation before preparation, between input chunks/decoded rows/selected pages and before pending-state commit; blocked synchronous caller I/O need not be interruptible. Borrowed streams stay open, positions are not restored, and failed-call owned data is released. Report cleanup failure separately.

Use [shared failure categories](shared.md#shared-fail-01-failure-categories). Explicit save/sign/workflow persistence retains its own destination/publication and failed-append unusability rules. Successful preparation does not exempt later save/sign from its limits. No automatic retry, output rollback or whole-workflow reuse follows failure.

## Acceptance map

All targets are **unexecuted**. Use independent rendering for geometry/alpha/painting order and a second PDF reader for structure; structural checks alone do not prove rendering.

| ID | Scenario |
| --- | --- |
| IMAGE-A1 | Grayscale/RGB JPEG, RGB/RGBA PNG and RGB tRNS: alpha/color retained; unsupported/profile-bearing variants refuse without pending change. |
| IMAGE-A2 | Crop offset, inherited 90/180/270 rotation and nondefault UserUnit: independent rendering confirms physical rectangle, center rotation and clipping. |
| IMAGE-A3 | Explicit stretching, default contain/center, opacity 0/0.5/1 and foreground/background under opaque content/annotations. |
| IMAGE-A4 | Actual encoded/nominal raster budgets at/exceeding boundary, false length, PNG excess rows, bad chunks/CRC and arithmetic overflow: no unchecked allocation/partial placement. |
| IMAGE-A5 | Watermark repeats/empty/invalid selection and source I/O/cancellation: whole-call atomicity and ownership. Inject failure on a later selected page after an earlier placement is provisionally prepared; no page from the failed call remains stamped, and earlier successful calls survive. |
| IMAGE-A6 | Password denial, certification, approval-signature opt-in and Q-33 signed rights: preservation and refusal before output. |
| IMAGE-A7 | Save/reopen new/incremental output: version, original prefix, resources/soft masks and later sign/save failures separately. |

## Remaining engineering gaps

Image APIs/decoding/placement remain planned. Implement/measure bounded preparation, choose additive API spelling for caller budgets, and verify color-declaration recognition, physical transforms, soft masks and restoration. No render, runtime or performance check ran. The initial subset may expand through reviewed contract changes; it is not an enduring scope exclusion.

## Adoption record

Approved by the owner on 2026-10-31 after the supplied agent review was addressed: displayed-crop physical-point coordinates, baseline grayscale/RGB JPEG and 8-bit RGB/RGBA PNG with tRNS/alpha, explicit embedded-profile refusal, and caller-supplied encoded/raster budgets without arbitrary defaults. IMAGE-A5 explicitly exercises late watermark failure and preservation of earlier calls. This clarifies verification of existing atomicity; all targets remain unexecuted and image implementation remains planned. No existing image API compatibility or qualification claim changes. History: [images](drafting/history.md#images).
