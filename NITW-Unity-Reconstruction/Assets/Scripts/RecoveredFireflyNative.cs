using System;
using System.Runtime.InteropServices;
using UnityEngine;

// Minimal C ABI for the shipped FMOD 1.09.06 engine. Raw ELF copies are loaded
// explicitly, so Unity's plugin loader cannot preload a conflicting FMOD version.
public sealed class RecoveredFireflyNative : IDisposable {
    [StructLayout(LayoutKind.Sequential)] public struct Attributes {
        public Vector3 position,velocity,forward,up;
        public Attributes(Vector3 p) { position=p;velocity=Vector3.zero;forward=Vector3.forward;up=Vector3.up; }
    }
    [DllImport("libdl.so.2")] private static extern IntPtr dlopen(string path,int flags);
    [DllImport("libdl.so.2")] private static extern IntPtr dlsym(IntPtr library,string name);
    [DllImport("libdl.so.2")] private static extern IntPtr dlerror();
    [DllImport("libdl.so.2")] private static extern int dlclose(IntPtr library);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate int CreateFn(out IntPtr system,uint version);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate int PointerFn(IntPtr p,out IntPtr value);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate int PlainFn(IntPtr p);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate int IntFn(IntPtr p,int value);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate int ReadIntFn(IntPtr p,out int value);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate int ReadVersionFn(IntPtr p,out uint value);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate int SoftwareFn(IntPtr p,int rate,int speakers,int raw);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate int InitFn(IntPtr p,int channels,uint studioFlags,uint coreFlags,IntPtr extra);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate int BankFn(IntPtr p,[MarshalAs(UnmanagedType.LPStr)]string path,uint flags,out IntPtr bank);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate int LookupFn(IntPtr p,[MarshalAs(UnmanagedType.LPStr)]string path,out IntPtr result);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate int AttributesFn(IntPtr p,ref Attributes value);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate int ListenerFn(IntPtr p,int index,ref Attributes value);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate int VolumeFn(IntPtr p,float value);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate int ReadVolumeFn(IntPtr p,out float value,out float final);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate int DistanceFn(IntPtr p,out float value);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate int ParameterFn(IntPtr p,[MarshalAs(UnmanagedType.LPStr)]string name,float value);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate int ReadParameterFn(IntPtr p,[MarshalAs(UnmanagedType.LPStr)]string name,out float value,out float final);
    public CreateFn Create;
    public PointerFn GetCore,CreateInstance;
    public PlainFn UpdateSystem,ReleaseSystem,FlushCommands,FlushSamples,LoadSamples,StartEvent,ReleaseEvent;
    public IntFn SetOutput,StopEvent;
    public ReadIntFn PlaybackState,Is3D,IsOneshot,ParameterCount,GetOutput;
    public ReadVersionFn Version;
    public SoftwareFn Software;
    public InitFn Initialize;
    public BankFn LoadBank;
    public LookupFn GetEvent,GetBus;
    public AttributesFn EventAttributes,GetEventAttributes;
    public ListenerFn ListenerAttributes;
    public VolumeFn EventVolume,BusVolume;
    public ReadVolumeFn GetVolume;
    public DistanceFn MinDistance,MaxDistance;
    public ParameterFn SetParameter;
    public ReadParameterFn GetParameter;
    private IntPtr core,studio;
    private static T Bind<T>(IntPtr lib,string name) where T:Delegate {
        var ptr=dlsym(lib,name);if(ptr==IntPtr.Zero)throw new InvalidOperationException("Missing FMOD symbol: "+name);
        return Marshal.GetDelegateForFunctionPointer<T>(ptr);
    }
    public static void Check(string operation,int code) { if(code!=0)throw new InvalidOperationException(operation+" FMOD_RESULT="+code); }
    public RecoveredFireflyNative(string folder) {
        try {
            core=dlopen(System.IO.Path.Combine(folder,"core.bin"),2|256|8);
            if(core==IntPtr.Zero)throw new InvalidOperationException("FMOD core: "+Marshal.PtrToStringAnsi(dlerror()));
            studio=dlopen(System.IO.Path.Combine(folder,"studio.bin"),2|8);
            if(studio==IntPtr.Zero)throw new InvalidOperationException("FMOD studio: "+Marshal.PtrToStringAnsi(dlerror()));
            Create=Bind<CreateFn>(studio,"FMOD_Studio_System_Create");GetCore=Bind<PointerFn>(studio,"FMOD_Studio_System_GetLowLevelSystem");
            CreateInstance=Bind<PointerFn>(studio,"FMOD_Studio_EventDescription_CreateInstance");
            UpdateSystem=Bind<PlainFn>(studio,"FMOD_Studio_System_Update");ReleaseSystem=Bind<PlainFn>(studio,"FMOD_Studio_System_Release");
            FlushCommands=Bind<PlainFn>(studio,"FMOD_Studio_System_FlushCommands");FlushSamples=Bind<PlainFn>(studio,"FMOD_Studio_System_FlushSampleLoading");
            LoadSamples=Bind<PlainFn>(studio,"FMOD_Studio_EventDescription_LoadSampleData");StartEvent=Bind<PlainFn>(studio,"FMOD_Studio_EventInstance_Start");
            ReleaseEvent=Bind<PlainFn>(studio,"FMOD_Studio_EventInstance_Release");SetOutput=Bind<IntFn>(core,"FMOD_System_SetOutput");
            StopEvent=Bind<IntFn>(studio,"FMOD_Studio_EventInstance_Stop");PlaybackState=Bind<ReadIntFn>(studio,"FMOD_Studio_EventInstance_GetPlaybackState");
            Is3D=Bind<ReadIntFn>(studio,"FMOD_Studio_EventDescription_Is3D");IsOneshot=Bind<ReadIntFn>(studio,"FMOD_Studio_EventDescription_IsOneshot");
            ParameterCount=Bind<ReadIntFn>(studio,"FMOD_Studio_EventDescription_GetParameterCount");GetOutput=Bind<ReadIntFn>(core,"FMOD_System_GetOutput");
            Version=Bind<ReadVersionFn>(core,"FMOD_System_GetVersion");Software=Bind<SoftwareFn>(core,"FMOD_System_SetSoftwareFormat");
            Initialize=Bind<InitFn>(studio,"FMOD_Studio_System_Initialize");LoadBank=Bind<BankFn>(studio,"FMOD_Studio_System_LoadBankFile");
            GetEvent=Bind<LookupFn>(studio,"FMOD_Studio_System_GetEvent");GetBus=Bind<LookupFn>(studio,"FMOD_Studio_System_GetBus");
            EventAttributes=Bind<AttributesFn>(studio,"FMOD_Studio_EventInstance_Set3DAttributes");ListenerAttributes=Bind<ListenerFn>(studio,"FMOD_Studio_System_SetListenerAttributes");
            GetEventAttributes=Bind<AttributesFn>(studio,"FMOD_Studio_EventInstance_Get3DAttributes");
            EventVolume=Bind<VolumeFn>(studio,"FMOD_Studio_EventInstance_SetVolume");BusVolume=Bind<VolumeFn>(studio,"FMOD_Studio_Bus_SetVolume");
            GetVolume=Bind<ReadVolumeFn>(studio,"FMOD_Studio_EventInstance_GetVolume");MinDistance=Bind<DistanceFn>(studio,"FMOD_Studio_EventDescription_GetMinimumDistance");
            MaxDistance=Bind<DistanceFn>(studio,"FMOD_Studio_EventDescription_GetMaximumDistance");
            SetParameter=Bind<ParameterFn>(studio,"FMOD_Studio_EventInstance_SetParameterValue");
            GetParameter=Bind<ReadParameterFn>(studio,"FMOD_Studio_EventInstance_GetParameterValue");
        } catch { Dispose();throw; }
    }
    public void Dispose() {
        if(studio!=IntPtr.Zero) { dlclose(studio);studio=IntPtr.Zero; }
        if(core!=IntPtr.Zero) { dlclose(core);core=IntPtr.Zero; }
    }
}
