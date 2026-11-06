// Thai data for the original game's SpriteText. Translated Yarn lines are
// stored as display strings in which every Thai grapheme cluster is one
// Private Use Area character; this class supplies a CharData (sprite + width)
// for each of those characters, so SpriteText lays out, types and animates
// Thai exactly like its Latin sprite font. Data comes from
// Tools/prepare_thai_mod.py and is loaded from NITWThai/data.
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace NITWThai.Runtime {
    public static class ThaiFont {
        public const char PuaFirst = '', PuaLast = '';
        sealed class Glyph { public Rect rect; public Vector2 pivot; public float width; }

        static bool tried;
        static Dictionary<string, string> strings;
        static HashSet<string> poemKeys;
        static Dictionary<char, Glyph> glyphs;
        static readonly Dictionary<char, SpriteFont.CharData> charDatas = new Dictionary<char, SpriteFont.CharData>();
        static readonly HashSet<int> adjustedFonts = new HashSet<int>();
        static Texture2D atlas;
        static float pixelsPerUnit = 100, lineHeight = 1;

        // English unless data loaded; NITWTHAI_LANG=en forces the original text.
        public static bool Enabled { get { Load(); return strings != null; } }

        static string DataDir() {
            string self = Environment.GetEnvironmentVariable("DOORSTOP_INVOKE_DLL_PATH");
            string dir = !string.IsNullOrEmpty(self) ? Path.GetDirectoryName(Path.GetFullPath(self))
                : Path.GetDirectoryName(typeof(ThaiFont).Assembly.Location);
            return Path.Combine(dir, "data");
        }

        static void Load() {
            if (tried) return;
            tried = true;
            try {
                if (Environment.GetEnvironmentVariable("NITWTHAI_LANG") == "en") { Hooks.Log("lang en: Thai disabled"); return; }
                string dir = DataDir();
                var meta = new Dictionary<string, string>();
                foreach (string line in File.ReadAllLines(Path.Combine(dir, "meta.tsv"))) {
                    string[] f = line.Split('\t');
                    if (f.Length == 2) meta[f[0]] = f[1];
                }
                pixelsPerUnit = float.Parse(meta["pixelsPerUnit"], System.Globalization.CultureInfo.InvariantCulture);
                lineHeight = float.Parse(meta["lineHeight"], System.Globalization.CultureInfo.InvariantCulture);
                var g = new Dictionary<char, Glyph>();
                foreach (string line in File.ReadAllLines(Path.Combine(dir, "glyphs.tsv"), System.Text.Encoding.UTF8)) {
                    string[] f = line.Split('\t');
                    if (f.Length < 8) continue;
                    var inv = System.Globalization.CultureInfo.InvariantCulture;
                    g[(char)int.Parse(f[0])] = new Glyph {
                        rect = new Rect(float.Parse(f[1], inv), float.Parse(f[2], inv), float.Parse(f[3], inv), float.Parse(f[4], inv)),
                        pivot = new Vector2(float.Parse(f[5], inv), float.Parse(f[6], inv)),
                        width = float.Parse(f[7], inv),
                    };
                }
                var s = new Dictionary<string, string>();
                var poem = new HashSet<string>();
                foreach (string line in File.ReadAllLines(Path.Combine(dir, "strings.tsv"), System.Text.Encoding.UTF8)) {
                    string[] f = line.Split('\t');
                    if (f.Length < 2) continue;
                    s[f[0]] = f[1];
                    if (f.Length > 2 && f[2] == "poem") poem.Add(f[0]);
                }
                poemKeys = poem;
                atlas = new Texture2D(2, 2, TextureFormat.RGBA32, true);
                if (!atlas.LoadImage(File.ReadAllBytes(Path.Combine(dir, "glyphs.png")))) throw new IOException("glyphs.png did not decode");
                atlas.filterMode = FilterMode.Trilinear;
                atlas.wrapMode = TextureWrapMode.Clamp;
                atlas.mipMapBias = -.5f;
                atlas.name = "NITWThai glyphs";
                glyphs = g;
                strings = s;
                Hooks.Log("thai data: " + s.Count + " strings, " + g.Count + " glyphs, atlas " + atlas.width + "x" + atlas.height);
            } catch (Exception e) {
                Hooks.Log("thai data FAILED, English only: " + e);
            }
        }

        public static string Translate(string key, out bool poem) {
            poem = false;
            if (!Enabled || key == null) return null;
            string value;
            if (!strings.TryGetValue(key, out value)) return null;
            poem = poemKeys.Contains(key);
            return value;
        }

        public static SpriteFont.CharData Get(SpriteFont font, char c) {
            if (c < PuaFirst || c > PuaLast || !Enabled) return null;
            // Thai rows need room for stacked marks; raise this font's line
            // height once, the first time it draws Thai.
            if (font != null && adjustedFonts.Add(font.GetInstanceID()) && font.lineHeight < lineHeight)
                font.lineHeight = lineHeight;
            SpriteFont.CharData data;
            if (charDatas.TryGetValue(c, out data)) return data;
            Glyph glyph;
            if (!glyphs.TryGetValue(c, out glyph)) return null;
            var sprite = Sprite.Create(atlas, glyph.rect, glyph.pivot, pixelsPerUnit, 0, SpriteMeshType.FullRect);
            sprite.name = "thai " + ((int)c).ToString("X4");
            data = new SpriteFont.CharData {
                character = c.ToString(),
                additionalCharacters = new string[0],
                width = glyph.width,
                // Same frame three times: SpriteText animates Latin glyphs
                // through three hand-drawn frames.
                frames = new[] { sprite, sprite, sprite },
            };
            charDatas[c] = data;
            return data;
        }
    }
}
