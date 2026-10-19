// A look at a set's materials from the command line (batch mode): name, shader, colour, base map and its alpha.
using UnityEditor;
using UnityEngine;

namespace BlackBird.EditorTools
{
    public static class MaterialProbe
    {
        public static void Run()
        {
            var args = System.Environment.GetCommandLineArgs();
            int k = System.Array.IndexOf(args, "-probePath");
            var path = k >= 0 && k + 1 < args.Length ? args[k + 1] : "Assets/BlackBird/Art/Stmark/stmark.fbx";
            var filter = k >= 0 && k + 2 < args.Length && !args[k + 2].StartsWith("-") ? args[k + 2].ToLowerInvariant() : "";
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (!(o is Material m)) continue;
                if (filter != "" && !m.name.ToLowerInvariant().Contains(filter)) continue;
                var t = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") as Texture2D : null;
                Debug.Log($"[MatProbe] {m.name} shader={m.shader.name} base={(m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor").ToString() : "-")} map={(t ? t.name + " alphaIsTransparency/format=" + t.format : "none")} clip={(m.HasProperty("_AlphaClip") ? m.GetFloat("_AlphaClip") : -1)} surface={(m.HasProperty("_Surface") ? m.GetFloat("_Surface") : -1)}");
            }
            // remapped (extracted) materials used by the scene
            var imp = AssetImporter.GetAtPath(path) as ModelImporter;
            if (imp != null) foreach (var kv in imp.GetExternalObjectMap()) if (kv.Value is Material em && (filter == "" || em.name.ToLowerInvariant().Contains(filter)))
            {
                var t = em.HasProperty("_BaseMap") ? em.GetTexture("_BaseMap") : null;
                Debug.Log($"[MatProbe] external {kv.Key.name} -> {AssetDatabase.GetAssetPath(em)} shader={em.shader.name} base={(em.HasProperty("_BaseColor") ? em.GetColor("_BaseColor").ToString() : "-")} map={(t ? t.name : "none")}");
            }
        }
    }
}
