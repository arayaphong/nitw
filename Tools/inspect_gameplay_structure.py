"""Read shipped Unity/CLI data without loading or executing managed assemblies.

Run with .tools/unity-inspect/bin/python. Outputs are analysis only; game and
reconstruction files are never modified.
"""
import os
import collections
import hashlib
import json
import pathlib
import re

import dnfile
import UnityPy
from dncil.cil.body import CilMethodBody
from dncil.cil.body.reader import CilMethodBodyReaderBytes

ROOT = pathlib.Path(os.environ.get("NITW_GAME_DATA", str(pathlib.Path.home() / ".local/share/Steam/steamapps/common/Night in the Woods/Night in the Woods_Data")))
OUT = (pathlib.Path(os.environ.get("NITW_WORKSPACE", str(pathlib.Path(__file__).resolve().parents[1]))) / 'research/gameplay')
OUT.mkdir(exist_ok=True)

def write(name, value):
    (OUT / name).write_text(json.dumps(value, indent=2, ensure_ascii=False) + '\n')

path = ROOT / 'Managed/Assembly-CSharp.dll'
pe = dnfile.dnPE(str(path))
tables = pe.net.mdtables
parents = {n.NestedClass.row_index: n.EnclosingClass.row_index for n in tables.NestedClass.rows}

def type_name(i):
    t = tables.TypeDef.rows[i - 1]
    if i in parents:
        return type_name(parents[i]) + '+' + str(t.TypeName)
    return (str(t.TypeNamespace) + '.' if str(t.TypeNamespace) else '') + str(t.TypeName)

tokens = {}
for i, t in enumerate(tables.TypeRef.rows, 1):
    tokens[0x01000000 | i] = (str(t.TypeNamespace) + '.' if str(t.TypeNamespace) else '') + str(t.TypeName)
types = []
methods = []
for i, t in enumerate(tables.TypeDef.rows, 1):
    name = type_name(i)
    tokens[0x02000000 | i] = name
    ms = []
    for m in t.MethodList:
        token = 0x06000000 | m.row_index
        tokens[token] = name + '.' + str(m.row.Name)
        entry = dict(token=f'0x{token:08x}', name=str(m.row.Name), owner=name, rva=m.row.Rva)
        methods.append((m.row, entry))
        ms.append(entry)
    for f in t.FieldList:
        tokens[0x04000000 | f.row_index] = name + '.' + str(f.row.Name)
    types.append(dict(token=f'0x{0x02000000 | i:08x}', name=name,
                      fields=[str(f.row.Name) for f in t.FieldList], methods=ms))
for i, m in enumerate(tables.MemberRef.rows, 1):
    owner = m.Class
    base = tokens.get((owner.table.number << 24) | owner.row_index, f'{owner.table.name}[{owner.row_index}]')
    tokens[0x0a000000 | i] = base + '.' + str(m.Name)
for i, m in enumerate(tables.MethodSpec.rows, 1):
    tokens[0x2b000000 | i] = tokens.get((m.Method.table.number << 24) | m.Method.row_index, '?')
sha = hashlib.sha256(path.read_bytes()).hexdigest()
write('managed-index.json', dict(source=str(path), sha256=sha, typeCount=len(types), types=types))

selected = {'Player', 'Entity', 'Interaction', 'SceneRoot', 'SceneManifest', 'SceneLink', 'SceneTransition', 'Global',
            'Continuity', 'Dialogue', 'DialogueImplementation', 'DialogueVariableStorage',
            'StateMonoBehaviour', 'State', 'StateMethod', 'Localization', 'Yarn.Dialogue',
            'Yarn.Loader', 'Yarn.VirtualMachine', 'Yarn.Unity.DialogueRunner', 'TitleBrain', 'IntroPoemBrain',
            'Yarn.Unity.Example.DialogueUI', 'InputButtonSprite', 'AmbientSFX', 'Util', 'Yarn.Compiler',
            'CameraData', 'CameraControl', 'ScreenTransitioner', 'DynamicEye', 'SFX', 'Constants',
            'Character', 'CharacterHead', 'CharacterRig', 'Head', 'ExistsVariable'}
decoded = []
errors = []
for row, entry in methods:
    if entry['owner'].split('+')[0] not in selected or not row.Rva:
        continue
    try:
        body = CilMethodBody(CilMethodBodyReaderBytes(pe.get_data(row.Rva)))
        instructions, calls, strings = [], [], []
        for ins in body.instructions:
            value = ins.operand
            token = getattr(value, 'value', None)
            resolved = None
            if token is not None:
                if token >> 24 == 0x70:
                    us = pe.net.user_strings.get(token & 0xffffff)
                    resolved = us.value if us else None
                    strings.append(resolved)
                else:
                    resolved = tokens.get(token, f'0x{token:08x}')
            instructions.append(dict(offset=ins.offset - body.header_size, opcode=ins.opcode.name,
                                     operand=str(value) if value is not None else None, resolved=resolved))
            if ins.opcode.name in ('call', 'callvirt', 'newobj', 'ldftn', 'ldvirtftn'):
                calls.append(resolved)
        decoded.append(dict(**entry, instructionCount=len(instructions), calls=calls,
                            strings=strings, instructions=instructions))
    except Exception as error:
        errors.append(dict(**entry, error=str(error)))
write('core-cil.json', dict(source=str(path), sha256=sha, decoder='dnfile + dncil; static only',
                           selectedTypes=sorted(selected), methods=decoded, errors=errors))
print('CLI:', len(types), 'types;', len(decoded), 'selected methods;', len(errors), 'errors', flush=True)

settings_env = UnityPy.load(str(ROOT / 'globalgamemanagers'))
settings = next(o.read_typetree() for o in settings_env.objects if o.type.name == 'BuildSettings')
scene_paths = settings['scenes']
scenes = [ROOT / f'level{i}' for i in range(len(scene_paths))]
asset_paths = sorted(ROOT.glob('sharedassets*.assets')) + [ROOT / 'resources.assets']
env = UnityPy.load(*map(str, asset_paths), *map(str, scenes))
script_cache = {}
inventory = []
dialogue_assets = []
scene_objects = {}
for obj in env.objects:
    file_name = pathlib.Path(obj.assets_file.name).name
    if re.fullmatch(r'level\d+', file_name):
        scene_objects.setdefault(file_name, []).append(obj)
    elif obj.type.name == 'TextAsset':
        asset = obj.read()
        text = asset.m_Script
        if isinstance(text, bytes):
            text = text.decode('utf-8', errors='replace')
        titles = re.findall(r'^title:\s*(.+)$', text, re.M)
        commands = re.findall(r'<<\s*(.*?)\s*>>', text, re.S)
        command_names = collections.Counter(c.split()[0] for c in commands if c.split())
        dialogue_assets.append(dict(file=file_name, id=obj.path_id, name=asset.m_Name,
                                   bytes=len(text.encode('utf-8', errors='replace')),
                                   sha256=hashlib.sha256(text.encode('utf-8', errors='replace')).hexdigest(),
                                   nodeCount=len(titles), nodes=titles,
                                   commandCounts=dict(command_names),
                                   conditionalLines=len(re.findall(r'<<\s*(?:if|elseif)\b', text)),
                                   optionLines=len(re.findall(r'^\s*(?:->|\[\[)', text, re.M))))
for i, scene_path in enumerate(scene_paths):
    filename = f'level{i}'
    objs = scene_objects.get(filename, [])
    classes = collections.Counter()
    examples = {}
    scene_errors = []
    for o in objs:
        if o.type.name != 'MonoBehaviour':
            continue
        try:
            h = o.parse_monobehaviour_head()
            key = (file_name, h.m_Script.m_FileID, h.m_Script.m_PathID)
            if key not in script_cache:
                s = h.m_Script.read()
                script_cache[key] = (s.m_Namespace + '.' if s.m_Namespace else '') + s.m_ClassName
            name = script_cache[key]
            classes[name] += 1
            if name in selected or name.endswith('Brain'):
                examples.setdefault(name, []).append(dict(id=o.path_id, gameObject=h.m_GameObject.read().m_Name))
        except Exception as error:
            scene_errors.append(dict(id=o.path_id, error=str(error)))
    inventory.append(dict(index=i, file=filename, scene=scene_path,
                          sha256=hashlib.sha256((ROOT / filename).read_bytes()).hexdigest(),
                          objectCount=len(objs), components=dict(classes), examples=examples, errors=scene_errors))
write('scene-inventory.json', dict(source=str(ROOT), sceneCount=len(inventory), scenes=inventory))
write('text-asset-inventory.json', dict(scope=[p.name for p in asset_paths], assets=dialogue_assets))
print('Unity:', len(inventory), 'scenes;', sum(len(s['errors']) for s in inventory), 'scene component errors;',
      len(dialogue_assets), 'text assets;', sum(a['nodeCount'] for a in dialogue_assets), 'dialogue nodes', flush=True)

# Catalogue lexical dispatch tests; aliases and duplicate tests are preserved.
command_method = next(m for m in decoded if m['owner'] == 'DialogueImplementation+<RunCommand>c__Iterator6'
                      and m['name'] == 'MoveNext')
instructions = command_method['instructions']
sites = []
for i, ins in enumerate(instructions):
    if (ins['resolved'] == 'DialogueImplementation.IsString' and i >= 4
            and instructions[i - 1]['opcode'] == 'ldstr'
            and instructions[i - 2]['opcode'] == 'ldelem.ref'
            and instructions[i - 3]['opcode'] == 'ldc.i4.0'):
        sites.append(dict(name=instructions[i - 1]['resolved'], ilOffset=ins['offset']))
names = {s['name'] for s in sites}
counts = collections.Counter()
for a in dialogue_assets:
    counts.update(a['commandCounts'])
write('command-catalog.json', dict(
    sourceMethod=command_method['owner'] + '.' + command_method['name'], sourceToken=command_method['token'],
    sha256=sha, rule='Literal passed to IsString immediately after tokens[0]; static dispatch candidates including aliases',
    dispatchSites=sites, uniqueDispatchNames=sorted(names), observedFirstTokens=dict(counts),
    observedOutsideDispatch=sorted(set(counts) - names),
    scope='Loose sharedassets*.assets and resources.assets TextAssets; AssetBundles/dynamic commands excluded. '
          'First tokens include Yarn syntax; outside-dispatch entries are not proof of unsupported commands.'))
write('summary.json', dict(
    sceneCount=len(inventory), sourceSceneComponentErrors=sum(len(s['errors']) for s in inventory),
    componentErrors=dict(collections.Counter(e['error'] for s in inventory for e in s['errors'])),
    yarnTextAssets=sum(bool(a['nodeCount']) for a in dialogue_assets),
    yarnNodeHeaders=sum(a['nodeCount'] for a in dialogue_assets),
    conditionalDirectives=sum(a['conditionalLines'] for a in dialogue_assets),
    optionLineCandidates=sum(a['optionLines'] for a in dialogue_assets),
    dispatchLiteralNames=len(names), dispatchLiteralSites=len(sites), selectedDecodedMethods=len(decoded),
    sceneBrainTypes=sorted({k for s in inventory for k in s['components'] if k.endswith('Brain')})))

chosen = ['Player+<Normal>c__Iterator1.MoveNext', 'SceneRoot+<RunInitLevel>c__Iterator1.MoveNext',
          'Dialogue.Parse', 'DialogueVariableStorage.SetNumber', 'DialogueVariableStorage.GetNumber',
          'Continuity.CollectData', 'Global+<DoLastPartOfLoad>c__Iterator1.MoveNext']
lines = ['Static CIL from original Assembly-CSharp.dll; not original C# source.', 'SHA256 ' + sha,
         'Branch operands retain dncil method-body offsets; printed IL labels exclude header.', '']
for name in chosen:
    method = next(m for m in decoded if m['owner'] + '.' + m['name'] == name)
    lines.append(name + ' ' + method['token'])
    for ins in method['instructions']:
        operand = repr(ins['resolved']) if ins['opcode'] == 'ldstr' else (ins['resolved'] or ins['operand'] or '')
        lines.append(f" IL_{ins['offset']:04x}: {ins['opcode']:<14} {operand}")
    lines.append('')
(OUT / 'selected-gameplay.il.txt').write_text('\n'.join(lines) + '\n')

# Cross-check independent decoder coordinates against retained REA anchors.
anchor_file = OUT / 'rea-call-anchors.json'
if anchor_file.exists():
    lookup = {m['token']: {i['offset']: i for i in m['instructions']} for m in decoded}
    checked, uncovered, mismatches = 0, [], []
    for group, rows in json.loads(anchor_file.read_text()).items():
        for row in rows:
            if row['caller_token'] not in lookup:
                uncovered.append(row)
                continue
            ins = lookup[row['caller_token']].get(row['il_offset'])
            checked += 1
            expected = f"token({row['target_token']})"
            if ins is None or ins['opcode'] != row['opcode'] or (ins['operand'] or '').lower() != expected.lower():
                mismatches.append(dict(group=group, rea=row, local=ins))
    write('static-validation.json', dict(checkedCallAnchors=checked, uncoveredCallAnchors=len(uncovered),
          mismatches=mismatches, method='Compare caller token, IL offset, opcode and raw target token against '
          'three REA inventories; verifies static coordinates, not runtime equivalence'))
    print('REA anchor check:', checked, 'checked;', len(uncovered), 'uncovered;', len(mismatches), 'mismatches')
