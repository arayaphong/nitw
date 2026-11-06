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

        // (type, method, hook) — hook is a static void Hook(string) in
        // NITWThai.Runtime.Hooks, called with the method's first argument.
        static readonly string[][] Hooks = {
            new[] { "Yarn.Program", "GetString", "OnGetString" },
            new[] { "SpriteText", "SetText", "OnSetText" },
            // Do not hook Global.LoadLevel: with that hook the title menu's
            // confirm button (Rewired Interact/Jump) stopped responding while
            // axes kept working. Cause not yet understood; bisected 2026-11-06.
        };

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
                var hookRef = module.ImportReference(hook);
                foreach (var method in type.Methods) {
                    if (method.Name != h[1] || !method.HasBody || method.Parameters.Count == 0 ||
                        method.Parameters[0].ParameterType.FullName != "System.String") continue;
                    var il = method.Body.GetILProcessor();
                    var first = method.Body.Instructions[0];
                    il.InsertBefore(first, il.Create(OpCodes.Ldarg, method.Parameters[0]));
                    il.InsertBefore(first, il.Create(OpCodes.Call, hookRef));
                    patched++;
                    Log("hooked " + method.FullName + " -> " + h[2]);
                }
                // Every spec must match at least one overload, or nothing is patched.
                if (patched == before) throw new InvalidOperationException("no method matched " + h[0] + "::" + h[1]);
            }
            var ms = new MemoryStream();
            game.Write(ms);
            // Load the runtime first so the patched image's reference resolves.
            Assembly.LoadFrom(Path.Combine(modDir, "NITWThai.Runtime.dll"));
            var loaded = Assembly.Load(ms.ToArray());
            Log("patched Assembly-CSharp loaded: " + loaded.FullName + " (" + ms.Length + " bytes)");
        }
    }
}
