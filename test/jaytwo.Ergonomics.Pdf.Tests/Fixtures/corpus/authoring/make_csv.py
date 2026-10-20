"""Writes lo-calc-ledger.csv: 3,000 rows of synthetic fuel purchases for the LibreOffice Calc report."""
import sys
stations = ["Basel", "Freiburg", "Karlsruhe", "Mannheim", "Mainz", "Bonn", "Köln", "Venlo"]
with open(sys.argv[1], "w", encoding="utf-8", newline="\n") as out:
    out.write("Receipt,Date,Station,Vehicle,Litres,Price per litre,Total\n")
    for n in range(3000):
        litres = 180 + (n * 53) % 420
        price = 1.55 + ((n * 7) % 40) / 100
        out.write(f"R{n:06d},2026-{1 + n % 12:02d}-{1 + n % 28:02d},{stations[n % 8]},TRK-{100 + n % 60},{litres},{price:.2f},{litres * price:.2f}\n")
