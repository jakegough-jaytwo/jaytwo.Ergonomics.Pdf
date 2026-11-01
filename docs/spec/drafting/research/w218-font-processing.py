"""Independent fontTools diagnostic, not a PDF embedding or target resource test.
Run with the same Python environment as w218-form-input-survey.py.
No installed font bytes or generated font programs are written to disk.
"""
from pathlib import Path
import hashlib
import io
import json
import platform
import subprocess
import sys
import time
import tracemalloc
import fontTools
from fontTools.ttLib import TTFont
from fontTools import subset

NAMES = ['arial.ttf', 'timesi.ttf', 'tahoma.ttf', 'sylfaen.ttf', 'LeelawUI.ttf', 'malgun.ttf']
if len(sys.argv) > 1:
    path = Path('C:/Windows/Fonts') / sys.argv[1]
    data = path.read_bytes()
    tracemalloc.start()
    start = time.process_time()
    font = TTFont(io.BytesIO(data), lazy=False, recalcTimestamp=False)
    before = font['maxp'].numGlyphs
    options = subset.Options()
    options.recalc_timestamp = False
    tool = subset.Subsetter(options=options)
    tool.populate(unicodes=range(32, 127))
    tool.subset(font)
    output = io.BytesIO()
    font.save(output)
    elapsed = time.process_time() - start
    _, peak = tracemalloc.get_traced_memory()
    print(json.dumps({'file': path.name, 'sha256': hashlib.sha256(data).hexdigest(),
                      'input_bytes': len(data), 'input_glyphs': before,
                      'output_bytes': output.tell(), 'output_glyphs': font['maxp'].numGlyphs,
                      'python_traced_peak_bytes_excluding_input': peak, 'process_cpu_seconds': elapsed,
                      'output_sha256': hashlib.sha256(output.getvalue()).hexdigest()}))
else:
    rows = []
    for name in NAMES:
        for repeat in range(2):
            run = subprocess.run([sys.executable, str(Path(__file__).resolve()), name],
                                 capture_output=True, text=True, check=True, timeout=90)
            row = json.loads(run.stdout)
            row.update(repeat=repeat + 1, diagnostics=run.stderr.strip())
            rows.append(row)
            print(name, repeat + 1, row['input_bytes'], row['output_bytes'],
                  row['python_traced_peak_bytes_excluding_input'], round(row['process_cpu_seconds'], 3), flush=True)
    result = {'classification': 'Measured independent fontTools ASCII subsetting diagnostic only; not PDF embedding, shaping, target memory/work, a bound or qualification',
              'python': sys.version, 'platform': platform.platform(), 'fontTools': fontTools.__version__,
              'workload': '95 ASCII scalars; each repeat in a fresh process; tracemalloc enabled; input loaded before tracing; output in memory only',
              'rows': rows}
    Path(__file__).with_suffix('.json').write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
