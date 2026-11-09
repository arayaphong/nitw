// Entry points the patched game calls. Signatures use only mscorlib types so
// the loader can import them into Assembly-CSharp without a self-reference;
// override hooks return null to let the original method run unchanged.
using System;
using System.Collections.Generic;
using System.IO;

namespace NITWThai.Runtime {
    public static class Hooks {
        static StreamWriter trace;
        static readonly HashSet<string> seenKeys = new HashSet<string>();
        static readonly DateTime started = DateTime.Now;

        static void Write(string kind, string value) {
            try {
                if (trace == null) {
                    string self = Environment.GetEnvironmentVariable("DOORSTOP_INVOKE_DLL_PATH");
                    string dir = !string.IsNullOrEmpty(self) ? Path.GetDirectoryName(Path.GetFullPath(self))
                        : Path.GetDirectoryName(typeof(Hooks).Assembly.Location);
                    trace = new StreamWriter(Path.Combine(dir, "trace.log"), true) { AutoFlush = true };
                    trace.WriteLine("# session " + started.ToString("s"));
                }
                double t = (DateTime.Now - started).TotalSeconds;
                trace.WriteLine(t.ToString("0.000") + "\t" + kind + "\t" + (value ?? "<null>").Replace("\n", "\\n"));
            } catch { }
        }

        public static void Log(string message) { Write("log", message); }

        // Yarn.Program.GetString(key): Thai display string, or null for original.
        public static string OnGetString(object program, string key) {
            try {
                if (key != null && seenKeys.Add(key)) Write("key", key);
                bool poem;
                string thai = ThaiFont.Translate(program, key, out poem);
                // The game fetches a whole node's lines when it starts, before
                // the poem coroutine clones its TextMesh prefab.
                if (poem) ThaiPoemFont.ApplyToScene();
                return thai;
            } catch (Exception e) { Write("error", "GetString " + e.Message); return null; }
        }

        // SpriteFont.GetCharData(char): Thai cluster CharData, or null for original.
        public static object OnGetCharData(object font, char c) {
            try { return ThaiFont.Get(font as SpriteFont, c); }
            catch (Exception e) { Write("error", "GetCharData " + e.Message); return null; }
        }

        public static void OnSetText(string text) {
            if (!string.IsNullOrEmpty(text)) Write("text", text);
        }
    }
}
