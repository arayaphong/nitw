// Runtime side of the NITW Thai mod. Phase 0: trace which Yarn string keys
// (line IDs) the game looks up and which strings SpriteText actually draws.
// Output goes to NITWThai/trace.log next to the game; nothing else changes.
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

        public static void OnGetString(string key) {
            // Program.GetString runs for every line/option shown; log each key
            // once per session to keep the trace readable.
            if (key != null && seenKeys.Add(key)) Write("key", key);
        }

        public static void OnSetText(string text) {
            if (!string.IsNullOrEmpty(text)) Write("text", text);
        }
    }
}
