using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
public sealed class RecoveredStationCheck : MonoBehaviour {
    private string output;private int failures;
    public void Configure(string path) {output=path;Directory.CreateDirectory(path);}
    private void Check(bool value,string reason) {if(!value) {failures++;Debug.LogError("NITW_STATION_CHECK_FAILURE "+reason);}}
    private IEnumerator Start() {
        yield return new WaitForSeconds(6);
        var menu=FindFirstObjectByType<TitlePrototype>();menu.Select(0);menu.ActivateSelected();
        while(SceneManager.GetActiveScene().name!="IntroPoem")yield return null;
        var poem=FindFirstObjectByType<RecoveredPoem>();
        Time.timeScale=16;
        while(poem!=null&&SceneManager.GetActiveScene().name=="IntroPoem") {
            if(poem.AwaitingAdvance) {
                poem.Advance();do {yield return null;}while(poem!=null&&poem.AwaitingAdvance);
            } else if(poem.AwaitingChoice) {
                while(poem.Typing)yield return null;
                yield return new WaitForSeconds(1.1f);poem.Advance();
                do {yield return null;}while(poem!=null&&poem.AwaitingChoice);
            } else yield return null;
        }
        Time.timeScale=1;
        Check(SceneManager.GetActiveScene().name=="BusStation","poem loads BusStation");
        var station=FindFirstObjectByType<RecoveredStation>();
        while(!station.Ready)yield return null;
        Check(station.AudioReady&&station.NativeErrors==0,"native ambient audio");
        Check(FindFirstObjectByType<RecoveredPoem>()==null,"poem and wind scene unloaded");
        Check(Vector3.Distance(station.sceneCamera.transform.position,station.source.camera)<.08f,"source entry camera with idle drift");
        Check(station.sceneCamera.fieldOfView==60,"source FOV");
        Capture("station-entry.png",station.sceneCamera);
        Vector3 player=station.player.position,camera=station.sceneCamera.transform.position;
        yield return new WaitForSeconds(3);
        Check(Vector3.Distance(station.player.position,player)<.002f&&Vector3.Distance(station.sceneCamera.transform.position,camera)<.16f,"idle remains supported with source camera drift");
        Check(SceneManager.GetActiveScene().name=="BusStation","no further stage loads");
        string result="NITW_STATION_CHECK menuPoemStation=1 sourceNodes=273 sourceSprites=174 failures="+failures;
        File.WriteAllText(Path.Combine(output,"result.txt"),result+"\n");Debug.Log(result);
    }
    private void Capture(string name,Camera camera) {
        var old=camera.targetTexture;var rect=camera.rect;
        var rt=new RenderTexture(960,540,24);camera.rect=new Rect(0,0,1,1);camera.targetTexture=rt;camera.Render();
        var active=RenderTexture.active;RenderTexture.active=rt;
        var image=new Texture2D(960,540,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,960,540),0,0);image.Apply();
        File.WriteAllBytes(Path.Combine(output,name),image.EncodeToPNG());
        RenderTexture.active=active;camera.targetTexture=old;camera.rect=rect;Destroy(image);Destroy(rt);
    }
}
