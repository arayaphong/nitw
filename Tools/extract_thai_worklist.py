"""Build/refresh Thai translation work files from the game's Yarn sources.

Reads every original Yarn file saved under ThaiMod/source/yarn/ (extracted
from the local Steam install) and writes one work file per Yarn file to
ThaiMod/translations/<name>.json:

  {"file": "BusStation.yarn", "kind": "bubble", "act": "A1",
   "lines": [{"id": "f53579", "node": "EnterLevel", "act": "A0",
              "type": "line|option|shortcut|narration", "speaker": "Mae",
              "en": "...", "th": "", "note": ""}]}

Re-running never discards work: existing "th"/"note" values are kept by
line ID, and the old single-scene files in Tools/ are migrated once.
Also writes ThaiMod/translations/INDEX.md and _index.json (lines, words and
translated counts per file and per act) for planning by chapter.
Everything here contains game text and stays local.
"""
import collections
import json
import pathlib
import re

WORK = pathlib.Path(__file__).resolve().parents[1]
SRC = WORK / 'ThaiMod/source/yarn'
OUT = WORK / 'ThaiMod/translations'
LEGACY = [WORK / 'Tools/station_dialogue_th.json', WORK / 'Tools/intro_poem_th.json']
# Display surface per Yarn file; everything else uses speech bubbles until a
# later phase identifies other UIs (laptop chat, journal...).
KINDS = {'IntroPoem.yarn': 'poem'}

LINE_ID = re.compile(r'#line:([0-9a-f]+)')
SPEAKER = re.compile(r'([A-Za-z][A-Za-z0-9_ ]*):\s*(.*)$')
ACT = re.compile(r'A(\d)D(\d+)')


def parse(path):
    """Yield (node, type, speaker, en, id) for every line carrying a line ID."""
    node, body = None, False
    for raw in path.read_text(encoding='utf-8').splitlines():
        s = raw.strip()
        m = re.match(r'title:\s*(.+)$', s)
        if m and not body:
            node = m.group(1).strip(); continue
        if s == '---':
            body = True; continue
        if s == '===':
            body = False; continue
        if not body:
            continue
        m = LINE_ID.search(s)
        if not m:
            continue
        lid, text = m.group(1), s[:m.start()].rstrip()
        if text.startswith('[['):
            o = re.match(r'\[\[(.*?)\|', text)
            yield node, 'option', '', (o.group(1) if o else text).strip(), lid
        elif text.startswith('->'):
            t = re.sub(r'(\s*<<[^>]*>>)+\s*$', '', text[2:]).strip()
            yield node, 'shortcut', '', t, lid
        else:
            sp = SPEAKER.match(text)
            if sp:
                yield node, 'line', sp.group(1).strip(), sp.group(2), lid
            else:
                yield node, 'narration', '', text, lid


def words(s):
    return len(re.sub(r'\{[^}]*\}', '', s).split())


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    legacy, keep = {}, set()
    for p in LEGACY:
        if p.exists():
            d = json.loads(p.read_text(encoding='utf-8'))
            keep |= set(d.get('keepTogether', []))
            for e in d['entries']:
                legacy[e['id']] = e['text']
    keep_path = OUT / 'keep_together.json'
    if keep_path.exists():
        keep |= set(json.loads(keep_path.read_text(encoding='utf-8')))
    keep_path.write_text(json.dumps(sorted(keep), ensure_ascii=False, indent=1) + '\n', encoding='utf-8')

    # A few line IDs are reused by unrelated files with different text; they
    # are kept apart by file, and the runtime disambiguates by original text.
    owners = collections.defaultdict(list)
    for src in sorted(SRC.glob('*.yarn')):
        for _, _, _, _, lid in parse(src):
            owners[lid].append(src.name)
    seen_ids, index = {}, []
    line_acts = collections.defaultdict(lambda: [0, 0, 0])
    for src in sorted(SRC.glob('*.yarn')):
        lines = list(parse(src))
        if not lines:
            continue
        out = OUT / (src.stem + '.json')
        old = {}
        if out.exists():
            for e in json.loads(out.read_text(encoding='utf-8'))['lines']:
                old[e['id']] = e
        acts = collections.Counter()
        rows = []
        for node, typ, speaker, en, lid in lines:
            if lid in seen_ids and seen_ids[lid] == src.name:
                raise SystemExit(f'line id {lid} repeated inside {src.name}')
            seen_ids[lid] = src.name
            m = ACT.search(node or '')
            act = f'A{m.group(1)}' if m else ''
            if act:
                acts[act] += words(en)
            prev = old.get(lid, {})
            row = dict(id=lid, node=node, act=act, type=typ, speaker=speaker, en=en,
                       th=prev.get('th') or legacy.get(lid, ''), note=prev.get('note', ''))
            others = [f for f in owners[lid] if f != src.name]
            if others:
                row['shared_id'] = others
            rows.append(row)
        file_act = 'LN' if src.name.startswith('LN2') else (acts.most_common(1)[0][0] if acts else '')
        for r in rows:
            r['act'] = 'LN' if file_act == 'LN' else (r['act'] or file_act)
        doc = dict(file=src.name, kind=KINDS.get(src.name, 'bubble'), act=file_act, lines=rows)
        out.write_text(json.dumps(doc, ensure_ascii=False, indent=1) + '\n', encoding='utf-8')
        index.append(dict(file=src.name, act=file_act, kind=doc['kind'], lines=len(rows),
                          words=sum(words(r['en']) for r in rows),
                          translated=sum(1 for r in rows if r['th'])))
        for r in rows:
            la = line_acts[r['act'] or '?']
            la[0] += 1; la[1] += words(r['en']); la[2] += 1 if r['th'] else 0

    (OUT / '_index.json').write_text(json.dumps(index, indent=1) + '\n', encoding='utf-8')
    md = ['# Thai translation index', '',
          'Per line: act from the node name (A#D#); untagged nodes take the file\'s main act ("?" = none).', '',
          '| act | lines | words | translated |', '|---|---|---|---|']
    for act in sorted(line_acts):
        l, w, t = line_acts[act]
        md.append(f'| {act} | {l} | {w} | {t} |')
    md += ['', '| file | act | kind | lines | words | translated |', '|---|---|---|---|---|---|']
    for i in sorted(index, key=lambda i: (i['act'], i['file'])):
        md.append(f"| {i['file']} | {i['act']} | {i['kind']} | {i['lines']} | {i['words']} | {i['translated']} |")
    (OUT / 'INDEX.md').write_text('\n'.join(md) + '\n', encoding='utf-8')
    total = sum(i['lines'] for i in index)
    print(f'{len(index)} work files, {total} lines, {sum(i["translated"] for i in index)} translated, '
          f'{len(keep)} keep-together words')
    missing = set(legacy) - set(seen_ids)
    if missing:
        raise SystemExit(f'legacy translations not found in Yarn: {sorted(missing)}')


if __name__ == '__main__':
    main()
