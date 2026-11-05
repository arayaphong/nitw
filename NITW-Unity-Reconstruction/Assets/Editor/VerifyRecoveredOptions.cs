using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class VerifyRecoveredOptions {
    public static void Run() {
        EditorSceneManager.OpenScene("Assets/TitlePrototype.unity");
        var view=UnityEngine.Object.FindFirstObjectByType<RecoveredOptionsView>();
        var so=new SerializedObject(view);
        var source=(TextAsset)so.FindProperty("source").objectReferenceValue;
        var data=JsonUtility.FromJson<RecoveredOptionsData>(source.text);
        var objects=so.FindProperty("objects");
        if(objects.arraySize!=data.nodes.Length)throw new Exception("Hierarchy count differs");
        int spriteCount=0,textCount=0;
        for(int i=0;i<data.nodes.Length;i++) {
            var n=data.nodes[i];var go=(GameObject)objects.GetArrayElementAtIndex(i).objectReferenceValue;
            if(go.name!=n.name || Vector3.Distance(go.transform.localPosition,n.position)>0.000001f || Vector3.Distance(go.transform.localScale,n.scale)>0.000001f || Quaternion.Angle(go.transform.localRotation,n.rotation)>0.001f)throw new Exception("Transform differs: "+n.name);
            if(n.sprite!=null && !string.IsNullOrEmpty(n.sprite.asset)) {
                spriteCount++;var sr=go.GetComponent<SpriteRenderer>();
                var asset=Array.Find(data.sprites,x=>x.key==n.sprite.asset);
                if(Mathf.Abs(sr.sprite.pixelsPerUnit-asset.ppu)>0.001f || (sr.sprite.pivot-new Vector2(asset.pivot.x*asset.width,asset.pivot.y*asset.height)).magnitude>0.01f)throw new Exception("Sprite origin differs: "+n.name);
                if(sr.sortingLayerID!=unchecked((int)n.sprite.sortingLayer) || sr.sortingOrder!=n.sprite.order)throw new Exception("Sprite sort differs: "+n.name);
            }
            if(n.text!=null && n.text.font!=null) {
                textCount++;var text=go.GetComponent<TextMesh>();
                if(text.text!=n.text.value || text.anchor!=(TextAnchor)n.text.anchor || Math.Abs(text.characterSize-n.text.characterSize)>0.000001f)throw new Exception("Text differs: "+n.name);
            }
        }
        int titleRows=0;foreach(var row in data.menus[0].items)if(row.journalOnlyOption==0)titleRows++;
        if(titleRows!=10)throw new Exception("Title row count differs");
        Debug.Log("NITW_SOURCE_VERIFICATION_SUCCESS nodes="+objects.arraySize+" sprites="+spriteCount+" texts="+textCount+" rows="+titleRows);
    }
}
