using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class RecoveredPoemImporter {
    public static void Import() {
        const string folder="Assets/Resources/RecoveredPoem/";
        var data=JsonUtility.FromJson<RecoveredPoemSceneData>(AssetDatabase.LoadAssetAtPath<TextAsset>(folder+"Scene.json").text);
        var fontImporter=(TrueTypeFontImporter)AssetImporter.GetAtPath(folder+"PoemFont.ttf");
        fontImporter.fontSize=Mathf.RoundToInt(data.fontSize);fontImporter.SaveAndReimport();
        var font=AssetDatabase.LoadAssetAtPath<Font>(folder+"PoemFont.ttf");
        var sprites=new Dictionary<string,Sprite>();
        foreach(var item in data.sprites) {
            var importer=(TextureImporter)AssetImporter.GetAtPath(folder+item.key+".png");
            importer.textureType=TextureImporterType.Sprite;importer.spritePixelsPerUnit=item.ppu;
            var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);
            settings.spriteAlignment=(int)SpriteAlignment.Custom;settings.spritePivot=item.pivot;
            importer.SetTextureSettings(settings);importer.alphaIsTransparency=true;importer.mipmapEnabled=false;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
            sprites[item.key]=AssetDatabase.LoadAssetAtPath<Sprite>(folder+item.key+".png");
        }
        var cameraObject=new GameObject("Main Camera");cameraObject.tag="MainCamera";
        data.camera.Apply(cameraObject.transform);
        var camera=cameraObject.AddComponent<Camera>();camera.fieldOfView=data.fov;
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
        camera.nearClipPlane=.3f;camera.farClipPlane=1000;camera.allowHDR=false;
        cameraObject.AddComponent<AudioListener>();cameraObject.AddComponent<RecoveredIntroAspect>();
        var fade=cameraObject.AddComponent<RecoveredIntroFade>();
        var root=new GameObject("Recovered IntroPoem");var poem=root.AddComponent<RecoveredPoem>();
        poem.source=data;poem.font=font;poem.screenFade=fade;
        poem.programAsset=AssetDatabase.LoadAssetAtPath<TextAsset>(folder+"Program.json");
        var atlasImporter=(TextureImporter)AssetImporter.GetAtPath(folder+"ThaiGlyphs.png");
        atlasImporter.textureType=TextureImporterType.Default;atlasImporter.mipmapEnabled=false;
        atlasImporter.alphaIsTransparency=true;atlasImporter.wrapMode=TextureWrapMode.Clamp;
        atlasImporter.textureCompression=TextureImporterCompression.Uncompressed;atlasImporter.SaveAndReimport();
        var atlas=AssetDatabase.LoadAssetAtPath<Texture2D>(folder+"ThaiGlyphs.png");
        const string materialPath="Assets/Resources/RecoveredPoem/ThaiGlyphs.mat";
        var glyphMaterial=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if(glyphMaterial==null) {
            glyphMaterial=new Material(Shader.Find("Sprites/Default"));AssetDatabase.CreateAsset(glyphMaterial,materialPath);
        }
        glyphMaterial.mainTexture=atlas;EditorUtility.SetDirty(glyphMaterial);
        var thai=root.AddComponent<RecoveredPoemThai>();poem.thai=thai;
        thai.translationAsset=AssetDatabase.LoadAssetAtPath<TextAsset>(folder+"Thai.json");
        thai.shapingAsset=AssetDatabase.LoadAssetAtPath<TextAsset>(folder+"ThaiShaping.json");
        thai.atlas=atlas;thai.glyphMaterial=glyphMaterial;
        thai.baselineOffset=data.fontAscent*data.text.m_CharacterSize*.1f;
        foreach(var item in data.nodes) {
            var go=new GameObject(item.name);go.transform.SetParent(root.transform,false);item.transform.Apply(go.transform);
            var renderer=go.AddComponent<SpriteRenderer>();renderer.sprite=sprites[item.sprite];renderer.color=item.color;
            if(item.name=="TriLeft")poem.arrowLeft=renderer;
            else if(item.name=="TriRight")poem.arrowRight=renderer;
            else poem.indicator=renderer;
        }
        poem.confirmFrames=new[]{sprites[data.keyboardConfirmFrames[0]],sprites[data.keyboardConfirmFrames[1]]};
        var audio=root.AddComponent<RecoveredPoemAudio>();audio.source=data;audio.screenFade=fade;poem.wind=audio;
        audio.configuration=AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/RecoveredTitle/Audio.json");
        var template=poem.CreateTextLine("Font metrics probe",Vector3.zero);
        float empty=template.GetComponent<MeshRenderer>().bounds.size.y;
        template.text=" ";float space=template.GetComponent<MeshRenderer>().bounds.size.y;
        template.text="Sample text";float sample=template.GetComponent<MeshRenderer>().bounds.size.y;
        UnityEngine.Object.DestroyImmediate(template.gameObject);
        if(data.fov!=60 || data.columns.Length!=2 || data.text.m_CharacterSize!=.04f || data.fontSize!=58)
            throw new Exception("Recovered IntroPoem source differs.");
        Debug.Log("NITW_POEM_SOURCE_VERIFIED font=Goudy size=58 fov=60 z=-6 columns=2 emptyHeight="+empty+" spaceHeight="+space+" sampleHeight="+sample);
    }
}
