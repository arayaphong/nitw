import os
import UnityPy, json, pathlib, hashlib
from UnityPy.helpers.TypeTreeGenerator import TypeTreeGenerator
from UnityPy.helpers.Tpk import get_typetree_node
ROOT=pathlib.Path(os.environ.get("NITW_GAME_DATA", str(pathlib.Path.home() / ".local/share/Steam/steamapps/common/Night in the Woods/Night in the Woods_Data")))
g=TypeTreeGenerator('5.6.2p4');g.load_local_dll_folder(str(ROOT/'Managed'))
out={'source':'Unity 5.6.2p4 static serialized scene data; TypeTreeGeneratorAPI AssetsTools backend parses DLL metadata, no target assembly execution','dll_sha256':hashlib.sha256((ROOT/'Managed/Assembly-CSharp.dll').read_bytes()).hexdigest(),'scenes':{}}
for scene in ('level0','level1'):
 e=UnityPy.load(str(ROOT/scene));e.typetree_generator=g;results=[];errors=[]
 for o in e.objects:
  if o.type.name!='MonoBehaviour':continue
  try:
   head=o.parse_monobehaviour_head();script=head.m_Script.read();cls=script.m_ClassName;ns=script.m_Namespace
   if not any(s in cls.lower() for s in ('option','camera','logo','font','sprite','scene','transition')):continue
   try:
    node_tree=g.get_nodes_up(script.m_AssemblyName,(ns+'.' if ns else '')+cls); node_tree.m_Children=list(get_typetree_node(o.type,o.version).m_Children)+list(node_tree.m_Children)[4:]; tree=o.read_typetree(nodes=node_tree); node={'id':o.path_id,'gameobject':head.m_GameObject.read().m_Name,'class':(ns+'.' if ns else '')+cls,'fields':tree}; results.append(node)
   except Exception as x:errors.append({'id':o.path_id,'class':cls,'error':str(x)})
  except Exception as x:errors.append({'id':o.path_id,'error':str(x)})
 out['scenes'][scene]={'source_sha256':hashlib.sha256((ROOT/scene).read_bytes()).hexdigest(),'components':results,'errors':errors}
p=(pathlib.Path(os.environ.get("NITW_WORKSPACE", str(pathlib.Path(__file__).resolve().parents[1]))) / 'research/options-serialized-fields.json');p.write_text(json.dumps(out,indent=2));print(p)
for scene,s in out['scenes'].items():
 print(scene,len(s['components']),'components',len(s['errors']),'errors')
 for c in s['components']:
  if c['class'] in ('OptionsMenu','OptionsMenuItem','LogosBrain','CameraControl','CameraData','SpriteText'):
   print(c['gameobject'],c['class'],json.dumps(c['fields'])[:2000])
