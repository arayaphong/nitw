"""Shape Thai with HarfBuzz/FreeType and bake portable glyphs for Unity.

Translations are keyed by original Yarn line IDs. Only text changes; source
commands, branch identity and stage boundary remain in Program.json.
"""
import os
import hashlib
import json
import pathlib
import re
import regex
import freetype
import uharfbuzz as hb
from fontTools.ttLib import TTFont
from PIL import Image

WORK = pathlib.Path(os.environ.get("NITW_WORKSPACE", str(pathlib.Path(__file__).resolve().parents[1])))
DEST = WORK / 'NITW-Unity-Reconstruction/Assets/Resources/RecoveredPoem'
FONT = pathlib.Path(os.environ["NITW_THAI_FONT"])
translation = json.loads((WORK / 'Tools/intro_poem_th.json').read_text())
entries = {e['id']: e['text'] for e in translation['entries']}
source = (DEST / 'IntroPoem.yarn.txt').read_text()
ids = re.findall(r'#line:([^\s]+)', source)
assert len(entries) == len(ids) == len(set(ids)) and set(entries) == set(ids)
(DEST / 'Thai.json').write_text(json.dumps(translation, ensure_ascii=False, indent=2) + '\n')

size = 64
face = freetype.Face(str(FONT)); face.set_pixel_sizes(0, size)
font = hb.Font(hb.Face(FONT.read_bytes())); font.scale = (size * 64, size * 64)
hb.ot_font_set_funcs(font)
runs, bitmaps = [], {}
for text in dict.fromkeys(part for value in entries.values() for part in value.split('|')):
    # Group combining marks; attach Thai preposed vowels to the following cluster.
    groups = [(m.start(), m.end()) for m in regex.finditer(r'\X', text)]
    merged = []
    i = 0
    while i < len(groups):
        start, end = groups[i]
        if len(text[start:end]) == 1 and '\u0e40' <= text[start] <= '\u0e44' and i + 1 < len(groups):
            end = groups[i + 1][1]; i += 1
        merged.append((start, end)); i += 1
    utf16 = lambda n: len(text[:n].encode('utf-16-le')) // 2
    ends = [utf16(end) for _, end in merged]
    buffer = hb.Buffer(); buffer.add_codepoints([ord(c) for c in text]); buffer.guess_segment_properties()
    hb.shape(font, buffer)
    pen = 0
    glyphs = []
    for info, pos in zip(buffer.glyph_infos, buffer.glyph_positions):
        gid = info.codepoint
        assert gid != 0, f'Missing glyph in {text!r}'
        end = next(utf16(b) for a, b in merged if a <= info.cluster < b)
        if gid not in bitmaps:
            face.load_glyph(gid, freetype.FT_LOAD_RENDER | freetype.FT_LOAD_TARGET_NORMAL)
            bitmap = face.glyph.bitmap
            image = None
            if bitmap.width and bitmap.rows:
                image = Image.frombytes('L', (bitmap.pitch, bitmap.rows), bytes(bitmap.buffer)).crop((0, 0, bitmap.width, bitmap.rows))
            bitmaps[gid] = dict(image=image, left=face.glyph.bitmap_left, top=face.glyph.bitmap_top)
        b = bitmaps[gid]
        x = pen / 64 + pos.x_offset / 64 + b['left']
        y = pos.y_offset / 64 + b['top']
        pen += pos.x_advance
        glyphs.append(dict(gid=gid, x=x, y=y, end=end, advance=pen / 64))
    widths = [max([g['advance'] for g in glyphs if g['end'] <= end] or [0]) for end in ends]
    runs.append(dict(text=text, ends=ends, widths=widths, glyphs=glyphs))

width = 1024
x = y = row_height = 2
for gid, b in sorted(bitmaps.items()):
    im = b['image']
    if im is None:
        continue
    if x + im.width + 2 > width:
        x = 2; y += row_height + 2; row_height = 0
    b['rect'] = (x, y, im.width, im.height)
    x += im.width + 2; row_height = max(row_height, im.height)
height = 1
while height < y + row_height + 2:
    height *= 2
atlas = Image.new('RGBA', (width, height), (255, 255, 255, 0))
for b in bitmaps.values():
    if b['image'] is not None:
        x, y, w, h = b['rect']
        tile = Image.new('RGBA', (w, h), 'white'); tile.putalpha(b['image']); atlas.paste(tile, (x, y))
atlas.save(DEST / 'ThaiGlyphs.png')
for run in runs:
    glyphs = []
    for g in run['glyphs']:
        b = bitmaps[g.pop('gid')]; g.pop('advance')
        if b['image'] is None:
            continue
        x, y, w, h = b['rect']
        g.update(width=w, height=h, uv=dict(x=x / width, y=1 - (y + h) / height, width=w / width, height=h / height))
        glyphs.append(g)
    run['glyphs'] = glyphs
data = dict(locale='th', fontName='Noto Serif Thai', fontSha256=hashlib.sha256(FONT.read_bytes()).hexdigest(),
            fontPixelSize=size, worldPerPixel=.004, shaper='HarfBuzz '+hb.version_string(), runs=runs)
(DEST / 'ThaiShaping.json').write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n')
# Include the actual font's notice and the standard OFL text already installed.
tt = TTFont(FONT)
copyright = next(n.toUnicode() for n in tt['name'].names if n.nameID == 0 and n.platformID == 3)
ofl = pathlib.Path('/usr/share/licenses/ttf-opensans/OFL.txt').read_text()
start = ofl.index('SIL OPEN FONT LICENSE')
(DEST / 'ThaiFontLicense.txt').write_text(copyright + '\n\nNoto Serif Thai\n\n' + ofl[start:])
report = dict(translatedEntries=len(entries), shapedRuns=len(runs), glyphs=len(bitmaps), atlas=[width, height],
              missingGlyphs=0, font=data['fontName'], fontSha256=data['fontSha256'], shaper=data['shaper'],
              sourceSha256=hashlib.sha256(source.encode()).hexdigest(),
              scope='Thai localization only; original Yarn commands and all source branch IDs remain unchanged.')
(WORK / 'research/intro-poem/thai-localization.json').write_text(json.dumps(report, indent=2) + '\n')
print(json.dumps(report))
