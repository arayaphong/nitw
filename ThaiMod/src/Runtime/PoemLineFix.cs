// Restores poem line spacing with the Thai font. ShowPoemLine places each new
// line at poemStarts[column] + poemColumnLines * (fresh clone bounds height)
// downward. A runtime custom font has no line spacing, so that height is 0
// and every line lands on the first row. This component is added to the poem
// prefab, so every clone carries it: Awake (inside Instantiate, before the
// game positions the clone) reads poemColumnLines; Start (before the clone is
// first drawn) adds the missing lines * original Goudy line height.
using System.Reflection;
using UnityEngine;

namespace NITWThai.Runtime {
    public class PoemLineFix : MonoBehaviour {
        // Bounds height of an empty/space line in the original Goudy font (world units).
        public const float OriginalLineHeight = .29464f;
        static Object owner;
        static TextMesh prefab;
        static FieldInfo linesField;
        int lines = -1;

        public static void Install(TextMesh mesh, Object dialogue) {
            owner = dialogue; prefab = mesh;
            linesField = dialogue.GetType().GetField("poemColumnLines", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (linesField == null) { Hooks.Log("poem: poemColumnLines not found; spacing not fixed"); return; }
            if (mesh.GetComponent<PoemLineFix>() == null) mesh.gameObject.AddComponent<PoemLineFix>();
        }

        void Awake() {
            var mesh = GetComponent<TextMesh>();
            if (mesh == null || mesh == prefab || owner == null || linesField == null) return;
            lines = (int)linesField.GetValue(owner);
        }

        void Start() {
            if (lines > 0) transform.position += Vector3.down * (lines * OriginalLineHeight);
        }
    }
}
