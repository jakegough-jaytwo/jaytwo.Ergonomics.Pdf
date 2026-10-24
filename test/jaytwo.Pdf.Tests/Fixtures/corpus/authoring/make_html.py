"""Writes the generated HTML sources for the browser-exported corpus files.

Usage: make_html.py outdir
  edge-handbook.html   a policy handbook with headings and internal links (Edge, tagged, outline)
  chrome-ledger.html   a 2,000-page shipment ledger (Chrome), one page per day
"""
import sys
from pathlib import Path

out = Path(sys.argv[1])

sections = ["Purpose", "Scope", "Working hours", "Leave", "Travel", "Expenses", "Equipment", "Security",
            "Data handling", "Conduct", "Grievances", "Health and safety", "Training", "Benefits", "Separation"]
parts = ['<!DOCTYPE html><html lang="en"><head><meta charset="utf-8"><title>Employee Handbook</title>'
         '<meta name="author" content="Example Freight People Team"><style>@page{size:A4;margin:20mm}'
         'body{font-family:Georgia,serif;font-size:11pt}h1{page-break-before:always}nav a{display:block}</style>'
         '</head><body><h2>Contents</h2><nav>']
parts += [f'<a href="#s{i}">{i + 1}. {name}</a>' for i, name in enumerate(sections)]
parts.append('</nav>')
for i, name in enumerate(sections):
    parts.append(f'<h1 id="s{i}">{i + 1}. {name}</h1>')
    for j in range(1, 4):
        parts.append(f'<h2>{i + 1}.{j} {name} detail {j}</h2>')
        parts.append('<p>' + ' '.join([f'{name} policy text, clause {i + 1}.{j}.{k}: employees follow the documented process '
                                        f'and record exceptions with their manager.' for k in range(1, 7)]) + '</p>')
    parts.append(f'<p><a href="#s{(i + 1) % len(sections)}">Next section</a> · <a href="https://example.com/handbook">Online version</a></p>')
parts.append('</body></html>')
(out / 'edge-handbook.html').write_text('\n'.join(parts), encoding='utf-8', newline='\n')

pages = 2000
lanes = ["ZRH-CGN", "CGN-AMS", "AMS-LHR", "LHR-DUB", "DUB-JFK", "JFK-ORD", "ORD-DEN", "DEN-SFO"]
parts = ['<!DOCTYPE html><html lang="en"><head><meta charset="utf-8"><title>Shipment Ledger 2026</title>'
         '<meta name="author" content="Example Freight Reporting"><style>@page{size:Letter;margin:12mm}'
         'body{font-family:Consolas,monospace;font-size:9pt}section{page-break-after:always}'
         'pre{margin:0}</style></head><body>']
for p in range(pages):
    # One <pre> block per page keeps Chrome's tagged structure tree to a few elements per page.
    lines = [f'Ledger page {p + 1} of {pages}', 'Shipment  Lane     Kg    Status']
    for r in range(20):
        n = p * 20 + r
        lines.append(f'S{n:07d}  {lanes[n % 8]}  {(n * 37) % 9000 + 100:>4}  {"DELIVERED" if n % 9 else "HELD"}')
    parts.append('<section><pre>' + '\n'.join(lines) + '</pre></section>')
parts.append('</body></html>')
(out / 'chrome-ledger.html').write_text('\n'.join(parts), encoding='utf-8', newline='\n')
