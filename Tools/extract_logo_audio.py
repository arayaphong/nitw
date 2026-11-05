#!/usr/bin/env python3
"""Render the original FMOD logos_appear event offline in a bounded child process.

Uses the original game's native FMOD 1.09.06 libraries and banks. No game process,
network access, audio hardware output, or target assembly execution is required.
Constants come from the shipped Assembly-CSharp-firstpass FMOD bindings.
"""
import os
import argparse, ctypes as C, hashlib, json, os, pathlib, resource, shutil, struct, subprocess, sys, tempfile, time
ROOT = pathlib.Path(os.environ.get("NITW_GAME_DATA", str(pathlib.Path.home() / ".local/share/Steam/steamapps/common/Night in the Woods/Night in the Woods_Data")))
PROJECT = pathlib.Path(os.environ.get("NITW_WORKSPACE", str(pathlib.Path(__file__).resolve().parents[1])))
EVENT = b'event:/ui/logos_appear'
VERSION = 67846  # FMOD.VERSION.number from shipped bindings: 0x00010906
WAVWRITER_NRT, STEREO = 5, 3
SYNCHRONOUS_UPDATE = 4
STREAM_FROM_UPDATE, MIX_FROM_UPDATE = 1, 2
STOPPED = 2

def sha(path):
    h=hashlib.sha256()
    with open(path,'rb') as f:
        for b in iter(lambda:f.read(1024*1024),b''): h.update(b)
    return h.hexdigest()

def worker(output, report):
    resource.setrlimit(resource.RLIMIT_CPU,(90,90))
    resource.setrlimit(resource.RLIMIT_AS,(4*1024**3,4*1024**3))
    libpath=pathlib.Path(os.environ.get('NITW_FMOD_LIBDIR',str(ROOT/'Plugins/x86_64')))
    core=C.CDLL(str(libpath/'libfmod.so'),mode=C.RTLD_GLOBAL)
    studio=C.CDLL(str(libpath/'libfmodstudio.so'))
    def fn(lib,name,args):
        f=getattr(lib,name);f.argtypes=args;f.restype=C.c_int;return f
    P=C.c_void_p; IP=C.POINTER(C.c_int); PP=C.POINTER(P)
    class Vector(C.Structure):_fields_=[('x',C.c_float),('y',C.c_float),('z',C.c_float)]
    class Attributes(C.Structure):_fields_=[('position',Vector),('velocity',Vector),('forward',Vector),('up',Vector)]
    attributes=Attributes(Vector(0,0,0),Vector(0,0,0),Vector(0,0,1),Vector(0,1,0))
    listener_attributes=Attributes(Vector(0,0,-1.5),Vector(0,0,0),Vector(0,0,1),Vector(0,1,0))
    setlistener=fn(studio,'FMOD_Studio_System_SetListenerAttributes',[P,C.c_int,C.POINTER(Attributes)])
    set3d=fn(studio,'FMOD_Studio_EventInstance_Set3DAttributes',[P,C.POINTER(Attributes)])
    create=fn(studio,'FMOD_Studio_System_Create',[PP,C.c_uint])
    lowlevel=fn(studio,'FMOD_Studio_System_GetLowLevelSystem',[P,PP])
    setoutput=fn(core,'FMOD_System_SetOutput',[P,C.c_int])
    setformat=fn(core,'FMOD_System_SetSoftwareFormat',[P,C.c_int,C.c_int,C.c_int])
    setbuffer=fn(core,'FMOD_System_SetDSPBufferSize',[P,C.c_uint,C.c_int])
    setstreambuffer=fn(core,'FMOD_System_SetStreamBufferSize',[P,C.c_uint,C.c_uint])
    getversion=fn(core,'FMOD_System_GetVersion',[P,C.POINTER(C.c_uint)])
    getmaster=fn(core,'FMOD_System_GetMasterChannelGroup',[P,PP])
    getclock=fn(core,'FMOD_ChannelGroup_GetDSPClock',[P,C.POINTER(C.c_ulonglong),C.POINTER(C.c_ulonglong)])
    init=fn(studio,'FMOD_Studio_System_Initialize',[P,C.c_int,C.c_uint,C.c_uint,P])
    loadbank=fn(studio,'FMOD_Studio_System_LoadBankFile',[P,C.c_char_p,C.c_uint,PP])
    getevent=fn(studio,'FMOD_Studio_System_GetEvent',[P,C.c_char_p,PP])
    getbus=fn(studio,'FMOD_Studio_System_GetBus',[P,C.c_char_p,PP])
    getbusvol=fn(studio,'FMOD_Studio_Bus_GetVolume',[P,C.POINTER(C.c_float),C.POINTER(C.c_float)])
    getbusmute=fn(studio,'FMOD_Studio_Bus_GetMute',[P,IP])
    getbuspaused=fn(studio,'FMOD_Studio_Bus_GetPaused',[P,IP])
    getbuscount=fn(studio,'FMOD_Studio_Bank_GetBusCount',[P,IP])
    getbuslist=fn(studio,'FMOD_Studio_Bank_GetBusList',[P,PP,C.c_int,IP])
    getbuspath=fn(studio,'FMOD_Studio_Bus_GetPath',[P,C.c_char_p,C.c_int,IP])
    lengthfn=fn(studio,'FMOD_Studio_EventDescription_GetLength',[P,IP])
    oneshotfn=fn(studio,'FMOD_Studio_EventDescription_IsOneshot',[P,IP])
    is3dfn=fn(studio,'FMOD_Studio_EventDescription_Is3D',[P,IP])
    isstreamfn=fn(studio,'FMOD_Studio_EventDescription_IsStream',[P,IP])
    paramsfn=fn(studio,'FMOD_Studio_EventDescription_GetParameterCount',[P,IP])
    minfn=fn(studio,'FMOD_Studio_EventDescription_GetMinimumDistance',[P,C.POINTER(C.c_float)])
    maxfn=fn(studio,'FMOD_Studio_EventDescription_GetMaximumDistance',[P,C.POINTER(C.c_float)])
    loadsamples=fn(studio,'FMOD_Studio_EventDescription_LoadSampleData',[P])
    flushsamples=fn(studio,'FMOD_Studio_System_FlushSampleLoading',[P])
    flush=fn(studio,'FMOD_Studio_System_FlushCommands',[P])
    instancefn=fn(studio,'FMOD_Studio_EventDescription_CreateInstance',[P,PP])
    start=fn(studio,'FMOD_Studio_EventInstance_Start',[P])
    update=fn(studio,'FMOD_Studio_System_Update',[P])
    statefn=fn(studio,'FMOD_Studio_EventInstance_GetPlaybackState',[P,IP])
    posfn=fn(studio,'FMOD_Studio_EventInstance_GetTimelinePosition',[P,IP])
    release=fn(studio,'FMOD_Studio_System_Release',[P])
    def check(name,result):
        if result:raise RuntimeError(f'{name} FMOD_RESULT={result}')
    system=P(); check('create',create(C.byref(system),VERSION)); core_system=P()
    data={}
    try:
        check('getlowlevel',lowlevel(system,C.byref(core_system)))
        version=C.c_uint();check('getversion',getversion(core_system,C.byref(version)))
        check('setoutput',setoutput(core_system,WAVWRITER_NRT))
        check('setformat',setformat(core_system,48000,STEREO,0))
        check('setbuffer',setbuffer(core_system,1024,4))
        check('setstreambuffer',setstreambuffer(core_system,16*1024*1024,8))
        filename=C.create_string_buffer(os.fsencode(output))
        check('initialize',init(system,64,SYNCHRONOUS_UPDATE,0,C.cast(filename,P)))
        banks=[]
        for name in ['Master.strings.bank','Master.bank']:
            bank=P();check('load '+name,loadbank(system,os.fsencode(ROOT/'StreamingAssets'/name),0,C.byref(bank)));banks.append(bank)
        desc=P();check('getevent',getevent(system,EVENT,C.byref(desc)))
        for name,f in [('timeline_length_ms',lengthfn),('is_oneshot',oneshotfn),('is_3d',is3dfn),('is_stream',isstreamfn),('parameter_count',paramsfn)]:
            v=C.c_int();check(name,f(desc,C.byref(v)));data[name]=v.value
        for name,f in [('minimum_distance',minfn),('maximum_distance',maxfn)]:
            v=C.c_float();check(name,f(desc,C.byref(v)));data[name]=v.value
        data['source_attributes']={'position':[0,0,0],'forward':[0,0,1],'up':[0,1,0]}
        data['listener_attributes']={'position':[0,0,-1.5],'forward':[0,0,1],'up':[0,1,0]}
        check('setlistener',setlistener(system,0,C.byref(listener_attributes)))
        check('load event samples',loadsamples(desc));check('flush samples',flushsamples(system))
        instance=P();check('createinstance',instancefn(desc,C.byref(instance)))
        check('set3d',set3d(instance,C.byref(attributes)))
        master=P();check('getmaster',getmaster(core_system,C.byref(master)))
        preclock=C.c_ulonglong();parentclock=C.c_ulonglong();check('preclock',getclock(master,C.byref(preclock),C.byref(parentclock)));data['pre_start_dsp_clock']=preclock.value
        check('start',start(instance));check('flush',flush(system))
        bcount=C.c_int();check('buscount',getbuscount(banks[-1],C.byref(bcount)));blist=(P*bcount.value)();actual=C.c_int();check('buslist',getbuslist(banks[-1],blist,bcount.value,C.byref(actual)));bd=[]
        for b in blist:
            path=C.create_string_buffer(512);length=C.c_int();v=C.c_float();final=C.c_float();mute=C.c_int();paused=C.c_int()
            check('buspath',getbuspath(b,path,512,C.byref(length)));check('busvol',getbusvol(b,C.byref(v),C.byref(final)));check('busmute',getbusmute(b,C.byref(mute)));check('buspaused',getbuspaused(b,C.byref(paused)));bd.append(dict(path=path.value.decode(),volume=v.value,final_volume=final.value,mute=mute.value,paused=paused.value))
        data['buses']=bd
        states=[];maxpos=0;updates=0;seen_active=False;early_positions=[]
        for i in range(10000):
            check('update',update(system));updates+=1;time.sleep(1024/48000)
            state=C.c_int();pos=C.c_int();check('playbackstate',statefn(instance,C.byref(state)));check('timelinepos',posfn(instance,C.byref(pos)))
            if not states or states[-1]['state']!=state.value:states.append({'update':updates,'state':state.value,'timeline_ms':pos.value})
            maxpos=max(maxpos,pos.value)
            if updates<25:
                clock=C.c_ulonglong();check('clock',getclock(master,C.byref(clock),C.byref(parentclock)));early_positions.append(dict(update=updates,timeline_ms=pos.value,dsp_clock=clock.value))
            if state.value!=STOPPED:seen_active=True
            if seen_active and state.value==STOPPED:break
        else: raise RuntimeError(f'event did not stop in {updates} updates; maxpos={maxpos}; states={states}')
        # Flush mixer latency and DSP release tails after the event has stopped.
        for _ in range(24):check('tail update',update(system))
        endclock=C.c_ulonglong();check('endclock',getclock(master,C.byref(endclock),C.byref(parentclock)));data['end_dsp_clock']=endclock.value
        data['early_positions']=early_positions
        data.update(native_version_hex=hex(version.value),updates=updates,states=states,max_timeline_ms=maxpos,tail_updates=24,sample_rate=48000,dsp_buffer_samples=1024)
    finally:check('release',release(system))
    pathlib.Path(report).write_text(json.dumps(data,indent=2))

def main():
    ap=argparse.ArgumentParser();ap.add_argument('--worker',action='store_true');ap.add_argument('--output');ap.add_argument('--report');args=ap.parse_args()
    if args.worker:return worker(args.output,args.report)
    out=PROJECT/'NITW-Unity-Reconstruction/Assets/Resources/RecoveredIntro/LogosAppear.wav'
    out.parent.mkdir(parents=True,exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='nitw-logo-audio-') as temp:
        temp=pathlib.Path(temp);raw=temp/'raw.wav';meta=temp/'native.json'
        # Modern glibc refuses legacy libfmod's executable-stack requirement.
        # Clear only PF_X on a disposable library copy; original is never written.
        libs=temp/'libs';libs.mkdir()
        for name in ['libfmod.so','libfmodstudio.so']:shutil.copy2(ROOT/'Plugins/x86_64'/name,libs/name)
        patched=libs/'libfmod.so';image=bytearray(patched.read_bytes());patches=[]
        if image[:6]!=b'\x7fELF\x02\x01':raise RuntimeError('Expected ELF64 little-endian core library')
        phoff=struct.unpack_from('<Q',image,32)[0];phsize,phnum=struct.unpack_from('<HH',image,54)
        for i in range(phnum):
            offset=phoff+i*phsize;typ,flags=struct.unpack_from('<II',image,offset)
            if typ==0x6474e551 and flags&1:
                struct.pack_into('<I',image,offset+4,flags&~1);patches.append({'offset':offset+4,'old_flags':flags,'new_flags':flags&~1,'reason':'Clear legacy executable-stack requirement on temporary copy for glibc compatibility'})
        patched.write_bytes(image)
        env=os.environ.copy();env['LD_LIBRARY_PATH']=str(libs);env['NITW_FMOD_LIBDIR']=str(libs)
        result=subprocess.run([sys.executable,__file__,'--worker','--output',str(raw),'--report',str(meta)],env=env,timeout=120,capture_output=True,text=True)
        if result.returncode:raise RuntimeError(f'worker exited {result.returncode}: {result.stdout[-2000:]} {result.stderr[-3000:]}')
        data=json.loads(meta.read_text());print(json.dumps(data,indent=2),flush=True)
        # Remove only known rendering setup before the event-start API call.
        # Do not strip authored silence or the event's scheduling latency.
        subprocess.run(['ffmpeg','-v','error','-y','-i',str(raw),'-af','atrim=start_sample='+str(data['pre_start_dsp_clock'])+',asetpts=PTS-STARTPTS','-c:a','pcm_s16le',str(out)],check=True,timeout=30)
        import wave, array
        with wave.open(str(out),'rb') as wav:
            channels=wav.getnchannels();rate=wav.getframerate();samples=array.array('h',wav.readframes(wav.getnframes()))
        nonzero=[i//channels for i,v in enumerate(samples) if abs(v)>1]
        if not nonzero:out.unlink();raise RuntimeError('Rendered event has no audible PCM samples')
        data['waveform']={'channels':channels,'sample_rate':rate,'frames':len(samples)//channels,'seconds':len(samples)/channels/rate,'peak_pcm_s16':max(abs(v)for v in samples),'first_above_1_lsb_frame':min(nonzero),'last_above_1_lsb_frame':max(nonzero),'removed_setup_frames':data['pre_start_dsp_clock'],'removed_setup_seconds':data['pre_start_dsp_clock']/rate,'remaining_leading_seconds':min(nonzero)/rate,'last_audible_seconds':max(nonzero)/rate}
        data['evidence_ids']={'fmod_enum_bindings':'ev_80c28b2892a9685c8b97387ef17a722173321b42c7da247f92202c4e562d53ec','source_runtime_manager':'ev_5e6cf324fc487b4591999a9934b08fc15c40e88b2f608c07b1b2532422046994','source_sfx':'ev_92406056346c9b39c57d4dd715721b0b986e97ed3a858c971ceada4d96a90789'}
        data['alignment_note']='Only measured core DSP startup clock before EventInstance_Start is trimmed. Remaining leading silence/scheduling latency and DSP tails are preserved. Exact hardware-backend latency of the original game is not established.'
        data['limitations']=['Offline source FMOD render; not a recording of the original game process.','Source/Listener position derived from level0 and CameraControl.LateUpdate. Additional original runtime mixer changes were not replayed.','No parameter modulation; event reports zero parameters.','Source libfmod temporary copy requires only GNU_STACK executable flag clearing for modern glibc.']
        probe=json.loads(subprocess.check_output(['ffprobe','-v','error','-show_format','-show_streams','-of','json',str(out)],text=True))
        data['native_constants_provenance']='Constant fields parsed from original bindings without target execution: FMOD.VERSION.number=67846, WAVWRITER_NRT=5, STEREO=3, Studio SYNCHRONOUS_UPDATE=4, TIMEUNIT RAWBYTES=8.'
        data.update(temporary_library_patches=patches,temporary_core_sha256=sha(patched),event=EVENT.decode(),method='Offline render with source FMOD WAVWRITER_NRT',output=str(out),output_sha256=sha(out),probe=probe,bindings_evidence_id='ev_80c28b2892a9685c8b97387ef17a722173321b42c7da247f92202c4e562d53ec',constants={'version':VERSION,'WAVWRITER_NRT':WAVWRITER_NRT,'STEREO':STEREO,'SYNCHRONOUS_UPDATE':SYNCHRONOUS_UPDATE,'STREAM_FROM_UPDATE':STREAM_FROM_UPDATE,'MIX_FROM_UPDATE':MIX_FROM_UPDATE,'actual_core_init_flags':0,'stream_buffer_bytes':16*1024*1024,'TIMEUNIT_RAWBYTES':8,'PLAYBACK_STOPPED':STOPPED},sources=[])
        for p in [ROOT/'Plugins/x86_64/libfmod.so',ROOT/'Plugins/x86_64/libfmodstudio.so',ROOT/'StreamingAssets/Master.bank',ROOT/'StreamingAssets/Master.strings.bank',ROOT/'Managed/Assembly-CSharp-firstpass.dll']:
            data['sources'].append({'path':str(p),'bytes':p.stat().st_size,'sha256':sha(p)})
        report=PROJECT/'research/intro-audio-analysis.json';report.write_text(json.dumps(data,indent=2));print('Wrote',out,report)
if __name__=='__main__':main()
