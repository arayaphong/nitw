"""Decode the fixed-width UnityScript Bloom payload using REA's field inventory."""
import os
import json, pathlib, struct, hashlib
import UnityPy
ROOT=pathlib.Path(os.environ.get("NITW_GAME_DATA", str(pathlib.Path.home() / ".local/share/Steam/steamapps/common/Night in the Woods/Night in the Woods_Data")))
WORK=pathlib.Path(os.environ.get("NITW_WORKSPACE", str(pathlib.Path(__file__).resolve().parents[1])))
e=UnityPy.load(str(ROOT/'level1')); obj=next(o for o in e.objects if o.path_id==856)
head=obj.parse_monobehaviour_head(); raw=obj.get_raw_data()
# Unity5.6 64-bit: GameObject PPtr12, enabled+padding4, script PPtr12, empty name4.
assert head.m_Name=='' and struct.unpack_from('<i',raw,28)[0]==0
layout=json.load(open(WORK/'research/menu-marker/bloom-field-layout.json'))
offset=32; fields={}; ranges=[]
for f in layout['fields']:
    if f['flags']&7!=6: continue
    kind=f['type']; start=offset
    if kind=='r4': value=struct.unpack_from('<f',raw,offset)[0];offset+=4
    elif kind=='i4' or kind.startswith('valuetype:') and kind!='valuetype:57':
        value=struct.unpack_from('<i',raw,offset)[0];offset+=4
    elif kind=='valuetype:57': value=dict(zip(('r','g','b','a'),struct.unpack_from('<4f',raw,offset)));offset+=16
    elif kind.startswith('class:'):
        file_id,path_id=struct.unpack_from('<iq',raw,offset);offset+=12
        value=dict(m_FileID=file_id,m_PathID=path_id)
    else: raise ValueError('Unhandled serialized type '+kind)
    fields[f['name']]=value;ranges.append(dict(name=f['name'],start=start,length=offset-start))
assert offset==len(raw), (offset,len(raw))
result=dict(scene='level1',componentId=856,enabled=head.m_Enabled,cameraHDR=True,
    fields=fields,byteRanges=ranges,byteLength=len(raw),decoder='public metadata field order; enums i32, floats f32, Color four f32, PPtr i32+i64; complete payload consumption verified',
    sourceSha256={n:hashlib.sha256((ROOT/n).read_bytes()).hexdigest() for n in ['level1','Managed/Assembly-UnityScript-firstpass.dll']})
(WORK/'research/menu-marker/murderfly-bloom-source.json').write_text(json.dumps(result,indent=2));print(json.dumps(fields,indent=2))
