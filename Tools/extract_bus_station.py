"""Recover BusStation's visual hierarchy for the scene-entry milestone."""
import os
import hashlib,json,pathlib,math
import UnityPy
from UnityPy.helpers.MeshHelper import MeshHandler
ROOT=pathlib.Path(os.environ.get("NITW_GAME_DATA", str(pathlib.Path.home() / ".local/share/Steam/steamapps/common/Night in the Woods/Night in the Woods_Data")))
WORK=pathlib.Path(os.environ.get("NITW_WORKSPACE", str(pathlib.Path(__file__).resolve().parents[1])));DEST=WORK/'NITW-Unity-Reconstruction/Assets/Resources/RecoveredStation'
DEST.mkdir(parents=True,exist_ok=True)
env=UnityPy.load(str(ROOT/'level11'));objects={o.path_id:o for o in env.objects}
gos={o.path_id:o.read() for o in env.objects if o.type.name=='GameObject'}
transforms={o.path_id:o.read() for o in env.objects if o.type.name=='Transform'}
fields=json.loads((WORK/'research/bus-station/components-source.json').read_text())['components']
textures={};sprites={};materials={};meshes={};nodes=[]
def key(p):return pathlib.Path(p.assets_file.name).stem+'_'+str(p.path_id)
def signed(n):return n if n<2**31 else n-2**32
def color(c):return dict(r=c.r,g=c.g,b=c.b,a=c.a)
def texture(p):
    if not p.m_PathID:return ''
    o=p.deref();k=key(o)
    if o.type.name!='Texture2D':return ''
    if k not in textures:
        d=o.read();im=d.image;im.save(DEST/(k+'.png'));textures[k]=dict(key=k,name=d.m_Name,width=im.width,height=im.height)
    return k
def sprite(p):
    if not p.m_PathID:return ''
    o=p.deref();k=key(o)
    if k not in sprites:
        s=o.read();image=s.m_RD.texture.read().image
        if image.size!=(round(s.m_Rect.width),round(s.m_Rect.height)):raise RuntimeError('Packed sprite requires explicit reconstruction: '+s.m_Name)
        image.save(DEST/(k+'.png'));sprites[k]=dict(key=k,name=s.m_Name,ppu=s.m_PixelsToUnits,pivot=dict(x=s.m_Pivot.x,y=s.m_Pivot.y))
    return k
def material(p):
    o=p.deref();k=key(o)
    if k not in materials:
        d=o.read();t=p.read_typetree();sh=d.m_Shader.read_typetree();props=t['m_SavedProperties'];cols=dict(props['m_Colors'])
        main=dict(props['m_TexEnvs']).get('_MainTex',{})
        tex=''
        # Resolve the texture pointer in the material's own file, not the scene.
        if main.get('m_Texture',{}).get('m_PathID'):
            from UnityPy.classes import PPtr
            ref=main['m_Texture'];tex=texture(PPtr(m_FileID=ref['m_FileID'],m_PathID=ref['m_PathID'],assetsfile=o.assets_file))
        materials[k]=dict(key=k,name=d.m_Name,shader=sh.get('m_Name') or sh.get('m_ParsedForm',{}).get('m_Name'),
            color=cols.get('_Color',dict(r=1,g=1,b=1,a=1)),tint=cols.get('_TintColor',dict(r=1,g=1,b=1,a=1)),texture=tex,
            scale=main.get('m_Scale',dict(x=1,y=1)),offset=main.get('m_Offset',dict(x=0,y=0)))
    return k
def mesh(p):
    if not p.m_PathID:return ''
    o=p.deref();k=key(o)
    if k not in meshes:
        h=MeshHandler(o.read());h.process()
        meshes[k]=dict(key=k,name=o.read().m_Name,vertices=[dict(zip(('x','y','z'),v)) for v in h.m_Vertices],
            uv=[dict(zip(('x','y'),v)) for v in (h.m_UV0 or [])],triangles=[i for sub in h.get_triangles() for tri in sub for i in tri])
    return k
for id,g in gos.items():
    t=next(objects[c.component.m_PathID].read() for c in g.m_Component if objects[c.component.m_PathID].type.name=='Transform')
    parent=transforms[t.m_Father.m_PathID].m_GameObject.m_PathID if t.m_Father.m_PathID else 0
    node=dict(id=id,name=g.m_Name,parent=parent,layer=g.m_Layer,active=g.m_IsActive,
        transform=dict(position=dict(x=t.m_LocalPosition.x,y=t.m_LocalPosition.y,z=t.m_LocalPosition.z),
            rotation=dict(x=t.m_LocalRotation.x,y=t.m_LocalRotation.y,z=t.m_LocalRotation.z,w=t.m_LocalRotation.w),
            scale=dict(x=t.m_LocalScale.x,y=t.m_LocalScale.y,z=t.m_LocalScale.z)))
    for c in g.m_Component:
        o=objects[c.component.m_PathID]
        if o.type.name in ['SpriteRenderer','MeshRenderer']:
            d=o.read();r=dict(enabled=bool(d.m_Enabled),color=color(d.m_Color) if o.type.name=='SpriteRenderer' else dict(r=1,g=1,b=1,a=1),
                sortingLayer=signed(o.read_typetree()['m_SortingLayerID']),order=d.m_SortingOrder,material=material(d.m_Materials[0]))
            if o.type.name=='SpriteRenderer':r.update(sprite=sprite(d.m_Sprite),flipX=d.m_FlipX,flipY=d.m_FlipY);node['sprite']=r
            else:node['meshRenderer']=r
        elif o.type.name=='MeshFilter':node['mesh']=mesh(o.read().m_Mesh)
    nodes.append(node)
cam=objects[553].read_typetree();cam_data=next(x['fields'] for x in fields if x['type']=='CameraData')
player=next(n for n in nodes if n['id']==219)
half=math.tan(math.radians(cam['field of view'])/2)*6*16/9
cameraX=max(cam_data['minX']+.094053097+half,min(cam_data['maxX']+.094053097-half,player['transform']['position']['x']))
sounds=[dict(event=x['fields']['eventName'],position=next(n['transform']['position'] for n in nodes if n['id']==x['fields']['m_GameObject']['m_PathID']),
    spatial=bool(x['fields']['usePosition'])) for x in fields if x['type']=='AmbientSFX' and x['fields']['playOnStart'] and x['fields']['eventName']]
data=dict(source=str(ROOT/'level11'),sha256=hashlib.sha256((ROOT/'level11').read_bytes()).hexdigest(),nodes=nodes,
    sprites=list(sprites.values()),textures=list(textures.values()),materials=list(materials.values()),meshes=list(meshes.values()),sounds=sounds,
    camera=dict(x=cameraX,y=cam_data['y'],z=-6),fov=cam['field of view'],background=cam['m_BackGroundColor'],mask=signed(cam['m_CullingMask']['m_Bits']),
    scope='Recovered serialized visual scene and locked entry camera. Player movement, dialogue and later scenes are not enabled.')
(DEST/'Scene.json').write_text(json.dumps(data,indent=2));(WORK/'research/bus-station/visual-source.json').write_text(json.dumps(data,indent=2))
print('Recovered',len(nodes),'nodes',len(sprites),'sprites',len(meshes),'meshes',len(materials),'materials; camera',data['camera'])
