using System;
using UnityEditor;
using UnityEngine;

public static class RecoveredTitleMenuImporter {
    public static void Import(Camera main) {
        var json=AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/RecoveredTitle/Menu.json");
        var data=JsonUtility.FromJson<RecoveredTitleMenuData>(json.text);
        const string path="Assets/Resources/RecoveredTitle/MenuFont.ttf";
        var importer=(TrueTypeFontImporter)AssetImporter.GetAtPath(path);
        importer.fontSize=Mathf.RoundToInt(data.fontDefaultSize);importer.SaveAndReimport();
        var font=AssetDatabase.LoadAssetAtPath<Font>(path);
        var cameraObject=new GameObject("CameraHUD - original title menu");data.hudTransform.Apply(cameraObject.transform);
        var hud=cameraObject.AddComponent<Camera>();hud.orthographic=false;hud.fieldOfView=data.hudFov;
        hud.aspect=16f/9f;hud.depth=data.hudDepth;hud.clearFlags=(CameraClearFlags)data.hudClearFlags;
        hud.cullingMask=data.hudMask;hud.allowHDR=false;hud.nearClipPlane=.3f;hud.farClipPlane=1000;
        cameraObject.AddComponent<RecoveredIntroAspect>();
        var follow=cameraObject.AddComponent<RecoveredTitleHud>();follow.sceneCamera=main;follow.sourceZ=data.hudTransform.position.z;
        // Keep the existing reconstructed Options layers on Main Camera, while
        // layer12 uses the recovered FOV60 HUD and bypasses scene Bloom.
        main.cullingMask&=~data.hudMask;main.depth=10;
        var root=new GameObject("Recovered title menu");var menu=root.AddComponent<TitlePrototype>();
        menu.source=data;menu.hudCamera=hud;menu.rows=new TextMesh[data.rows.Length];
        for(int i=0;i<data.rows.Length;i++) {
            var n=data.rows[i];var go=new GameObject(n.name);go.transform.SetParent(root.transform,false);
            n.transform.Apply(go.transform);go.layer=n.layer;
            var text=go.AddComponent<TextMesh>();text.font=font;
            // The prototype starts without a save; Continue support comes later.
            // Retain the recovered save-present label in source data as evidence.
            text.text=i==0?TitlePrototype.NewGameLabel:n.label;
            text.fontSize=n.fontSize;text.fontStyle=(FontStyle)n.fontStyle;text.characterSize=n.characterSize;
            text.anchor=(TextAnchor)n.anchor;text.alignment=(TextAlignment)n.alignment;
            text.lineSpacing=n.lineSpacing;text.offsetZ=n.offsetZ;text.richText=n.richText;text.color=n.color;
            var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=font.material;
            renderer.sortingLayerID=n.sortingLayer;renderer.sortingOrder=n.order;menu.rows[i]=text;
            if(Vector3.Distance(go.transform.localPosition,n.transform.position)>1e-6f || text.anchor!=TextAnchor.MiddleRight || go.layer!=12)
                throw new Exception("Source title menu geometry differs: "+n.label);
        }
        if(menu.rows.Length!=4 || hud.fieldOfView!=60 || hud.cullingMask!=4096 || (main.cullingMask&4096)!=0)
            throw new Exception("Source title HUD setup differs.");
        Debug.Log("NITW_TITLE_MENU_SOURCE_VERIFIED rows=4 hudFov=60 hudZ="+hud.transform.position.z+" fontSize="+data.fontDefaultSize);
    }
}
