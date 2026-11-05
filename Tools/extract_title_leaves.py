"""Read the installed title assets into a bounded, native-coordinate manifest."""
import os
import hashlib, json, pathlib
import UnityPy
from UnityPy.helpers.MeshHelper import MeshHandler

ROOT = pathlib.Path(os.environ.get("NITW_GAME_DATA", str(pathlib.Path.home() / ".local/share/Steam/steamapps/common/Night in the Woods/Night in the Woods_Data")))
WORK = pathlib.Path(os.environ.get("NITW_WORKSPACE", str(pathlib.Path(__file__).resolve().parents[1])))
DEST = WORK / 'NITW-Unity-Reconstruction/Assets/Resources/RecoveredTitle'
DEST.mkdir(parents=True, exist_ok=True)
scene = UnityPy.load(str(ROOT / 'level1'))
objects = {o.path_id:o for o in scene.objects}
assets = {o.path_id:o for o in UnityPy.load(str(ROOT / 'sharedassets1.assets')).objects}
gos = {o.path_id:o.read() for o in scene.objects if o.type.name == 'GameObject'}
def transform(go):
    t = next(objects[c.component.m_PathID].read_typetree() for c in gos[go].m_Component if objects[c.component.m_PathID].type.name == 'Transform')
    return dict(position=t['m_LocalPosition'], rotation=t['m_LocalRotation'], scale=t['m_LocalScale'])
def mesh(id):
    h = MeshHandler(assets[id].read()); h.process()
    return dict(id=id, name=assets[id].read().m_Name,
                vertices=[dict(zip(('x','y','z'),v)) for v in h.m_Vertices],
                triangles=[i for sub in h.get_triangles() for tri in sub for i in tri])
def material(id):
    d = assets[id].read_typetree()
    colors=dict(d['m_SavedProperties']['m_Colors'])
    return dict(id=id, color=colors['_Color'], tint=colors.get('_TintColor',colors['_Color']))
leaves = json.loads((WORK/'research/menu-marker/title-leaves-source.json').read_text())
if hashlib.sha256((ROOT/'level1').read_bytes()).hexdigest() != leaves['sceneSha256']:
    raise RuntimeError('Source scene changed; refresh recovered script fields first.')
cil = json.loads((WORK/'research/menu-marker/title-leaves-cil.json').read_text())
if hashlib.sha256((ROOT/'Managed/Assembly-CSharp.dll').read_bytes()).hexdigest() != cil['artifact']['digest']['sha256']:
    raise RuntimeError('Source assembly changed; refresh recovered CIL and fields first.')
renderers = {r['leaf']:r for r in json.loads((WORK/'research/menu-marker/title-leaf-renderers.json').read_text())}
rows = []
for leaf in leaves['leaves']:
    r = renderers[leaf['id']]; t = leaf['Transform']; ch = r['localTransform']; mr = r['MeshRenderer']
    rows.append(dict(id=leaf['id'], name=leaf['go'], fields=leaf['fields'],
        transform=dict(position=t['m_LocalPosition'],rotation=t['m_LocalRotation'],scale=t['m_LocalScale']),
        child=dict(position=ch['m_LocalPosition'],rotation=ch['m_LocalRotation'],scale=ch['m_LocalScale']),
        material=mr['m_Materials'][0]['m_PathID'], sortingLayer=mr['m_SortingLayerID'], order=mr['m_SortingOrder']))
logo = assets[23].read(); logo.m_RD.texture.read().image.save(DEST/'Logo.png')
bark = objects[565].read_typetree()
camera = objects[498].read_typetree()
brain = json.loads((WORK/'research/menu-marker/title-brain-source.json').read_text())
fly_source=json.loads((WORK/'research/menu-marker/murderfly-source.json').read_text())
if fly_source['sha256']['level1'] != leaves['sceneSha256']: raise RuntimeError('Murderfly source identity mismatch')
flies=[]
for fly in fly_source['instances']:
    children=[]
    for ch in fly['hierarchy']['children']:
        mr=next(c['data'] for c in ch['components'] if c['type']=='MeshRenderer')
        children.append(dict(name=ch['name'],transform=transform(ch['id']),material=mr['m_Materials'][0]['m_PathID'],sortingLayer=mr['m_SortingLayerID'],order=mr['m_SortingOrder']))
    flies.append(dict(id=fly['id'],fields=fly['fields'],transform=transform(fly['hierarchy']['id']),children=children))
bloom=json.loads((WORK/'research/menu-marker/murderfly-bloom-source.json').read_text())['fields']
data = dict(source=str(ROOT), sha256={n:hashlib.sha256((ROOT/n).read_bytes()).hexdigest() for n in ['level1','sharedassets1.assets','Managed/Assembly-CSharp.dll']},
    parent=transform(53), leaves=rows, flies=flies, bloom=bloom, meshes=[mesh(15),mesh(14),mesh(13)], materials=[material(i) for i in [3,4,5,6,7,8]],
    logo=transform(2), logoPPU=logo.m_PixelsToUnits, logoPivot=dict(x=logo.m_Pivot.x,y=logo.m_Pivot.y),
    bark=transform(199), barkLayer=bark['m_SortingLayerID'], barkOrder=bark['m_SortingOrder'],
    camera=transform(92), fov=camera['field of view'], background=camera['m_BackGroundColor'],
    introTime=brain['leafIntroTime'], cameraStartZ=brain['cameraStartZ'])
(DEST/'Scene.json').write_text(json.dumps(data,indent=2))
print(f'Exported {len(rows)} leaves, {len(flies)} murderflies, source meshes/materials/Bloom/logo/camera to {DEST}')
