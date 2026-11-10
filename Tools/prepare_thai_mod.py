"""Build Thai data for the NITW Thai mod (original game, SpriteText speech).

Each Thai grapheme cluster is shaped with HarfBuzz (Kanit Medium), rendered
to its own sprite and mapped to a Private Use Area character. Translated
lines are rewritten to those characters, so the game's own SpriteText lays
out, types and animates Thai exactly like its Latin sprite font. Because
SpriteText only wraps at spaces, lines are broken offline (ICU line-break
opportunities + a keep-together list, game width metrics) with '|' and
spaces inside Thai become a non-breaking PUA space.

Inputs: every translation work file ThaiMod/translations/<yarn>.json (see
Tools/extract_thai_worklist.py; lines with a non-empty "th" are built) and
the original Yarn in ThaiMod/source/yarn/ for speaker prefixes.

Outputs (local only; they contain translated narrative text):
  ThaiMod/data/strings.tsv   line:<id> \t display \t bubble|poem \t original
                             (original English only for IDs that two Yarn
                             files share; the runtime picks by original text)
  ThaiMod/reports/thai_build.json  errors (skipped lines) and overflows
  ThaiMod/data/glyphs.png    cluster atlas (white + alpha)
  ThaiMod/data/glyphs.tsv    codepoint, rect, pivot, CharData width, text
  ThaiMod/data/meta.tsv      key \t value (pixelsPerUnit, lineHeight, ...)
"""
import os
import ctypes
import hashlib
import json
import os
import pathlib
import re

import freetype
import regex
import uharfbuzz as hb
from PIL import Image

WORK = pathlib.Path(__file__).resolve().parents[1]
# Game SpriteText/SpriteFont metrics and emoticon table, produced from the
# installed game by Tools/extract_station_dialogue.py.
PRESENTATION = pathlib.Path(os.environ.get('NITW_PRESENTATION', str(
    WORK / 'NITW-Unity-Reconstruction/Assets/Resources/RecoveredStationDialogue/Presentation.json')))
TRANS = WORK / 'ThaiMod/translations'   # work files (one per Yarn file)
YARN = WORK / 'ThaiMod/source/yarn'      # original Yarn: speaker prefix and original strings
REPORTS = WORK / 'ThaiMod/reports'
# Fonts (SIL OFL). Order: $NITW_THAI_FONT / $NITW_THAI_POEM_FONT, then the
# copies ThaiMod/fetch_deps.sh downloads from google/fonts, then the
# development machine's installed copies.
def font_path(env, fetched, installed):
    if os.environ.get(env):
        return pathlib.Path(os.environ[env])
    return fetched if fetched.exists() else installed
POEM_FONT = font_path('NITW_THAI_POEM_FONT', WORK / 'ThaiMod/fonts/NotoSerifThai.ttf',
    pathlib.Path.home() / '.local/share/fonts/TH_FONTs/Google/TrueType/Noto Serif Thai/Noto_Serif_Thai_Regular.ttf')
POEM_PIXELS = 64        # TextMesh custom font: world = px * characterSize(.04) * .1
OUT = WORK / 'ThaiMod/data'
FONT = font_path('NITW_THAI_FONT', WORK / 'ThaiMod/fonts/Kanit-Medium.ttf', pathlib.Path.home() / '.local/share/fonts/TH_FONTs/Kanit-Medium.ttf')
BODY_PIXELS = 32        # rendered consonant body height (ก)
BODY = .22              # bubble-local consonant body height
LINE_PITCH = .5         # bubble-local row pitch for Thai (Latin: lineHeight 1 x scale .35)
BASELINE = -.13         # baseline relative to the row centre, bubble-local
PAD = 4
PUA = 0xE000
THAI_SPACE = chr(0xF8FF)  # non-breaking space glyph used inside Thai text

for need, hint in ((PRESENTATION, 'run Tools/extract_station_dialogue.py or set NITW_PRESENTATION'),
                   (FONT, 'set NITW_THAI_FONT to Kanit-Medium.ttf'), (POEM_FONT, 'set NITW_THAI_POEM_FONT to NotoSerifThai-Regular.ttf'),
                   (TRANS / 'keep_together.json', 'run Tools/extract_thai_worklist.py')):
    if not need.exists():
        raise SystemExit(f'missing {need}: {hint}')
pres = json.loads(PRESENTATION.read_text())
SCALE = pres['textScale']            # SpriteText.scale
SPACING = pres['spacingMultiplier']
XPAD = pres['xPadding']
latin = {}
for c in pres['characters']:
    w = (c['width'] if c['frames'] else pres['spaceWidth']) * SPACING * SCALE + XPAD
    for ch in (c['character'] or '') + (c.get('additionalCharacters') or ''):
        latin[ch] = w
EMOTICONS = [e['emoticon'] for e in pres['emoticons']]

# --- ICU line breaks -------------------------------------------------------
icu = ctypes.CDLL('libicuuc.so')
suffix = next(s for s in [''] + [f'_{v}' for v in range(99, 59, -1)] if hasattr(icu, 'ubrk_open' + s))
def fn(name, res, args):
    f = getattr(icu, name + suffix); f.restype = res; f.argtypes = args; return f
ubrk_open = fn('ubrk_open', ctypes.c_void_p, [ctypes.c_int, ctypes.c_char_p, ctypes.POINTER(ctypes.c_uint16), ctypes.c_int32, ctypes.POINTER(ctypes.c_int)])
ubrk_first = fn('ubrk_first', ctypes.c_int32, [ctypes.c_void_p])
ubrk_next = fn('ubrk_next', ctypes.c_int32, [ctypes.c_void_p])
ubrk_close = fn('ubrk_close', None, [ctypes.c_void_p])
def icu_breaks(text):
    units = text.encode('utf-16-le'); n = len(units) // 2
    buf = (ctypes.c_uint16 * n).from_buffer_copy(units); st = ctypes.c_int(0)
    it = ubrk_open(2, b'th', buf, n, ctypes.byref(st)); assert st.value <= 0
    out, p = set(), ubrk_first(it)
    while p != -1:
        out.add(p); p = ubrk_next(it)
    ubrk_close(it); return out

# --- shaping ------------------------------------------------------------------
face = freetype.Face(str(FONT)); face.set_pixel_sizes(0, 100); face.load_char('ก', freetype.FT_LOAD_RENDER)
PIXEL_SIZE = round(100 * BODY_PIXELS / face.glyph.bitmap.rows)
face.set_pixel_sizes(0, PIXEL_SIZE)
font = hb.Font(hb.Face(FONT.read_bytes())); font.scale = (PIXEL_SIZE * 64, PIXEL_SIZE * 64); hb.ot_font_set_funcs(font)
face.load_char('ก', freetype.FT_LOAD_RENDER); body_px = face.glyph.bitmap.rows
LPP = BODY / body_px                       # bubble-local units per pixel
PPU = SCALE / LPP                          # sprite pixels per unit (letters are scaled by SpriteText.scale)

def is_thai(ch):
    return '฀' <= ch <= '๿'

def clusters_of(text):
    """Grapheme clusters; Thai preposed vowels join the following cluster."""
    g = [m.group() for m in regex.finditer(r'\X', text)]
    out, i = [], 0
    while i < len(g):
        c = g[i]
        if len(c) == 1 and 'เ' <= c <= 'ไ' and i + 1 < len(g):
            c += g[i + 1]; i += 1
        out.append(c); i += 1
    return out

cluster_art = {}
def render(cluster):
    if cluster in cluster_art:
        return cluster_art[cluster]
    buf = hb.Buffer(); buf.add_str(cluster); buf.guess_segment_properties(); hb.shape(font, buf)
    pen, ink = 0.0, []
    for info, pos in zip(buf.glyph_infos, buf.glyph_positions):
        assert info.codepoint != 0, f'missing glyph {cluster!r}'
        face.load_glyph(info.codepoint, freetype.FT_LOAD_RENDER | freetype.FT_LOAD_TARGET_NORMAL)
        b = face.glyph.bitmap
        if b.width and b.rows:
            im = Image.frombytes('L', (b.pitch, b.rows), bytes(b.buffer)).crop((0, 0, b.width, b.rows))
            left = pen + pos.x_offset / 64 + face.glyph.bitmap_left
            top = pos.y_offset / 64 + face.glyph.bitmap_top
            ink.append((im, left, top))
        pen += pos.x_advance / 64
    x0 = min(l for _, l, _ in ink); x1 = max(l + im.width for im, l, _ in ink)
    y1 = max(t for _, _, t in ink); y0 = min(t - im.height for im, _, t in ink)   # y up from baseline
    w, h = int(round(x1 - x0)) + 2, int(round(y1 - y0)) + 2
    canvas = Image.new('L', (w, h), 0)
    for im, l, t in ink:
        canvas.paste(Image.composite(im, canvas.crop((int(round(l - x0)) + 1, int(round(y1 - t)) + 1, int(round(l - x0)) + 1 + im.width, int(round(y1 - t)) + 1 + im.height)), im),
                     (int(round(l - x0)) + 1, int(round(y1 - t)) + 1))
    centre_x = pen / 2 - x0 + 1                       # advance centre, canvas px from left
    row_centre = -BASELINE / LPP                       # px above baseline
    pivot = (centre_x / w, (row_centre - y0 + 1) / h)  # from bottom-left
    art = dict(image=canvas, pivot=pivot, advance=pen * LPP)
    cluster_art[cluster] = art
    return art

space_px = font.get_glyph_h_advance(font.get_nominal_glyph(ord(' '))) / 64
THAI_SPACE_ADV = space_px * LPP

# --- per-line conversion and layout ------------------------------------------
codes = {}
def code_for(cluster):
    if cluster not in codes:
        codes[cluster] = chr(PUA + len(codes))
    return codes[cluster]

def protected_spans(text):
    """Markup {..}, [tag]/[/tag] and emoticon tokens stay verbatim (the game parses them)."""
    spans = [m.span() for m in re.finditer(r'\{[^}]*\}', text)]
    spans += [m.span() for m in re.finditer(r'(?<!\[)\[/?[A-Za-z]+(?:=[^\[\]]*)?\](?!\])', text)]
    for e in EMOTICONS:
        i = text.find(e)
        if i >= 0:
            spans.append((i, i + len(e)))
    return sorted(spans)

def convert(text, keep, width_limit, no_break_after=()):
    # 1. tokens: ('raw', s) protected, or ('t', cluster/char, advance, break_before, kind)
    breaks = icu_breaks(text)
    for word in keep:
        for m in re.finditer(re.escape(word), text):
            breaks -= set(range(m.start() + 1, m.end())); breaks |= {m.start(), m.end()}
    # Words that bind to what follows (negation, classifiers): no break after
    # them unless a space follows.
    for word in no_break_after:
        for m in re.finditer(re.escape(word), text):
            if m.end() < len(text) and text[m.end()] != ' ':
                breaks.discard(m.end())
    prot = protected_spans(text)
    tokens, i = [], 0
    in_prot = lambda k: next((s for s in prot if s[0] <= k < s[1]), None)
    has_thai = any(is_thai(c) for c in text)
    while i < len(text):
        p = in_prot(i)
        if p:
            tokens.append(('raw', text[p[0]:p[1]])); i = p[1]; continue
        j = i
        while j < len(text) and not in_prot(j):
            j += 1
        seg = text[i:j]; k = i
        for cl in clusters_of(seg):
            bb = k in breaks and k > 0
            if cl == ' ':
                tokens.append(('t', ' ', THAI_SPACE_ADV if has_thai else latin.get(' ', 0), bb, 'space'))
            elif is_thai(cl[0]):
                tokens.append(('t', code_for(cl), render(cl)['advance'], bb, 'thai'))
            else:
                ch = cl.upper() if pres['upperCaseOnly'] else cl
                if ch not in latin:
                    raise ValueError(f'no game glyph for {cl!r}')
                tokens.append(('t', cl, latin[ch], bb, 'latin'))
            k += len(cl)
        i = j
    # 2. greedy wrap on visible tokens, preferring phrase spaces filling >= 60%
    vis = [t for t in tokens if t[0] == 't']
    def width(a, b):
        while b > a and vis[b - 1][4] == 'space': b -= 1
        while a < b and vis[a][4] == 'space': a += 1
        return sum(t[2] for t in vis[a:b])
    def wrap(limit):
        rows, start = [], 0
        while start < len(vis):
            end, last_break, last_space = len(vis), -1, -1
            for n in range(start, len(vis)):
                if n > start and vis[n][3]:
                    last_break = n
                    if vis[n - 1][4] == 'space' and width(start, n) >= .6 * limit: last_space = n
                if n > start and width(start, n + 1) > limit:
                    end = last_space if last_space > start else last_break if last_break > start else n
                    break
            rows.append(end); start = end
        return rows
    rows = wrap(width_limit)
    if len(rows) > 3:
        rows = wrap(4.5)
    if len(rows) > 3:
        overflow.append(len(rows))
    cut = set(rows[:-1])                     # break before these visible indices
    # 3. emit: '|' at breaks (dropping spaces there), Thai spaces -> THAI_SPACE
    out, v = [], 0
    for ti, t in enumerate(tokens):
        if t[0] == 'raw':
            out.append(t[1]); continue
        next_raw = ti + 1 < len(tokens) and tokens[ti + 1][0] == 'raw'
        if v in cut:
            while out and out[-1] in (' ', THAI_SPACE): out.pop()
            out.append('|')
        if t[4] == 'space':
            prev = out[-1] if out else ''
            # spaces at row starts vanish; spaces next to protected tokens stay plain
            if prev in ('', '|'):
                pass
            elif has_thai and not prev.startswith('{') and not next_raw:
                out.append(THAI_SPACE)
            else:
                out.append(' ')
        else:
            out.append(t[1])
        v += 1
    return ''.join(out).replace('|' + THAI_SPACE, '|'), len(rows)

def speaker_prefixes(yarn_path):
    """line id -> text before the spoken words ("Mae: ", "Janitor:"), as authored."""
    out = {}
    for line in yarn_path.read_text(encoding='utf-8').splitlines():
        m = re.search(r'#line:([0-9a-f]+)', line)
        if not m:
            continue
        body = line.strip()
        if body.startswith('->') or body.startswith('[['):
            out[m.group(1)] = ''
            continue
        p = re.match(r'([A-Za-z][A-Za-z0-9_ ]*:\s*)', body)
        out[m.group(1)] = p.group(1) if p else ''
    return out

def original_strings(yarn_path):
    """line id -> the string the game's Program holds for it (before #line)."""
    out = {}
    for line in yarn_path.read_text(encoding='utf-8').splitlines():
        m = re.search(r'#line:([0-9a-f]+)', line)
        if not m:
            continue
        text = line[:m.start()].strip()
        if text.startswith('[['):
            o = re.match(r'\[\[(.*?)\|', text); text = o.group(1) if o else text
        elif text.startswith('->'):
            text = re.sub(r'(\s*<<[^>]*>>)+\s*$', '', text[2:])
        out[m.group(1)] = text.strip()
    return out

def tokens_of(text):
    """Markup and emoticons the game parses; a translation must keep them."""
    return sorted(text[a:b] for a, b in protected_spans(text))

keep = json.loads((TRANS / 'keep_together.json').read_text(encoding='utf-8'))
nba_path = TRANS / 'no_break_after.json'
no_break_after = json.loads(nba_path.read_text(encoding='utf-8')) if nba_path.exists() else []
strings, poem_texts, errors, overflows = [], [], [], []
overflow = []
for wf in sorted(TRANS.glob('*.json')):
    if wf.name.startswith('_') or wf.name in ('keep_together.json', 'no_break_after.json'):
        continue
    data = json.loads(wf.read_text(encoding='utf-8'))
    kind, yarn = data['kind'], YARN / data['file']
    prefixes, originals = speaker_prefixes(yarn), original_strings(yarn)
    for e in data['lines']:
        th = (e.get('th') or '').strip()
        if not th:
            continue
        where = f"{data['file']}#{e['id']}"
        if tokens_of(th) != tokens_of(e['en']):
            errors.append(dict(line=where, error=f"markup/emoticons differ: en {tokens_of(e['en'])} th {tokens_of(th)}")); continue
        try:
            if kind == 'bubble':
                m = re.search(r'\{width=([0-9.]+)\}', th)
                limit = float(m.group(1)) if m else pres['width']
                overflow.clear()
                display, nrows = convert(th, keep, limit, no_break_after)
                if overflow:
                    overflow.clear()
                    try:
                        en_rows = convert(e['en'], [], limit)[1]   # same metrics, original text
                    except ValueError:
                        en_rows = None
                    overflow.clear()
                    overflows.append(dict(line=where, rows=nrows, english_rows=en_rows))
            else:
                # Poem TextMesh: no wrapping; '|' keeps the poem's own extra lines.
                display = ''.join(code_for(cl) if is_thai(cl[0]) else cl for cl in clusters_of(th))
                poem_texts.append(th)
        except (ValueError, AssertionError) as x:
            errors.append(dict(line=where, error=str(x))); continue
        original = originals[e['id']] if e.get('shared_id') else ''
        strings.append(('line:' + e['id'], prefixes[e['id']] + display, kind, original))

# --- poem font (TextMesh custom font, Noto Serif Thai) -------------------------
pface = freetype.Face(str(POEM_FONT)); pface.set_pixel_sizes(0, POEM_PIXELS)
pfont = hb.Font(hb.Face(POEM_FONT.read_bytes())); pfont.scale = (POEM_PIXELS * 64, POEM_PIXELS * 64); hb.ot_font_set_funcs(pfont)
def render_poem(text):
    """Shape one cluster/char; returns (L image or None, minX, maxY, advance) in px, y up."""
    buf = hb.Buffer(); buf.add_str(text); buf.guess_segment_properties(); hb.shape(pfont, buf)
    pen, ink = 0.0, []
    for info, pos in zip(buf.glyph_infos, buf.glyph_positions):
        assert info.codepoint != 0, f'poem font lacks {text!r}'
        pface.load_glyph(info.codepoint, freetype.FT_LOAD_RENDER | freetype.FT_LOAD_TARGET_NORMAL)
        b = pface.glyph.bitmap
        if b.width and b.rows:
            im = Image.frombytes('L', (b.pitch, b.rows), bytes(b.buffer)).crop((0, 0, b.width, b.rows))
            ink.append((im, pen + pos.x_offset / 64 + pface.glyph.bitmap_left, pos.y_offset / 64 + pface.glyph.bitmap_top))
        pen += pos.x_advance / 64
    if not ink:
        return None, 0, 0, pen
    x0 = min(l for _, l, _ in ink); x1 = max(l + im.width for im, l, _ in ink)
    y1 = max(t for _, _, t in ink); y0 = min(t - im.height for im, _, t in ink)
    w, h = int(round(x1 - x0)), int(round(y1 - y0))
    canvas = Image.new('L', (w, h), 0)
    for im, l, t in ink:
        ox, oy = int(round(l - x0)), int(round(y1 - t))
        canvas.paste(Image.composite(im, canvas.crop((ox, oy, ox + im.width, oy + im.height)), im), (ox, oy))
    return canvas, x0, y1, pen

poem_chars = {}
for text in poem_texts:
    for cl in clusters_of(text):
        if cl == '|':
            continue
        key = code_for(cl) if is_thai(cl[0]) else cl
        assert len(key) == 1, f'multi-char non-Thai cluster {cl!r}'
        poem_chars.setdefault(key, cl)

def pack(images, width=512):
    x, y, row, place = PAD, PAD, 0, {}
    for k, im in images:
        if im is None:
            continue
        if x + im.width + PAD > width:
            x = PAD; y += row + PAD; row = 0
        place[k] = (x, y); x += im.width + PAD; row = max(row, im.height)
    h = 1
    while h < y + row + PAD:
        h *= 2
    return place, h

prows, pimgs = [], []
for key, cl in sorted(poem_chars.items()):
    im, minx, maxy, adv = render_poem(cl)
    pimgs.append((key, im)); prows.append((key, im, minx, maxy, adv, cl))
pplace, PH = pack(pimgs)
patlas = Image.new('RGBA', (512, PH), (255, 255, 255, 0))
poem_out = []
for key, im, minx, maxy, adv, cl in prows:
    if im is None:
        poem_out.append((ord(key), 0, 0, 0, 0, 0, 0, 0, 0, adv, cl)); continue
    px, py = pplace[key]
    tile = Image.new('RGBA', im.size, 'white'); tile.putalpha(im); patlas.paste(tile, (px, py))
    # uv rect bottom-left origin; vertex box minX,maxX,minY,maxY (px, y up from baseline)
    poem_out.append((ord(key), px, PH - py - im.height, im.width, im.height,
                     minx, minx + im.width, maxy - im.height, maxy, adv, cl))
OUT.mkdir(parents=True, exist_ok=True)
patlas.save(OUT / 'poem_glyphs.png')
(OUT / 'poem_glyphs.tsv').write_text(''.join('%d\t%d\t%d\t%d\t%d\t%.3f\t%.3f\t%.3f\t%.3f\t%.3f\t%s\n' % r for r in poem_out), encoding='utf-8')

# --- bubble atlas ----------------------------------------------------------------
items = sorted(codes.items(), key=lambda kv: kv[1])
for cl, _ in items:
    render(cl)          # poem-only clusters also get bubble art (one shared code table)
for W in (512, 1024, 2048, 4096):        # widen until the atlas is at most 4096 tall
    x, y, row, place = PAD, PAD, 0, {}
    for cl, _ in items:
        im = cluster_art[cl]['image']
        if x + im.width + PAD > W:
            x = PAD; y += row + PAD; row = 0
        place[cl] = (x, y); x += im.width + PAD; row = max(row, im.height)
    if y + row + 2 * PAD + 2 <= 4096:
        break
space_slot = (x if x + 2 + PAD <= W else PAD, y if x + 2 + PAD <= W else y + row + PAD)
H = 1
while H < max(y + row, space_slot[1] + 2) + PAD:
    H *= 2
atlas = Image.new('RGBA', (W, H), (255, 255, 255, 0))
OUT.mkdir(parents=True, exist_ok=True)
rows_out = []
for cl, cp in items:
    art = cluster_art[cl]; im = art['image']; px, py = place[cl]
    tile = Image.new('RGBA', im.size, 'white'); tile.putalpha(im); atlas.paste(tile, (px, py))
    # Unity rects start bottom-left
    rows_out.append((ord(cp), px, H - py - im.height, im.width, im.height, art['pivot'][0], art['pivot'][1], art['advance'] / (SPACING * SCALE), cl))
sx, sy = space_slot
rows_out.append((ord(THAI_SPACE), sx, H - sy - 2, 2, 2, .5, .5, THAI_SPACE_ADV / (SPACING * SCALE), ' '))
atlas.save(OUT / 'glyphs.png')
(OUT / 'glyphs.tsv').write_text(''.join('%d\t%d\t%d\t%d\t%d\t%.6f\t%.6f\t%.6f\t%s\n' % r for r in rows_out), encoding='utf-8')
(OUT / 'strings.tsv').write_text(''.join(f'{k}\t{v}\t{kind}\t{o}\n' for k, v, kind, o in strings), encoding='utf-8')
REPORTS.mkdir(parents=True, exist_ok=True)
(REPORTS / 'thai_build.json').write_text(json.dumps(dict(strings=len(strings), errors=errors, overflows=overflows),
                                                     ensure_ascii=False, indent=1) + '\n', encoding='utf-8')
meta = dict(pixelsPerUnit=PPU, lineHeight=LINE_PITCH / SCALE, font='Kanit Medium',
            fontSha256=hashlib.sha256(FONT.read_bytes()).hexdigest(), shaper='HarfBuzz ' + hb.version_string(),
            clusters=len(codes), strings=len(strings), atlas=f'{W}x{H}', poemGlyphs=len(poem_out), poemAtlas=f'512x{PH}')
(OUT / 'meta.tsv').write_text(''.join(f'{k}\t{v}\n' for k, v in meta.items()), encoding='utf-8')
print(json.dumps(meta))
print(f'errors (skipped): {len(errors)}  overflows (>3 rows): {len(overflows)}  report: {REPORTS / "thai_build.json"}')
