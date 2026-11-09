"""Report which game lines were reached in play but are not translated yet.

The mod's trace.log records every Yarn key the game fetches (the game fetches
a whole node's lines when the node starts). This tool merges the keys from
the given trace logs into ThaiMod/reports/seen_keys.json (kept across
sessions, since a reinstall clears trace.log), then writes
ThaiMod/reports/coverage.md: translated/total per act and per file, and the
untranslated lines that were reached, grouped by file and node, in the order
they were first seen.

Usage: python3 -I Tools/thai_coverage.py [trace.log ...]
       (default: the Steam install's and the dev copy's NITWThai/trace.log)
"""
import collections
import json
import pathlib
import sys

WORK = pathlib.Path(__file__).resolve().parents[1]
TRANS = WORK / 'ThaiMod/translations'
REPORTS = WORK / 'ThaiMod/reports'
DEFAULT_TRACES = [
    pathlib.Path.home() / '.local/share/Steam/steamapps/common/Night in the Woods/NITWThai/trace.log',
    WORK / 'ThaiMod/game/NITWThai/trace.log',
]


def main(paths):
    REPORTS.mkdir(parents=True, exist_ok=True)
    seen_path = REPORTS / 'seen_keys.json'
    seen = json.loads(seen_path.read_text()) if seen_path.exists() else []
    known = set(seen)
    for p in paths:
        if not p.exists():
            continue
        for line in p.read_text(encoding='utf-8', errors='replace').splitlines():
            f = line.split('\t')
            if len(f) == 3 and f[1] == 'key' and f[2].startswith('line:') and f[2] not in known:
                known.add(f[2]); seen.append(f[2])
    seen_path.write_text(json.dumps(seen, indent=0) + '\n')

    lines = {}
    for wf in sorted(TRANS.glob('*.json')):
        if wf.name.startswith('_') or wf.name in ('keep_together.json', 'no_break_after.json'):
            continue
        d = json.loads(wf.read_text(encoding='utf-8'))
        for e in d['lines']:
            lines.setdefault('line:' + e['id'], []).append((d['file'], e))

    per_file = collections.defaultdict(lambda: [0, 0, 0])   # total, translated, seen
    per_act = collections.defaultdict(lambda: [0, 0, 0])
    for key, owners in lines.items():
        for file, e in owners:
            for bucket in (per_file[file], per_act[e['act'] or '?']):
                bucket[0] += 1
                bucket[1] += 1 if e['th'] else 0
                bucket[2] += 1 if key in known else 0

    todo = collections.OrderedDict()
    for key in seen:
        for file, e in lines.get(key, []):
            if not e['th']:
                todo.setdefault((file, e['node']), []).append(e)

    md = ['# Thai coverage', '',
          f'Keys seen in play: {len(seen)}. Seen but untranslated: {sum(len(v) for v in todo.values())}.', '',
          '| act | lines | translated | seen in play |', '|---|---|---|---|']
    for act in sorted(per_act):
        t, tr, s = per_act[act]
        md.append(f'| {act} | {t} | {tr} | {s} |')
    md += ['', '## Reached in play, not translated (first-seen order)', '']
    for (file, node), es in todo.items():
        md.append(f'### {file} / {node} ({len(es)})')
        for e in es:
            who = e['speaker'] or e['type']
            md.append(f"- `{e['id']}` {who}: {e['en']}")
        md.append('')
    md += ['## Files with any progress', '', '| file | lines | translated | seen |', '|---|---|---|---|']
    for file in sorted(per_file):
        t, tr, s = per_file[file]
        if tr or s:
            md.append(f'| {file} | {t} | {tr} | {s} |')
    (REPORTS / 'coverage.md').write_text('\n'.join(md) + '\n', encoding='utf-8')
    print(f'seen {len(seen)} keys; untranslated reached: {sum(len(v) for v in todo.values())} '
          f'in {len(todo)} nodes; report: {REPORTS / "coverage.md"}')


if __name__ == '__main__':
    main([pathlib.Path(a) for a in sys.argv[1:]] or DEFAULT_TRACES)
