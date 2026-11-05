#!/usr/bin/env python3
"""Recover immutable level0 intro hierarchy and animation from local installed NITW."""
import os
import argparse, hashlib, json, pathlib, struct, zlib
import UnityPy
from UnityPy.helpers.MeshHelper import MeshHandler

PROJECT=pathlib.Path(__file__).resolve().parents[1]
ROOT=pathlib.Path(os.environ.get("NITW_GAME_DATA", str(pathlib.Path.home() / ".local/share/Steam/steamapps/common/Night in the Woods/Night in the Woods_Data")))

def v2(v):return {'x':float(v.x),'y':float(v.y)}
def v3(v):return {'x':float(v.x),'y':float(v.y),'z':float(v.z)}
def quat(v):return dict(v3(v),w=float(v.w))
def color(v):return {k:float(getattr(v,k)) for k in ('r','g','b','a')}
def packed_color(v):return {k:((v>>(8*i))&255)/255 for i,k in enumerate(('r','g','b','a'))}
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()

def main():
 out=PROJECT/'Assets/Resources/RecoveredIntro';(out/'Textures').mkdir(parents=True,exist_ok=True);(out/'Fonts').mkdir(exist_ok=True)
 scene=UnityPy.load(str(ROOT/'level0'));assets=UnityPy.load(str(ROOT/'sharedassets0.assets'))
 ix={o.path_id:o for o in assets.objects};gobjects={o.path_id:o.read() for o in scene.objects if o.type.name=='GameObject'};transforms={o.path_id:o.read() for o in scene.objects if o.type.name=='Transform'}
 go_to_t={t.m_GameObject.m_PathID:(tid,t) for tid,t in transforms.items()};clip=ix[6287].read_typetree();ctrl=ix[6346].read_typetree()
 selected=[]
 def add(goid,parent_id=0,path=''):
  go=gobjects[goid];tid,t=go_to_t[goid];selected.append((goid,parent_id,go,t,path))
  for ptr in t.m_Children:
   child=transforms[ptr.m_PathID];cid=child.m_GameObject.m_PathID;name=gobjects[cid].m_Name;add(cid,goid,path+'/'+name if path else name)
 add(12)
 sprite_ids=set(p['m_PathID'] for p in clip['m_ClipBindingConstant']['pptrCurveMapping']);font_ids=set();nodes=[]
 for goid,parent_id,go,t,path in selected:
  n={'id':goid,'name':go.m_Name,'parentId':parent_id,'active':go.m_IsActive,'layer':go.m_Layer,'localPosition':v3(t.m_LocalPosition),'localRotation':quat(t.m_LocalRotation),'localScale':v3(t.m_LocalScale),'isAnimator':False}
  for c in go.m_Component:
   ob=c.component
   if ob.type.name=='SpriteRenderer':
    d=ob.read();sprite_ids.add(d.m_Sprite.m_PathID);n['renderer']={'enabled':d.m_Enabled,'spriteId':d.m_Sprite.m_PathID,'color':color(d.m_Color),'sortingLayerId':d.m_SortingLayerID,'sortingOrder':d.m_SortingOrder,'flipX':d.m_FlipX,'flipY':d.m_FlipY}
   if ob.type.name=='TextMesh':
    d=ob.read_typetree();font_id=d['m_Font']['m_PathID'];font_ids.add(font_id);n['text']={'text':d['m_Text'],'fontId':font_id,'characterSize':d['m_CharacterSize'],'fontSize':d['m_FontSize'],'fontStyle':d['m_FontStyle'],'anchor':d['m_Anchor'],'alignment':d['m_Alignment'],'lineSpacing':d['m_LineSpacing'],'richText':d['m_RichText'],'color':packed_color(d['m_Color']['rgba']),'offsetZ':d['m_OffsetZ'],'tabSize':d['m_TabSize']}
   if ob.type.name=='Animator':n['isAnimator']=True
  nodes.append(n)
 sprites=[]
 for sid in sorted(sprite_ids):
  ob=ix[sid];d=ob.read();texture=d.m_RD.texture.read();texid=d.m_RD.texture.m_PathID;resource=f'RecoveredIntro/Textures/Texture_{texid}';file=out/'Textures'/f'Texture_{texid}.png'
  if not file.exists():texture.image.save(file)
  mesh=MeshHandler(d.m_RD,ob.version);mesh.process();verts=[{'x':float(v[0]),'y':float(v[1])} for v in mesh.m_Vertices];tris=[i for group in mesh.get_triangles() for tri in group for i in tri]
  sprites.append({'id':sid,'name':d.m_Name,'textureResource':resource,'pixelsPerUnit':d.m_PixelsToUnits,'pivot':v2(d.m_Pivot),'rect':{'x':d.m_Rect.x,'y':d.m_Rect.y,'width':d.m_Rect.width,'height':d.m_Rect.height},'border':{'x':d.m_Border.x,'y':d.m_Border.y,'z':d.m_Border.z,'w':d.m_Border.w},'vertices':verts,'triangles':tris,'sourceTextureId':texid,'sourceTextureSize':[texture.m_Width,texture.m_Height],'sourceTextureRect':{'x':d.m_RD.textureRect.x,'y':d.m_RD.textureRect.y,'width':d.m_RD.textureRect.width,'height':d.m_RD.textureRect.height}})
 fonts=[]
 for fid in sorted(font_ids):
  font=ix[fid].read();data=bytes(font.m_FontData);file=out/'Fonts'/f'Font_{fid}.ttf';file.write_bytes(data);fonts.append({'id':fid,'name':font.m_Name,'resource':f'RecoveredIntro/Fonts/Font_{fid}','sourceFontSize':font.m_FontSize,'sha256':hashlib.sha256(data).hexdigest()})
 streamed=clip['m_MuscleClip']['m_Clip']['data']['m_StreamedClip'];binary=struct.pack('<%dI'%len(streamed['data']),*streamed['data']);pos=0;curves={i:[] for i in range(streamed['curveCount'])}
 while pos<len(binary):
  time,count=struct.unpack_from('<fI',binary,pos);pos+=8
  for _ in range(count):
   index,*coeff=struct.unpack_from('<I4f',binary,pos);pos+=20;time=max(time,0.)
   key={'time':time,'value':coeff[3],'coefficients':coeff}
   if curves[index] and curves[index][-1]['time']==time:curves[index][-1]=key
   else:curves[index].append(key)
 paths={}
 for goid,parent_id,go,t,path in selected:
  if path=='logos_animation':rel=''
  elif path.startswith('logos_animation/'):rel=path[len('logos_animation/'):]
  else:continue
  paths[zlib.crc32(rel.encode())]=rel
 mapping=clip['m_ClipBindingConstant']['pptrCurveMapping'];float_curves=[];object_curves=[];index=0
 attrs={zlib.crc32(x.encode()):x for x in ('m_Enabled','m_Color.a')}
 for b in clip['m_ClipBindingConstant']['genericBindings']:
  path=paths[b['path']];width=3 if b['typeID']==4 and b['attribute']==1 else 1
  for axis in range(width):
   keys=curves[index]
   if b['isPPtrCurve']:
    object_curves.append({'path':path,'property':'m_Sprite','keys':[{'time':k['time'],'spriteId':mapping[int(k['value'])]['m_PathID']} for k in keys]})
   else:
    prop='m_LocalPosition.'+'xyz'[axis] if width==3 else attrs[b['attribute']];ks=[]
    for j,k in enumerate(keys):
     it=0.
     if j>0:
      prev=keys[j-1];dt=k['time']-prev['time'];a,bb,c,d=prev['coefficients'];it=3*a*dt*dt+2*bb*dt+c
     ks.append({'time':k['time'],'value':k['value'],'inTangent':it,'outTangent':k['coefficients'][2],'coefficients':k['coefficients']})
    float_curves.append({'path':path,'property':prop,'component':'Transform' if width==3 else 'SpriteRenderer','stepped':prop=='m_Enabled','keys':ks})
   index+=1
 cameras=[]
 for o in scene.objects:
  if o.type.name=='Camera':
   d=o.read_typetree();cameras.append({'id':o.path_id,'gameObject':o.read().m_GameObject.read().m_Name,'fields':d})
 data={'sourceUnityVersion':'5.6.2p4','sourceLevel':'level0','sourceSceneSha256':sha(ROOT/'level0'),'sourceAssetsSha256':sha(ROOT/'sharedassets0.assets'),'rootObjectId':12,'animatorObjectId':29,'nodes':nodes,'sprites':sprites,'fonts':fonts,'clipName':clip['m_Name'],'clipLength':clip['m_MuscleClip']['m_StopTime'],'sampleRate':clip['m_SampleRate'],'floatCurves':float_curves,'objectCurves':object_curves,'events':clip['m_Events'],'cameraReference':cameras,'controllerReference':ctrl,'limitations':['Native postprocessing and game script logic are reconstructed separately. Source Sprite geometry and full Texture2D rectangle are preserved; no tight-image cropping is used.']}
 ix[4837].read().image.save(out/'Textures/Texture_4837.png')
 build_text=ix[5934].read().m_Script
 (out/'BuildNumber.txt').write_bytes(build_text.encode('utf8','surrogateescape') if isinstance(build_text,str) else bytes(build_text))
 (out/'IntroSource.json').write_text(json.dumps(data,indent=2));print('Recovered',len(nodes),'objects,',len(sprites),'sprites,',len(float_curves),'float curves,',len(object_curves),'object curves to',out)
if __name__=='__main__':main()
