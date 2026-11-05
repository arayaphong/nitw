using System.Collections;
using System.IO;
using UnityEngine;

// Optional visual/state smoke test. No diagnostic mutation during normal play.
[DefaultExecutionOrder(-200)]
public sealed class RecoveredMurderflyRuntimeCheck : MonoBehaviour {
    private string output;
    private RecoveredTitleMotion motion;
    private float started,nextCheck,minScale=2,maxScale;
    private int failures,redPixels;
    private Vector3 firstPosition;
    public void Configure(string path) { output=path;Directory.CreateDirectory(output); }
    private void Start() { motion=GetComponent<RecoveredTitleMotion>();started=Time.time;StartCoroutine(Check()); }
    private void Assert(bool value,string reason) { if(!value) { failures++;Debug.LogError("NITW_MURDERFLY_CHECK_FAILURE "+reason); } }
    private void Update() {
        if(motion==null || Time.time<nextCheck)return;
        nextCheck=Time.time+1f/60f;
        foreach(var fly in motion.flies) {
            var p=fly.transform.position;var s=fly.transform.localScale;
            Assert(float.IsFinite(p.x)&&float.IsFinite(p.y),"finite motion");
            Assert(s.x>=.7999f&&s.x<=1.2001f&&Mathf.Abs(s.x-s.y)<.0001f,"source pulse range/symmetry");
            minScale=Mathf.Min(minScale,s.x);maxScale=Mathf.Max(maxScale,s.x);
        }
    }
    private IEnumerator Check() {
        Assert(motion.flies.Length==3,"three actors");
        firstPosition=motion.flies[0].transform.position;
        foreach(float point in new[]{1f,3f,6f,8f}) {
            while(Time.time-started<point)yield return null;
            var tex=Capture("fly-"+point.ToString("F0")+".png");
            foreach(var c in tex.GetPixels32())if(c.r>100&&c.r>c.g*1.8f&&c.r>c.b*1.8f)redPixels++;
            Destroy(tex);
            if(point==3) {
                var fly=motion.flies[0];float baseSpeed=fly.source.fields.baseSpeed.GetPicked();int wraps=fly.Wraps;
                fly.transform.position=new Vector3(11f,0,0);
                yield return null;yield return null;
                Assert(fly.Wraps>wraps,"out-of-bounds respawn");
                Assert(fly.source.fields.baseSpeed.GetPicked()==baseSpeed,"base speed survives respawn");
            }
        }
        Assert((motion.flies[0].transform.position-firstPosition).magnitude>.1f,"actor moved");
        Assert(minScale<.81f&&maxScale>1.19f,"pulse traverses 80-120 percent");
        Assert(redPixels>10,"red light visible");
        var bloom=motion.viewCamera.GetComponent<RecoveredTitleBloom>();
        var processed=Capture("bloom-on.png");bloom.enabled=false;
        var raw=Capture("bloom-off.png");bloom.enabled=true;
        int brighter=0;var a=processed.GetPixels32();var b=raw.GetPixels32();
        for(int i=0;i<a.Length;i++)if(a[i].r>b[i].r+3 || a[i].g>b[i].g+3 || a[i].b>b[i].b+3)brighter++;
        Assert(brighter>100,"Bloom adds halo pixels");Destroy(raw);Destroy(processed);
        string result="NITW_MURDERFLY_RUNTIME_CHECK actors=3 captures=6 failures="+failures+" redPixels="+redPixels+" bloomPixels="+brighter;
        File.WriteAllText(Path.Combine(output,"result.txt"),result+"\n");Debug.Log(result);enabled=false;
    }
    private Texture2D Capture(string name) {
        var camera=motion.viewCamera;var old=camera.targetTexture;var oldRect=camera.rect;
        // The off-screen target has its own 16:9 size, independent of desktop aspect.
        camera.rect=new Rect(0,0,1,1);
        var rt=new RenderTexture(960,540,24,RenderTextureFormat.ARGBHalf);camera.targetTexture=rt;camera.Render();
        var previous=RenderTexture.active;RenderTexture.active=rt;
        var tex=new Texture2D(960,540,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,960,540),0,0);tex.Apply();
        File.WriteAllBytes(Path.Combine(output,name),tex.EncodeToPNG());
        camera.targetTexture=old;camera.rect=oldRect;RenderTexture.active=previous;Destroy(rt);return tex;
    }
}
