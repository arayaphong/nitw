#!/usr/bin/env python3
"""APK step 2: put the Thai IntroPoem into the Android (IL2CPP) data bundle.

No code hooks are possible on IL2CPP, so this edits assets only:
  * sharedassets81.assets / TextAsset IntroPoem.yarn: every `#line:<id>` line
    gets the Thai display string from ThaiMod/data/strings.tsv (pre-shaped
    clusters as Private Use Area characters, same table as the Linux mod).
  * sharedassets81.assets / Font goudy_bookletter_1911 (dynamic TTF used by the
    poem TextMesh): m_FontData is replaced by Noto Serif Thai plus one
    composite glyph per PUA cluster, with Goudy's line metrics kept.

Usage: apk_thai_poem.py <in data.unity3d> <out data.unity3d>
Run with .tools/unity-inspect/bin/python -I
"""
import os
import io, pathlib, re, sys
import UnityPy
import uharfbuzz as hb
from fontTools.ttLib import TTFont
from fontTools.ttLib.tables._g_l_y_f import Glyph, GlyphComponent

WORK = pathlib.Path(__file__).resolve().parent.parent
STRINGS = WORK / 'ThaiMod/data/strings.tsv'
POEM_GLYPHS = WORK / 'ThaiMod/data/poem_glyphs.tsv'
FETCHED = WORK / 'ThaiMod/fonts/NotoSerifThai.ttf'
POEM_FONT = pathlib.Path(os.environ.get("NITW_THAI_POEM_FONT", FETCHED))
ASSETS, YARN_ID, FONT_ID = 'sharedassets81.assets', 4, 5
SCALE = 64 / 58     # Linux mod drew Thai at 64 px where Goudy is 58 px


def thai_strings():
    out = {}
    for row in STRINGS.read_text(encoding='utf-8').split('\n'):
        cols = row.split('\t')
        if len(cols) >= 3 and cols[2] == 'poem':
            out[cols[0].split(':', 1)[1]] = cols[1]
    return out


def patch_yarn(text, thai):
    lines, done = [], 0
    for ln in text.split('\n'):
        m = re.match(r'^(\s*(?:-> )?)(.*?)(\s*#line:(\w+)\s*)$', ln)
        if m and m.group(4) in thai:
            ln = m.group(1) + thai[m.group(4)] + ' #line:' + m.group(4)
            done += 1
        lines.append(ln)
    return '\n'.join(lines), done


def build_font(goudy_bytes):
    goudy = TTFont(io.BytesIO(goudy_bytes))
    font = TTFont(str(POEM_FONT))
    if 'fvar' in font:                           # variable font: pin every axis to its default (Regular)
        from fontTools.varLib import instancer
        font = instancer.instantiateVariableFont(font, {a.axisTag: a.defaultValue for a in font['fvar'].axes})
    assert 'glyf' in font and 'fvar' not in font, 'need a TrueType-outline font'
    static = io.BytesIO(); font.save(static)
    raw = static.getvalue()
    font = TTFont(io.BytesIO(raw))
    upem = font['head'].unitsPerEm
    hbfont = hb.Font(hb.Face(raw)); hbfont.scale = (upem, upem)
    order = font.getGlyphOrder()
    glyf, hmtx = font['glyf'], font['hmtx']
    cmaps = [t for t in font['cmap'].tables if t.isUnicode()]
    added = 0
    for row in POEM_GLYPHS.read_text(encoding='utf-8').split('\n'):
        cols = row.split('\t')
        if len(cols) < 11:
            continue
        code, cluster = int(cols[0]), cols[10]
        if code < 0xE000:
            assert any(code in t.cmap for t in cmaps) or code == 32, f'font lacks U+{code:04X}'
            continue
        if code == 0xF8FF:                       # non-breaking Thai space
            for t in cmaps:
                t.cmap[code] = t.cmap[32]
            continue
        buf = hb.Buffer(); buf.add_str(cluster); buf.guess_segment_properties(); hb.shape(hbfont, buf)
        g = Glyph(); g.numberOfContours = -1; g.components = []
        pen = 0
        for info, pos in zip(buf.glyph_infos, buf.glyph_positions):
            assert info.codepoint != 0, f'font lacks {cluster!r}'
            c = GlyphComponent(); c.glyphName = order[info.codepoint]
            c.x, c.y, c.flags = pen + pos.x_offset, pos.y_offset, 0x0002 | 0x0004   # ARGS_ARE_XY, ROUND_XY
            g.components.append(c)
            pen += pos.x_advance
        name = 'thai%04X' % code
        glyf[name] = g
        g.recalcBounds(glyf)
        hmtx[name] = (pen, getattr(g, 'xMin', 0))
        for t in cmaps:
            t.cmap[code] = name
        added += 1
    font.setGlyphOrder(font.getGlyphOrder())
    font['maxp'].numGlyphs = len(font.getGlyphOrder())
    for tag in ('GSUB', 'GPOS', 'GDEF', 'DSIG'):
        if tag in font:
            del font[tag]
    # Bigger glyphs (SCALE) by shrinking the em, then Goudy's line metrics.
    new_upem = round(upem / SCALE)
    font['head'].unitsPerEm = new_upem
    k = new_upem / goudy['head'].unitsPerEm
    for tab, names in (('hhea', ('ascent', 'descent', 'lineGap')),
                       ('OS/2', ('sTypoAscender', 'sTypoDescender', 'sTypoLineGap', 'usWinAscent', 'usWinDescent'))):
        for n in names:
            setattr(font[tab], n, round(getattr(goudy[tab], n) * k))
    for rec in font['name'].names:               # keep the family name the asset expects
        if rec.nameID in (1, 4, 6, 16):
            src = goudy['name'].getName(rec.nameID, rec.platformID, rec.platEncID, rec.langID) or goudy['name'].getDebugName(rec.nameID)
            if src:
                rec.string = str(src)
    out = io.BytesIO(); font.save(out)
    return out.getvalue(), added


def main(src, dst):
    thai = thai_strings()
    env = UnityPy.load(src)
    seen = 0
    for o in env.objects:
        if o.assets_file.name != ASSETS or o.path_id not in (YARN_ID, FONT_ID):
            continue
        tt = o.read_typetree()
        if o.path_id == YARN_ID:
            assert tt['m_Name'] == 'IntroPoem.yarn', tt['m_Name']
            tt['m_Script'], n = patch_yarn(tt['m_Script'], thai)
            print(f'yarn: {n} of {len(thai)} poem lines replaced')
            assert n == len(thai)
        else:
            assert tt['m_Name'] == 'goudy_bookletter_1911', tt['m_Name']
            old = tt['m_FontData']
            data, added = build_font(bytes(old))
            tt['m_FontData'] = data if isinstance(old, (bytes, bytearray)) else list(data)
            print(f'font: {len(old)} -> {len(data)} bytes, {added} Thai cluster glyphs')
        o.save_typetree(tt); seen += 1
    assert seen == 2
    bundle = next(iter(env.files.values()))
    pathlib.Path(dst).parent.mkdir(parents=True, exist_ok=True)
    with open(dst, 'wb') as f:
        f.write(bundle.save(packer='lz4'))
    print('wrote', dst)


if __name__ == '__main__':
    main(sys.argv[1], sys.argv[2])
