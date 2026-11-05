using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Imports data extracted statically from the installed Unity 5.6 player.
public static class RecoveredIntroImporter
{
    private const string ResourceFolder = "Assets/Resources/RecoveredIntro/";
    private const string GeneratedFolder = "Assets/RecoveredIntro/";
    public const string ClipPath = GeneratedFolder + "PreTitleLogos.anim";
    public const string ControllerPath = GeneratedFolder + "logos_animation.controller";

    [Serializable] public class Source
    {
        public string sourceUnityVersion, sourceSceneSha256, sourceAssetsSha256, clipName;
        public int rootObjectId, animatorObjectId;
        public float clipLength, sampleRate;
        public Node[] nodes;
        public SpriteSource[] sprites;
        public FontSource[] fonts;
        public FloatCurveSource[] floatCurves;
        public ObjectCurveSource[] objectCurves;
    }
    [Serializable] public class Node
    {
        public int id, parentId, layer;
        public string name;
        public bool active, isAnimator;
        public Vector3 localPosition, localScale;
        public Quaternion localRotation;
        public RendererSource renderer;
        public TextSource text;
    }
    [Serializable] public class RendererSource
    {
        public bool enabled, flipX, flipY;
        public int spriteId, sortingLayerId, sortingOrder;
        public Color color;
    }
    [Serializable] public class TextSource
    {
        public string text;
        public int fontId, fontSize, fontStyle, anchor, alignment;
        public float characterSize, lineSpacing, offsetZ, tabSize;
        public bool richText;
        public Color color;
    }
    [Serializable] public class SourceRect { public float x, y, width, height; }
    [Serializable] public class SpriteSource
    {
        public int id;
        public string name, textureResource;
        public float pixelsPerUnit;
        public Vector2 pivot;
        public SourceRect rect;
        public Vector4 border;
        public Vector2[] vertices;
        public int[] triangles;
    }
    [Serializable] public class FontSource { public int id; public string name, resource; public float sourceFontSize; }
    [Serializable] public class FloatCurveSource
    {
        public string path, property, component;
        public bool stepped;
        public FloatKey[] keys;
    }
    [Serializable] public class FloatKey { public float time, value, inTangent, outTangent; public float[] coefficients; }
    [Serializable] public class ObjectCurveSource { public string path, property; public ObjectKey[] keys; }
    [Serializable] public class ObjectKey { public float time; public int spriteId; }

    public static void VerifyStandalone()
    {
        GameObject root = Import();
        UnityEngine.Object.DestroyImmediate(root);
    }

    public static Source ReadSource()
    {
        return JsonUtility.FromJson<Source>(File.ReadAllText(ResourceFolder + "IntroSource.json"));
    }

    public static GameObject Import()
    {
        Directory.CreateDirectory(GeneratedFolder);
        Directory.CreateDirectory(GeneratedFolder + "Sprites");
        AssetDatabase.Refresh();
        Source source = ReadSource();
        Dictionary<int, Sprite> sprites = ImportSprites(source);
        var lutImporter = AssetImporter.GetAtPath(ResourceFolder + "Textures/Texture_4837.png") as TextureImporter;
        if (lutImporter != null)
        {
            lutImporter.textureType = TextureImporterType.Default;
            lutImporter.textureCompression = TextureImporterCompression.Uncompressed;
            lutImporter.sRGBTexture = false;
            lutImporter.alphaIsTransparency = false;
            lutImporter.mipmapEnabled = false;
            lutImporter.npotScale = TextureImporterNPOTScale.None;
            lutImporter.filterMode = FilterMode.Bilinear;
            lutImporter.anisoLevel = 0;
            lutImporter.wrapMode = TextureWrapMode.Clamp;
            lutImporter.SaveAndReimport();
        }
        var fonts = new Dictionary<int, Font>();
        foreach (FontSource data in source.fonts)
        {
            string path = "Assets/Resources/" + data.resource + ".ttf";
            var importer = AssetImporter.GetAtPath(path) as TrueTypeFontImporter;
            if (importer != null)
            {
                importer.fontSize = data.sourceFontSize > 0 ? Mathf.RoundToInt(data.sourceFontSize) : 64;
                importer.fontTextureCase = FontTextureCase.Dynamic;
                importer.SaveAndReimport();
            }
            fonts[data.id] = AssetDatabase.LoadAssetAtPath<Font>(path);
            if (fonts[data.id] == null) throw new Exception("Recovered intro font missing: " + path);
        }
        var objects = new Dictionary<int, GameObject>();
        foreach (Node node in source.nodes) objects[node.id] = new GameObject(node.name);
        foreach (Node node in source.nodes)
        {
            GameObject go = objects[node.id];
            if (node.parentId != 0) go.transform.SetParent(objects[node.parentId].transform, false);
            go.transform.localPosition = node.localPosition;
            go.transform.localRotation = node.localRotation;
            go.transform.localScale = node.localScale;
            go.layer = node.layer;
            if (node.renderer != null && node.renderer.spriteId != 0)
            {
                RendererSource data = node.renderer;
                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = sprites[data.spriteId];
                renderer.color = data.color;
                renderer.enabled = data.enabled;
                renderer.flipX = data.flipX;
                renderer.flipY = data.flipY;
                renderer.sortingLayerID = data.sortingLayerId;
                renderer.sortingOrder = data.sortingOrder;
            }
            if (node.text != null && node.text.fontId != 0)
            {
                TextSource data = node.text;
                var text = go.AddComponent<TextMesh>();
                text.font = fonts[data.fontId];
                text.text = data.text;
                text.fontSize = data.fontSize;
                text.characterSize = data.characterSize;
                text.fontStyle = (FontStyle)data.fontStyle;
                text.anchor = (TextAnchor)data.anchor;
                text.alignment = (TextAlignment)data.alignment;
                text.lineSpacing = data.lineSpacing;
                text.richText = data.richText;
                text.color = data.color;
                text.offsetZ = data.offsetZ;
                text.tabSize = data.tabSize;
                go.GetComponent<MeshRenderer>().sharedMaterial = text.font.material;
            }
            go.SetActive(node.active);
        }
        AnimationClip clip = CreateClip(source, sprites);
        AnimatorController controller = CreateController(clip);
        GameObject animatorObject = objects[source.animatorObjectId];
        var animator = animatorObject.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.updateMode = AnimatorUpdateMode.Normal;
        AssetDatabase.SaveAssets();
        GameObject root = objects[source.rootObjectId];
        VerifySource(root);
        return root;
    }

    public static SpriteSource MeshSource(int id)
    {
        if (meshSources == null)
        {
            meshSources = new Dictionary<int, SpriteSource>();
            foreach (var sprite in ReadSource().sprites) meshSources[sprite.id] = sprite;
        }
        return meshSources[id];
    }
    private static Dictionary<int, SpriteSource> meshSources;
    public static string SpriteAssetPath(int id)
    {
        return "Assets/Resources/" + MeshSource(id).textureResource + ".png";
    }

    private static Dictionary<int, Sprite> ImportSprites(Source source)
    {
        meshSources = new Dictionary<int, SpriteSource>();
        foreach (var data in source.sprites) meshSources[data.id] = data;
        var sprites = new Dictionary<int, Sprite>();
        foreach (SpriteSource data in source.sprites)
        {
            string texturePath = SpriteAssetPath(data.id);
            var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (importer == null) throw new Exception("Missing intro texture: " + texturePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = data.pixelsPerUnit;
            importer.spriteBorder = data.border;
            importer.userData = "RecoveredIntro:" + data.id;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 4096;
            importer.alphaIsTransparency = false;
            importer.isReadable = true;
            importer.sRGBTexture = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = data.pivot;
            settings.spriteMeshType = SpriteMeshType.Tight;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(texturePath);
            if (sprite == null) throw new Exception("Recovered sprite not imported: " + data.name);
            sprites.Add(data.id, sprite);
            // Remove obsolete provisional Sprite.Create assets, retaining imported subassets.
            AssetDatabase.DeleteAsset(GeneratedFolder + "Sprites/Sprite_" + data.id + ".asset");
        }
        return sprites;
    }

    private static AnimationClip CreateClip(Source source, Dictionary<int, Sprite> sprites)
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
        if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, ClipPath); }
        clip.ClearCurves(); clip.name = source.clipName; clip.frameRate = source.sampleRate; clip.wrapMode = WrapMode.Once;
        foreach (FloatCurveSource data in source.floatCurves)
        {
            var keys = new Keyframe[data.keys.Length];
            for (int i = 0; i < keys.Length; ++i)
            {
                FloatKey k = data.keys[i];
                keys[i] = new Keyframe(k.time, k.value, data.stepped ? float.PositiveInfinity : k.inTangent,
                    data.stepped ? float.PositiveInfinity : k.outTangent);
            }
            var curve = new AnimationCurve(keys);
            for (int i = 0; i < keys.Length; ++i)
            {
                AnimationUtility.SetKeyBroken(curve, i, true);
                AnimationUtility.SetKeyLeftTangentMode(curve, i, data.stepped ? AnimationUtility.TangentMode.Constant : AnimationUtility.TangentMode.Free);
                AnimationUtility.SetKeyRightTangentMode(curve, i, data.stepped ? AnimationUtility.TangentMode.Constant : AnimationUtility.TangentMode.Free);
            }
            var binding = EditorCurveBinding.FloatCurve(data.path, ComponentType(data.component), data.property);
            AnimationUtility.SetEditorCurve(clip, binding, curve);
        }
        foreach (ObjectCurveSource data in source.objectCurves)
        {
            var keys = new ObjectReferenceKeyframe[data.keys.Length];
            for (int i = 0; i < keys.Length; ++i) keys[i] = new ObjectReferenceKeyframe { time = data.keys[i].time, value = sprites[data.keys[i].spriteId] };
            AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve(data.path, typeof(SpriteRenderer), data.property), keys);
        }
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = false; settings.startTime = 0; settings.stopTime = source.clipLength;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        AnimationUtility.SetAnimationEvents(clip, Array.Empty<AnimationEvent>());
        EditorUtility.SetDirty(clip);
        return clip;
    }

    private static AnimatorController CreateController(AnimationClip clip)
    {
        AssetDatabase.DeleteAsset(ControllerPath);
        var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        controller.AddParameter("Go", AnimatorControllerParameterType.Trigger);
        var machine = controller.layers[0].stateMachine;
        var idle = machine.AddState("New State");
        var play = machine.AddState("PreTitleLogos");
        idle.speed = play.speed = 1f; play.motion = clip;
        idle.writeDefaultValues = play.writeDefaultValues = true;
        machine.defaultState = idle;
        var transition = idle.AddTransition(play);
        transition.hasExitTime = false; transition.duration = 0; transition.offset = 0;
        transition.hasFixedDuration = false; transition.canTransitionToSelf = true;
        transition.AddCondition(AnimatorConditionMode.If, 0, "Go");
        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static Type ComponentType(string name) { return name == "Transform" ? typeof(Transform) : typeof(SpriteRenderer); }
    private static Transform FindRelative(Transform root, string path) { return path.Length == 0 ? root : root.Find(path); }
    private static void Require(bool condition, string message) { if (!condition) throw new Exception("Recovered intro verification: " + message); }
    private static bool Near(float a, float b) { return Mathf.Abs(a - b) < 0.00015f; }

    public static void VerifySource(GameObject root)
    {
        Source source = ReadSource();
        var objects = new Dictionary<int, Transform>();
        foreach (Node node in source.nodes)
        {
            Transform t = node.parentId == 0 ? root.transform : objects[node.parentId].Find(node.name);
            Require(t != null, "missing object " + node.name);
            objects[node.id] = t;
            Require(Vector3.Distance(t.localPosition, node.localPosition) < 0.00001f, "position " + node.name);
            Require(Quaternion.Angle(t.localRotation, node.localRotation) < 0.001f, "rotation " + node.name);
            Require(Vector3.Distance(t.localScale, node.localScale) < 0.00001f, "scale " + node.name);
            Require(t.gameObject.activeSelf == node.active, "active " + node.name);
        }
        foreach (SpriteSource data in source.sprites)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteAssetPath(data.id));
            Require(sprite != null, "sprite asset " + data.id);
            Require(Near(sprite.rect.x,data.rect.x) && Near(sprite.rect.y,data.rect.y) && Near(sprite.rect.width,data.rect.width) && Near(sprite.rect.height,data.rect.height),"sprite full rect " + data.id);
            Require(Near(sprite.pixelsPerUnit,data.pixelsPerUnit),"sprite PPU " + data.id);
            Require(Near(sprite.pivot.x,data.pivot.x*data.rect.width) && Near(sprite.pivot.y,data.pivot.y*data.rect.height),"sprite pivot " + data.id);
            Vector2[] vertices=sprite.vertices; ushort[] triangles=sprite.triangles;
            Require(vertices.Length==data.vertices.Length && triangles.Length==data.triangles.Length,"sprite mesh counts " + data.id+" actual="+vertices.Length+"/"+triangles.Length+" expected="+data.vertices.Length+"/"+data.triangles.Length);
            for(int i=0;i<vertices.Length;++i) Require(Vector2.Distance(vertices[i],data.vertices[i])<0.00001f,"sprite vertex " + data.id+" #"+i);
            for(int i=0;i<triangles.Length;++i) Require(triangles[i]==data.triangles[i],"sprite triangle " + data.id+" #"+i);
        }
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
        Require(clip != null && Near(clip.length, source.clipLength), "clip duration");
        Require(AnimationUtility.GetCurveBindings(clip).Length == source.floatCurves.Length, "float curve count");
        Require(AnimationUtility.GetObjectReferenceCurveBindings(clip).Length == source.objectCurves.Length, "sprite curve count");
        foreach (FloatCurveSource data in source.floatCurves)
        {
            var curve = AnimationUtility.GetEditorCurve(clip, EditorCurveBinding.FloatCurve(data.path, ComponentType(data.component), data.property));
            Require(curve != null && curve.length == data.keys.Length, "keys " + data.path + ":" + data.property);
            for (int i = 0; i < data.keys.Length; ++i)
            {
                var a = curve.keys[i]; var b = data.keys[i];
                Require(Near(a.time,b.time) && Near(a.value,b.value), "float key " + data.property + " #" + i);
                if (!data.stepped) Require(Near(a.inTangent,b.inTangent) && Near(a.outTangent,b.outTangent), "tangents " + data.property + " #" + i);
                for (int j = 1; i + 1 < data.keys.Length && j <= 3; ++j)
                {
                    float time = Mathf.Lerp(b.time,data.keys[i+1].time,j/4f);
                    Require(Near(curve.Evaluate(time),EvaluateSource(data,time)), "polynomial " + data.property);
                }
            }
        }
        foreach (ObjectCurveSource data in source.objectCurves)
        {
            var keys = AnimationUtility.GetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve(data.path,typeof(SpriteRenderer),data.property));
            Require(keys != null && keys.Length == data.keys.Length,"object keys " + data.path);
            for (int i=0;i<keys.Length;++i)
                Require(Near(keys[i].time,data.keys[i].time) && AssetDatabase.GetAssetPath(keys[i].value)==SpriteAssetPath(data.keys[i].spriteId),"sprite key #"+i);
        }
        SampleSourceChecks(root);
        Debug.Log("RECOVERED_INTRO_SOURCE_VERIFIED: hierarchy, every animation key, tangents, polynomial samples, sprite refs and sampled poses");
    }

    public static void SampleSourceChecks(GameObject root)
    {
        Source source=ReadSource();var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
        Transform animator=root.transform.Find("logos_animation");
        foreach(float time in new[]{0f,0.067f,1f,2.94f,4.25f,4.5f,5f,7f,9.7f})
        {
            clip.SampleAnimation(animator.gameObject,time);
            foreach(FloatCurveSource data in source.floatCurves)
            {
                Transform t=FindRelative(animator,data.path);float actual;
                if(data.component=="Transform") actual=data.property.EndsWith(".x")?t.localPosition.x:data.property.EndsWith(".y")?t.localPosition.y:t.localPosition.z;
                else actual=data.property=="m_Enabled"?(t.GetComponent<SpriteRenderer>().enabled?1f:0f):t.GetComponent<SpriteRenderer>().color.a;
                Require(Near(actual,EvaluateSource(data,time)),"sample "+time+" "+data.path+":"+data.property+" actual="+actual);
            }
            foreach(ObjectCurveSource data in source.objectCurves)
            {
                ObjectKey k=data.keys[0];foreach(ObjectKey candidate in data.keys)if(candidate.time<=time)k=candidate;
                var sprite=FindRelative(animator,data.path).GetComponent<SpriteRenderer>().sprite;
                Require(AssetDatabase.GetAssetPath(sprite)==SpriteAssetPath(k.spriteId),"sample sprite "+time);
            }
        }
        // Restore serialized scene state after editor-only sampling.
        var objects=new Dictionary<int,Transform>();
        foreach(Node n in source.nodes)
        {
            var t=n.parentId==0?root.transform:objects[n.parentId].Find(n.name);objects[n.id]=t;
            t.localPosition=n.localPosition;t.localRotation=n.localRotation;t.localScale=n.localScale;
            if(n.renderer!=null && n.renderer.spriteId!=0){var r=t.GetComponent<SpriteRenderer>();r.enabled=n.renderer.enabled;r.color=n.renderer.color;r.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(SpriteAssetPath(n.renderer.spriteId));}
        }
    }

    private static float EvaluateSource(FloatCurveSource data,float time)
    {
        FloatKey k=data.keys[0];foreach(FloatKey candidate in data.keys)if(candidate.time<=time)k=candidate;
        if(data.stepped)return k.value;
        float dt=Mathf.Max(0,time-k.time);float[] c=k.coefficients;
        return ((c[0]*dt+c[1])*dt+c[2])*dt+c[3];
    }
}

// Unity only permits native sprite mesh overrides inside the import lifecycle or
// player loop. Persist the original geometry during import, rather than changing
// a Sprite.Create result in an editor execute-method callback.
public sealed class RecoveredIntroSpritePostprocessor : AssetPostprocessor
{
    private void OnPostprocessSprites(Texture2D texture, Sprite[] sprites)
    {
        var importer = assetImporter as TextureImporter;
        if (importer == null || !importer.userData.StartsWith("RecoveredIntro:")) return;
        int id = int.Parse(importer.userData.Substring("RecoveredIntro:".Length));
        var data = RecoveredIntroImporter.MeshSource(id);
        if (sprites.Length != 1) throw new Exception("Expected one recovered source sprite: " + id);
        var triangles = new ushort[data.triangles.Length];
        for (int i = 0; i < triangles.Length; ++i) triangles[i] = checked((ushort)data.triangles[i]);
        sprites[0].name = data.name;
        // The source mesh stores pivot-relative world units; OverrideGeometry
        // accepts bottom-left-relative Sprite.rect pixels and converts them back.
        var vertices = new Vector2[data.vertices.Length];
        for(int i=0;i<vertices.Length;++i)
            vertices[i] = new Vector2(
                Mathf.Clamp(data.vertices[i].x*data.pixelsPerUnit+data.pivot.x*data.rect.width,0,data.rect.width),
                Mathf.Clamp(data.vertices[i].y*data.pixelsPerUnit+data.pivot.y*data.rect.height,0,data.rect.height));
        sprites[0].OverrideGeometry(vertices, triangles);
        Debug.Log("RECOVERED_MESH_IMPORTED " + id + " source=" + data.vertices.Length + "/" + triangles.Length + " actual=" + sprites[0].vertices.Length + "/" + sprites[0].triangles.Length);
    }
}
