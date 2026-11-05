"""Prepare source FMOD native libraries and local bank paths; never alter the game."""
import os
import ctypes as C, hashlib, json, pathlib, resource, struct
ROOT=pathlib.Path(os.environ.get("NITW_GAME_DATA", str(pathlib.Path.home() / ".local/share/Steam/steamapps/common/Night in the Woods/Night in the Woods_Data")))
WORK=pathlib.Path(os.environ.get("NITW_WORKSPACE", str(pathlib.Path(__file__).resolve().parents[1])))
OUT=WORK/'NITW-Unity-Reconstruction/Assets/StreamingAssets/RecoveredFMOD'
OUT.mkdir(parents=True,exist_ok=True)
resource.setrlimit(resource.RLIMIT_CPU,(60,60));resource.setrlimit(resource.RLIMIT_AS,(4*1024**3,4*1024**3))
def sha(p): return hashlib.file_digest(open(p,'rb'),'sha256').hexdigest()
sources=[]
for name,dest in [('libfmod.so','core.bin'),('libfmodstudio.so','studio.bin')]:
    p=ROOT/'Plugins/x86_64'/name;data=bytearray(p.read_bytes());patches=[]
    assert data[:6]==b'\x7fELF\x02\x01'
    off=struct.unpack_from('<Q',data,32)[0];size,count=struct.unpack_from('<HH',data,54)
    for i in range(count):
        at=off+i*size;typ,flags=struct.unpack_from('<II',data,at)
        if typ==0x6474e551 and flags&1:
            struct.pack_into('<I',data,at+4,flags&~1);patches.append({'offset':at+4,'from':flags,'to':flags&~1})
    target=OUT/dest;target.write_bytes(data)
    sources.append(dict(path=str(p),sha256=sha(p),output=str(target),output_sha256=sha(target),gnu_stack_patches=patches))
config=dict(bankDirectory=str(ROOT/'StreamingAssets'),eventPath='event:/title/firefly',nativeVersion=67846)
(WORK/'NITW-Unity-Reconstruction/Assets/Resources/RecoveredTitle/Audio.json').write_text(json.dumps(config,indent=2))
core=C.CDLL(str(OUT/'core.bin'),mode=2|256|8);studio=C.CDLL(str(OUT/'studio.bin'),mode=2|8)
P=C.c_void_p;PP=C.POINTER(P);IP=C.POINTER(C.c_int)
def fn(lib,name,args):
    f=getattr(lib,name);f.argtypes=args;f.restype=C.c_int;return f
def check(code):
    if code: raise RuntimeError('FMOD_RESULT='+str(code))
create=fn(studio,'FMOD_Studio_System_Create',[PP,C.c_uint]);getcore=fn(studio,'FMOD_Studio_System_GetLowLevelSystem',[P,PP]);setout=fn(core,'FMOD_System_SetOutput',[P,C.c_int]);init=fn(studio,'FMOD_Studio_System_Initialize',[P,C.c_int,C.c_uint,C.c_uint,P]);load=fn(studio,'FMOD_Studio_System_LoadBankFile',[P,C.c_char_p,C.c_uint,PP]);event=fn(studio,'FMOD_Studio_System_GetEvent',[P,C.c_char_p,PP]);release=fn(studio,'FMOD_Studio_System_Release',[P])
system=P();check(create(C.byref(system),67846));metadata={}
try:
    low=P();check(getcore(system,C.byref(low)));check(setout(low,2));check(init(system,128,4,0,None))
    for name in ['Master.strings.bank','Master.bank']:
        p=ROOT/'StreamingAssets'/name;b=P();check(load(system,str(p).encode(),0,C.byref(b)));sources.append(dict(path=str(p),bytes=p.stat().st_size,sha256=sha(p)))
    desc=P();check(event(system,config['eventPath'].encode(),C.byref(desc)))
    for name,symbol in [('is3D','Is3D'),('isOneshot','IsOneshot'),('isStream','IsStream'),('parameterCount','GetParameterCount'),('timelineMilliseconds','GetLength')]:
        v=C.c_int();check(fn(studio,'FMOD_Studio_EventDescription_'+symbol,[P,IP])(desc,C.byref(v)));metadata[name]=v.value
    for name,symbol in [('minimumDistance','GetMinimumDistance'),('maximumDistance','GetMaximumDistance')]:
        v=C.c_float();check(fn(studio,'FMOD_Studio_EventDescription_'+symbol,[P,C.POINTER(C.c_float)])(desc,C.byref(v)));metadata[name]=v.value
finally: check(release(system))
report=dict(config=config,event=metadata,sources=sources,notes=['Source banks are read in place; no 1GB copy is added to the build.','ELF files copied with .bin extension to prevent Unity auto-loading native FMOD during startup.','Only executable GNU_STACK flags on copies are cleared for modern glibc compatibility.'])
(WORK/'research/menu-marker/firefly-audio-source.json').write_text(json.dumps(report,indent=2));print(json.dumps(metadata,indent=2))
