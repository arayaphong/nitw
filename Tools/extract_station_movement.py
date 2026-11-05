"""Export BusStation movement tuning and Entity obstruction geometry unchanged."""
import os
import json,pathlib
ROOT=pathlib.Path(os.environ.get("NITW_WORKSPACE", str(pathlib.Path(__file__).resolve().parents[1])))
source=json.loads((ROOT/'research/bus-station/components-source.json').read_text())['components']
player=next(c['fields'] for c in source if c['type']=='Player')
entity=next(c['fields'] for c in source if c['id']==780)
constants=next(c['fields'] for c in source if c['type']=='Constants')
tuning={k:v for k,v in player.items() if isinstance(v,(float,int,bool))}
obstacles=[]
for c in source:
 f=c['fields']
 if c['type']=='Entity' and f['type']==1:
  obstacles.append(dict(id=f['m_GameObject']['m_PathID'],name=c['gameObject'],points=f['points'],
   width=f['collideBox']['width'],height=f['collideBox']['height'],x=f['collideBox']['x'],y=f['collideBox']['y'],
   depth=f['meshZDepth'],oneWay=bool(f['oneWay']),noFallThrough=bool(f['noFallThrough']),
   surfaceAnimation=f['surfaceAnimationType'],surfaceSound=f['surfaceSoundType'],depthReference=f['depthReference']['m_PathID']))
data=dict(tuning=tuning,obstacles=obstacles,boxOffset=entity['boxOffset'],boxHalfExtents=entity['boxHalfExtents'],
 boxWidth=entity['collideBox']['width'],boxHeight=entity['collideBox']['height'],boxX=entity['collideBox']['x'],boxY=entity['collideBox']['y'],
 moveStep=.01,moveStepY=.05,fineMoveStep=constants['fineMoveStep'],slopeClimb=constants['slopeClimb'])
p=ROOT/'NITW-Unity-Reconstruction/Assets/Resources/RecoveredStation/Movement.json';p.write_text(json.dumps(data,indent=2)+'\n')
cil=json.loads((ROOT/'research/gameplay/core-cil.json').read_text())
selected=[m for m in cil['methods'] if m['owner'] in ['Player','Entity','SFX','Util'] and m['name'] in ['UpdateMovement','ForceJump','Jump','UpdateFall','UpdateFriction','OnHitY','OnHitX','MoveX','MoveY','StepX','StepY','SnapToFloor','CollideEntityAndObstructions','OverlapTest','IsOverlapping','Collide','GetLeftRightTopBottom','GenerateCollisionMeshFromPoints','GetFootstepEventName','PlaySurfaceFootstepWalk','PlaySurfaceFootstepRun','PlaySurfaceFootstepJump','PlaySurfaceFootstepLand','ResetTripleJump']]
(ROOT/'research/bus-station/player-movement-cil.json').write_text(json.dumps(dict(sha256=cil['sha256'],rea_parent='ev_e8b1be1ea281ed44793a684b6954859cecf4125c84ae457bae8af5097f456d0b',methods=selected),indent=2)+'\n')
print('Movement:',len(obstacles),'original obstructions; source tuning exported')
