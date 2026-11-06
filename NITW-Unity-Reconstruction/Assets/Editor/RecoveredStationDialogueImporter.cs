using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class RecoveredStationDialogueImporter {
    const string Folder="Assets/Resources/RecoveredStationDialogue/";
    private static TextAsset cachedResource;
    private static Sprite[] cachedSprites;
    public static RecoveredStationDialogueView Attach(Dictionary<int,GameObject> nodes) {
        cachedResource=null;cachedSprites=null;
        return AttachSpeaker(nodes,"Mae");
    }
    public static RecoveredStationDialogueView AttachSpeaker(Dictionary<int,GameObject> nodes,string speakerName) {
        AssetDatabase.Refresh();
        var resource=AssetDatabase.LoadAssetAtPath<TextAsset>(Folder+"Presentation.json");
        if(resource==null)throw new Exception("Recover station dialogue presentation before importing the scene.");
        var data=JsonUtility.FromJson<RecoveredStationDialogueData>(resource.text);
        var sprites=new Sprite[data.sprites.Length];
        if(cachedResource==resource&&cachedSprites!=null)sprites=cachedSprites;
        else for(int i=0;i<data.sprites.Length;i++) {
            var d=data.sprites[i];string path=Folder+d.id+".png";
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            importer.spritePixelsPerUnit=d.ppu;
            var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);
            settings.spriteAlignment=(int)SpriteAlignment.Custom;settings.spritePivot=d.pivot;
            settings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(settings);
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.alphaIsTransparency=true;
            importer.mipmapEnabled=false;importer.npotScale=TextureImporterNPOTScale.None;
            importer.maxTextureSize=2048;importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Bilinear;
            importer.SaveAndReimport();sprites[i]=AssetDatabase.LoadAssetAtPath<Sprite>(path);
            var s=sprites[i];
            if(s==null||Mathf.Abs(s.pixelsPerUnit-d.ppu)>.00001f||
                Vector2.Distance(new Vector2(s.pivot.x/s.rect.width,s.pivot.y/s.rect.height),d.pivot)>.00001f)
                throw new Exception("Station dialogue glyph pivot/PPU mismatch "+d.id);
        }
        cachedResource=resource;cachedSprites=sprites;
        var go=new GameObject("Recovered "+speakerName+" speech bubble");
        var view=go.AddComponent<RecoveredStationDialogueView>();
        view.presentation=resource;view.sprites=sprites;
        var speaker=Array.Find(data.speakers,s=>s.name==speakerName);
        if(speaker==null)throw new Exception("Unknown recovered station speaker "+speakerName);
        view.SpeakerName=speakerName;view.speakerRoot=nodes[speaker.root].transform;
        view.headTracker=speaker.headTracker!=0?nodes[speaker.headTracker].transform:null;
        view.fixedLocator=nodes[speaker.locator].transform;
        view.sceneCamera=nodes[128].GetComponent<Camera>();
        Debug.Log("NITW_STATION_DIALOGUE_PRESENTATION_VERIFIED glyphs="+data.characters.Length+" sprites="+sprites.Length+" head="+data.headTrackerGameObject);
        return view;
    }
    public static RecoveredStationChoiceView AttachChoices(Dictionary<int,GameObject> nodes,RecoveredStationDialogueView bubble) {
        var source=JsonUtility.FromJson<RecoveredStationDialogueData>(bubble.presentation.text);
        var view=new GameObject("Recovered dialogue option arrows and dots").AddComponent<RecoveredStationChoiceView>();
        view.bubble=bubble;
        view.arrowSprite=AssetDatabase.LoadAssetAtPath<Sprite>(Folder+source.choiceArrowSprite+".png");
        view.dotSprite=AssetDatabase.LoadAssetAtPath<Sprite>(Folder+source.choiceDotSprite+".png");
        return view;
    }
}
