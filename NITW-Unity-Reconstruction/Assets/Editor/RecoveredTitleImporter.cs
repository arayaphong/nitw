using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class RecoveredTitleImporter {
    private const string Dir="Assets/RecoveredTitle";
    private static T Save<T>(T value,string path) where T:UnityEngine.Object {
        var previous=AssetDatabase.LoadAssetAtPath<T>(path);
        if(previous==null) { AssetDatabase.CreateAsset(value,path); return value; }
        EditorUtility.CopySerialized(value,previous); UnityEngine.Object.DestroyImmediate(value); return previous;
    }
    private static GameObject Node(string name,Transform parent,RecoveredTitleTransform pose) {
        var go=new GameObject(name); if(parent!=null)go.transform.SetParent(parent,false);pose.Apply(go.transform);return go;
    }
    public static void Import(Camera camera) {
        if(!AssetDatabase.IsValidFolder(Dir))AssetDatabase.CreateFolder("Assets","RecoveredTitle");
        var source=AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/RecoveredTitle/Scene.json");
        var data=JsonUtility.FromJson<RecoveredTitleData>(source.text);
        var meshes=new Dictionary<int,Mesh>(); var materials=new Dictionary<int,Material>();
        foreach(var m in data.meshes) {
            var mesh=new Mesh { name=m.name, vertices=m.vertices, triangles=m.triangles };
            mesh.RecalculateBounds(); meshes[m.id]=Save(mesh,Dir+"/Mesh_"+m.id+".asset");
        }
        var shader=Resources.Load<Shader>("RecoveredTitleMesh");
        if(shader==null)throw new Exception("Missing recovered title mesh shader.");
        foreach(var m in data.materials) {
            var selected=m.id==3?Resources.Load<Shader>("RecoveredMurderflyCore"):m.id==4?Resources.Load<Shader>("RecoveredMurderflyGlow"):shader;
            if(selected==null)throw new Exception("Missing source material shader: "+m.id);
            var mat=new Material(selected) { name="Source material "+m.id,color=m.color };
            if(m.id==3)mat.SetColor("_TintColor",m.tint);
            materials[m.id]=Save(mat,Dir+"/Material_"+m.id+".mat");
        }
        camera.orthographic=false; camera.fieldOfView=data.fov;
        data.camera.Apply(camera.transform);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=data.background;
        camera.gameObject.AddComponent<RecoveredIntroAspect>();
        camera.allowHDR=true;
        camera.gameObject.AddComponent<RecoveredTitleBloom>().settings=data.bloom;
        const string path="Assets/Resources/RecoveredTitle/Logo.png";
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
        importer.spritePixelsPerUnit=data.logoPPU;importer.alphaIsTransparency=false;
        importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Uncompressed;
        var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);
        settings.spriteAlignment=(int)SpriteAlignment.Custom;settings.spritePivot=data.logoPivot;settings.spriteMeshType=SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);importer.SaveAndReimport();
        var logo=Node("e3_demo_title_logo",null,data.logo).AddComponent<SpriteRenderer>();logo.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);
        var bark=Node("e3_demo_title_bark",null,data.bark);
        bark.AddComponent<MeshFilter>().sharedMesh=meshes[14];var br=bark.AddComponent<MeshRenderer>();
        br.sharedMaterial=materials[8];br.sortingLayerID=data.barkLayer;br.sortingOrder=data.barkOrder;
        var root=Node("TitleLeavesAndFlies",null,data.parent);
        var audio=root.AddComponent<RecoveredFireflyAudio>();audio.viewCamera=camera;
        audio.configuration=AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/RecoveredTitle/Audio.json");
        if(audio.configuration==null)throw new Exception("Recovered firefly audio has not been prepared.");
        var motion=root.AddComponent<RecoveredTitleMotion>();motion.source=data;motion.viewCamera=camera;
        var leaves=new List<RecoveredTitleLeaf>();
        foreach(var leaf in data.leaves) {
            var go=Node(leaf.name+" ["+leaf.id+"]",root.transform,leaf.transform);
            var child=Node("e3_demo_title_leaf",go.transform,leaf.child);
            child.AddComponent<MeshFilter>().sharedMesh=meshes[15];var mr=child.AddComponent<MeshRenderer>();
            mr.sharedMaterial=materials[leaf.material];mr.sortingLayerID=leaf.sortingLayer;mr.sortingOrder=leaf.order;
            var behavior=go.AddComponent<RecoveredTitleLeaf>();behavior.source=leaf;behavior.motion=motion;leaves.Add(behavior);
        }
        motion.leaves=leaves.ToArray();
        var flies=new List<RecoveredTitleMurderfly>();
        foreach(var fly in data.flies) {
            var go=Node("E3TitleMurderfly ["+fly.id+"]",root.transform,fly.transform);
            foreach(var ch in fly.children) {
                var child=Node(ch.name,go.transform,ch.transform);
                child.AddComponent<MeshFilter>().sharedMesh=meshes[13];
                var mr=child.AddComponent<MeshRenderer>();mr.sharedMaterial=materials[ch.material];mr.sortingLayerID=ch.sortingLayer;mr.sortingOrder=ch.order;
            }
            var behavior=go.AddComponent<RecoveredTitleMurderfly>();behavior.source=fly;behavior.audioSystem=audio;flies.Add(behavior);
        }
        motion.flies=flies.ToArray();
        Verify(motion,data,meshes,materials);
        AssetDatabase.SaveAssets();
    }
    private static void Pose(Transform t,RecoveredTitleTransform s) {
        if((t.localPosition-s.position).magnitude>1e-6f || Quaternion.Angle(t.localRotation,s.rotation)>.001f || (t.localScale-s.scale).magnitude>1e-6f)
            throw new Exception("Title source transform mismatch: "+t.name);
    }
    private static void Verify(RecoveredTitleMotion motion,RecoveredTitleData data,Dictionary<int,Mesh> meshes,Dictionary<int,Material> materials) {
        if(motion.leaves.Length!=31)throw new Exception("Expected 31 title leaves.");
        Pose(motion.transform,data.parent);
        foreach(var leaf in motion.leaves) {
            Pose(leaf.transform,leaf.source.transform);Pose(leaf.transform.GetChild(0),leaf.source.child);
            var mr=leaf.GetComponentInChildren<MeshRenderer>();
            if(mr.sharedMaterial!=materials[leaf.source.material] || mr.sortingLayerID!=leaf.source.sortingLayer || mr.sortingOrder!=leaf.source.order)
                throw new Exception("Title material/sorting mismatch: "+leaf.source.id+" layer="+mr.sortingLayerID+" expected="+leaf.source.sortingLayer+" material="+mr.sharedMaterial.name+" expectedMaterial="+materials[leaf.source.material].name);
        }
        foreach(var m in data.meshes) {
            var mesh=meshes[m.id];if(!mesh.vertices.SequenceEqual(m.vertices)||!mesh.triangles.SequenceEqual(m.triangles))throw new Exception("Native mesh mismatch: "+m.id);
        }
        if(meshes[15].vertexCount!=50 || meshes[15].triangles.Length!=144)throw new Exception("Source leaf topology mismatch.");
        foreach(var m in data.materials)if(materials[m.id].color!=m.color)throw new Exception("Source color mismatch.");
        if(motion.flies.Length!=3 || meshes[13].vertexCount!=64 || meshes[13].triangles.Length!=372)throw new Exception("Murderfly source topology mismatch.");
        foreach(var fly in motion.flies) {
            Pose(fly.transform,fly.source.transform);
            for(int i=0;i<fly.source.children.Length;i++) {
                var ch=fly.source.children[i];var t=fly.transform.GetChild(i);Pose(t,ch.transform);var mr=t.GetComponent<MeshRenderer>();
                if(mr.sharedMaterial!=materials[ch.material] || mr.sortingLayerID!=ch.sortingLayer || mr.sortingOrder!=ch.order)throw new Exception("Murderfly source renderer mismatch.");
            }
        }
        if(data.bloom.quality!=1 || data.bloom.lensflareIntensity!=0)throw new Exception("Uncovered Bloom branch.");
        Debug.Log("NITW_MURDERFLY_SOURCE_VERIFIED instances=3 native_vertices=64 native_triangles=124 child_meshes=6 bloom_intensity="+data.bloom.bloomIntensity);
        Debug.Log("NITW_TITLE_SOURCE_VERIFIED leaves=31 native_vertices=50 native_triangles=48 materials=3 transforms=63");
    }
}
