// NITW Thai mod loader. UnityDoorstop 3 looks up "*:Main" (a static Main() in
// any class) and invokes it right after Mono initialises, before Unity loads
// any game assembly. The loader
// patches Assembly-CSharp in memory with Mono.Cecil and loads the patched
// image first, so Unity's later load by name resolves to it. Game files on
// disk are never written. Any failure leaves the original game untouched.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Doorstop {
    // Doorstop 4 would call Doorstop.Entrypoint.Start(); the Doorstop 3 build
    // shipped for BepInEx 5.4.22 only finds Main().
    public class Entrypoint {
        static string modDir, managedDir, logPath;

        public static void Main() {
            try {
                // Doorstop exports the invoked DLL path; Assembly.Location can be
                // empty this early in this Mono version.
                string self = Environment.GetEnvironmentVariable("DOORSTOP_INVOKE_DLL_PATH");
                if (string.IsNullOrEmpty(self)) self = typeof(Entrypoint).Assembly.Location;
                modDir = Path.GetDirectoryName(Path.GetFullPath(self));
                logPath = Path.Combine(modDir, "loader.log");
                File.WriteAllText(logPath, "NITWThai loader " + DateTime.Now.ToString("s") + "\n");
                string gameDir = Path.GetDirectoryName(modDir);
                foreach (string d in Directory.GetDirectories(gameDir, "*_Data"))
                    if (Directory.Exists(Path.Combine(d, "Managed"))) managedDir = Path.Combine(d, "Managed");
                if (managedDir == null) throw new DirectoryNotFoundException("Managed folder not found");
                AppDomain.CurrentDomain.AssemblyResolve += Resolve;
                // Diagnostics: NITWTHAI_MODE=off (load nothing), roundtrip
                // (Cecil read/write without hooks), default full.
                string mode = Environment.GetEnvironmentVariable("NITWTHAI_MODE") ?? "full";
                Log("mode " + mode);
                if (mode == "off") return;
                Patch(mode != "roundtrip");
            } catch (Exception e) {
                Log("FAILED, game runs unmodified: " + e);
            }
        }

        static Assembly Resolve(object sender, ResolveEventArgs args) {
            string name = new AssemblyName(args.Name).Name;
            string path = Path.Combine(modDir, name + ".dll");
            return File.Exists(path) ? Assembly.LoadFrom(path) : null;
        }

        static void Log(string line) {
            try { File.AppendAllText(logPath, line + "\n"); }
            catch { Console.Error.WriteLine("[NITWThai] " + line); }
        }

        // (type, method, hook in NITWThai.Runtime.Hooks, kind, arguments).
        // observe:  call Hook(args) at method entry, then run the method.
        // override: r = Hook(args); if r != null return r; else run the method.
        // Arguments: "this" (passed as object) or "argN" (Nth parameter).
        static readonly string[][] Hooks = {
            new[] { "Yarn.Program", "GetString", "OnGetString", "override", "arg0" },
            new[] { "SpriteFont", "GetCharData", "OnGetCharData", "override", "this,arg0" },
            new[] { "SpriteText", "SetText", "OnSetText", "observe", "arg0" },
            // Known-safe set only. Patching Global.LoadLevel, or adding
            // SpeechBubble.Say + DialogueImplementation.ShowPoemLine, made the
            // title menu ignore confirm (Rewired Interact/Jump) while axes kept
            // working, even before any hook ran. Cause not yet understood
            // (2026-11-06); verify confirm at Title after any change here.
        };

        // Does `method` fit hook spec `h` for hook method `hook`?
        static bool Matches(MethodDefinition method, string[] h, MethodDefinition hook) {
            if (method.Name != h[1] || !method.HasBody) return false;
            string[] args = h[4].Split(',');
            if (hook.Parameters.Count != args.Length) return false;
            for (int k = 0; k < args.Length; k++) {
                string want = hook.Parameters[k].ParameterType.FullName;
                if (args[k] == "this") { if (method.IsStatic || want != "System.Object") return false; continue; }
                int n = int.Parse(args[k].Substring(3));
                if (n >= method.Parameters.Count || method.Parameters[n].ParameterType.FullName != want) return false;
            }
            if (h[3] == "override") {
                string ret = hook.ReturnType.FullName;
                if (ret != method.ReturnType.FullName && ret != "System.Object") return false;
                if (method.ReturnType.IsValueType) return false;   // null means "no override"
            }
            return true;
        }

        static void Inject(MethodDefinition method, string[] h, MethodReference hookRef, MethodDefinition hook) {
            var il = method.Body.GetILProcessor();
            var first = method.Body.Instructions[0];
            var code = new List<Instruction>();
            foreach (string a in h[4].Split(','))
                code.Add(a == "this" ? il.Create(OpCodes.Ldarg_0) : il.Create(OpCodes.Ldarg, method.Parameters[int.Parse(a.Substring(3))]));
            code.Add(il.Create(OpCodes.Call, hookRef));
            if (h[3] == "override") {
                if (hook.ReturnType.FullName != method.ReturnType.FullName)
                    code.Add(il.Create(OpCodes.Castclass, method.ReturnType));
                var fallThrough = il.Create(OpCodes.Pop);
                code.Add(il.Create(OpCodes.Dup));
                code.Add(il.Create(OpCodes.Brfalse, fallThrough));
                code.Add(il.Create(OpCodes.Ret));
                code.Add(fallThrough);
            }
            foreach (var ins in code) il.InsertBefore(first, ins);
        }

        static void Patch(bool hooks) {
            var resolver = new DefaultAssemblyResolver();
            resolver.AddSearchDirectory(managedDir);
            resolver.AddSearchDirectory(modDir);
            var readParams = new ReaderParameters { AssemblyResolver = resolver };
            string target = Path.Combine(managedDir, "Assembly-CSharp.dll");
            var game = AssemblyDefinition.ReadAssembly(target, readParams);
            var runtime = AssemblyDefinition.ReadAssembly(Path.Combine(modDir, "NITWThai.Runtime.dll"), readParams);
            var hookType = runtime.MainModule.GetType("NITWThai.Runtime.Hooks");
            var module = game.MainModule;
            int patched = 0;
            // Diagnostics: NITWTHAI_HOOKS=GetString,SetText limits the hook set.
            string only = Environment.GetEnvironmentVariable("NITWTHAI_HOOKS");
            var selected = new List<string[]>();
            if (hooks) foreach (var h in Hooks)
                if (string.IsNullOrEmpty(only) || Array.IndexOf(only.Split(','), h[1]) >= 0) selected.Add(h);
            Log("hooks: " + (hooks ? (string.IsNullOrEmpty(only) ? "all" : only) : "none"));
            foreach (var h in selected) {
                int before = patched;
                var type = module.GetType(h[0]);
                if (type == null) { Log("missing type " + h[0]); continue; }
                MethodDefinition hook = null;
                foreach (var m in hookType.Methods) if (m.Name == h[2]) hook = m;
                if (hook == null) throw new MissingMethodException("NITWThai.Runtime.Hooks", h[2]);
                var hookRef = module.ImportReference(hook);
                foreach (var method in type.Methods) {
                    if (!Matches(method, h, hook)) continue;
                    Inject(method, h, hookRef, hook);
                    patched++;
                    Log("hooked " + method.FullName + " -> " + h[2] + " (" + h[3] + ")");
                }
                // Every spec must match at least one overload, or nothing is patched.
                if (patched == before) throw new InvalidOperationException("no method matched " + h[0] + "::" + h[1]);
            }
            var ms = new MemoryStream();
            game.Write(ms);
            // Diagnostics: NITWTHAI_DUMP=1 saves the patched image for inspection.
            if (Environment.GetEnvironmentVariable("NITWTHAI_DUMP") == "1")
                File.WriteAllBytes(Path.Combine(modDir, "Assembly-CSharp.patched.dll"), ms.ToArray());
            // Load the runtime first so the patched image's reference resolves.
            Assembly.LoadFrom(Path.Combine(modDir, "NITWThai.Runtime.dll"));
            var loaded = Assembly.Load(ms.ToArray());
            Log("patched Assembly-CSharp loaded: " + loaded.FullName + " (" + ms.Length + " bytes)");
        }
    }
}
