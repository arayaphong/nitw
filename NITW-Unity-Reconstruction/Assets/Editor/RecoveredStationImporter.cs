using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
public static class RecoveredStationImporter {
    private const string Folder="Assets/Resources/RecoveredStation/";
    private static T Save<T>(T asset,string file) where T:UnityEngine.Object {
        string path=Folder+file;var old=AssetDatabase.LoadAssetAtPath<T>(path);
        if(old==null) {AssetDatabase.CreateAsset(asset,path);return asset;}
        EditorUtility.CopySerialized(asset,old);UnityEngine.Object.DestroyImmediate(asset);return old;
    }
    public static void Import() {
        var data=JsonUtility.FromJson<RecoveredStationData>(AssetDatabase.LoadAssetAtPath<TextAsset>(Folder+"Scene.json").text);
        var sprites=new Dictionary<string,Sprite>();var materials=new Dictionary<string,Material>();var meshes=new Dictionary<string,Mesh>();
        foreach(var d in data.sprites) {
            string path=Folder+d.key+".png";var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            importer.spritePixelsPerUnit=d.ppu;
            var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);
            settings.spriteAlignment=(int)SpriteAlignment.Custom;settings.spritePivot=d.pivot;settings.spriteMeshType=SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);importer.alphaIsTransparency=true;importer.mipmapEnabled=false;
            importer.maxTextureSize=8192;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
            sprites[d.key]=AssetDatabase.LoadAssetAtPath<Sprite>(path);
            // Multiple mode uses each sheet entry's alignment and ignores the
            // top-level custom pivot. Validate the loaded Sprite, not just the
            // import settings (Mae's head pivot is .5,.2 rather than center).
            var sprite=sprites[d.key];
            if(sprite==null)throw new Exception("Station sprite missing: "+d.key);
            var pivot=new Vector2(sprite.pivot.x/sprite.rect.width,sprite.pivot.y/sprite.rect.height);
            if(Vector2.Distance(pivot,d.pivot)>.00001f||Mathf.Abs(sprite.pixelsPerUnit-d.ppu)>.00001f)
                throw new Exception("Station sprite pivot/PPU mismatch: "+d.key+" actual="+pivot+" source="+d.pivot);
        }
        foreach(var d in data.textures) {
            var importer=(TextureImporter)AssetImporter.GetAtPath(Folder+d.key+".png");
            importer.textureType=TextureImporterType.Default;importer.mipmapEnabled=false;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
        }
        foreach(var d in data.materials) {
            var shader=d.shader=="Sprites-Additive"||d.shader=="Particles/Additive"?Resources.Load<Shader>("RecoveredStationAdditive"):
                Shader.Find(d.shader=="Sprites/Default"?"Sprites/Default":"Unlit/Transparent");
            var mat=new Material(shader) {name=d.name,color=d.color};
            if(!string.IsNullOrEmpty(d.texture))mat.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+d.texture+".png");
            mat.mainTextureScale=d.scale;mat.mainTextureOffset=d.offset;
            materials[d.key]=Save(mat,d.key+".mat");
        }
        foreach(var d in data.meshes) {
            var mesh=new Mesh {name=d.name,vertices=d.vertices,triangles=d.triangles};
            if(d.uv.Length==d.vertices.Length)mesh.uv=d.uv;
            mesh.RecalculateBounds();meshes[d.key]=Save(mesh,d.key+".asset");
        }
        var nodes=new Dictionary<int,GameObject>();
        foreach(var d in data.nodes)nodes[d.id]=new GameObject(d.name);
        int spriteCount=0,meshCount=0;
        foreach(var d in data.nodes) {
            var go=nodes[d.id];if(d.parent!=0)go.transform.SetParent(nodes[d.parent].transform,false);
            d.transform.Apply(go.transform);go.layer=d.layer;
            if(d.sprite!=null&&!string.IsNullOrEmpty(d.sprite.material)) {
                var r=go.AddComponent<SpriteRenderer>();var s=d.sprite;
                if(!string.IsNullOrEmpty(s.sprite))r.sprite=sprites[s.sprite];
                r.sharedMaterial=materials[s.material];r.color=s.color;r.enabled=s.enabled;
                r.flipX=s.flipX;r.flipY=s.flipY;r.sortingLayerID=s.sortingLayer;r.sortingOrder=s.order;spriteCount++;
            }
            if(d.meshRenderer!=null&&!string.IsNullOrEmpty(d.mesh)) {
                go.AddComponent<MeshFilter>().sharedMesh=meshes[d.mesh];var r=go.AddComponent<MeshRenderer>();var s=d.meshRenderer;
                r.sharedMaterial=materials[s.material];r.enabled=s.enabled&&materials[s.material].name!="BubbleRenderTexture";
                r.sortingLayerID=s.sortingLayer;r.sortingOrder=s.order;meshCount++;
            }
            go.SetActive(d.active);
        }
        foreach(var d in data.nodes) {
            var t=nodes[d.id].transform;
            if(Vector3.Distance(t.localPosition,d.transform.position)>1e-6f||Quaternion.Angle(t.localRotation,d.transform.rotation)>.001f
                ||Vector3.Distance(t.localScale,d.transform.scale)>1e-6f)throw new Exception("Station transform mismatch: "+d.id);
        }
        // These source subtrees are activated by the absent gameplay/UI systems.
        // Keep their initial entry state off while the scene is held for review.
        foreach(var d in data.nodes)if(d.name=="InteractIndicator"||d.name=="Indicator"||d.name=="BigPaw"
            ||d.name=="MaePaws"||d.name=="PlayerSpeechBubble"||d.name=="JanitorSpeechBubble"
            ||d.name.Contains("Harfest_hat")||d.name=="SpriteAnimation")nodes[d.id].SetActive(false);
        var camera=nodes[128].AddComponent<Camera>();nodes[128].tag="MainCamera";
        camera.transform.position=data.camera;camera.fieldOfView=data.fov;camera.cullingMask=data.mask;
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=data.background;camera.allowHDR=false;
        camera.gameObject.AddComponent<AudioListener>();camera.gameObject.AddComponent<RecoveredIntroAspect>();
        camera.gameObject.AddComponent<RecoveredIntroColor>();camera.gameObject.AddComponent<RecoveredIntroAA>();
        var stage=new GameObject("BusStation entry milestone").AddComponent<RecoveredStation>();
        stage.source=data;stage.sceneCamera=camera;stage.player=nodes[219].transform;
        stage.configuration=AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/RecoveredTitle/Audio.json");
        var animation=RecoveredStationAnimationImporter.Attach(nodes);
        RecoveredStationMovementImporter.Attach(nodes,stage,animation);
        if(nodes.Count!=273||spriteCount!=174||meshCount!=4)throw new Exception("Station source counts differ.");
        Debug.Log("NITW_STATION_SOURCE_VERIFIED nodes="+nodes.Count+" sprites="+spriteCount+" meshes="+meshCount+" camera="+data.camera);
        AssetDatabase.SaveAssets();
    }
}
