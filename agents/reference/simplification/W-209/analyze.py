"""W-209 summarizes observations.jsonl + qpdf-check.json into the tables quoted in findings.md. Run from any directory."""
import json, os, sys
here = os.path.dirname(os.path.abspath(__file__))
rows = [json.loads(l) for l in open(os.path.join(here, 'observations.jsonl'), encoding='utf-8-sig') if '"case"' in l]
qc = {r['id']: r for r in json.load(open(os.path.join(here, 'qpdf-check.json'), encoding='utf-8-sig'))}
mode = sys.argv[1] if len(sys.argv) > 1 else 'all'
print('cases', len(rows))
if mode in ('all', 'valid'):
    bad = [r for r in rows if r['kind'] == 'valid' and r['match'] is not True]
    print('valid ok', sum(1 for r in rows if r['kind'] == 'valid' and r['match'] is True), 'of', sum(1 for r in rows if r['kind'] == 'valid'))
    print('valid NOT matching:', [(r['id'], r['exType'], r['exMsg'][:80]) for r in bad])
    print('qpdf nonzero/warn on valid:', [(i, q['qpdfExit'], q['qpdfSummary'][:90]) for i, q in qc.items() if q['kind'] == 'valid' and (q['qpdfExit'] != 0 or 'WARNING' in q['qpdfSummary'])])
if mode in ('all', 'mal'):
    for site in ('xref', 'objstm', 'xmp', 'attachment'):
        print('---', site)
        for r in rows:
            if r['site'] == site and r['kind'] in ('malformed', 'unsupported'):
                print(r['id'].split('__')[1], '|', r['outcome'], r['exType'], r['inner'], 'match=%s' % r['match'], 'bytes=%s' % r['gotBytes'], '|', r['exMsg'][:60], '| qpdf', qc[r['id']]['qpdfExit'])
if mode in ('all', 'bomb'):
    for r in rows:
        if r['kind'] == 'limit':
            print(r['id'], r['outcome'], r['exType'], r['ms'], 'ms', round(r['allocatedBytes'] / 2**20, 1), 'MiB alloc', 'file', r['fileBytes'], 'peakWS', r['peakWorkingSetMiB'], 'gotBytes', r['gotBytes'], r['exMsg'][:60])
