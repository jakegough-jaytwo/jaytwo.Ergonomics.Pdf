"""Independent drafting arithmetic; no target-library or PDF-reader verification."""

import hashlib
import json
import platform
import sys
from pathlib import Path
from xml.etree import ElementTree


def canonical(octets):
    if 0 in octets:
        raise ValueError("NUL is not PDF name content")
    excluded = b"()<>[]{}/%#"
    return "".join(
        chr(b) if 0x21 <= b <= 0x7E and b not in excluded else f"#{b:02X}"
        for b in octets
    )


def parse_canonical(value):
    out = bytearray()
    i = 0
    while i < len(value):
        if value[i] == "#":
            if i + 3 > len(value):
                raise ValueError("short escape")
            out.append(int(value[i + 1:i + 3], 16))
            i += 3
        else:
            out.append(ord(value[i]))
            i += 1
    if canonical(out) != value:
        raise ValueError("not canonical")
    return bytes(out)


def point(matrix, x, y):
    a, b, c, d, e, f = matrix
    return a * x + c * y + e, b * x + d * y + f


def mapping(bbox, matrix, rect):
    corners = [(x, y) for x in (bbox[0], bbox[2]) for y in (bbox[1], bbox[3])]
    transformed = [point(matrix, *p) for p in corners]
    left, bottom = map(min, zip(*transformed))
    right, top = map(max, zip(*transformed))
    sx = (rect[2] - rect[0]) / (right - left)
    sy = (rect[3] - rect[1]) / (top - bottom)
    fit = (sx, 0, 0, sy, rect[0] - left * sx, rect[1] - bottom * sy)
    mapped = [point(fit, *p) for p in transformed]
    extents = [min(p[0] for p in mapped), min(p[1] for p in mapped),
               max(p[0] for p in mapped), max(p[1] for p in mapped)]
    return {"bbox": bbox, "matrix": matrix, "rect": rect,
            "transformed_corners": transformed, "fit_matrix": fit,
            "mapped_corners": mapped, "extents": extents,
            "extents_match_rect": extents == list(rect)}


def main():
    names = [bytes([b]) for b in range(1, 256)] + [b"", b"AB", b"A#42", b"Off", b"off", b"\xfe\xff"]
    spellings = [canonical(n) for n in names]
    names_ok = len(set(spellings)) == len(set(names)) and all(
        parse_canonical(canonical(n)) == n for n in names
    )
    invalid = ["#00", "#fe", "#41", "#", "/", " "]
    rejected = []
    for value in invalid:
        try:
            parse_canonical(value)
        except ValueError:
            rejected.append(value)

    maps = [mapping(*case) for case in [
        ((0, 0, 20, 10), (1, 0, 0, 1, 0, 0), (100, 200, 140, 220)),
        ((0, 0, 20, 10), (0, 1, -1, 0, 10, 0), (100, 200, 130, 260)),
        ((0, 0, 10, 10), (2, 0, 0, 3, 5, 7), (100, 200, 120, 230)),
        ((-5, -10, 5, 10), (0, -1, 1, 0, 0, 0), (100, 200, 160, 230)),
    ]]
    # Conservative round-join/butt-cap stroke envelope, no raster assumption.
    marks = []
    for w, h in [(20, 20), (40, 10), (10, 40), (1, 1)]:
        m = min(w, h)
        bounds = (0.20 * w - 0.05 * m, 0.30 * h - 0.05 * m,
                  0.80 * w + 0.05 * m, 0.75 * h + 0.05 * m)
        marks.append({"content_size": [w, h], "tick_stroke_envelope": bounds,
                      "contained": bounds[0] >= 0 and bounds[1] >= 0
                      and bounds[2] <= w and bounds[3] <= h})

    # These are XML namespace observations, not schema validation or PDF validity.
    candidates = [
        '<body xmlns="http://www.w3.org/1999/xhtml" '
        'xmlns:xfa="http://www.xfa.org/schema/xfa-data/1.0" '
        'xfa:contentType="text/html" xfa:APIVersion="jaytwo.Pdf:1.0" '
        'xfa:spec="2.4"><p/></body>',
        '<body xmlns="http://www.w3.org/1999/xhtml"><p/></body>',
    ]
    xml = []
    for candidate in candidates:
        root = ElementTree.fromstring(candidate)
        xml.append({"utf8_sha256": hashlib.sha256(candidate.encode()).hexdigest(),
                    "root": root.tag, "attributes": root.attrib,
                    "children": [n.tag for n in root],
                    "character_data": "".join(root.itertext()),
                    "well_formed": True})
    slash_uris_distinct = (
        ElementTree.fromstring('<p xmlns:x="http://www.xfa.org/schema/xfa-data/1.0" x:spec="2.4"/>').attrib
        != ElementTree.fromstring('<p xmlns:x="http://www.xfa.org/schema/xfa-data/1.0/" x:spec="2.4"/>').attrib
    )
    ok = names_ok and len(rejected) == len(invalid) and all(
        x["extents_match_rect"] for x in maps
    ) and all(x["contained"] for x in marks) and slash_uris_distinct
    result = {"kind": "independent drafting arithmetic, not target tests or rendering",
              "python": sys.version, "platform": platform.platform(),
              "script_sha256": hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
              "name_examples": len(names), "name_roundtrips_and_uniqueness": names_ok,
              "noncanonical_or_invalid_rejected": rejected,
              "mapping_cases": maps, "tick_cases": marks,
              "xml_candidates": xml, "namespace_uris_distinct": slash_uris_distinct,
              "all_observations_match_arithmetic_oracles": ok}
    Path(__file__).with_suffix('.json').write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({"name_examples": len(names), "mapping_cases": len(maps),
                      "tick_cases": len(marks), "xml_candidates": len(xml), "ok": ok}))
    return 0 if ok else 1


if __name__ == '__main__':
    raise SystemExit(main())
