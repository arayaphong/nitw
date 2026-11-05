"""Decode Janitor's entry-only shipped Unity 5.6 streamed/constant animation curves without executing target code."""
import os
import collections,hashlib,json,math,pathlib,struct,zlib
import UnityPy
ROOT=pathlib.Path(os.environ.get("NITW_GAME_DATA", str(pathlib.Path.home() / ".local/share/Steam/steamapps/common/Night in the Woods/Night in the Woods_Data")))
WORK=pathlib.Path(os.environ.get("NITW_WORKSPACE", str(pathlib.Path(__file__).resolve().parents[1])));DEST=WORK/'NITW-Unity-Reconstruction/Assets/Resources/RecoveredStationJanitor';DEST.mkdir(parents=True,exist_ok=True)
env=UnityPy.load(str(ROOT/'sharedassets11.assets'));idx={o.path_id:o for o in env.objects}
scene=UnityPy.load(str(ROOT/'level11'));gos={o.path_id:o.read() for o in scene.objects if o.type.name=='GameObject'};ts={o.path_id:o.read() for o in scene.objects if o.type.name=='Transform'};paths={}
def walk(t,p):
 paths[zlib.crc32(p.encode())]=p
 for q in t.m_Children:
  ch=ts[q.m_PathID];walk(ch,p+'/'+gos[ch.m_GameObject.m_PathID].m_Name if p else gos[ch.m_GameObject.m_PathID].m_Name)
walk(next(t for t in ts.values() if t.m_GameObject.m_PathID==266),'')
c=idx[553].read_typetree();tos=dict(c['m_TOS']);machine=c['m_Controller']['m_StateMachineArray'][0]['data'];selected=[0,1,4,5]
states=[];clipids=set();unknown=[];sprites={}
for si in selected:
 s=machine['m_StateConstantArray'][si]['data'];bi=s['m_BlendTreeConstantArray'][0]['data']['m_NodeArray'][0]['data']['m_ClipID'];ci=c['m_AnimationClips'][bi]['m_PathID'];clipids.add(ci)
 tr=[]
 for v in s['m_TransitionConstantArray']:
  t=v['data'];destination=t['m_DestinationState']
  if destination not in selected:continue
  tr.append(dict(destination=tos[machine['m_StateConstantArray'][destination]['data']['m_NameID']],duration=t['m_TransitionDuration'],offset=t['m_TransitionOffset'],exitTime=t['m_ExitTime'],hasExitTime=t['m_HasExitTime'],fixedDuration=t['m_HasFixedDuration'],canTransitionToSelf=t['m_CanTransitionToSelf'],conditions=[dict(mode=v['data']['m_ConditionMode'],parameter=tos[v['data']['m_EventID']],threshold=v['data']['m_EventThreshold']) for v in t['m_ConditionConstantArray']]))
 states.append(dict(index=si,name=tos[s['m_NameID']],clip=ci,speed=s['m_Speed'],loop=s['m_Loop'],transitions=tr))
def sprite(id):
 if id in sprites:return
 s=idx[id].read();im=s.m_RD.texture.read().image
 assert im.size==(round(s.m_Rect.width),round(s.m_Rect.height)),(s.m_Name,im.size,s.m_Rect)
 im.save(DEST/(str(id)+'.png'));sprites[id]=dict(id=id,name=s.m_Name,ppu=s.m_PixelsToUnits,pivot=dict(x=s.m_Pivot.x,y=s.m_Pivot.y))

clips=[]
for id in sorted(clipids):
 t=idx[id].read_typetree();data=t['m_MuscleClip']['m_Clip']['data'];sc=data['m_StreamedClip'];dc=data['m_DenseClip'];constant=data['m_ConstantClip']['data'];assert dc['m_CurveCount']==0
 raw=struct.pack('<%dI'%len(sc['data']),*sc['data']);pos=0;curves=collections.defaultdict(list)
 while pos<len(raw):
  time,n=struct.unpack_from('<fI',raw,pos);pos+=8
  for _ in range(n):
   index,*co=struct.unpack_from('<I4f',raw,pos);pos+=20;curves[index].append(dict(time=time,value=co[3],coefficients=co))
 offset=0;fc=[];oc=[]
 for b in t['m_ClipBindingConstant']['genericBindings']:
  width=3 if b['typeID']==4 and b['attribute'] in [1,3,4] else 4 if b['typeID']==4 and b['attribute']==2 else 1
  path=paths.get(b['path']);prop=({1:'m_LocalPosition',2:'m_LocalRotation',3:'m_LocalScale',4:'localEulerAnglesRaw'}[b['attribute']] if b['typeID']==4 else 'm_IsActive' if b['typeID']==1 else 'm_Sprite' if b['isPPtrCurve'] else {zlib.crc32(n.encode()):n for n in ['m_Color.a','m_Color.r','m_Color.g','m_Color.b','m_Enabled']}.get(b['attribute']) if b['typeID']==212 else 'setRenderer' if b['typeID']==114 and b['attribute']==4211852368 else None)
  if path is None:unknown.append(dict(clip=t['m_Name'],pathHash=b['path'],note='Path is absent from the shipped level11 JanitorRig hierarchy, so native binding also has no target.'))
  if prop is None:raise ValueError(b)
  for axis in range(width):
   ix=offset+axis
   if ix<sc['curveCount']:
    keys=[x for x in curves[ix] if x['time']>=0 and math.isfinite(x['time']) and x['time']<t['m_MuscleClip']['m_StopTime']+1e-4]
    if not keys:keys=[dict(time=0,value=curves[ix][0]['value'],coefficients=[0,0,0,curves[ix][0]['value']])]
    elif keys[0]['time']>0:keys.insert(0,dict(time=0,value=curves[ix][0]['value'],coefficients=[0,0,0,curves[ix][0]['value']]))
   else:
    value=constant[ix-sc['curveCount']];keys=[dict(time=0,value=value,coefficients=[0,0,0,value])]
   if path is None:continue
   if b['isPPtrCurve']:
    out=[]
    for k in keys:
     ref=t['m_ClipBindingConstant']['pptrCurveMapping'][int(k['value'])];assert ref['m_FileID']==0;sid=ref['m_PathID'];sprite(sid);out.append(dict(time=k['time'],sprite=sid))
    oc.append(dict(path=path,property=prop,keys=out))
   else:
    out=[]
    for ki,k in enumerate(keys):
     prev=keys[ki-1] if ki else k;dt=k['time']-prev['time'];p=prev['coefficients'];it=(3*p[0]*dt+2*p[1])*dt+p[2] if ki else k['coefficients'][2]
     out.append(dict(time=k['time'],value=k['value'],inTangent=it,outTangent=k['coefficients'][2],coefficients=k['coefficients']))
    fc.append(dict(path=path,property=prop+('.'+'xyzw'[axis] if width>1 else ''),component='Transform' if b['typeID']==4 else 'GameObject' if b['typeID']==1 else 'SpriteRenderer' if b['typeID']==212 else 'RecoveredStationExistsVariable',stepped=b['typeID'] in [1,114],keys=out))
  offset+=width
 assert offset==sc['curveCount']+len(constant),(id,offset,sc['curveCount'],len(constant))
 clips.append(dict(id=id,name=t['m_Name'],length=t['m_MuscleClip']['m_StopTime'],sampleRate=t['m_SampleRate'],loop=t['m_MuscleClip']['m_LoopTime'],floatCurves=fc,objectCurves=oc,events=[dict(time=v['time'],function=v['functionName']) for v in t['m_Events']]))
out=dict(source=str(ROOT/'sharedassets11.assets'),sha256=hashlib.sha256((ROOT/'sharedassets11.assets').read_bytes()).hexdigest(),controllerPathId=553,animatorGameObjectId=266,states=states,clips=clips,sprites=list(sprites.values()),unboundPaths=unknown,drillEvent='event:/bus_station/janitor_drill',drillEmitter=82,clothEmitter=89,drillFade=.1)
(DEST/'Animation.json').write_text(json.dumps(out,indent=2)+'\n')
print('Recovered Janitor entry:',len(states),'states',len(clips),'clips',len(sprites),'sprites',sum(len(x['floatCurves'])+len(x['objectCurves']) for x in clips),'bindings; unbound',unknown)
