using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Restores only the native controller path reached by EnterLevel drilling=1.
public static class RecoveredStationJanitorImporter {
    const string Folder="Assets/Resources/RecoveredStationJanitor/";
    const string Generated="Assets/RecoveredStationJanitor/";
    [Serializable] private sealed class Source {
        public RecoveredStationAnimationImporter.State[] states;
        public RecoveredStationAnimationImporter.Clip[] clips;
        public RecoveredStationAnimationImporter.SpriteData[] sprites;
        public string drillEvent;public int drillEmitter,clothEmitter;public float drillFade;
    }
    public static RecoveredStationJanitor Attach(Dictionary<int,GameObject> nodes,RecoveredStation station) {
        Directory.CreateDirectory(Generated);AssetDatabase.Refresh();
        var source=JsonUtility.FromJson<Source>(File.ReadAllText(Folder+"Animation.json"));
        var sprites=new Dictionary<int,Sprite>();
        foreach(var d in source.sprites) {
            string path=Folder+d.id+".png";var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            importer.spritePixelsPerUnit=d.ppu;
            var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);
            settings.spriteAlignment=(int)SpriteAlignment.Custom;settings.spritePivot=d.pivot;
            settings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(settings);
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.alphaIsTransparency=true;
            importer.mipmapEnabled=false;importer.npotScale=TextureImporterNPOTScale.None;
            importer.maxTextureSize=4096;importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Bilinear;
            importer.SaveAndReimport();
            var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);sprites[d.id]=sprite;
            if(sprite==null||Vector2.Distance(new Vector2(sprite.pivot.x/sprite.rect.width,sprite.pivot.y/sprite.rect.height),d.pivot)>.00001f)
                throw new Exception("Janitor loaded pivot mismatch "+d.id);
        }
        var clips=new Dictionary<int,AnimationClip>();int curveCount=0;
        foreach(var d in source.clips) {
            string path=Generated+d.id+".anim";var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if(clip==null){clip=new AnimationClip();AssetDatabase.CreateAsset(clip,path);}
            clip.ClearCurves();clip.name=d.name;clip.frameRate=d.sampleRate;
            foreach(var f in d.floatCurves) {
                var keys=new Keyframe[f.keys.Length];
                for(int i=0;i<keys.Length;i++){
                    var k=f.keys[i];keys[i]=new Keyframe(k.time,k.value,f.stepped?float.PositiveInfinity:k.inTangent,f.stepped?float.PositiveInfinity:k.outTangent);
                }
                var curve=new AnimationCurve(keys);
                for(int i=0;i<keys.Length;i++) {
                    AnimationUtility.SetKeyBroken(curve,i,true);
                    AnimationUtility.SetKeyLeftTangentMode(curve,i,f.stepped?AnimationUtility.TangentMode.Constant:AnimationUtility.TangentMode.Free);
                    AnimationUtility.SetKeyRightTangentMode(curve,i,f.stepped?AnimationUtility.TangentMode.Constant:AnimationUtility.TangentMode.Free);
                }
                Type type=f.component=="Transform"?typeof(Transform):f.component=="GameObject"?typeof(GameObject):typeof(SpriteRenderer);
                AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(f.path,type,f.property),curve);curveCount++;
            }
            foreach(var f in d.objectCurves) {
                var keys=new ObjectReferenceKeyframe[f.keys.Length];
                for(int i=0;i<keys.Length;i++)keys[i]=new ObjectReferenceKeyframe{time=f.keys[i].time,value=sprites[f.keys[i].sprite]};
                AnimationUtility.SetObjectReferenceCurve(clip,EditorCurveBinding.PPtrCurve(f.path,typeof(SpriteRenderer),f.property),keys);curveCount++;
            }
            var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=d.loop;
            settings.startTime=0;settings.stopTime=d.length;AnimationUtility.SetAnimationClipSettings(clip,settings);
            var events=new AnimationEvent[d.events.Length];
            for(int i=0;i<events.Length;i++)events[i]=new AnimationEvent{time=d.events[i].time,functionName=d.events[i].function,messageOptions=SendMessageOptions.RequireReceiver};
            AnimationUtility.SetAnimationEvents(clip,events);EditorUtility.SetDirty(clip);clips[d.id]=clip;
            if(Mathf.Abs(clip.length-d.length)>.0001f)throw new Exception("Janitor native duration mismatch "+d.name);
        }
        string controllerPath=Generated+"JanitorEntry.controller";
        AssetDatabase.DeleteAsset(controllerPath);var controller=AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        controller.AddParameter("flip",AnimatorControllerParameterType.Int);
        controller.AddParameter("drilling",AnimatorControllerParameterType.Int);
        controller.AddParameter("walking",AnimatorControllerParameterType.Bool);
        var machine=controller.layers[0].stateMachine;var states=new Dictionary<string,AnimatorState>();
        foreach(var d in source.states) {
            var state=machine.AddState(d.name);state.motion=clips[d.clip];state.speed=d.speed;state.writeDefaultValues=true;states[d.name]=state;
        }
        machine.defaultState=states["JanitorBod_Idle_R"];
        foreach(var d in source.states)foreach(var t in d.transitions) {
            var transition=states[d.name].AddTransition(states[t.destination]);transition.duration=t.duration;
            transition.offset=t.offset;transition.exitTime=t.exitTime;transition.hasExitTime=t.hasExitTime;
            transition.hasFixedDuration=t.fixedDuration;transition.canTransitionToSelf=t.canTransitionToSelf;
            foreach(var condition in t.conditions)transition.AddCondition((AnimatorConditionMode)condition.mode,condition.threshold,condition.parameter);
        }
        var root=nodes[266];var animator=root.AddComponent<Animator>();animator.runtimeAnimatorController=controller;
        animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
        var bridge=root.AddComponent<RecoveredStationJanitor>();bridge.animator=animator;bridge.character=nodes[117].transform;
        bridge.drillEmitter=nodes[source.drillEmitter].transform;bridge.clothEmitter=nodes[source.clothEmitter].transform;
        bridge.drillEvent=source.drillEvent;bridge.drillFade=source.drillFade;bridge.station=station;
        AssetDatabase.SaveAssets();
        Debug.Log("NITW_STATION_JANITOR_ENTRY_VERIFIED states="+states.Count+" clips="+clips.Count+" sprites="+sprites.Count+" curves="+curveCount);
        return bridge;
    }
}
