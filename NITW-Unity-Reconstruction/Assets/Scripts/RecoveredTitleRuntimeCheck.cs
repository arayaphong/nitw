using System.Collections;
using System.IO;
using UnityEngine;

// Optional graphical smoke check: five captures plus idle/lean/release assertions.
[DefaultExecutionOrder(-200)]
public sealed class RecoveredTitleRuntimeCheck : MonoBehaviour {
    private string output;
    private float started;
    private RecoveredTitleMotion motion;
    private int failures;
    public void Configure(string path) { output=path;Directory.CreateDirectory(output); }
    private void Start() { motion=GetComponent<RecoveredTitleMotion>();started=Time.time;StartCoroutine(Check()); }
    private void Update() { if(motion!=null)motion.DiagnosticInput=Time.time-started>=4 && Time.time-started<5.5f?Vector3.right:Vector3.zero; }
    private void Assert(bool value,string reason) { if(!value) { failures++;Debug.LogError("NITW_TITLE_CHECK_FAILURE "+reason); } }
    private IEnumerator Check() {
        var points=new[]{.3f,1.5f,3f,5.25f,8.25f};
        foreach(float point in points) {
            while(Time.time-started<point)yield return null;
            // Camera.Render includes the source meshes and animation pose.
            Capture(point);
            Assert(motion.leaves.Length==31,"leaf count");
            foreach(var leaf in motion.leaves) {
                var p=leaf.transform.localPosition;
                Assert(float.IsFinite(p.x)&&float.IsFinite(p.y)&&float.IsFinite(leaf.Phase),"finite pose "+leaf.source.id);
                if(point==3f) {
                    Assert(leaf.WaveSpeed>=.99f&&leaf.WaveSpeed<=2.01f,"idle speed");
                    Assert((p-leaf.RestPosition).magnitude<.01f,"intro reaches rest");
                }
                if(point==5.25f) {
                    Assert(leaf.WaveSpeed>=6.9f&&leaf.WaveSpeed<=14.01f,"lean accelerates");
                    Assert(p.x-leaf.RestPosition.x>.7f,"leans right");
                }
                if(point==8.25f) {
                    Assert(leaf.WaveSpeed>=.99f&&leaf.WaveSpeed<=2.01f,"release speed");
                    Assert((p-leaf.RestPosition).magnitude<.02f,"release returns");
                }
            }
        }
        motion.DiagnosticInput=null;
        string result="NITW_TITLE_RUNTIME_CHECK leaves=31 captures=5 failures="+failures;
        File.WriteAllText(Path.Combine(output,"result.txt"),result+"\n");Debug.Log(result);
        enabled=false;
    }
    private void Capture(float point) {
        var camera=motion.viewCamera;var old=camera.targetTexture;var oldRect=camera.rect;camera.rect=new Rect(0,0,1,1);var rt=new RenderTexture(960,540,24);
        camera.targetTexture=rt;camera.Render();var active=RenderTexture.active;RenderTexture.active=rt;
        var tex=new Texture2D(960,540,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,960,540),0,0);tex.Apply();
        File.WriteAllBytes(Path.Combine(output,"title-"+point.ToString("F2",System.Globalization.CultureInfo.InvariantCulture)+".png"),tex.EncodeToPNG());
        camera.targetTexture=old;camera.rect=oldRect;RenderTexture.active=active;Destroy(rt);Destroy(tex);
    }
}
