// Thai for the IntroPoem. The poem types one character at a time into
// TextMesh instances cloned from DialogueImplementation.prefabTextMeshLine,
// so the prefab gets a runtime custom Font whose characters are the same
// Private Use Area clusters as the speech-bubble data (plus the ASCII and
// quotes the poem uses). Glyph boxes are in font pixels, y up from baseline;
// TextMesh scales them by characterSize like the original Goudy font.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace NITWThai.Runtime {
    public static class ThaiPoemFont {
        static bool tried;
        static Font font;

        static Font Build(Shader shader) {
            string dir = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(
                Environment.GetEnvironmentVariable("DOORSTOP_INVOKE_DLL_PATH"))), "data");
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, true);
            if (!tex.LoadImage(File.ReadAllBytes(Path.Combine(dir, "poem_glyphs.png")))) throw new IOException("poem_glyphs.png did not decode");
            tex.filterMode = FilterMode.Trilinear; tex.wrapMode = TextureWrapMode.Clamp; tex.mipMapBias = -.5f;
            float tw = tex.width, th = tex.height;
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            var infos = new List<CharacterInfo>();
            foreach (string line in File.ReadAllLines(Path.Combine(dir, "poem_glyphs.tsv"), System.Text.Encoding.UTF8)) {
                string[] f = line.Split('\t');
                if (f.Length < 10) continue;
                float x = float.Parse(f[1], inv), y = float.Parse(f[2], inv), w = float.Parse(f[3], inv), h = float.Parse(f[4], inv);
                var ci = new CharacterInfo {
                    index = int.Parse(f[0]),
                    advance = Mathf.RoundToInt(float.Parse(f[9], inv)),
                    minX = Mathf.RoundToInt(float.Parse(f[5], inv)), maxX = Mathf.RoundToInt(float.Parse(f[6], inv)),
                    minY = Mathf.RoundToInt(float.Parse(f[7], inv)), maxY = Mathf.RoundToInt(float.Parse(f[8], inv)),
                    uvBottomLeft = new Vector2(x / tw, y / th), uvBottomRight = new Vector2((x + w) / tw, y / th),
                    uvTopLeft = new Vector2(x / tw, (y + h) / th), uvTopRight = new Vector2((x + w) / tw, (y + h) / th),
                };
                infos.Add(ci);
            }
            var f2 = new Font("NITWThai Poem");
            f2.material = new Material(shader) { mainTexture = tex, name = "NITWThai Poem" };
            f2.characterInfo = infos.ToArray();
            Hooks.Log("poem font: " + infos.Count + " glyphs, atlas " + tex.width + "x" + tex.height);
            return f2;
        }

        static bool loggedMiss;

        // Called when a Thai poem line is fetched: switch the scene's poem
        // TextMesh prefab to the Thai font before the poem coroutine clones it.
        // Every DialogueImplementation is checked each time (nothing is cached
        // as "not found"); only the one owning a TextMesh prefab is changed.
        public static void ApplyToScene() {
            if (!ThaiFont.Enabled) return;
            int applied = 0;
            foreach (var dialogue in Resources.FindObjectsOfTypeAll(typeof(DialogueImplementation)))
                if (Apply(dialogue)) applied++;
            if (applied == 0 && !loggedMiss) { loggedMiss = true; Hooks.Log("poem: no DialogueImplementation with a TextMesh prefab yet"); }
        }

        static bool Apply(object dialogue) {
            var field = dialogue.GetType().GetField("prefabTextMeshLine", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var value = field == null ? null : field.GetValue(dialogue);
            var mesh = value as TextMesh ?? (value is Component ? ((Component)value).GetComponent<TextMesh>()
                : value is GameObject ? ((GameObject)value).GetComponent<TextMesh>() : null);
            if (mesh == null) return false;
            var renderer = mesh.GetComponent<MeshRenderer>();
            if (font == null) {
                if (tried) return false;     // building failed once; don't retry every line
                tried = true;
                font = Build(renderer.sharedMaterial.shader);
                tried = font == null;
            }
            if (mesh.font == font) return true;
            mesh.font = font;
            mesh.fontSize = 0;              // custom fonts render at their native pixel size
            renderer.sharedMaterial = font.material;
            PoemLineFix.Install(mesh, (UnityEngine.Object)dialogue);
            Hooks.Log("poem: Thai font set on " + mesh.name + " of " + ((UnityEngine.Object)dialogue).name);
            return true;
        }
    }
}
