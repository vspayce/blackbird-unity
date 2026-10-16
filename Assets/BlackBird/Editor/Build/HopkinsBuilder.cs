// Black Bird ▸ Hopkins ▸ Build Night / Build Day: the Mark Hopkins house on Nob Hill from Art/Hopkins
// (made by Tools/blender/hopkins_grand.py), walkable on its real floors and stairs, lit for night (every
// gasolier baked, the moon, the city's lights below) or for day (the sun, the sky, and Muybridge's 1878
// panorama, shot from this house's own tower, on the horizon). Then Black Bird ▸ Hopkins ▸ Bake Lighting.
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
    public static class HopkinsBuilder
    {
        const string Root = SceneBuilder.Root, Gen = SceneBuilder.Gen;
        const string ArtDir = Root + "/Art/Hopkins";
        const string Model = ArtDir + "/hopkins.fbx";
        const string Json = ArtDir + "/hopkins.json";
        const string Panorama = Root + "/Art/Panorama/muybridge_1878.jpg";
        public static string ScenePath(bool day) => $"{Root}/Scenes/Hopkins{(day ? "Day" : "Night")}.unity";

        // gaslight
        static readonly Color Gas = new Color(1f, 0.70f, 0.40f);
        public static float LampIntensity = 8f, LampRange = 18f, BigLampIntensity = 30f, BigLampRange = 42f, OutdoorIntensity = 10f, OutdoorRange = 16f;   // the house doubled: rooms 16 m across

        // Holmes's remarks on the things he can look at (free roam; the chapter's own lines live in the case)
        static readonly Dictionary<string, string> Lines = new Dictionary<string, string>
        {
            ["archive"] = "The Association's archive. Minutes, receipts and every student since 'seventy-four, filed by a careful hand.",
            ["curator"] = "A tidy desk. Too tidy: someone has been through these drawers and put everything back a quarter inch out of true.",
            ["view"] = "All of San Francisco, Watson, and not an honest light in it after midnight.",
            ["telescope"] = "Hopkins's telescope. From up here a man could read the name on every ship in the bay.",
            ["prints"] = "Engravings of Malta. The Knights' great siege, laid out as if someone wished to study it.",
            ["studio"] = "The model's throne. The cushion still holds the shape of whoever sat here last.",
            ["bedroom"] = "Mrs. Hopkins's bed, kept as she left it. The Association has the decency not to sleep in it.",
            ["casts"] = "Plaster gods for the antique class. Apollo has lost his nose and Venus her arms. Art is a hard school.",
            ["fountain"] = "The fountain still runs. Someone pays the water company for a house nobody lives in.",
            ["flood"] = "Flood's famous fence: thirty thousand dollars of bronze, polished every morning by a man who does nothing else.",
            ["tower"] = "Up we go, Watson. Mind the stair; it was built for a man who never climbed it.",
            ["tower2"] = "Up we go, Watson.",
            ["down"] = "Down again.",
        };

        [MenuItem("Black Bird/Hopkins/Build Night", priority = 10)] public static void BuildNight() => Build(false);
        [MenuItem("Black Bird/Hopkins/Build Day", priority = 11)] public static void BuildDay() => Build(true);

        [MenuItem("Black Bird/Hopkins/Bake Lighting (open scene)", priority = 12)]
        public static void Bake()
        {
            if (Lightmapping.isRunning) { Debug.Log("[Black Bird] A bake is already running."); return; }
            Lightmapping.bakeCompleted -= OnBaked; Lightmapping.bakeCompleted += OnBaked;
            Lightmapping.BakeAsync();
            Debug.Log("[Black Bird] Baking " + EditorSceneManager.GetActiveScene().name + "…");
        }
        static void OnBaked()
        {
            Lightmapping.bakeCompleted -= OnBaked;
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log($"[Black Bird] Bake finished: {LightmapSettings.lightmaps.Length} lightmaps for {EditorSceneManager.GetActiveScene().name}.");
        }

        public static void Build(bool day)
        {
            if (!AssetDatabase.LoadAssetAtPath<GameObject>(Model)) { Debug.LogError($"[Black Bird] {Model} is missing: run Tools/blender/hopkins_grand.py."); return; }
            var data = D(Parse(File.ReadAllText(Json)));
            Directory.CreateDirectory(Gen); Directory.CreateDirectory(Root + "/Scenes");
            SceneBuilder.EnsurePipeline();
            TPose.EnforceOnCast();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ---- the house ----
            var set = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Model));
            set.name = "Hopkins";
            CheckAxes(set, data);
            TuneMaterials(set, day);
            var flags = StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic | StaticEditorFlags.BatchingStatic;
            foreach (var t in set.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags);
            // walk on the real floors and stairs: every surface collides except light itself
            foreach (var mf in set.GetComponentsInChildren<MeshFilter>())
            {
                var mat = mf.GetComponent<Renderer>().sharedMaterial;
                string n = mat ? Bare(mat.name) : "";
                // glass lets the sun and the moon through: the rooms are lit through their windows in the bake
                if (n.StartsWith("glass") || n == "stained" || n == "lamp_glass")
                    mf.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                if (n == "lamp_glass" || n == "candle" || n.StartsWith("glass_lit")) continue;
                mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
            }
            var boxes = new GameObject("Colliders").transform;
            foreach (var c in L(data["colliders"]).Select(L))
            {
                var b = Web.Box(F(c[0]), F(c[1]), F(c[2]), F(c[3]), F(c[4]), F(c[5]));
                var bc = new GameObject("Box").AddComponent<BoxCollider>();
                bc.transform.SetParent(boxes, false); bc.transform.position = b.center; bc.size = b.size;
            }

            // ---- light ----
            var lamps = new GameObject("Gaslight").transform;
            var lampPts = L(data["lamps"]).Select(p => { var a = L(p); return Web.W(F(a[0]), F(a[1]), F(a[2])); }).ToList();
            var hallBox = L(data["hall"]).Select(F).ToArray();   // x0 x1 z0 z1 (web)
            // indoors: the house and the palm house (rectangles from the export, in web x0 x1 z0 z1)
            var indoor = new List<float[]> { L(data["house"]).Select(F).ToArray() };
            if (data.TryGetValue("palm", out var palmRects)) indoor.AddRange(L(palmRects).Select(r => L(r).Select(F).ToArray()));
            foreach (var p in lampPts)
            {
                bool big = p.y > 6 && InHall(p, hallBox) || p.y > RoomH(data) * 2.4f;
                var web = new Vector3(p.x * Web.SX, p.y, p.z * Web.SZ);
                bool outdoor = !indoor.Any(r => web.x > r[0] - 0.3f && web.x < r[1] + 0.3f && web.z > r[2] - 0.3f && web.z < r[3] + 0.3f);
                var l = new GameObject("Gasolier").AddComponent<Light>();
                l.transform.SetParent(lamps, false); l.transform.position = p + Vector3.down * 0.15f;
                l.type = LightType.Point; l.color = Gas;
                l.intensity = big ? BigLampIntensity : outdoor ? OutdoorIntensity : LampIntensity;
                l.range = big ? BigLampRange : outdoor ? OutdoorRange : LampRange;
                l.lightmapBakeType = LightmapBakeType.Baked;
                l.shadows = LightShadows.Soft; l.shapeRadius = 0.08f;
                l.enabled = !day;   // by day the gas is off
            }
            var sunGo = new GameObject(day ? "Sun" : "Moon");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.lightmapBakeType = LightmapBakeType.Mixed;
            sun.shadows = LightShadows.Soft; sun.shadowStrength = day ? 0.85f : 0.6f;
            if (day)
            {
                // a clear afternoon, the sun over the Pacific: from the west-south-west, 38° up
                sun.color = new Color(1f, 0.95f, 0.86f); sun.intensity = 1.9f;
                sun.transform.rotation = Quaternion.Euler(36, 32, 0);   // afternoon sun from the south-west, full on the California Street front
            }
            else
            {
                // the moon behind the house over the bay; light the street front softly from the other side
                sun.color = SceneBuilder.Hex("#9ab0d0"); sun.intensity = 0.55f;
                sun.transform.rotation = Quaternion.LookRotation(-Web.W(-30, 45, 60).normalized);
            }
            RenderSettings.sun = sun;

            // ---- sky, fog, grade ----
            Material sky = null;
            if (day)
            {
                sky = new Material(Shader.Find("Skybox/Procedural"));
                sky.SetFloat("_SunSize", 0.035f); sky.SetFloat("_AtmosphereThickness", 1.35f);   // coal smoke and sea haze, 1878
                sky.SetColor("_SkyTint", new Color(0.62f, 0.62f, 0.62f)); sky.SetColor("_GroundColor", new Color(0.42f, 0.4f, 0.36f));
                sky.SetFloat("_Exposure", 1.15f);
                sky = SaveMat(sky, "HopkinsDaySky");
                RenderSettings.skybox = sky;
                RenderSettings.ambientMode = AmbientMode.Skybox; RenderSettings.ambientIntensity = 1f;
                RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogColor = new Color(0.74f, 0.73f, 0.69f); RenderSettings.fogDensity = 0.0004f;   // a light haze; the panorama carries the distance
            }
            else
            {
                RenderSettings.skybox = null;
                RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = new Color(0.14f, 0.18f, 0.27f);
                RenderSettings.ambientEquatorColor = new Color(0.08f, 0.09f, 0.12f);
                RenderSettings.ambientGroundColor = new Color(0.04f, 0.035f, 0.03f);
                RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogColor = SceneBuilder.Hex("#101823"); RenderSettings.fogDensity = 0.0035f;
            }
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;

            var grade = new GameObject("Grade").AddComponent<Volume>();
            grade.isGlobal = true;
            grade.sharedProfile = SceneBuilder.Profile(day ? "HopkinsDayGrade" : "HopkinsNightGrade", p =>
            {
                p.Add<Tonemapping>(true).mode.Override(TonemappingMode.ACES);
                var bloom = p.Add<Bloom>(true); bloom.threshold.Override(day ? 1.2f : 0.95f); bloom.intensity.Override(day ? 0.35f : 0.9f); bloom.scatter.Override(0.7f);
                var ca = p.Add<ColorAdjustments>(true);
                if (day) { ca.postExposure.Override(0.1f); ca.contrast.Override(12f); ca.saturation.Override(-8f); }
                else { ca.postExposure.Override(0.85f); ca.contrast.Override(14f); ca.saturation.Override(-10f); ca.colorFilter.Override(new Color(0.95f, 0.97f, 1.03f)); }
                var v = p.Add<Vignette>(true); v.intensity.Override(day ? 0.22f : 0.3f); v.smoothness.Override(0.45f);
                var g = p.Add<FilmGrain>(true); g.type.Override(FilmGrainLookup.Thin1); g.intensity.Override(day ? 0.12f : 0.2f);
            });
            var focusVol = new GameObject("FocusGrade").AddComponent<Volume>();
            focusVol.isGlobal = true; focusVol.priority = 10; focusVol.weight = 0;
            focusVol.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>($"{Gen}/FocusGrade.asset");

            // ---- camera ----
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = day ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
            cam.backgroundColor = SceneBuilder.Hex("#0a1018");
            cam.fieldOfView = 55; cam.nearClipPlane = 0.12f; cam.farClipPlane = 5000f;   // the city is far off; keep depth precision for the house
            camGo.AddComponent<AudioListener>();
            var cd = camGo.AddComponent<UniversalAdditionalCameraData>();
            cd.renderPostProcessing = true; cd.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            var rig = camGo.AddComponent<CameraRig>();
            rig.blockers = ~(1 << 2);

            // ---- Holmes and Watson ----
            var ctrl = SceneBuilder.BuildController("People", false, out float walk, out float run);
            var holmes = SceneBuilder.Person("Holmes", "holmes", ctrl, walk, run, true);
            var watson = SceneBuilder.Person("Watson", "watson", ctrl, walk, run, false);
            rig.target = holmes.transform;
            var lookAt = new GameObject("LookAt").transform; lookAt.SetParent(holmes.transform, false); lookAt.localPosition = new Vector3(0, 1.62f, 0);

            // ---- systems ----
            var systems = new GameObject("Systems");
            systems.AddComponent<GameInput>();
            var focus = systems.AddComponent<Focus>(); focus.volume = focusVol;
            var hud = systems.AddComponent<Hud>();
            hud.serif = AssetDatabase.LoadAssetAtPath<Font>(Root + "/Fonts/IMFellEnglishSC-Regular.ttf");
            var ex = systems.AddComponent<Explorer>(); ex.holmes = holmes; ex.watson = watson; ex.cameraRig = rig;
            var roam = systems.AddComponent<Roam>();
            roam.ex = ex;
            roam.title = "The Mark Hopkins Institute of Art";
            roam.opening = day ? "The Hopkins house by daylight, Watson. Muybridge photographed the whole city from that tower."
                               : "The Hopkins house, Watson. Forty rooms, a pipe organ, and a tower that sees the whole city.";
            FillRoam(roam, data);
            // by night the house is Chapter IV: the Fat Man is upstairs, and the case runs instead of the free walk
            if (!day) ChapterFour(systems, roam, holmes, watson, rig, ctrl, walk, run);
            systems.AddComponent<SceneSwitch>();
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            if (day) { DayBackdrop(data); Daylight(data); } else City();

            // ---- probes ----
            Physics.SyncTransforms();
            LightProbes(data);
            ReflectionProbes(data);

            // ---- baking ----
            Lightmapping.lightingSettings = LightingSettingsAsset(day);
            var path = ScenePath(day);
            EditorSceneManager.SaveScene(scene, path);
            AddToBuild(path);
            Debug.Log($"[Black Bird] Built {path}: {lampPts.Count} gasoliers. Next: Black Bird ▸ Hopkins ▸ Bake Lighting.");
        }

        // ---- checks and materials ----------------------------------------------------------------------
        static void CheckAxes(GameObject set, Dictionary<string, object> data)
        {
            var m = set.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "Lamp_0");
            var a = L(L(data["lamps"])[0]);
            if (!m) { Debug.LogWarning("[Black Bird] No Lamp_0 marker in the house."); return; }
            var want = Web.W(F(a[0]), F(a[1]), F(a[2]));
            if (Vector3.Distance(m.position, want) > 0.5f) Debug.LogError($"[Black Bird] The house came in at a different orientation: Lamp_0 at {m.position}, expected {want}.");
        }

        static bool InHall(Vector3 p, float[] h)
        {
            var a = Web.W(h[0], 0, h[2]); var b = Web.W(h[1], 0, h[3]);
            return p.x > Mathf.Min(a.x, b.x) && p.x < Mathf.Max(a.x, b.x) && p.z > Mathf.Min(a.z, b.z) && p.z < Mathf.Max(a.z, b.z);
        }

        static string Bare(string matName) { int i = matName.IndexOf('-'); return (i >= 0 ? matName.Substring(i + 1) : matName).ToLowerInvariant(); }

        static void TuneMaterials(GameObject set, bool day)
        {
            SceneBuilder.TuneSetMaterials(set);
            foreach (var r in set.GetComponentsInChildren<Renderer>())
            foreach (var m in r.sharedMaterials)
            {
                if (!m) continue;
                var n = Bare(m.name);
                if (n == "glass_clear" || n == "glass_cons")
                {
                    // thin clear glass: you see the palms and the city through it, and the sky and lamps in it
                    SceneBuilder.Transparent(m, false);
                    m.SetColor("_BaseColor", n == "glass_cons" ? new Color(0.62f, 0.7f, 0.72f, 0.18f) : new Color(0.55f, 0.62f, 0.68f, 0.14f));
                    m.SetFloat("_Smoothness", 0.86f); m.SetFloat("_Metallic", 0);
                    m.SetFloat("_Cull", 0);
                    m.SetTexture("_BaseMap", null);
                }
                else if (n == "frond" || n == "fernleaf")
                {
                    // leaflets cut out of the frond texture, seen from both sides
                    m.SetFloat("_AlphaClip", 1); m.EnableKeyword("_ALPHATEST_ON"); m.SetFloat("_Cutoff", 0.45f);
                    m.SetFloat("_Cull", 0); m.SetOverrideTag("RenderType", "TransparentCutout");
                    m.renderQueue = (int)RenderQueue.AlphaTest;
                    m.SetColor("_BaseColor", Color.white); m.SetFloat("_Smoothness", 0.35f);
                }
                else if (n == "water")
                {
                    m.SetColor("_BaseColor", new Color(0.16f, 0.22f, 0.24f)); m.SetFloat("_Smoothness", 0.97f); m.SetFloat("_Metallic", 0.1f);
                }
                else if (n == "stained" || n == "candle")
                {
                    // the skylight's colours glow by themselves (and light the hall in the bake)
                    var tex = m.GetTexture("_BaseMap");
                    m.EnableKeyword("_EMISSION");
                    if (tex) m.SetTexture("_EmissionMap", tex);
                    m.SetColor("_EmissionColor", n == "candle" ? new Color(1f, 0.75f, 0.45f) * 3f : Color.white * (day ? 1.6f : 0.35f));
                    m.SetColor("_BaseColor", n == "candle" ? new Color(1f, 0.8f, 0.5f) : Color.white * 0.3f);
                    m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
                }
                else if (n == "lamp_glass")
                {
                    // a gas mantle's glow: warm white at the heart, enough to bloom a little, not a blob
                    m.SetColor("_EmissionColor", day ? new Color(0.25f, 0.22f, 0.18f) : new Color(1f, 0.82f, 0.58f) * 2.2f);
                    m.SetColor("_BaseColor", new Color(1f, 0.92f, 0.8f));
                    m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                }
                else if (n.StartsWith("glass_lit"))
                {
                    // lit windows in the neighbours' houses: dark by day
                    m.SetColor("_EmissionColor", day ? Color.black : Color.white * 1.6f);
                }
                EditorUtility.SetDirty(m);
            }
        }

        static Material SaveMat(Material m, string name)
        {
            string path = $"{Gen}/{name}.mat";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        // ---- Chapter IV: the case on the house, its positions carried through the warp ----
        static void ChapterFour(GameObject systems, Roam roam, Walker holmes, Walker watson, CameraRig rig, RuntimeAnimatorController ctrl, float walk, float run)
        {
            var data = CaseData.Load("gutman");
            var ch = systems.AddComponent<Chapter>();
            ch.caseId = "gutman"; ch.warp = "hopkins"; ch.nextScene = null;
            ch.holmes = holmes; ch.watson = watson; ch.cameraRig = rig;
            var cast = new List<CastMember>();
            foreach (var p in data.people.Values.Where(p => p.id != "watson" && p.pos != null))
            {
                if (!AssetDatabase.LoadAssetAtPath<GameObject>(SceneBuilder.CharModel(p.model ?? p.id))) { Debug.LogWarning($"[Black Bird] No model for {p.id}."); continue; }
                var w = SceneBuilder.Person(p.name, p.model ?? p.id, ctrl, walk, run, false);
                w.transform.position = ch.P(p.pos);
                cast.Add(new CastMember { id = p.id, walker = w });
            }
            ch.cast = cast.ToArray();
            // the set's places: the case's spots by their world ids; whatever no spot uses stays usable (the tower stair)
            ch.places = roam.places.Select(pl => new NamedPlace { id = pl.id, pos = pl.pos }).ToArray();
            var taken = new HashSet<string>(data.spots.Select(sp => sp.worldId ?? sp.place).Where(x => x != null));
            ch.uses = roam.places.Where(pl => !taken.Contains(pl.id)).ToArray();
            ch.destinations = roam.destinations;
            ch.spawn = roam.spawn.pos; ch.spawnYaw = roam.spawn.yaw;
            Object.DestroyImmediate(roam);
        }

        // ---- the Roam data ------------------------------------------------------------------------------
        static void FillRoam(Roam roam, Dictionary<string, object> data)
        {
            roam.rooms = L(data["rooms"]).Select(D).Select(r =>
            {
                float y = F(r["y"]);
                var b = Web.Box(F(r["x0"]), y, F(r["z0"]), F(r["x1"]), y + RoomH(data), F(r["z1"]));
                return new RoomDef { id = S(r, "id"), name = S(r, "name"), box = b, upper = y > 4 };
            }).ToArray();
            roam.places = L(data["interact"]).Select(D).Select(i =>
            {
                var p = L(i["pos"]);
                Lines.TryGetValue(S(i, "id") ?? "", out var line);
                return new PlaceDef { id = S(i, "id"), label = S(i, "label"), to = S(i, "to"), line = line, pos = Web.W(F(p[0]), F(p[1]), F(p[2])), r = F(i["r"]) };
            }).ToArray();
            var tower = D(data["tower"]);
            var top = L(tower["top"]); var foot = L(tower["foot"]);
            roam.destinations = new[]
            {
                new Destination { id = "tower_room", pos = Web.W(F(top[0]), F(top[1]) + 0.05f, F(top[2])), yaw = 0 },   // facing the bay
                new Destination { id = "tower_foot", pos = Web.W(F(foot[0]), F(foot[1]) + 0.05f, F(foot[2])), yaw = 180 },
            };
            var sp = D(data["spawn"]); var spos = L(sp["pos"]);
            roam.spawn = new Destination { id = "spawn", pos = Web.W(F(spos[0]), F(spos[1]) + 0.05f, F(spos[2])), yaw = 0 };   // facing the house
            var h = L(data["hall"]).Select(F).ToArray();
            roam.hall = Web.Box(h[0], 0, h[2], h[1], 20, h[3]);
            float wall = F(data["wall"]);
            roam.streetZ = Web.W(0, 0, wall).z;
            roam.streetIsPositive = Web.W(0, 0, wall + 1).z > roam.streetZ;
        }

        // ---- the view ----------------------------------------------------------------------------------
        static void City()
        {
            var go = new GameObject("NightCity");
            var city = go.AddComponent<NightCity>();
            var glowShader = Shader.Find("BlackBird/Glow (no fog)");
            var lights = new Material(glowShader);
            lights.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{Gen}/glow.png"));
            lights.SetColor("_BaseColor", new Color(3f, 3f, 3f, 1f));
            city.lightsMaterial = SaveMat(lights, "CityLights");
            city.landMaterial = SceneBuilder.Lit("CityLand", SceneBuilder.Hex("#0d1014"), 0.05f);
            city.bayMaterial = SceneBuilder.Lit("Bay", SceneBuilder.Hex("#05080c"), 0.85f, 0.6f);
            var hills = new Material(Shader.Find("Universal Render Pipeline/Unlit")); hills.SetColor("_BaseColor", SceneBuilder.Hex("#0b0f16")); hills.SetFloat("_Cull", 0);
            city.hillsMaterial = SaveMat(hills, "FarHills");
            var moon = new Material(glowShader);
            moon.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{Gen}/glow.png"));
            moon.SetColor("_BaseColor", new Color(2.6f, 2.6f, 2.4f, 1f));
            city.moonMaterial = SaveMat(moon, "Moon");
        }

        static void DayBackdrop(Dictionary<string, object> data)
        {
            var go = new GameObject("Muybridge1878");
            var pano = go.AddComponent<Panorama>();
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(Panorama);
            if (!tex) { Debug.LogWarning($"[Black Bird] {Panorama} missing; no panorama on the horizon."); return; }
            var m = new Material(Shader.Find("BlackBird/Panorama"));
            m.SetTexture("_BaseMap", tex); m.SetColor("_BaseColor", new Color(1.05f, 1.0f, 0.92f));
            m.SetColor("_Haze", new Color(0.80f, 0.79f, 0.76f));
            pano.material = SaveMat(m, "Muybridge1878");
            var top = L(D(data["tower"])["top"]);
            pano.centre = Web.W(F(top[0]), F(top[1]) + 2f, F(top[2]));      // the tower, where Muybridge stood
            pano.radius = 900f;
            pano.horizon = 0.30f;                  // the far hills sit about 30% down the plates
            pano.heading = -150f;                  // Alcatraz and Angel Island (40% along the strip) over the game's bay
        }

        // ---- daylight indoors ----
        // The sun comes through the windows, but a house this deep needs the sky's light too: the hall's laylight
        // pours down as a broad soft source, and each room gets the even glow of daylight off its windows.
        static void Daylight(Dictionary<string, object> data)
        {
            var parent = new GameObject("Daylight").transform;
            var h = L(data["hall"]).Select(F).ToArray();
            float hh = data.TryGetValue("hall_h", out var hho) ? F(hho) : 14f;
            var hb = Web.Box(h[0], hh - 0.3f, h[2], h[1], hh - 0.3f, h[3]);
            var lay = new GameObject("Laylight").AddComponent<Light>();
            lay.transform.SetParent(parent, false); lay.transform.position = hb.center; lay.transform.rotation = Quaternion.Euler(90, 0, 0);
            lay.type = LightType.Rectangle; lay.areaSize = new Vector2(hb.size.x * 0.8f, hb.size.z * 0.8f);
            lay.color = new Color(1f, 0.96f, 0.88f); lay.intensity = DayLaylight;
            lay.lightmapBakeType = LightmapBakeType.Baked;
            foreach (var r in L(data["rooms"]).Select(D))
            {
                float y = F(r["y"]);
                var b = Web.Box(F(r["x0"]), y + RoomH(data) * 0.64f, F(r["z0"]), F(r["x1"]), y + RoomH(data) * 0.64f, F(r["z1"]));
                var l = new GameObject("Day_" + S(r, "id")).AddComponent<Light>();
                l.transform.SetParent(parent, false); l.transform.position = b.center;
                l.type = LightType.Point; l.color = new Color(0.93f, 0.95f, 1f);
                l.intensity = DayRoomFill; l.range = Mathf.Max(b.size.x, b.size.z) * 0.9f + 2f;
                l.lightmapBakeType = LightmapBakeType.Baked; l.shadows = LightShadows.Soft; l.shapeRadius = 1.2f;
            }
        }
        const float DayLaylight = 6f, DayRoomFill = 3f;
        static float RoomH(Dictionary<string, object> data) => data.TryGetValue("room_h", out var v) ? F(v) : 6.6f;

        // ---- probes --------------------------------------------------------------------------------------
        static void LightProbes(Dictionary<string, object> data)
        {
            var pts = new List<Vector3>();
            foreach (var r in L(data["rooms"]).Select(D))
            {
                float y = F(r["y"]);
                var b = Web.Box(F(r["x0"]), y, F(r["z0"]), F(r["x1"]), y, F(r["z1"]));
                for (float x = b.min.x + 0.8f; x <= b.max.x - 0.8f; x += 3f)
                for (float z = b.min.z + 0.8f; z <= b.max.z - 0.8f; z += 3f)
                { pts.Add(new Vector3(x, y + 0.7f, z)); pts.Add(new Vector3(x, y + 2.2f, z)); pts.Add(new Vector3(x, y + RoomH(data) * 0.6f, z)); }
            }
            // the hall's height, and the grounds (dropped onto whatever is there)
            var h = L(data["hall"]).Select(F).ToArray();
            var hb = Web.Box(h[0], 0, h[2], h[1], 0, h[3]);
            float hh = data.TryGetValue("hall_h", out var hho) ? F(hho) : 14f;
            for (float x = hb.min.x + 1; x <= hb.max.x - 1; x += 3.5f)
            for (float z = hb.min.z + 1; z <= hb.max.z - 1; z += 3.5f)
            for (float y = 3.5f; y <= hh - 2f; y += 3f) pts.Add(new Vector3(x, y, z));
            for (float x = -96; x <= 96; x += 8)
            for (float z = -96; z <= 96; z += 8)
            {
                if (Physics.Raycast(new Vector3(x, 90, z), Vector3.down, out var hit, 180))
                { pts.Add(hit.point + Vector3.up * 0.8f); pts.Add(hit.point + Vector3.up * 2.6f); }
            }
            var g = new GameObject("LightProbes").AddComponent<LightProbeGroup>();
            g.probePositions = pts.ToArray();
        }

        static void ReflectionProbes(Dictionary<string, object> data)
        {
            var parent = new GameObject("ReflectionProbes").transform;
            void Probe(string name, Bounds b)
            {
                var p = new GameObject(name).AddComponent<ReflectionProbe>();
                p.transform.SetParent(parent, false);
                p.transform.position = b.center;
                p.size = b.size; p.boxProjection = true;
                p.mode = ReflectionProbeMode.Baked; p.resolution = 128; p.hdr = true;
            }
            foreach (var r in L(data["rooms"]).Select(D))
            {
                float y = F(r["y"]);
                Probe("Room_" + S(r, "id"), Web.Box(F(r["x0"]), y + 0.1f, F(r["z0"]), F(r["x1"]), y + RoomH(data) - 0.2f, F(r["z1"])));
            }
            var h = L(data["hall"]).Select(F).ToArray();
            float hh = data.TryGetValue("hall_h", out var hho) ? F(hho) : 14f;
            Probe("Hall", Web.Box(h[0], 0.1f, h[2], h[1], hh, h[3]));
            if (data.TryGetValue("grand", out var g) && g is bool gb && gb)
            {
                var pr = L(data["palm"]).Select(r => L(r).Select(F).ToArray()).ToArray();
                if (pr.Length > 0) Probe("PalmHouse", Web.Box(pr[0][0], 0.1f, pr[0][2], pr[0][1], 23f, pr[0][3]));
                if (pr.Length > 1) Probe("PalmHouseTransept", Web.Box(pr[1][0], 0.1f, pr[1][2], pr[1][1], 15f, pr[1][3]));
            }
            var outside = new GameObject("Outside").AddComponent<ReflectionProbe>();
            outside.transform.SetParent(parent, false); outside.transform.position = Web.W(0, 4, F(data["wall"]) + 4f);
            outside.size = new Vector3(200, 60, 200); outside.mode = ReflectionProbeMode.Baked; outside.resolution = 128; outside.importance = 0;
        }

        static LightingSettings LightingSettingsAsset(bool day)
        {
            string path = $"{Gen}/Hopkins{(day ? "Day" : "Night")}Lighting.lighting";
            var ls = AssetDatabase.LoadAssetAtPath<LightingSettings>(path);
            if (!ls) { ls = new LightingSettings(); AssetDatabase.CreateAsset(ls, path); }
            ls.bakedGI = true; ls.realtimeGI = false;
            ls.lightmapper = LightingSettings.Lightmapper.ProgressiveGPU;
            ls.mixedBakeMode = MixedLightingMode.IndirectOnly;
            ls.lightmapResolution = 6f;          // texels per metre: ~17 cm, plenty under gaslight
            ls.lightmapMaxSize = 2048;
            ls.lightmapPadding = 4;
            ls.directSampleCount = 32; ls.indirectSampleCount = 160; ls.environmentSampleCount = 96;
            ls.maxBounces = 3;
            ls.directionalityMode = LightmapsMode.NonDirectional;
            ls.ao = true; ls.aoMaxDistance = 1.2f; ls.aoExponentDirect = 0.3f; ls.aoExponentIndirect = 1.1f;
            ls.lightmapCompression = LightmapCompression.NormalQuality;
            ls.filteringMode = LightingSettings.FilterMode.Auto;
            EditorUtility.SetDirty(ls);
            AssetDatabase.SaveAssets();
            return ls;
        }

        static void AddToBuild(string path)
        {
            var list = EditorBuildSettings.scenes.Where(s => s.path != path).ToList();
            if (!list.Any(s => s.path.EndsWith("BurrittAlley.unity"))) list.Insert(0, new EditorBuildSettingsScene($"{Root}/Scenes/BurrittAlley.unity", true));
            list.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = list.ToArray();
        }
    }
}
