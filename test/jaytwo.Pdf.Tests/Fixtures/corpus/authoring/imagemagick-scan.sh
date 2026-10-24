#!/bin/sh
# Builds imagemagick-scan.pdf: three 150 dpi grayscale "scanned" delivery receipts (JPEG in PDF) with ImageMagick 7.
# Usage: imagemagick-scan.sh outdir
set -e
out="$1"
for n in 1 2 3; do
  magick -size 1275x1650 xc:white -font Arial -pointsize 34 -fill black \
    -annotate +90+140 "EXAMPLE FREIGHT LLC - DELIVERY RECEIPT $n" \
    -pointsize 26 -annotate +90+220 "Consignment: C-2026-00$n   Driver: TRK-10$n" \
    -annotate +90+270 "Delivered to: Example Customer GmbH, Koln" \
    -annotate +90+320 "Pieces: 1$n   Weight: 4${n}0 kg   Condition: good" \
    -annotate +90+1400 "Received by: ______________________" \
    -attenuate 0.4 +noise Gaussian -rotate "0.$n" -colorspace Gray -density 150 -units PixelsPerInch \
    -gravity center -extent 1275x1650 "$out/receipt-$n.png"
done
magick "$out/receipt-1.png" "$out/receipt-2.png" "$out/receipt-3.png" -compress JPEG -quality 60 -density 150 -units PixelsPerInch \
  -define pdf:Title="Scanned delivery receipts" -define pdf:Author="Example Freight Dispatch" "$out/imagemagick-scan.pdf"
rm "$out"/receipt-*.png
