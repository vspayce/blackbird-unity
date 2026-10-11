// First-open setup. This assembly depends on nothing outside the editor, so it compiles in a bare
// project and installs the packages the game needs; the game's own assemblies only compile once
// those packages are present (see the defineConstraints in BlackBird.asmdef).
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace BlackBird.Setup
{
    [InitializeOnLoad]
    static class ProjectSetup
    {
        // Added by name, so Unity picks the version that matches this editor.
        static readonly string[] Needed =
        {
            "com.unity.render-pipelines.universal",
            "com.unity.inputsystem",
            "com.unity.ugui",
        };
        const string McpPackage = "com.coplaydev.unity-mcp";
        const string McpSource = "https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity";

        static ListRequest list;
        static AddAndRemoveRequest add;

        static ProjectSetup()
        {
            if (SessionState.GetBool("BlackBird.SetupChecked", false)) return;
            SessionState.SetBool("BlackBird.SetupChecked", true);
            EditorApplication.delayCall += Check;
        }

        [MenuItem("Black Bird/Setup/Install Packages")]
        static void Check()
        {
            list = Client.List(true, false);
            EditorApplication.update += WaitList;
        }

        static void WaitList()
        {
            if (!list.IsCompleted) return;
            EditorApplication.update -= WaitList;
            if (list.Status != StatusCode.Success) { Debug.LogWarning("[Black Bird] Could not list packages: " + list.Error?.message); return; }
            var have = new HashSet<string>(list.Result.Select(p => p.name));
            var missing = Needed.Where(n => !have.Contains(n)).ToList();
            if (!have.Contains(McpPackage)) missing.Add(McpSource);
            if (missing.Count == 0) { Debug.Log("[Black Bird] Packages are in place."); return; }
            Debug.Log("[Black Bird] Installing: " + string.Join(", ", missing));
            add = Client.AddAndRemove(missing.ToArray(), null);
            EditorApplication.update += WaitAdd;
        }

        static void WaitAdd()
        {
            if (!add.IsCompleted) return;
            EditorApplication.update -= WaitAdd;
            if (add.Status == StatusCode.Success) Debug.Log("[Black Bird] Packages installed. Next: Black Bird ▸ Build Burritt Alley.");
            else Debug.LogError("[Black Bird] Package install failed: " + add.Error?.message);
        }
    }

    // How the exported models come in. Characters (and any mocap dropped into Art/Animations) are
    // Humanoid, so animation from any humanoid source retargets onto the cast.
    class ModelImport : AssetPostprocessor
    {
        static readonly string[] Looping = { "idle", "walk", "run", "talk", "stroll", "locomotion" };

        void OnPreprocessModel()
        {
            var mi = (ModelImporter)assetImporter;
            var p = assetPath.Replace('\\', '/');
            if (!p.StartsWith("Assets/BlackBird/Art/")) return;

            mi.importCameras = false;
            mi.importLights = false;
            mi.globalScale = 1f;
            mi.useFileScale = true;
            mi.bakeAxisConversion = true;

            if (p.Contains("/Set/"))
            {
                mi.animationType = ModelImporterAnimationType.None;
                mi.importAnimation = false;
                mi.addCollider = false;
                mi.isReadable = false;
                mi.meshCompression = ModelImporterMeshCompression.Medium;
                External(mi);
            }
            else if (p.Contains("/Characters/") || p.Contains("/Animations/"))
            {
                mi.animationType = ModelImporterAnimationType.Human;
                mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                mi.importAnimation = true;
                mi.optimizeGameObjects = false;
                mi.meshCompression = ModelImporterMeshCompression.Low;
                External(mi);
                mi.resampleCurves = true;
                mi.animationCompression = ModelImporterAnimationCompression.Optimal;
            }
        }

        // Textures exported beside the models: normal maps flagged, sizes kept phone/WebGL friendly.
        void OnPreprocessTexture()
        {
            var p = assetPath.Replace('\\', '/');
            if (!p.StartsWith("Assets/BlackBird/Art/")) return;
            var ti = (TextureImporter)assetImporter;
            var n = System.IO.Path.GetFileNameWithoutExtension(p).ToLowerInvariant();
            if (n.EndsWith("_n") || n.Contains("normal") || n.EndsWith("_nrm"))
            {
                ti.textureType = TextureImporterType.NormalMap;
                ti.sRGBTexture = false;
            }
            ti.maxTextureSize = p.Contains("/Set/") ? 1024 : 2048;
            ti.mipmapEnabled = true;
            ti.anisoLevel = p.Contains("/Set/") ? 4 : 1;
        }

        // Materials as editable .mat files in a Materials folder beside the model (so the scene builder
        // can tune them), named model_material so the cast's shared MakeHuman names don't collide, and
        // textures found by file name in the Textures folder beside the model.
        static void External(ModelImporter mi)
        {
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
#pragma warning disable 618
            mi.materialLocation = ModelImporterMaterialLocation.External;
#pragma warning restore 618
            mi.materialName = ModelImporterMaterialName.BasedOnModelNameAndMaterialName;
            mi.materialSearch = ModelImporterMaterialSearch.Local;
        }

        // Loop the cyclic clips and keep them in place: the game moves the characters itself and
        // scales the playback to their real speed, so feet don't slide.
        void OnPreprocessAnimation()
        {
            var p = assetPath.Replace('\\', '/');
            if (!p.StartsWith("Assets/BlackBird/Art/")) return;
            var mi = (ModelImporter)assetImporter;
            var clips = mi.clipAnimations.Length > 0 ? mi.clipAnimations : mi.defaultClipAnimations;
            foreach (var c in clips)
            {
                var n = c.name.ToLowerInvariant();
                bool loop = Looping.Any(n.Contains);
                c.loopTime = loop;
                c.loopPose = loop && !n.Contains("walk") && !n.Contains("run");
                c.lockRootRotation = true;
                c.keepOriginalOrientation = true;
                c.lockRootHeightY = true;
                c.keepOriginalPositionY = true;
                c.lockRootPositionXZ = false;   // keeps averageSpeed meaningful for speed matching
                c.keepOriginalPositionXZ = false;
            }
            mi.clipAnimations = clips;
        }
    }
}
