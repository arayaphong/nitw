"""Export the original OptionsMenu hierarchy and UI assets without executing game code."""
import os
import hashlib, json, pathlib, sys
import UnityPy
SOURCE=pathlib.Path(os.environ.get("NITW_GAME_DATA", str(pathlib.Path.home() / ".local/share/Steam/steamapps/common/Night in the Woods/Night in the Woods_Data")))
DEST=(pathlib.Path(os.environ.get("NITW_WORKSPACE", str(pathlib.Path(__file__).resolve().parents[1]))) / 'NITW-Unity-Reconstruction/Assets/Resources/RecoveredOptions')
DEST.mkdir(parents=True,exist_ok=True)
env=UnityPy.load(str(SOURCE/'level1'))
objects={x.path_id:x for x in env.objects}
transforms={x.path_id:x.read() for x in env.objects if x.type.name=='Transform'}
gos={x.path_id:x.read() for x in env.objects if x.type.name=='GameObject'}
assets={i:{x.path_id:x for x in UnityPy.load(str(SOURCE/n)).objects} for i,n in [(2,'sharedassets1.assets'),(3,'sharedassets0.assets')]}
root=next(t for t in transforms.values() if gos[t.m_GameObject.m_PathID].m_Name=='OptionsMenu')
def v(a):return dict(x=a.x,y=a.y,z=a.z)
def q(a):return dict(x=a.x,y=a.y,z=a.z,w=a.w)
def c(a):return dict(r=a.r,g=a.g,b=a.b,a=a.a)
exports={};fonts={};nodes=[]
def sprite(p):
 key=f'{p.m_FileID}_{p.m_PathID}'
 if key in exports:return key
 d=assets[p.m_FileID][p.m_PathID].read();image=d.m_RD.texture.read().image
 full=(round(d.m_Rect.width),round(d.m_Rect.height))
 if image.size != full:raise RuntimeError(f'Atlas requires explicit crop reconstruction: {key}, {image.size}, {full}')
 image.save(DEST/(key+'.png'))
 exports[key]=dict(key=key,name=d.m_Name,ppu=d.m_PixelsToUnits,pivot=dict(x=d.m_Pivot.x,y=d.m_Pivot.y),width=image.width,height=image.height)
 return key
def font(p):
 key=f'{p.m_FileID}_{p.m_PathID}'
 if key not in fonts:
  d=assets[p.m_FileID][p.m_PathID].read();(DEST/(key+'.ttf')).write_bytes(bytes(d.m_FontData))
  fonts[key]=dict(key=key,name=d.m_Name,size=d.m_FontSize)
 return key
def walk(t,parent=-1):
 g=gos[t.m_GameObject.m_PathID];idx=len(nodes)
 n=dict(id=t.m_GameObject.m_PathID,name=g.m_Name,parent=parent,position=v(t.m_LocalPosition),rotation=q(t.m_LocalRotation),scale=v(t.m_LocalScale),active=g.m_IsActive,layer=g.m_Layer)
 nodes.append(n)
 for comp in g.m_Component:
  o=objects[comp.component.m_PathID]
  if o.type.name=='SpriteRenderer':
   d=o.read();n['sprite']=dict(asset=sprite(d.m_Sprite),color=c(d.m_Color),enabled=d.m_Enabled,order=d.m_SortingOrder,sortingLayer=o.read_typetree()['m_SortingLayerID'],flipX=d.m_FlipX,flipY=d.m_FlipY)
  if o.type.name=='TextMesh':
   d=o.read();packed=o.read_typetree()['m_Color']['rgba'];col=dict(r=(packed&255)/255,g=((packed>>8)&255)/255,b=((packed>>16)&255)/255,a=((packed>>24)&255)/255)
   n['text']=dict(value=d.m_Text,font=font(d.m_Font),fontSize=d.m_FontSize,characterSize=d.m_CharacterSize,anchor=d.m_Anchor,alignment=d.m_Alignment,lineSpacing=d.m_LineSpacing,offsetZ=d.m_OffsetZ,richText=d.m_RichText,color=col)
  if o.type.name=='MeshRenderer':
   d=o.read();n['renderer']=dict(enabled=d.m_Enabled,order=d.m_SortingOrder,sortingLayer=o.read_typetree()['m_SortingLayerID'])
 for child in t.m_Children:walk(transforms[child.m_PathID],idx)
walk(root)
from types import SimpleNamespace
for path_id in (8724,9950):sprite(SimpleNamespace(m_FileID=3,m_PathID=path_id))
# Resolve scene component references to source GameObject IDs.
component_go={}
for g_id,g in gos.items():
 for comp in g.m_Component:component_go[comp.component.m_PathID]=g_id
recovered=json.load(open(pathlib.Path(os.environ.get("NITW_WORKSPACE", str(pathlib.Path(__file__).resolve().parents[1]))) / 'research/options-serialized-fields.json'))['scenes']['level1']['components']
components={x['id']:x for x in recovered}
def go(ref):
 value=ref.get('m_PathID',0)
 return value if value in gos else component_go.get(value,0)
def menu(menu_id):
 fields=components[menu_id]['fields'];items=[]
 for ref in fields['menuItems']:
  row=components[ref['m_PathID']];f=row['fields']
  item={k:f.get(k) for k in ('type','target','buttonType','pole','holdToConfirm','titleOnlyOption','journalOnlyOption','sliderWidth')}
  item['go']=f['m_GameObject']['m_PathID']
  for k in ('label','display','sliderBody','sliderNotch','arrowPrev','arrowNext','confirmLabel','yesLabel','noLabel','captureInputPopup'):item[k]=go(f[k])
  items.append(item)
 return dict(items=items,selector=go(fields['selector']))
menus=[menu(894),menu(873)]
sortingLayers=next(o.read_typetree()['m_SortingLayers'] for o in UnityPy.load(str(SOURCE/'globalgamemanagers')).objects if o.type.name=='TagManager')
data=dict(source=str(SOURCE/'level1'),sourceSha256=hashlib.sha256((SOURCE/'level1').read_bytes()).hexdigest(),nodes=nodes,sprites=list(exports.values()),fonts=list(fonts.values()),menus=menus,sortingLayers=sortingLayers)
(DEST/'Scene.json').write_text(json.dumps(data,indent=2))
print(json.dumps(dict(nodes=len(nodes),sprites=len(exports),fonts=list(fonts.values()),sha256=data['sourceSha256'])))
