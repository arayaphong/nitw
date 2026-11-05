using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class DiagnoseIntroColor {
 public static void Run() {
  EditorSceneManager.OpenScene("Assets/ReferenceScene.unity");
  var sequence=Object.FindFirstObjectByType<StartupPreview>();
  sequence.text.SetActive(false);
  var anim=sequence.GetComponentInChildren<Animator>();
  var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(RecoveredIntroImporter.ClipPath);
  clip.SampleAnimation(anim.gameObject,2.95f);
  var camera=Camera.main;camera.GetComponent<RecoveredIntroColor>().enabled=false;camera.GetComponent<RecoveredIntroAA>().enabled=false;
  var raw=new RenderTexture(960,540,24);camera.targetTexture=raw;camera.Render();
  var grade=new RenderTexture(960,540,0);var faded=new RenderTexture(960,540,0);var final=new RenderTexture(960,540,0);
  var color=new Material(Resources.Load<Shader>("RecoveredIntroColor"));var lut=Resources.Load<Texture2D>("RecoveredIntro/Textures/Texture_4837");Debug.Log("LUT texture="+lut.width+"x"+lut.height+" aniso="+lut.anisoLevel+" filter="+lut.filterMode+" mips="+lut.mipmapCount);lut.anisoLevel=0;color.SetTexture("_RgbTex",lut);
  var aa=new Material(Resources.Load<Shader>("RecoveredIntroAA"));aa.SetFloat("_FadeAlpha",0);
  Graphics.Blit(raw,grade,color,0);Graphics.Blit(grade,faded,aa,0);Graphics.Blit(faded,final,aa,1);
  string path=Path.GetFullPath(Path.Combine(Application.dataPath,"../../research/validation/intro-color-aniso0/"));Directory.CreateDirectory(path);
  Save(raw,path+"raw.png");Save(grade,path+"lut.png");Save(faded,path+"fade0.png");Save(final,path+"fxaa.png");
  Debug.Log("NITW_COLOR_STAGES_RENDERED");
 }
 private static void Save(RenderTexture rt,string path){RenderTexture.active=rt;var t=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);t.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);t.Apply();File.WriteAllBytes(path,t.EncodeToPNG());Object.DestroyImmediate(t);RenderTexture.active=null;}
}
