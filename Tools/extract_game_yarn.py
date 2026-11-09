"""Copy the game's Yarn dialogue sources out of the installed game.

Scans the shared asset files of the local Night in the Woods install for
TextAssets named *.yarn and writes each to ThaiMod/source/yarn/<name>.yarn
(original text, UTF-8). Read-only: the game files are opened, never changed.
The output is game text and stays local.

Game data folder: $NITW_GAME_DATA, default
~/.local/share/Steam/steamapps/common/Night in the Woods/Night in the Woods_Data
Usage: python3 -I Tools/extract_game_yarn.py
"""
import os
import collections
import os
import pathlib

import UnityPy

WORK = pathlib.Path(__file__).resolve().parents[1]
DATA = pathlib.Path(os.environ.get('NITW_GAME_DATA', str(
    pathlib.Path.home() / '.local/share/Steam/steamapps/common/Night in the Woods/Night in the Woods_Data')))
OUT = WORK / 'ThaiMod/source/yarn'


def main():
    if not (DATA / 'Managed').is_dir():
        raise SystemExit(f'not a Night in the Woods data folder: {DATA} (set NITW_GAME_DATA)')
    found = collections.defaultdict(list)
    for path in sorted(DATA.glob('sharedassets*.assets')) + [DATA / 'resources.assets']:
        if not path.exists():
            continue
        env = UnityPy.load(str(path))
        for obj in env.objects:
            if obj.type.name != 'TextAsset':
                continue
            asset = obj.read()
            if not asset.m_Name.endswith('.yarn'):
                continue
            script = asset.m_Script
            text = script if isinstance(script, str) else bytes(script).decode('utf-8', 'replace')
            found[asset.m_Name].append((path.name, text))
    OUT.mkdir(parents=True, exist_ok=True)
    for name, copies in found.items():
        texts = {t for _, t in copies}
        if len(texts) > 1:
            raise SystemExit(f'{name} differs between {[f for f, _ in copies]}; cannot pick one')
        (OUT / name).write_text(copies[0][1], encoding='utf-8')
    print(f'{len(found)} Yarn files -> {OUT}')


if __name__ == '__main__':
    main()
