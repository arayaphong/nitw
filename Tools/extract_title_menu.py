"""Recover desktop title menu TextMeshes and their independent HUD camera."""
import os
import hashlib,json,pathlib
import UnityPy
ROOT=pathlib.Path(os.environ.get("NITW_GAME_DATA", str(pathlib.Path.home() / ".local/share/Steam/steamapps/common/Night in the Woods/Night in the Woods_Data")))
WORK=pathlib.Path(os.environ.get("NITW_WORKSPACE", str(pathlib.Path(__file__).resolve().parents[1])));DEST=WORK/'NITW-Unity-Reconstruction/Assets/Resources/RecoveredTitle'
e=UnityPy.load(str(ROOT/'level1'));objects={o.path_id:o for o in e.objects};gos={o.path_id:o.read() for o in e.objects if o.type.name=='GameObject'}
def component(id,kind): return next(objects[c.component.m_PathID] for c in gos[id].m_Component if objects[c.component.m_PathID].type.name==kind)
def pose(id):
    t=component(id,'Transform').read_typetree();return dict(position=t['m_LocalPosition'],rotation=t['m_LocalRotation'],scale=t['m_LocalScale'])
rows=[]
for id,label in [(189,'Continue'),(241,'Options'),(89,'Extras'),(18,'Exit')]:
    t=component(id,'TextMesh').read_typetree();mr=component(id,'MeshRenderer').read_typetree();rgba=t['m_Color']['rgba']
    rows.append(dict(id=id,name=gos[id].m_Name,label=label,serializedText=t['m_Text'],transform=pose(id),layer=gos[id].m_Layer,
        characterSize=t['m_CharacterSize'],fontSize=t['m_FontSize'],fontStyle=t['m_FontStyle'],anchor=t['m_Anchor'],alignment=t['m_Alignment'],
        lineSpacing=t['m_LineSpacing'],offsetZ=t['m_OffsetZ'],richText=t['m_RichText'],
        color=dict(zip(('r','g','b','a'),[((rgba>>shift)&255)/255 for shift in [0,8,16,24]])),sortingLayer=mr['m_SortingLayerID'],order=mr['m_SortingOrder']))
assets={o.path_id:o for o in UnityPy.load(str(ROOT/'sharedassets0.assets')).objects};font=assets[6360].read();DEST.mkdir(exist_ok=True)
(DEST/'MenuFont.ttf').write_bytes(bytes(font.m_FontData));cam=component(206,'Camera').read_typetree()
data=dict(source=str(ROOT/'level1'),sourceSha256=hashlib.sha256((ROOT/'level1').read_bytes()).hexdigest(),rows=rows,fontName=font.m_Name,fontDefaultSize=font.m_FontSize,
    hudTransform=pose(206),hudFov=cam['field of view'],hudDepth=cam['m_Depth'],hudClearFlags=cam['m_ClearFlags'],hudMask=cam['m_CullingMask']['m_Bits'],
    menuVisibleTime=4.0,scope='Four desktop rows observed in original player. Continue runtime label comes from TitleBrain save-present branch; CurrentUser is not shown in this desktop context.')
(DEST/'Menu.json').write_text(json.dumps(data,indent=2));(WORK/'research/menu-marker/title-menu-source.json').write_text(json.dumps(data,indent=2))
print('Recovered four rows:',[(r['label'],r['transform']['position']) for r in rows]);print('HUD FOV',data['hudFov'],'mask',data['hudMask'],'font',font.m_Name,font.m_FontSize)
