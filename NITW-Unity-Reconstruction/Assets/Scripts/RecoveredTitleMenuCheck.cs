using System.Collections;
using System.IO;
using UnityEngine;

public sealed class RecoveredTitleMenuCheck : MonoBehaviour {
    private string output;
    private int failures;
    private TitlePrototype menu;
    public void Configure(string path) { output=path;Directory.CreateDirectory(output); }
    private void Assert(bool value,string reason) { if(!value) { failures++;Debug.LogError("NITW_TITLE_MENU_CHECK_FAILURE "+reason); } }
    private IEnumerator Start() {
        menu=GetComponent<TitlePrototype>();
        yield return new WaitForSeconds(6f);
        Assert(menu.rows.Length==4,"four rows");
        Assert(menu.rows[0].text==TitlePrototype.NewGameLabel,"new game label without save support");
        Assert(menu.hudCamera.fieldOfView==60 && menu.hudCamera.cullingMask==4096,"source HUD");
        var anchors="";
        for(int i=0;i<menu.rows.Length;i++) {
            var row=menu.rows[i];var n=menu.source.rows[i];
            Assert(Vector3.Distance(row.transform.localPosition,n.transform.position)<1e-6f,"source position "+i);
            var p=menu.hudCamera.WorldToViewportPoint(row.transform.position);
            anchors+=row.text+" rightAnchor=("+(p.x*960).ToString("F3")+","+((1-p.y)*540).ToString("F3")+")\n";
            Assert(Mathf.Abs(p.x*960-948.1768f)<.05f,"right edge "+i);
            Assert(row.anchor==TextAnchor.MiddleRight&&Mathf.Abs(row.characterSize-.06f)<1e-6f,"font layout "+i);
            Assert(Mathf.Abs(row.GetComponent<MeshRenderer>().material.color.a-(i==0?1f:.25f))<.01f,"selection alpha "+i);
        }
        File.WriteAllText(Path.Combine(output,"anchors.txt"),anchors);
        Capture("menu-start-new-game.png");menu.Select(1);
        yield return new WaitForSeconds(1.5f);Capture("menu-options-selected.png");
        Assert(menu.rows[1].GetComponent<MeshRenderer>().material.color.a>.99f,"Options highlight");
        var options=FindFirstObjectByType<RecoveredOptionsView>();options.Show();
        yield return new WaitForSeconds(1.7f);
        Assert(options.IsOpen,"Options opens");
        foreach(var row in menu.rows)Assert(row.GetComponent<MeshRenderer>().material.color.a<.01f,"title hidden in Options");
        Capture("options-open.png");options.Close();
        yield return new WaitForSeconds(3f);
        Assert(!options.IsOpen,"Options closes");Capture("menu-returned.png");
        Assert(menu.rows[1].GetComponent<MeshRenderer>().material.color.a>.99f,"highlight returns");
        string result="NITW_TITLE_MENU_RUNTIME_CHECK rows=4 captures=4 failures="+failures;
        File.WriteAllText(Path.Combine(output,"result.txt"),result+"\n");Debug.Log(result);
    }
    private void Capture(string name) {
        var scene=Camera.main;var hud=menu.hudCamera;
        var oldScene=scene.targetTexture;var oldHud=hud.targetTexture;var sceneRect=scene.rect;var hudRect=hud.rect;
        var rt=new RenderTexture(960,540,24,RenderTextureFormat.ARGBHalf);
        scene.rect=hud.rect=new Rect(0,0,1,1);scene.targetTexture=hud.targetTexture=rt;
        scene.Render();hud.Render();
        var active=RenderTexture.active;RenderTexture.active=rt;
        var tex=new Texture2D(960,540,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,960,540),0,0);tex.Apply();
        File.WriteAllBytes(Path.Combine(output,name),tex.EncodeToPNG());
        scene.targetTexture=oldScene;hud.targetTexture=oldHud;scene.rect=sceneRect;hud.rect=hudRect;
        RenderTexture.active=active;Destroy(tex);Destroy(rt);
    }
}
