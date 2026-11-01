"""Merge the raw run records of the W-218 font size-limit evidence into one JSON file.

python -I w218-font-limit-merge.py --evidence E.json --itext I.json --padding P.json --out OUT.json
Also records the download provenance table for the acquired fonts (URLs as fetched on 2026-10-31).
"""
import argparse
import hashlib
import json

GH = 'https://github.com/'
RAW = 'https://raw.githubusercontent.com/google/fonts/main/'
NF = GH + 'notofonts/notofonts.github.io/raw/main/fonts/'
PROVENANCE = {
    'NotoSans-Regular.ttf': (NF + 'NotoSans/full/ttf/NotoSans-Regular.ttf', 'SIL OFL 1.1 (font name table)'),
    'NotoSansArabic-Regular.ttf': (NF + 'NotoSansArabic/full/ttf/NotoSansArabic-Regular.ttf', 'SIL OFL 1.1 (font name table)'),
    'NotoNaskhArabic-Regular.ttf': (NF + 'NotoNaskhArabic/full/ttf/NotoNaskhArabic-Regular.ttf', 'SIL OFL 1.1 (font name table)'),
    'NotoSansHebrew-Regular.ttf': (NF + 'NotoSansHebrew/full/ttf/NotoSansHebrew-Regular.ttf', 'SIL OFL 1.1 (font name table)'),
    'NotoSansDevanagari-Regular.ttf': (NF + 'NotoSansDevanagari/full/ttf/NotoSansDevanagari-Regular.ttf', 'SIL OFL 1.1 (font name table)'),
    'NotoSansThai-Regular.ttf': (NF + 'NotoSansThai/full/ttf/NotoSansThai-Regular.ttf', 'SIL OFL 1.1 (font name table)'),
    'NotoSansTamil-Regular.ttf': (NF + 'NotoSansTamil/full/ttf/NotoSansTamil-Regular.ttf', 'SIL OFL 1.1 (font name table)'),
    'LiberationSans-Regular.ttf': (GH + 'liberationfonts/liberation-fonts/files/7261482/liberation-fonts-ttf-2.1.5.tar.gz (extracted)', 'SIL OFL 1.1 (font name table)'),
    'Amiri-Regular.ttf': (RAW + 'ofl/amiri/Amiri-Regular.ttf', 'SIL OFL 1.1 (font name table)'),
    'Hind-Regular.ttf': (RAW + 'ofl/hind/Hind-Regular.ttf', 'SIL OFL 1.1 (font name table)'),
    'Sarabun-Regular.ttf': (RAW + 'ofl/sarabun/Sarabun-Regular.ttf', 'SIL OFL 1.1 (font name table)'),
    'MPLUS1p-Regular.ttf': (RAW + 'ofl/mplus1p/MPLUS1p-Regular.ttf', 'SIL OFL 1.1 (font name table)'),
    'Kosugi-Regular.ttf': (RAW + 'apache/kosugi/Kosugi-Regular.ttf', 'Apache 2.0 (font name table)'),
    'NanumGothic-Regular.ttf': (RAW + 'ofl/nanumgothic/NanumGothic-Regular.ttf', 'OFL 1.1 by the google/fonts ofl/ directory only; the name table names NHN Corporation and no license text (not independently verified)'),
    'DroidSansFallbackFull.ttf': (GH + 'aosp-mirror/platform_frameworks_base/raw/android-4.4_r1/data/fonts/DroidSansFallbackFull.ttf', 'Apache 2.0 (font name table)'),
    'LXGWWenKai-Regular.ttf': (GH + 'lxgw/LxgwWenKai/releases/download/v1.521/LXGWWenKai-Regular.ttf', 'SIL OFL 1.1 (font name table)'),
    'NotoSansSC-var.ttf': (RAW + 'ofl/notosanssc/NotoSansSC%5Bwght%5D.ttf', 'SIL OFL 1.1 (font name table); variable, ineligible under FORM-FONT-03, comparator only'),
    'Roboto-Regular.ttf': (RAW + 'ofl/roboto/Roboto%5Bwdth%2Cwght%5D.ttf', 'SIL OFL 1.1 (font name table); variable, ineligible under FORM-FONT-03, comparator only'),
}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--evidence')
    ap.add_argument('--itext')
    ap.add_argument('--padding')
    ap.add_argument('--out')
    a = ap.parse_args()
    load = lambda p: json.load(open(p, encoding='utf-8'))
    ev, it, pad = load(a.evidence), load(a.itext), load(a.padding)
    for f in ev.get('acquired_fonts', []):
        url, lic = PROVENANCE.get(f['file'], (None, None))
        f['download_url'] = url
        f['license_recorded'] = lic
    out = {'description': 'W-218 font size-limit evidence: raw records. Independent research; not target-library evidence.',
           'merge_script_sha256': hashlib.sha256(open(__file__, 'rb').read()).hexdigest(),
           'embedder_and_font_run': ev, 'itext_run': it, 'synthetic_size_ladder': pad}
    open(a.out, 'w', encoding='utf-8').write(json.dumps(out, indent=1, ensure_ascii=False))


if __name__ == '__main__':
    main()
