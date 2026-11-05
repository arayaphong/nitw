using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class RecoveredOptionsImporter {
    public static RecoveredOptionsView Import() {
        const string dir = "Assets/Resources/RecoveredOptions/";
        var source = AssetDatabase.LoadAssetAtPath<TextAsset>(dir + "Scene.json");
        var data = JsonUtility.FromJson<RecoveredOptionsData>(source.text);
        var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers = tagManager.FindProperty("m_SortingLayers"); layers.arraySize = data.sortingLayers.Length;
        for (int i=0;i<data.sortingLayers.Length;i++) {
            var entry=layers.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("name").stringValue=data.sortingLayers[i].name;
            entry.FindPropertyRelative("uniqueID").uintValue=checked((uint)data.sortingLayers[i].uniqueID);
            entry.FindPropertyRelative("locked").boolValue=false;
        }
        tagManager.ApplyModifiedPropertiesWithoutUndo();
        var sprites = new Dictionary<string, Sprite>();
        var fonts = new Dictionary<string, Font>();
        foreach (var s in data.sprites) {
            var path = dir + s.key + ".png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = s.ppu;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = s.pivot;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            sprites[s.key] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        foreach (var f in data.fonts) {
            var path = dir + f.key + ".ttf";
            var importer = (TrueTypeFontImporter)AssetImporter.GetAtPath(path);
            importer.fontSize = Mathf.RoundToInt(f.size);
            importer.SaveAndReimport();
            fonts[f.key] = AssetDatabase.LoadAssetAtPath<Font>(path);
        }
        var objects = new GameObject[data.nodes.Length];
        for (int i = 0; i < objects.Length; i++) {
            var n = data.nodes[i]; var go = new GameObject(n.name); objects[i] = go;
            if (n.parent >= 0) go.transform.SetParent(objects[n.parent].transform, false);
            go.transform.localPosition = n.position; go.transform.localRotation = n.rotation; go.transform.localScale = n.scale;
            // Original Options core uses layer zero; retain other source layer IDs for submenus.
            go.layer = n.layer;
            if (n.sprite != null && !string.IsNullOrEmpty(n.sprite.asset)) {
                var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = sprites[n.sprite.asset]; sr.color = n.sprite.color;
                sr.sortingLayerID=unchecked((int)n.sprite.sortingLayer); sr.sortingOrder = n.sprite.order; sr.flipX = n.sprite.flipX; sr.flipY = n.sprite.flipY; sr.enabled = n.sprite.enabled;
            }
            if (n.text != null && n.text.font != null) {
                var t = go.AddComponent<TextMesh>(); t.font = fonts[n.text.font]; t.text = n.text.value;
                t.fontSize = n.text.fontSize; t.characterSize = n.text.characterSize;
                t.anchor = (TextAnchor)n.text.anchor; t.alignment = (TextAlignment)n.text.alignment;
                t.lineSpacing = n.text.lineSpacing; t.offsetZ = n.text.offsetZ; t.richText = n.text.richText; t.color = n.text.color;
                var mr = go.GetComponent<MeshRenderer>(); mr.sharedMaterial = t.font.material;
                if (n.renderer != null) { mr.sortingLayerID=unchecked((int)n.renderer.sortingLayer); mr.sortingOrder = n.renderer.order; mr.enabled = n.renderer.enabled; }
            }
            go.SetActive(n.active);
        }
        var controller = new GameObject("Recovered options controller").AddComponent<RecoveredOptionsView>();
        controller.Configure(source, objects, new[] { sprites["3_8308"], sprites["3_8724"], sprites["3_9950"] });
        return controller;
    }
}
