"""Recover the first gameplay scene's presentation and scoped Yarn program."""
import os
import hashlib
import json
import pathlib
import re
import UnityPy

ROOT = pathlib.Path(os.environ.get("NITW_GAME_DATA", str(pathlib.Path.home() / ".local/share/Steam/steamapps/common/Night in the Woods/Night in the Woods_Data")))
WORK = pathlib.Path(os.environ.get("NITW_WORKSPACE", str(pathlib.Path(__file__).resolve().parents[1])))
DEST = WORK / 'NITW-Unity-Reconstruction/Assets/Resources/RecoveredPoem'
EVIDENCE = WORK / 'research/intro-poem'
DEST.mkdir(parents=True, exist_ok=True)
EVIDENCE.mkdir(parents=True, exist_ok=True)
env = UnityPy.load(str(ROOT / 'level10'))
objects = {o.path_id: o for o in env.objects}
gos = {o.path_id: o.read() for o in env.objects if o.type.name == 'GameObject'}
assets = {o.path_id: o for o in UnityPy.load(str(ROOT / 'sharedassets10.assets')).objects}
shared = {o.path_id: o for o in UnityPy.load(str(ROOT / 'sharedassets0.assets')).objects}

def pose(transform):
    t = transform.read_typetree()
    return dict(position=t['m_LocalPosition'], rotation=t['m_LocalRotation'], scale=t['m_LocalScale'])

def component(go, kind):
    return next(objects[c.component.m_PathID] for c in gos[go].m_Component
                if objects[c.component.m_PathID].type.name == kind)

font = assets[5].read()
(DEST / 'PoemFont.ttf').write_bytes(bytes(font.m_FontData))
text = assets[4].read().m_Script
if isinstance(text, bytes):
    text = text.decode('utf-8')
(DEST / 'IntroPoem.yarn.txt').write_text(text)
(EVIDENCE / 'IntroPoem.yarn').write_text(text)

# Only compile the syntax present in this shipped node; reject any other form.
body = text.split('---\n', 1)[1].split('===', 1)[0]
rows = [(len(line) - len(line.lstrip()), re.sub(r'\s+#line:[^\s]+\s*$', '', line.strip()),
         re.search(r'#line:([^\s]+)', line).group(1) if '#line:' in line else '')
        for line in body.splitlines() if line.strip()]

def parse(start, indent):
    steps = []
    i = start
    while i < len(rows) and rows[i][0] >= indent:
        depth, value, line_id = rows[i]
        if depth != indent:
            raise ValueError(f'Unexpected indentation at {i}: {value}')
        if value.startswith('-> '):
            options = []
            while i < len(rows) and rows[i][0] == indent and rows[i][1].startswith('-> '):
                label = rows[i][1][3:]
                option_id = rows[i][2]
                i += 1
                children = []
                if i < len(rows) and rows[i][0] > indent:
                    children, i = parse(i, rows[i][0])
                options.append(dict(text=label, lineId=option_id, steps=children))
            steps.append(dict(op='choice', options=options))
        elif value.startswith('<<') and value.endswith('>>'):
            steps.append(dict(op='command', text=value[2:-2].strip()))
            i += 1
        elif value.startswith('Mae:'):
            steps.append(dict(op='line', text=value.split(':', 1)[1], lineId=line_id))
            i += 1
        else:
            raise ValueError(f'Unsupported source syntax: {value}')
    return steps, i

program, consumed = parse(0, 0)
assert consumed == len(rows)
choices = [s for s in program if s['op'] == 'choice']
assert [len(s['options']) for s in choices] == [2, 3, 4]
assert program[-1]['text'] == 'scene BusStation'
program_data = dict(sourceSha256=hashlib.sha256(text.encode()).hexdigest(), node='Start', steps=program)
(DEST / 'Program.json').write_text(json.dumps(program_data, indent=2))

sprites = []
nodes = []
def export_sprite(path_id):
    sprite = shared[path_id].read()
    key = str(path_id)
    if not any(s['key'] == key for s in sprites):
        image = sprite.m_RD.texture.read().image
        assert image.size == (round(sprite.m_Rect.width), round(sprite.m_Rect.height))
        image.save(DEST / (key + '.png'))
        sprites.append(dict(key=key, name=sprite.m_Name, ppu=sprite.m_PixelsToUnits,
                            pivot=dict(x=sprite.m_Pivot.x, y=sprite.m_Pivot.y)))
    return key

for go_id in [28, 10, 29]:
    go = gos[go_id]
    renderer = component(go_id, 'SpriteRenderer').read()
    key = export_sprite(renderer.m_Sprite.m_PathID)
    nodes.append(dict(id=go_id, name=go.m_Name, transform=pose(component(go_id, 'Transform')),
                      sprite=key, color=dict(r=renderer.m_Color.r,
                      g=renderer.m_Color.g, b=renderer.m_Color.b, a=renderer.m_Color.a)))
cam = component(15, 'Camera').read_typetree()
layout = dict(source=str(ROOT / 'level10'), sourceSha256=hashlib.sha256((ROOT / 'level10').read_bytes()).hexdigest(),
              camera=pose(component(15, 'Transform')), fov=cam['field of view'],
              columns=[pose(objects[47])['position'], pose(objects[38])['position']],
              text=assets[9].read_typetree(), fontName=font.m_Name, fontSize=font.m_FontSize,
              fontLineSpacing=font.m_LineSpacing, fontAscent=font.m_Ascent, sprites=sprites, nodes=nodes,
              keyboardConfirmFrames=[export_sprite(6846), export_sprite(7529)],
              windEvent='event:/intro_poem/wind', windParameter='wind_intensity',
              scope='Menu -> IntroPoem only. Preserve BusStation directive as data; runtime halts there. '
                    'shuffleNextOptions is handled by Yarn.Compiler and Yarn.VirtualMachine, '
                    'using a forward Fisher-Yates shuffle, then reset after that choice group.')
(DEST / 'Scene.json').write_text(json.dumps(layout, indent=2))
(EVIDENCE / 'recovered-source.json').write_text(json.dumps(dict(layout=layout, program=program_data), indent=2))
print('Recovered original Goudy font, three scene sprite nodes, two columns, Start node; choices=2/3/4, paths=24.')
