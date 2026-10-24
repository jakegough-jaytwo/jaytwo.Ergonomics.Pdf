#!/bin/sh
# Builds the image assets the Office, browser, and Inkscape corpus sources reference, with ImageMagick 7.
# Usage: make_assets.sh outdir   (the sources expect outdir to be ./assets next to them)
set -e
mkdir -p "$1"
magick -size 1600x1067 -seed 42 plasma:steelblue-wheat -blur 0x2 -quality 88 "$1/photo1.jpg"
magick -size 1600x1067 -seed 7 plasma:darkgreen-khaki -quality 88 "$1/photo2.jpg"
magick -size 1600x1067 -seed 99 plasma:firebrick-gold -swirl 120 -quality 88 "$1/photo3.jpg"
magick -size 600x200 xc:white -fill navy -font Arial -pointsize 64 -gravity center -annotate 0 "Example Freight" "$1/logo.png"
