using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
public static class RecoveredStationAnimationImporter {
    const string Folder="Assets/Resources/RecoveredStationAnimation/";
    const string Generated="Assets/RecoveredStationAnimation/";
    [Serializable] public class Source {public State[] states;public Clip[] clips;public SpriteData[] sprites;public int eyeCenter,pivot;public EyeData[] eyes;public TargetData[] lookTargets;}
    [Serializable] public class EyeData {public int node;public Vector2 offset;public float dilation,lookSpeed,radius;public Vector3 shape;}
    [Serializable] public class TargetData {public int node,pivot,tracker;public Vector3 headPosition,detectOffset;public float range;public bool despiteFacing;}
    [Serializable] public class State {public string name;public int clip;public float speed;public bool loop;public Transition[] transitions;}
    [Serializable] public class Transition {public string destination;public float duration,offset,exitTime;public bool hasExitTime,fixedDuration,canTransitionToSelf;public Condition[] conditions;}
    [Serializable] public class Condition {public string parameter;public int mode;public float threshold;}
    [Serializable] public class SpriteData {public int id;public string name;public float ppu;public Vector2 pivot;}
    [Serializable] public class Clip {public int id;public string name;public float length,sampleRate;public bool loop;public FloatCurve[] floatCurves;public ObjectCurve[] objectCurves;public Event[] events;}
    [Serializable] public class FloatCurve {public string path,property,component;public bool stepped;public Key[] keys;}
    [Serializable] public class Key {public float time,value,inTangent,outTangent;public float[] coefficients;}
    [Serializable] public class ObjectCurve {public string path,property;public ObjectKey[] keys;}
    [Serializable] public class ObjectKey {public float time;public int sprite;}
    [Serializable] public class Event {public float time;public string function;}
    public static void VerifyStandalone() {
        var scene=JsonUtility.FromJson<RecoveredStationData>(File.ReadAllText("Assets/Resources/RecoveredStation/Scene.json"));
        var nodes=new Dictionary<int,GameObject>();foreach(var d in scene.nodes)nodes[d.id]=new GameObject(d.name);
        foreach(var d in scene.nodes){var go=nodes[d.id];if(d.parent!=0)go.transform.SetParent(nodes[d.parent].transform,false);d.transform.Apply(go.transform);if(d.sprite!=null&&!string.IsNullOrEmpty(d.sprite.material))go.AddComponent<SpriteRenderer>();if(d.meshRenderer!=null&&!string.IsNullOrEmpty(d.mesh))go.AddComponent<MeshRenderer>();go.SetActive(d.active);}
        var bridge=Attach(nodes);var source=JsonUtility.FromJson<Source>(File.ReadAllText(Folder+"Animation.json"));
        foreach(var d in source.clips) {
            var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(Generated+d.id+".anim");
            foreach(var f in d.floatCurves) {
                Type type=f.component=="Transform"?typeof(Transform):f.component=="GameObject"?typeof(GameObject):f.component=="SpriteRenderer"?typeof(SpriteRenderer):typeof(RecoveredStationExistsVariable);
                var curve=AnimationUtility.GetEditorCurve(clip,EditorCurveBinding.FloatCurve(f.path,type,f.property));
                for(int i=0;i<f.keys.Length;i++) {
                    var key=f.keys[i];if(Mathf.Abs(curve.keys[i].time-key.time)>1e-6f||Mathf.Abs(curve.keys[i].value-key.value)>1e-6f)throw new Exception("Mae float key mismatch "+d.name);
                    if(f.stepped)continue;
                    for(int j=1;i+1<f.keys.Length&&j<4;j++){
                        float time=Mathf.Lerp(key.time,f.keys[i+1].time,j/4f);float dt=time-key.time;var c=key.coefficients;
                        float expected=((c[0]*dt+c[1])*dt+c[2])*dt+c[3];
                        if(Mathf.Abs(curve.Evaluate(time)-expected)>.0002f)throw new Exception("Mae source cubic mismatch "+d.name+" "+f.path+" "+f.property);
                    }
                }
            }
            foreach(float ratio in new[]{0f,.17f,.51f,.91f}) {
                float time=d.length*ratio;clip.SampleAnimation(nodes[52],time);
                foreach(var c in d.objectCurves) {
                    var k=c.keys[0];foreach(var candidate in c.keys)if(candidate.time<=time+.00001f)k=candidate;
                    var renderer=nodes[52].transform.Find(c.path).GetComponent<SpriteRenderer>();
                    string expected=Folder+k.sprite+".png";if(AssetDatabase.GetAssetPath(renderer.sprite)!=expected)throw new Exception("Mae sprite sample "+d.name+" @"+time+" "+c.path);
                }
            }
        }
        foreach(var d in scene.nodes)if(d.parent==0)UnityEngine.Object.DestroyImmediate(nodes[d.id]);
        Debug.Log("NITW_STATION_ANIMATION_SAMPLES_VERIFIED 32 clips x4 times; every sprite curve and source cubic checked");
    }
    public static RecoveredStationAnimation Attach(Dictionary<int,GameObject> nodes) {
        Directory.CreateDirectory(Generated);AssetDatabase.Refresh();
        var source=JsonUtility.FromJson<Source>(File.ReadAllText(Folder+"Animation.json"));
        var sprites=new Dictionary<int,Sprite>();
        foreach(var d in source.sprites) {
            string p=Folder+d.id+".png";var importer=(TextureImporter)AssetImporter.GetAtPath(p);
            bool changed=importer.userData!="RecoveredStationAnimation:"+d.id;
            if(changed) {
                importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
                importer.spritePixelsPerUnit=d.ppu;var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);
                settings.spriteAlignment=(int)SpriteAlignment.Custom;settings.spritePivot=d.pivot;settings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(settings);
                importer.textureCompression=TextureImporterCompression.Uncompressed;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;
                importer.npotScale=TextureImporterNPOTScale.None;importer.maxTextureSize=4096;importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Bilinear;
                importer.userData="RecoveredStationAnimation:"+d.id;importer.SaveAndReimport();
            }
            sprites[d.id]=AssetDatabase.LoadAssetAtPath<Sprite>(p);
            if(sprites[d.id]==null)throw new Exception("Missing Mae sprite "+d.id);
        }
        var clips=new Dictionary<int,AnimationClip>();int curveCount=0;
        foreach(var d in source.clips) {
            string path=Generated+d.id+".anim";var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if(clip==null){clip=new AnimationClip();AssetDatabase.CreateAsset(clip,path);}
            clip.ClearCurves();clip.name=d.name;clip.frameRate=d.sampleRate;
            foreach(var f in d.floatCurves) {
                var keys=new Keyframe[f.keys.Length];
                for(int i=0;i<keys.Length;i++){var k=f.keys[i];keys[i]=new Keyframe(k.time,k.value,f.stepped?float.PositiveInfinity:k.inTangent,f.stepped?float.PositiveInfinity:k.outTangent);}
                var curve=new AnimationCurve(keys);
                for(int i=0;i<keys.Length;i++) {
                    AnimationUtility.SetKeyBroken(curve,i,true);
                    AnimationUtility.SetKeyLeftTangentMode(curve,i,f.stepped?AnimationUtility.TangentMode.Constant:AnimationUtility.TangentMode.Free);
                    AnimationUtility.SetKeyRightTangentMode(curve,i,f.stepped?AnimationUtility.TangentMode.Constant:AnimationUtility.TangentMode.Free);
                }
                Type type=f.component=="Transform"?typeof(Transform):f.component=="GameObject"?typeof(GameObject):f.component=="SpriteRenderer"?typeof(SpriteRenderer):typeof(RecoveredStationExistsVariable);
                AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(f.path,type,f.property),curve);curveCount++;
            }
            foreach(var f in d.objectCurves) {
                var keys=new ObjectReferenceKeyframe[f.keys.Length];for(int i=0;i<keys.Length;i++)keys[i]=new ObjectReferenceKeyframe{time=f.keys[i].time,value=sprites[f.keys[i].sprite]};
                AnimationUtility.SetObjectReferenceCurve(clip,EditorCurveBinding.PPtrCurve(f.path,typeof(SpriteRenderer),f.property),keys);curveCount++;
            }
            var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=d.loop;settings.startTime=0;settings.stopTime=d.length;AnimationUtility.SetAnimationClipSettings(clip,settings);
            var events=new AnimationEvent[d.events.Length];for(int i=0;i<events.Length;i++)events[i]=new AnimationEvent{time=d.events[i].time,functionName=d.events[i].function,messageOptions=SendMessageOptions.RequireReceiver};AnimationUtility.SetAnimationEvents(clip,events);
            EditorUtility.SetDirty(clip);clips[d.id]=clip;
            if(Mathf.Abs(clip.length-d.length)>.0001f)throw new Exception("Mae duration mismatch "+d.name);
            if(AnimationUtility.GetCurveBindings(clip).Length!=d.floatCurves.Length || AnimationUtility.GetObjectReferenceCurveBindings(clip).Length!=d.objectCurves.Length)throw new Exception("Mae curve mismatch "+d.name);
        }
        string controllerPath=Generated+"MaeRig.controller";AssetDatabase.DeleteAsset(controllerPath);var controller=AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        controller.AddParameter("walking",AnimatorControllerParameterType.Bool);
        var machine=controller.layers[0].stateMachine;var states=new Dictionary<string,AnimatorState>();
        foreach(var d in source.states) {var state=machine.AddState(d.name);state.motion=clips[d.clip];state.speed=d.speed;state.writeDefaultValues=true;states[d.name]=state;}
        machine.defaultState=states["MaeIdleLeft"];
        foreach(var d in source.states)foreach(var t in d.transitions) {
            var transition=states[d.name].AddTransition(states[t.destination]);transition.duration=t.duration;transition.offset=t.offset;
            transition.exitTime=t.exitTime;transition.hasExitTime=t.hasExitTime;transition.hasFixedDuration=t.fixedDuration;transition.canTransitionToSelf=t.canTransitionToSelf;
            foreach(var condition in t.conditions)transition.AddCondition((AnimatorConditionMode)condition.mode,condition.threshold,condition.parameter);
        }
        // Native secondary layers Hair (default loop) and neutral Eyes/Blink.
        // Other expression states remain outside this walking milestone.
        var hairMachine=new AnimatorStateMachine{name="Hair"};AssetDatabase.AddObjectToAsset(hairMachine,controller);
        var hair=hairMachine.AddState("MaeHairLoop");hair.motion=clips[6144];hair.speed=1;hair.writeDefaultValues=true;hairMachine.defaultState=hair;
        controller.AddLayer(new AnimatorControllerLayer{name="Hair",defaultWeight=1,stateMachine=hairMachine,blendingMode=AnimatorLayerBlendingMode.Override});
        var eyesMachine=new AnimatorStateMachine{name="Eyes"};AssetDatabase.AddObjectToAsset(eyesMachine,controller);
        var neutral=eyesMachine.AddState("MaeNothing");neutral.writeDefaultValues=true;
        var blink=eyesMachine.AddState("MaeBlink");blink.motion=clips[6126];blink.speed=1;blink.writeDefaultValues=true;eyesMachine.defaultState=neutral;
        controller.AddParameter("Blink",AnimatorControllerParameterType.Trigger);
        var blinkIn=neutral.AddTransition(blink);blinkIn.duration=0;blinkIn.hasExitTime=false;blinkIn.canTransitionToSelf=false;blinkIn.AddCondition(AnimatorConditionMode.If,0,"Blink");
        var blinkOut=blink.AddTransition(neutral);blinkOut.duration=0;blinkOut.hasExitTime=true;blinkOut.exitTime=1;blinkOut.hasFixedDuration=false;blinkOut.canTransitionToSelf=false;
        controller.AddLayer(new AnimatorControllerLayer{name="Eyes",defaultWeight=1,stateMachine=eyesMachine,blendingMode=AnimatorLayerBlendingMode.Override});
        var root=nodes[52];var animator=root.AddComponent<Animator>();animator.runtimeAnimatorController=controller;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
        var bridge=root.AddComponent<RecoveredStationAnimation>();bridge.animator=animator;
        var gaze=root.AddComponent<RecoveredStationGaze>();gaze.eyeCenter=nodes[source.eyeCenter].transform;gaze.pivot=nodes[source.pivot].transform;
        gaze.eyes=new RecoveredStationGaze.Eye[source.eyes.Length];for(int i=0;i<gaze.eyes.Length;i++){var d=source.eyes[i];gaze.eyes[i]=new RecoveredStationGaze.Eye{transform=nodes[d.node].transform,renderer=nodes[d.node].GetComponent<Renderer>(),offset=d.offset,dilation=d.dilation,lookSpeed=d.lookSpeed,radius=d.radius,shape=d.shape};}
        gaze.targets=new RecoveredStationGaze.Target[source.lookTargets.Length];for(int i=0;i<gaze.targets.Length;i++){var d=source.lookTargets[i];gaze.targets[i]=new RecoveredStationGaze.Target{transform=nodes[d.node].transform,pivot=d.pivot!=0?nodes[d.pivot].transform:null,tracker=d.tracker!=0?nodes[d.tracker].transform:null,headPosition=d.headPosition,detectOffset=d.detectOffset,range=d.range,despiteFacing=d.despiteFacing};}
        bridge.gaze=gaze;
        root.transform.Find("Rig/Body/BodyBase/ArmFront").gameObject.AddComponent<RecoveredStationExistsVariable>();
        // costume=0 keeps these source ExistsVariable objects absent.
        foreach(var t in root.GetComponentsInChildren<Transform>(true))if(t.name.Contains("Harfest")||t.name.Contains("harfest")) {
            var r=t.GetComponent<Renderer>();if(r!=null)r.enabled=false;
        }
        AssetDatabase.SaveAssets();Debug.Log("NITW_STATION_ANIMATION_VERIFIED states="+states.Count+" clips="+clips.Count+" sprites="+sprites.Count+" curves="+curveCount);
        return bridge;
    }
}
