using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Active only with -validate-intro <output-directory>. Cross-checks actual Animator
// poses against independently extracted source key data; no on-screen debug UI.
public sealed class RecoveredIntroRuntimeCheck : MonoBehaviour {
    [Serializable] private class Source { public float clipLength; public Curve[] floatCurves; public ObjectCurve[] objectCurves; public SpriteInfo[] sprites; }
    [Serializable] private class Curve { public string path,property,component; public bool stepped; public Key[] keys; }
    [Serializable] private class Key { public float time,value; public float[] coefficients; }
    [Serializable] private class ObjectCurve { public string path; public ObjectKey[] keys; }
    [Serializable] private class ObjectKey { public float time; public int spriteId; }
    [Serializable] private class SpriteInfo { public int id; public string name; }
    private Source source;
    private StartupPreview sequence;
    private Transform animationRoot;
    private readonly Dictionary<int,string> spriteNames=new Dictionary<int,string>();
    private string output;
    private int checks,failures,capture;
    private readonly float[] captures={0.1f,1f,2.95f,4.25f,4.55f,5.5f,7f,9.7f};
    public void Configure(string path) { output=path; }
    private void Start() {
        source=JsonUtility.FromJson<Source>(Resources.Load<TextAsset>("RecoveredIntro/IntroSource").text);
        foreach(var s in source.sprites)spriteNames[s.id]=s.name;
        sequence=GetComponent<StartupPreview>();animationRoot=transform.Find("logos_animation");
        Directory.CreateDirectory(output);
    }
    private Transform Find(string path) { return string.IsNullOrEmpty(path)?animationRoot:animationRoot.Find(path); }
    private void LateUpdate() {
        if(sequence.GoTime<0 || sequence.LogoAnimator==null)return;
        var state=sequence.LogoAnimator.GetCurrentAnimatorStateInfo(0);
        if(!state.IsName("PreTitleLogos"))return;
        float time=Mathf.Clamp(state.normalizedTime*source.clipLength,0,source.clipLength);
        foreach(var curve in source.floatCurves) {
            // Animator state time and discrete property evaluation can straddle
            // a key by a float ULP. Check stepped values away from that boundary.
            bool boundary=false;
            if(curve.stepped) foreach(var key in curve.keys) if(Mathf.Abs(time-key.time)<.0001f) boundary=true;
            if(boundary) continue;
            var target=Find(curve.path);float actual;
            if(curve.component=="Transform") actual=curve.property.EndsWith(".x")?target.localPosition.x:curve.property.EndsWith(".y")?target.localPosition.y:target.localPosition.z;
            else actual=curve.property=="m_Enabled"?(target.GetComponent<SpriteRenderer>().enabled?1f:0f):target.GetComponent<SpriteRenderer>().color.a;
            var k=curve.keys[0];foreach(var candidate in curve.keys)if(candidate.time<=time)k=candidate;
            float dt=time-k.time;var c=k.coefficients;
            float expected=curve.stepped?k.value:((c[0]*dt+c[1])*dt+c[2])*dt+c[3];
            checks++;if(Mathf.Abs(actual-expected)>.001f)Fail("float "+curve.path+":"+curve.property+" at "+time+" expected="+expected+" actual="+actual);
        }
        foreach(var curve in source.objectCurves) {
            var k=curve.keys[0];bool boundary=false;
            foreach(var candidate in curve.keys) {if(candidate.time<=time)k=candidate;if(Mathf.Abs(time-candidate.time)<.0001f)boundary=true;}
            if(boundary)continue;
            string actual=Find(curve.path).GetComponent<SpriteRenderer>().sprite.name;
            checks++;if(actual!=spriteNames[k.spriteId])Fail("sprite at "+time+" expected="+spriteNames[k.spriteId]+" actual="+actual);
        }
        if(capture<captures.Length && time>=captures[capture]) {
            Capture(time,output+"/intro-"+captures[capture].ToString("F2",System.Globalization.CultureInfo.InvariantCulture)+".png");capture++;
        }
    }
    private void Fail(string message) { failures++;if(failures<10)Debug.LogError("NITW_INTRO_POSE_MISMATCH "+message); }
    private void Capture(float time,string path) {
        var camera=Camera.main;var old=camera.targetTexture;var rt=new RenderTexture(960,540,24);camera.targetTexture=rt;camera.Render();
        var active=RenderTexture.active;RenderTexture.active=rt;var tex=new Texture2D(960,540,TextureFormat.RGB24,false);
        tex.ReadPixels(new Rect(0,0,960,540),0,0);tex.Apply();File.WriteAllBytes(path,tex.EncodeToPNG());
        camera.targetTexture=old;RenderTexture.active=active;Destroy(rt);Destroy(tex);
        Debug.Log("NITW_INTRO_CAPTURE clip_t="+time+" "+path);
    }
    private void OnDestroy() {
        var result="NITW_INTRO_RUNTIME_CHECK checks="+checks+" failures="+failures+" captures="+capture;
        Debug.Log(result);if(!string.IsNullOrEmpty(output))File.WriteAllText(output+"/runtime-result.txt",result+"\n");
    }
}
