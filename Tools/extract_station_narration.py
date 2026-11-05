"""Export only BusStation EnterLevel's source program for local use."""
import os
import hashlib,json,pathlib,re
import UnityPy
ROOT=pathlib.Path(os.environ.get("NITW_GAME_DATA", str(pathlib.Path.home() / ".local/share/Steam/steamapps/common/Night in the Woods/Night in the Woods_Data")))
WORK=pathlib.Path(os.environ.get("NITW_WORKSPACE", str(pathlib.Path(__file__).resolve().parents[1])))
OUT=WORK/'NITW-Unity-Reconstruction/Assets/Resources/RecoveredStationEntry';OUT.mkdir(parents=True,exist_ok=True)
env=UnityPy.load(str(ROOT/'sharedassets11.assets'))
asset=next(o for o in env.objects if o.path_id==524).read();text=asset.m_Script
if isinstance(text,bytes):text=text.decode('utf-8')
node=next(n for n in re.split(r'(?m)^title: ',text)[1:] if n.splitlines()[0]=='EnterLevel')
body=node.split('---\n',1)[1].split('===',1)[0]
commands=[];lines=[]
for row in body.splitlines():
 row=row.strip()
 if not row:continue
 if row.startswith('<<'):
  if lines:raise ValueError('Command after entry lines requires a new implementation review')
  commands.append(row[2:-2])
 else:
  m=re.fullmatch(r'([^:]+):\s*(.*?)\s+#line:([^\s]+)',row)
  if not m:raise ValueError('Unsupported EnterLevel line')
  speaker,value,id=m.groups();assert speaker=='Mae';lines.append(dict(id=id,speaker=speaker,text=value))
assert len(lines)==5 and len({r['id'] for r in lines})==5
program=dict(node='EnterLevel',assetName=asset.m_Name,assetPathId=524,sourceFile='sharedassets11.assets',sourceSha256=hashlib.sha256((ROOT/'sharedassets11.assets').read_bytes()).hexdigest(),scriptSha256=hashlib.sha256(text.encode()).hexdigest(),commands=commands,lines=lines,scope='Fresh entry narration and source actor setup only; no interactions, exits or save/load.')
(OUT/'Program.json').write_text(json.dumps(program,indent=2)+'\n')
evidence=WORK/'research/bus-station/entry-narration';evidence.mkdir(parents=True,exist_ok=True)
(evidence/'program-source.json').write_text(json.dumps(program,indent=2)+'\n')
print('Recovered EnterLevel:',len(commands),'commands,',len(lines),'source lines')
