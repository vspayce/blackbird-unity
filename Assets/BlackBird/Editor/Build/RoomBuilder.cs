// Black Bird ▸ Chapters ▸ Build …: a playable scene for a chapter set in one of the web game's other sets, from
// Art/<Set>/<set>.fbx and its JSON (Tools/blender/export_rooms.py) and the chapter's case (Resources/Cases):
// the set with real colliders, its gas lamps baked, Holmes and Watson, the chapter's people where the case
// puts them, and the Chapter runner. Then Black Bird ▸ Hopkins ▸ Bake Lighting bakes it.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using BlackBird;
using static BlackBird.EditorTools.MiniJson;

namespace BlackBird.EditorTools
{
    public static class RoomBuilder
    {
        const string Root = SceneBuilder.Root, Gen = SceneBuilder.Gen;

        public class Spec
        {
            public string set, scene, caseId, next;
            public bool daylight;                 // a morning interior: soft daylight and dimmed gas
            public float fog = 0.02f;
            public Color fogColor = new Color(0.55f, 0.58f, 0.62f);
        }

        public static readonly Spec Palace = new Spec { set = "palace", scene = "PalaceHotel", caseId = "cairo", next = "StMark", daylight = true };
        public static readonly Spec StMark = new Spec { set = "stmark", scene = "StMark", caseId = "brigid", next = "HopkinsNight", daylight = true };

        [MenuItem("Black Bird/Chapters/Build Chapter II · Palace Hotel", priority = 30)] public static void BuildPalace() => Build(Palace);
        [MenuItem("Black Bird/Chapters/Build Chapter III · St. Mark", priority = 31)] public static void BuildStMark() => Build(StMark);

        public static string ScenePath(Spec s) => $"{Root}/Scenes/{s.scene}.unity";

        public static void Build(Spec spec)
        {
            string dir = $"{Root}/Art/{char.ToUpper(spec.set[0])}{spec.set.Substring(1)}";
            var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{dir}/{spec.set}.fbx");
            if (!model) { Debug.LogError($"[Black Bird] {dir}/{spec.set}.fbx is missing: run Tools/blender/export_rooms.py."); return; }
            var data = (Dictionary<string, object>)Parse(File.ReadAllText($"{dir}/{spec.set}.json"));
            var caseData = CaseData.Load(spec.caseId);
            Directory.CreateDirectory(Gen);
            SceneBuilder.EnsurePipeline();
            TPose.EnforceOnCast();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ---- the set: static, every surface collides except light and glass ----
            var set = (GameObject)PrefabUtility.InstantiatePrefab(model);
            set.name = "Set";
            SceneBuilder.TuneSetMaterials(set);
            foreach (var r in set.GetComponentsInChildren<Renderer>())
                foreach (var m in r.sharedMaterials)
                {
                    if (!m) continue;
                    var n = m.name.Contains('-') ? m.name.Substring(m.name.IndexOf('-') + 1) : m.name;
                    if (n.StartsWith("glass") && !n.StartsWith("glass_lit")) { SceneBuilder.Transparent(m, false); m.SetColor("_BaseColor", new Color(0.6f, 0.66f, 0.7f, 0.16f)); m.SetFloat("_Smoothness", 0.88f); }
                    if (n.StartsWith("view_")) { m.shader = Shader.Find("Universal Render Pipeline/Unlit"); }   // the painted street outside
                }
            var flags = StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic | StaticEditorFlags.BatchingStatic;
            foreach (var t in set.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags);
            foreach (var mf in set.GetComponentsInChildren<MeshFilter>())
            {
                var mat = mf.GetComponent<Renderer>().sharedMaterial; var n = mat ? mat.name : "";
                if (n.Contains("glass") || n.Contains("lamp_glass") || n.Contains("view_")) { mf.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off; continue; }
                mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
            }
            var boxes = new GameObject("Colliders").transform;
            float Fv(object o) => (float)(double)o;
            foreach (var c in ((List<object>)data["colliders"]).Cast<List<object>>())
            {
                var b = Web.Box(Fv(c[0]), Fv(c[1]), Fv(c[2]), Fv(c[3]), Fv(c[4]), Fv(c[5]));
                var bc = new GameObject("Box").AddComponent<BoxCollider>();
                bc.transform.SetParent(boxes, false); bc.transform.position = b.center; bc.size = b.size;
            }
            var ground = new GameObject("Ground").AddComponent<BoxCollider>(); ground.transform.SetParent(boxes, false);
            ground.center = new Vector3(0, -0.5f, 0); ground.size = new Vector3(300, 1, 300);

            // ---- light: the gas, baked; morning through the windows ----
            var gas = new Color(1f, 0.72f, 0.45f);
            foreach (var t in set.GetComponentsInChildren<Transform>().Where(t => t.name.StartsWith("Lamp_")))
            {
                var l = new GameObject("Gas").AddComponent<Light>();
                l.transform.SetParent(t, false); l.transform.localPosition = Vector3.down * 0.15f;
                l.type = LightType.Point; l.color = gas; l.intensity = spec.daylight ? 3.5f : 6f; l.range = 9f;
                l.lightmapBakeType = LightmapBakeType.Baked; l.shadows = LightShadows.Soft; l.shapeRadius = 0.08f;
            }
            var sun = new GameObject("Daylight").AddComponent<Light>();
            sun.type = LightType.Directional; sun.lightmapBakeType = LightmapBakeType.Mixed; sun.shadows = LightShadows.Soft;
            sun.color = new Color(0.86f, 0.9f, 1f); sun.intensity = spec.daylight ? 1.1f : 0.25f;
            sun.transform.rotation = Quaternion.Euler(28, 160, 0);   // a low, cool morning sun from the street side
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = spec.daylight ? new Color(0.5f, 0.55f, 0.62f) : new Color(0.12f, 0.14f, 0.2f);
            RenderSettings.ambientEquatorColor = spec.daylight ? new Color(0.36f, 0.33f, 0.3f) : new Color(0.08f, 0.08f, 0.1f);
            RenderSettings.ambientGroundColor = new Color(0.16f, 0.12f, 0.09f);
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = spec.fog; RenderSettings.fogColor = spec.fogColor;
            RenderSettings.skybox = null;

            // ---- grade and Focus ----
            var grade = new GameObject("Grade").AddComponent<Volume>(); grade.isGlobal = true;
            grade.sharedProfile = SceneBuilder.Profile(spec.scene + "Grade", p =>
            {
                p.Add<Tonemapping>(true).mode.Override(TonemappingMode.ACES);
                var bloom = p.Add<Bloom>(true); bloom.threshold.Override(1.1f); bloom.intensity.Override(0.5f); bloom.scatter.Override(0.7f);
                var ca = p.Add<ColorAdjustments>(true); ca.postExposure.Override(0.4f); ca.contrast.Override(10f); ca.saturation.Override(-10f);
                var v = p.Add<Vignette>(true); v.intensity.Override(0.26f); v.smoothness.Override(0.45f);
                var g = p.Add<FilmGrain>(true); g.type.Override(FilmGrainLookup.Thin1); g.intensity.Override(0.15f);
            });
            var focusVol = new GameObject("FocusGrade").AddComponent<Volume>();
            focusVol.isGlobal = true; focusVol.priority = 10; focusVol.weight = 0;
            focusVol.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>($"{Gen}/FocusGrade.asset");

            // ---- camera ----
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = spec.fogColor;
            cam.fieldOfView = 60; cam.nearClipPlane = 0.08f; cam.farClipPlane = 400f;
            camGo.AddComponent<AudioListener>();
            var cd = camGo.AddComponent<UniversalAdditionalCameraData>(); cd.renderPostProcessing = true; cd.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            var rig = camGo.AddComponent<CameraRig>(); rig.blockers = ~(1 << 2);

            // ---- the cast ----
            var ctrl = SceneBuilder.BuildController("People", false, out float walk, out float run);
            var holmes = SceneBuilder.Person("Holmes", "holmes", ctrl, walk, run, true);
            var watson = SceneBuilder.Person("Watson", "watson", ctrl, walk, run, false);
            rig.target = holmes.transform;
            var lookAt = new GameObject("LookAt").transform; lookAt.SetParent(holmes.transform, false); lookAt.localPosition = new Vector3(0, 1.62f, 0);
            var cast = new List<CastMember>();
            foreach (var p in caseData.people.Values.Where(p => p.id != "watson" && p.pos != null))
            {
                if (!AssetDatabase.LoadAssetAtPath<GameObject>(SceneBuilder.CharModel(p.model ?? p.id))) { Debug.LogWarning($"[Black Bird] No model for {p.id} ({p.model}); export it with Tools/blender/export_unity.py."); continue; }
                var w = SceneBuilder.Person(p.name, p.model ?? p.id, ctrl, walk, run, false);
                w.transform.position = Chapter.Pos(p.pos);
                cast.Add(new CastMember { id = p.id, walker = w });
            }

            // ---- systems ----
            var systems = new GameObject("Systems");
            systems.AddComponent<GameInput>();
            var focus = systems.AddComponent<Focus>(); focus.volume = focusVol;
            var hud = systems.AddComponent<Hud>(); hud.serif = AssetDatabase.LoadAssetAtPath<Font>(Root + "/Fonts/IMFellEnglishSC-Regular.ttf");
            var ex = systems.AddComponent<Explorer>(); ex.holmes = holmes; ex.watson = watson; ex.cameraRig = rig;
            var ch = systems.AddComponent<Chapter>();
            ch.caseId = spec.caseId; ch.holmes = holmes; ch.watson = watson; ch.cameraRig = rig; ch.cast = cast.ToArray(); ch.nextScene = spec.next;
            var places = (Dictionary<string, object>)data["places"];
            ch.places = places.Select(kv => { var a = ((List<object>)kv.Value).Select(Fv).ToArray(); return new NamedPlace { id = kv.Key, pos = Web.W(a[0], a.Length > 2 ? a[1] : 0, a[a.Length - 1]) }; }).ToArray();
            var sp = (Dictionary<string, object>)data["spawn"]; var spos = ((List<object>)sp["pos"]).Select(Fv).ToArray();
            ch.spawn = Web.W(spos[0], spos[1], spos[2]);
            ch.spawnYaw = Mathf.Repeat(180f - (float)(double)sp["yaw"] * Mathf.Rad2Deg, 360f);
            if (sp.TryGetValue("watson", out var wo)) { var a = ((List<object>)wo).Select(Fv).ToArray(); ch.watsonOffset = new Vector3(a[0], a[1], -a[2]); }
            systems.AddComponent<SceneSwitch>();
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            // ---- probes and baking settings ----
            var b0 = new Bounds(Vector3.zero, Vector3.zero);
            foreach (var r in set.GetComponentsInChildren<Renderer>()) b0.Encapsulate(r.bounds);
            var probes = new List<Vector3>();
            for (float x = b0.min.x + 0.5f; x < b0.max.x; x += 2f)
                for (float z = b0.min.z + 0.5f; z < b0.max.z; z += 2f)
                    foreach (var y in new[] { 0.6f, 1.8f, 3.2f }) probes.Add(new Vector3(x, y, z));
            new GameObject("LightProbes").AddComponent<LightProbeGroup>().probePositions = probes.ToArray();
            var rp = new GameObject("Reflections").AddComponent<ReflectionProbe>();
            rp.transform.position = b0.center; rp.size = b0.size; rp.boxProjection = true; rp.mode = ReflectionProbeMode.Baked; rp.resolution = 128;
            Lightmapping.lightingSettings = Lighting(spec.scene);

            var path = ScenePath(spec);
            EditorSceneManager.SaveScene(scene, path);
            var list = EditorBuildSettings.scenes.ToList();
            if (!list.Any(s => s.path == path)) { list.Add(new EditorBuildSettingsScene(path, true)); EditorBuildSettings.scenes = list.ToArray(); }
            Debug.Log($"[Black Bird] Built {path}: {cast.Count} people, {ch.places.Length} places. Next: Black Bird ▸ Hopkins ▸ Bake Lighting.");
        }

        static LightingSettings Lighting(string name)
        {
            string path = $"{Gen}/{name}Lighting.lighting";
            var ls = AssetDatabase.LoadAssetAtPath<LightingSettings>(path);
            if (!ls) { ls = new LightingSettings(); AssetDatabase.CreateAsset(ls, path); }
            ls.bakedGI = true; ls.realtimeGI = false;
            ls.lightmapper = LightingSettings.Lightmapper.ProgressiveGPU;
            ls.mixedBakeMode = MixedLightingMode.IndirectOnly;
            ls.lightmapResolution = 12f; ls.lightmapMaxSize = 1024; ls.lightmapPadding = 4;
            ls.directSampleCount = 32; ls.indirectSampleCount = 160; ls.environmentSampleCount = 96; ls.maxBounces = 3;
            ls.directionalityMode = LightmapsMode.NonDirectional;
            ls.ao = true; ls.aoMaxDistance = 0.8f;
            ls.lightmapCompression = LightmapCompression.NormalQuality;
            EditorUtility.SetDirty(ls); AssetDatabase.SaveAssets();
            return ls;
        }
    }
}
