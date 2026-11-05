using System.Collections;
using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

// Optional integration check. Normal play never injects input or changes time.
public sealed class RecoveredPoemCheck : MonoBehaviour {
    private string output;
    private int failures,paths;
    public void Configure(string folder) { output=folder;Directory.CreateDirectory(output); }
    private void Check(bool value,string reason) {
        if(!value) {failures++;Debug.LogError("NITW_POEM_CHECK_FAILURE "+reason);}
    }
    private IEnumerator Start() {
        yield return new WaitForSeconds(6);
        var menu=FindFirstObjectByType<TitlePrototype>();
        Check(menu!=null&&menu.rows[0].text=="Start New Game","new-game menu");
        File.WriteAllText(Path.Combine(output,"display.txt"),Screen.width+"x"+Screen.height+" fullscreen="+Screen.fullScreen+"\n");
        menu.Select(0);float began=Time.time;menu.ActivateSelected();
        while(SceneManager.GetActiveScene().name!="IntroPoem"&&Time.time-began<5)yield return null;
        Check(SceneManager.GetActiveScene().name=="IntroPoem","menu loads original first scene");
        Check(Time.time-began>=1.1f&&Time.time-began<1.5f,"source transition delay");
        var poem=FindFirstObjectByType<RecoveredPoem>();
        poem.HoldAtSceneBoundary=true;
        float entered=Time.time;
        while(!poem.AwaitingAdvance&&Time.time-entered<10)yield return null;
        Check(poem.Lines.Count==1&&!string.IsNullOrEmpty(poem.Lines[0].text),"Thai first line");
        Check(poem.thai!=null&&poem.thai.Data.fontName=="Noto Serif Thai","shaped Thai font");
        Check(poem.font.name=="PoemFont"&&poem.font.fontSize==58,"source Goudy size");
        Check(Vector3.Distance(poem.Lines[0].transform.position,poem.source.columns[0])<1e-5f,"first column position");
        Check(Camera.main.fieldOfView==60&&Camera.main.transform.position.z==-6,"source poem camera");
        yield return new WaitForSeconds(.7f);Capture("first-line.png");
        // Accelerate only workflow coverage after the normal-speed entry check.
        Time.timeScale=16;
        yield return PlayPath(poem,new[]{0,0,0},"first-path");paths++;
        SceneManager.LoadScene("IntroPoem");yield return null;
        poem=FindFirstObjectByType<RecoveredPoem>();
        poem.HoldAtSceneBoundary=true;
        yield return PlayPath(poem,new[]{1,2,2},"alternate-path");paths++;
        Time.timeScale=1;
        string result="NITW_POEM_CHECK menuToPoem=1 paths="+paths+" failures="+failures;
        File.WriteAllText(Path.Combine(output,"result.txt"),result+"\n");Debug.Log(result);
    }
    private IEnumerator PlayPath(RecoveredPoem poem,int[] choices,string label) {
        float began=Time.time;bool captured=false;
        while(!poem.Completed&&Time.time-began<600) {
            if(poem.AwaitingAdvance) {
                poem.Advance();
                do {yield return null;}while(poem.AwaitingAdvance);
            } else if(poem.AwaitingChoice) {
                int target=Array.IndexOf(poem.DisplayedSourceOptions,choices[poem.ChoiceNumber-1]);
                Check(target>=0,"shuffled choice retains original branch");
                while(poem.Typing)yield return null;
                while(poem.CurrentOption!=target) {
                    poem.MoveOption(1);yield return null;
                    while(poem.Typing)yield return null;
                }
                yield return new WaitForSeconds(1.1f);
                if(!captured) {Capture(label+"-choice.png");captured=true;}
                poem.Advance();
                do {yield return null;}while(poem.AwaitingChoice);
            } else yield return null;
        }
        Check(poem.Completed,label+" reaches script boundary");
        Check(poem.DeferredScene=="BusStation"&&SceneManager.GetActiveScene().name=="IntroPoem",label+" stops before next step");
        Check(poem.Column==1&&poem.ChoiceNumber==3&&poem.Lines.Count>=20,label+" two columns and three choices");
        Check(poem.screenFade.Alpha==1,label+" source fade out");
        // MoveParameter is nonblocking. Shorter localized text can reach the
        // ending before a one-unit/second ramp reaches10; verify its target
        // and actual native submission rather than requiring an early arrival.
        Check(poem.wind.Ready&&poem.wind.NativeErrors==0&&poem.wind.TargetParameterValue==10
            &&poem.wind.ParameterValue>=0&&poem.wind.ParameterValue<=10
            &&Mathf.Abs(poem.wind.NativeParameterValue-poem.wind.ParameterValue)<.01f,label+" native wind parameter");
        // Narrative-specific assertion omitted from public snapshot.
        var text="";
        foreach(var line in poem.Lines)text+=line.transform.position.ToString("F4")+" "+line.text+"\n";
        File.WriteAllText(Path.Combine(output,label+"-lines.txt"),text);
    }
    private void Capture(string name) {
        var camera=Camera.main;var old=camera.targetTexture;var oldRect=camera.rect;
        var rt=new RenderTexture(960,540,24);camera.rect=new Rect(0,0,1,1);camera.targetTexture=rt;camera.Render();
        var active=RenderTexture.active;RenderTexture.active=rt;
        var image=new Texture2D(960,540,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,960,540),0,0);image.Apply();
        // The first narrative line must remain visible while an option is shown.
        // This catches atlas rebuilds leaving old TextMesh glyphs invisible.
        int visible=0;
        for(int y=380;y<402;y++)for(int x=90;x<310;x++) {
            var p=image.GetPixel(x,y);
            if(p.r>.25f&&p.g>.25f&&p.b>.25f)visible++;
        }
        Check(visible>50,"first line persists in "+name);
        File.WriteAllBytes(Path.Combine(output,name),image.EncodeToPNG());
        RenderTexture.active=active;camera.targetTexture=old;camera.rect=oldRect;Destroy(image);Destroy(rt);
    }
}
