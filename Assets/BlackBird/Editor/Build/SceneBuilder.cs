// Black Bird ▸ Build Burritt Alley: assembles Chapter I's scene from the exported set and cast.
// Everything the scene needs (render pipeline, grade, materials, generated textures, animator
// controllers) is (re)made here, so the scene can be rebuilt from scratch at any time.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using BlackBird;

namespace BlackBird.EditorTools
{
    public static class SceneBuilder
    {
        const string Root = "Assets/BlackBird";
        const string Gen = Root + "/Generated";
        const string ScenePath = Root + "/Scenes/BurrittAlley.unity";
        const string SetModel = Root + "/Art/Set/burritt.fbx";
        static string CharModel(string n) => $"{Root}/Art/Characters/{n}.fbx";

        static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }

        [MenuItem("Black Bird/Build Burritt Alley", priority = 0)]
        public static void Build()
        {
            if (!AssetDatabase.LoadAssetAtPath<GameObject>(SetModel))
            {
                EditorUtility.DisplayDialog("Black Bird", $"The set isn't exported yet:\n{SetModel}\n\nRun Tools/blender/export_unity.py in Blender first (see README).", "OK");
                return;
            }
            Directory.CreateDirectory(Gen); Directory.CreateDirectory(Root + "/Scenes");

            var pipeline = EnsurePipeline();
            TPose.EnforceOnCast();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ---- the set, and which way it faces relative to the web coordinates ----
            var set = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(SetModel));
            set.name = "Set";
            set.isStatic = true;
            foreach (var t in set.GetComponentsInChildren<Transform>()) t.gameObject.isStatic = true;
            var axes = new GameObject("WorldAxes").AddComponent<WorldAxes>();
            MeasureAxes(set, axes);
            if (axes.sx != Web.SX || axes.sz != Web.SZ)
            {
                Debug.LogError($"[Black Bird] The set came in with axes sx={axes.sx} sz={axes.sz}, but Web.SX/SZ are {Web.SX}/{Web.SZ}. Update them in Chapter1.cs, then rebuild.");
                return;
            }
            TuneSetMaterials(set);
            TuneCastMaterials();
            AlleyDressing.Build(null, AssetDatabase.LoadAssetAtPath<Font>(Root + "/Fonts/IMFellEnglishSC-Regular.ttf"));

            // ---- environment ----
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.06f;
            RenderSettings.fogColor = new Color(0.17f, 0.21f, 0.26f);   // a lit fog, not a black night
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.22f, 0.28f, 0.38f);
            RenderSettings.ambientEquatorColor = new Color(0.12f, 0.14f, 0.18f);
            RenderSettings.ambientGroundColor = new Color(0.09f, 0.075f, 0.06f);
            RenderSettings.skybox = null;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.reflectionIntensity = 0.6f;

            var moon = new GameObject("Moon").AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.color = Hex("#8fa6c8"); moon.intensity = 0.55f;
            moon.transform.rotation = Quaternion.LookRotation(-Web.W(-6, 14, 8).normalized);
            moon.shadows = LightShadows.Soft; moon.shadowStrength = 0.55f;
            RenderSettings.sun = moon;

            var colliders = new GameObject("Colliders");
            foreach (var b in Chapter1.Colliders())
            {
                var bc = new GameObject("Box").AddComponent<BoxCollider>();
                bc.transform.SetParent(colliders.transform, false);
                bc.transform.position = b.center; bc.size = b.size;
            }
            var ground = new GameObject("Ground").AddComponent<BoxCollider>();
            ground.transform.SetParent(colliders.transform, false);
            ground.center = new Vector3(0, -0.5f, 0); ground.size = new Vector3(140, 1, 140);

            // gas lamps at the set's Lamp_* markers
            var lamps = new List<Light>();
            foreach (var t in set.GetComponentsInChildren<Transform>().Where(t => t.name.StartsWith("Lamp_")).OrderBy(t => t.name))
            {
                bool bracket = t.position.y < 3.6f && Mathf.Abs(t.position.x) < 4f && t.position.z * Web.SZ < 0;
                var l = new GameObject("GasLight").AddComponent<Light>();
                l.transform.SetParent(t, false);
                l.type = LightType.Point; l.color = Hex("#ffb468");
                l.intensity = bracket ? 14f : 24f; l.range = bracket ? 12f : 16f;
                l.shadows = LightShadows.None;
                lamps.Add(l);
            }
            var lantern = PointLight("PoliceLantern", Web.W(-0.9f, 0.5f, -17.8f), Hex("#ffc27a"), 4f, 7f);
            lamps.Add(lantern);
            var lanternGlass = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(lanternGlass.GetComponent<Collider>());
            lanternGlass.name = "LanternGlass";
            lanternGlass.transform.position = Web.W(-0.9f, 0.14f, -17.8f); lanternGlass.transform.localScale = new Vector3(0.18f, 0.14f, 0.18f);
            lanternGlass.GetComponent<Renderer>().sharedMaterial = Emissive("Lantern", Hex("#ffc27a"), 3.5f);
            var pocket = PointLight("PocketLamp", Web.W(1.0f, 1.0f, -19.6f), Hex("#ffd7a0"), 1.8f, 4f);
            pocket.enabled = false;

            // ---- grade ----
            var post = new GameObject("Grade").AddComponent<Volume>();
            post.isGlobal = true; post.priority = 0;
            post.sharedProfile = Profile("Grade", p =>
            {
                p.Add<Tonemapping>(true).mode.Override(TonemappingMode.ACES);
                var bloom = p.Add<Bloom>(true); bloom.threshold.Override(1.0f); bloom.intensity.Override(0.9f); bloom.scatter.Override(0.68f); bloom.highQualityFiltering.Override(false);
                var ca = p.Add<ColorAdjustments>(true); ca.postExposure.Override(0.75f); ca.contrast.Override(14f); ca.saturation.Override(-14f); ca.colorFilter.Override(new Color(0.93f, 0.97f, 1.04f));
                p.Add<WhiteBalance>(true).temperature.Override(-14f);
                var v = p.Add<Vignette>(true); v.intensity.Override(0.33f); v.smoothness.Override(0.45f);
                var g = p.Add<FilmGrain>(true); g.type.Override(FilmGrainLookup.Medium1); g.intensity.Override(0.22f); g.response.Override(0.75f);
            });
            var focusVol = new GameObject("FocusGrade").AddComponent<Volume>();
            focusVol.isGlobal = true; focusVol.priority = 10; focusVol.weight = 0;
            focusVol.sharedProfile = Profile("FocusGrade", p =>
            {
                var ca = p.Add<ColorAdjustments>(true); ca.saturation.Override(-88f); ca.contrast.Override(22f); ca.colorFilter.Override(new Color(0.82f, 0.9f, 1.06f)); ca.postExposure.Override(0.15f);
                var v = p.Add<Vignette>(true); v.intensity.Override(0.5f); v.smoothness.Override(0.5f);
                p.Add<ChromaticAberration>(true).intensity.Override(0.22f);
                p.Add<LensDistortion>(true).intensity.Override(-0.12f);
            });

            // ---- camera ----
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = RenderSettings.fogColor * 0.8f;
            cam.fieldOfView = 55; cam.nearClipPlane = 0.08f; cam.farClipPlane = 140f;
            camGo.AddComponent<AudioListener>();
            var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            var rig = camGo.AddComponent<CameraRig>();
            rig.blockers = ~(1 << 2);   // everything but Ignore Raycast (the people)

            // ---- cast ----
            var ctrl = BuildController("People", false, out float walkSpeed, out float runSpeed);
            var archerCtrl = BuildController("Archer", true, out _, out _);
            var holmes = Person("Holmes", "holmes", ctrl, walkSpeed, runSpeed, true);
            var watson = Person("Watson", "watson", ctrl, walkSpeed, runSpeed, false);
            holmes.transform.position = Chapter1.Start;
            watson.transform.position = Chapter1.Start + Web.W(-1.1f, 0, 0.7f);
            var people = Chapter1.People.Select(d => Person(d.name, d.model, ctrl, walkSpeed, runSpeed, false)).ToArray();
            for (int i = 0; i < people.Length; i++) people[i].transform.position = Chapter1.People[i].pos;
            rig.target = holmes.transform;
            var lookAt = new GameObject("LookAt").transform; lookAt.SetParent(holmes.transform, false); lookAt.localPosition = new Vector3(0, 1.62f, 0);

            BuildArcher(archerCtrl);
            var hidden = BuildHiddenDetails(out var hiddenIds);

            // ---- systems ----
            var systems = new GameObject("Systems");
            systems.AddComponent<GameInput>();
            var focus = systems.AddComponent<Focus>(); focus.volume = focusVol;
            var hud = systems.AddComponent<Hud>();
            hud.serif = AssetDatabase.LoadAssetAtPath<Font>(Root + "/Fonts/IMFellEnglishSC-Regular.ttf");
            var atmo = systems.AddComponent<Atmosphere>();
            atmo.fogMaterial = FogMaterial(); atmo.coneMaterial = ConeMaterial(); atmo.lamps = lamps;
            var game = systems.AddComponent<Game>();
            game.holmes = holmes; game.watson = watson; game.people = people; game.cameraRig = rig; game.pocketLamp = pocket;
            game.hiddenBySpot = hidden; game.hiddenSpotIds = hiddenIds;

            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            // reflections for the wet cobbles
            var probe = new GameObject("Reflections").AddComponent<ReflectionProbe>();
            probe.transform.position = Web.W(0, 1.5f, 0);
            probe.size = new Vector3(70, 20, 50); probe.mode = ReflectionProbeMode.Baked; probe.boxProjection = false;
            probe.resolution = 128;

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Lightmapping.BakeReflectionProbe(probe, Gen + "/AlleyReflections.exr");
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Black Bird] Built {ScenePath} (axes sx={axes.sx} sz={axes.sz}, walk clip {walkSpeed:0.00} m/s).");
        }

        // ---- the set ---------------------------------------------------------------------------------
        static void MeasureAxes(GameObject set, WorldAxes axes)
        {
            // the bracket lamp on the alley wall is the last Lamp_ marker: web (≈-2, 3.35, -8)
            var lamp = set.GetComponentsInChildren<Transform>().Where(t => t.name.StartsWith("Lamp_")).OrderBy(t => t.name).LastOrDefault();
            if (!lamp) { Debug.LogWarning("[Black Bird] No Lamp_ markers in the set; assuming sx=-1, sz=1."); return; }
            var p = lamp.position;
            float bestD = float.MaxValue;
            foreach (var sx in new[] { -1f, 1f }) foreach (var sz in new[] { -1f, 1f })
            {
                float d = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(sx * -2.0f, sz * -8f));
                if (d < bestD) { bestD = d; axes.sx = sx; axes.sz = sz; }
            }
            if (bestD > 1.5f) Debug.LogWarning($"[Black Bird] The set's alley lamp is at {p}, not where expected; check the export.");
        }

        static void TuneSetMaterials(GameObject set)
        {
            // stone wants to look wet; glass and flames want to glow above the bloom threshold
            foreach (var r in set.GetComponentsInChildren<Renderer>())
                foreach (var m in r.sharedMaterials)
                {
                    if (!m) continue;
                    var n = m.name.ToLowerInvariant().Replace("burritt-", "");
                    if (m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") && !n.StartsWith("glass_lit"))
                        m.SetColor("_BaseColor", Color.white);   // the texture carries the colour
                    if (n.StartsWith("brick") || n.StartsWith("clap") || n.Contains("wood")) m.SetFloat("_Smoothness", 0.18f);
                    if (n.StartsWith("stone"))
                    { if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.62f); }
                    else if (n.StartsWith("glass_dark"))
                    { if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.92f); }
                    else if (n.StartsWith("glass_lit") || n.StartsWith("lamp_glass"))
                    {
                        // the export puts the emission image (or colour) in the base map; glow from it
                        bool lamp = n.StartsWith("lamp_glass");
                        var tex = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : null;
                        m.EnableKeyword("_EMISSION");
                        if (tex) m.SetTexture("_EmissionMap", tex);
                        var c = lamp ? Hex("#ffcf8a") : Color.white;
                        m.SetColor("_EmissionColor", c * (lamp ? 5f : 1.6f));
                        m.SetColor("_BaseColor", lamp ? c : Color.white * 0.2f);
                        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                    }
                    EditorUtility.SetDirty(m);
                }
        }

        static void TuneCastMaterials()
        {
            foreach (var g in AssetDatabase.FindAssets("t:Material", new[] { Root + "/Art/Characters" }))
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g));
                if (!m || !m.HasProperty("_Smoothness")) continue;
                var n = m.name.ToLowerInvariant();
                m.SetFloat("_Metallic", 0);
                if (n.Contains("hair") || n.Contains("eyebrow") || n.Contains("eyelash") || n.Contains("moustache") || n.Contains("beard") || n.Contains("lace"))
                {
                    // cards with alpha: cut out, both sides
                    m.SetFloat("_AlphaClip", 1); m.EnableKeyword("_ALPHATEST_ON");
                    m.SetFloat("_Cutoff", n.Contains("eyelash") ? 0.3f : 0.45f);
                    m.SetFloat("_Cull", 0);
                    m.SetOverrideTag("RenderType", "TransparentCutout");
                    m.renderQueue = (int)RenderQueue.AlphaTest;
                    m.SetFloat("_Smoothness", 0.3f);
                }
                else if (n.Contains("silk")) m.SetFloat("_Smoothness", 0.55f);
                else if (n.Contains("silver") || n.Contains("gold") || n.Contains("brass") || n.Contains("button") || n.Contains("chain"))
                { m.SetFloat("_Metallic", 0.9f); m.SetFloat("_Smoothness", 0.6f); }
                else if (n.Contains("body") || n.Contains("skin")) m.SetFloat("_Smoothness", 0.32f);
                else if (n.Contains("eye") || n.Contains("low-poly")) m.SetFloat("_Smoothness", 0.8f);
                else if (n.Contains("shoe") || n.Contains("boot") || n.Contains("leather")) m.SetFloat("_Smoothness", 0.45f);
                else m.SetFloat("_Smoothness", 0.12f);   // wool, tweed, suits
                // Blender's FBX export leaves base colour grey when a texture drives it
                if (m.GetTexture("_BaseMap") && m.GetColor("_BaseColor").maxColorComponent < 0.95f) m.SetColor("_BaseColor", Color.white);
                EditorUtility.SetDirty(m);
            }
        }

        // ---- people ----------------------------------------------------------------------------------
        static Walker Person(string name, string model, RuntimeAnimatorController ctrl, float walk, float run, bool player)
        {
            var root = new GameObject(name);
            root.layer = 2;
            var w = root.AddComponent<Walker>();
            w.walkClipSpeed = walk; w.runClipSpeed = run;
            if (player)
            {
                var cc = root.AddComponent<CharacterController>();
                cc.height = 1.8f; cc.radius = 0.28f; cc.center = new Vector3(0, 0.92f, 0); cc.stepOffset = 0.25f; cc.skinWidth = 0.03f;
            }
            else
            {
                var cc = root.AddComponent<CharacterController>();   // Watson walks too; others simply stand
                cc.height = 1.8f; cc.radius = 0.24f; cc.center = new Vector3(0, 0.92f, 0);
            }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharModel(model));
            GameObject body;
            if (prefab) body = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            else
            {
                Debug.LogWarning($"[Black Bird] {CharModel(model)} missing; using a capsule.");
                body = GameObject.CreatePrimitive(PrimitiveType.Capsule); Object.DestroyImmediate(body.GetComponent<Collider>());
                body.transform.localPosition = Vector3.up * 0.9f;
            }
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            FaceForward(body);
            foreach (var t in body.GetComponentsInChildren<Transform>()) t.gameObject.layer = 2;
            var anim = body.GetComponent<Animator>();
            if (!anim) anim = body.AddComponent<Animator>();
            anim.runtimeAnimatorController = ctrl;
            anim.applyRootMotion = false;
            body.AddComponent<IKRelay>().walker = w;
            foreach (var r in body.GetComponentsInChildren<SkinnedMeshRenderer>()) { r.updateWhenOffscreen = false; r.skinnedMotionVectors = false; }
            return w;
        }

        // turn a model so it faces its parent's +Z (Walker moves along the root's forward)
        static void FaceForward(GameObject body)
        {
            var anim = body.GetComponent<Animator>();
            if (!anim || !anim.isHuman) return;
            var l = anim.GetBoneTransform(HumanBodyBones.LeftUpperArm); var r = anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
            if (!l || !r) return;
            var right = r.position - l.position; right.y = 0;
            var fwd = Vector3.Cross(right.normalized, Vector3.up);
            var parent = body.transform.parent;
            float ang = Vector3.SignedAngle(parent.InverseTransformDirection(fwd), Vector3.forward, Vector3.up);
            body.transform.localRotation = Quaternion.Euler(0, ang, 0) * body.transform.localRotation;
        }

        static void BuildArcher(RuntimeAnimatorController ctrl)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharModel("archer"));
            var archer = new GameObject("Miles Archer");
            var body = prefab ? (GameObject)PrefabUtility.InstantiatePrefab(prefab) : GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body"; body.transform.SetParent(archer.transform, false);
            FaceForward(body);
            // on his back, head toward the fence (the web lays the standing pose down)
            archer.transform.position = Web.W(0.5f, 0.18f, -19.0f);
            archer.transform.rotation = Quaternion.LookRotation(Vector3.up, Web.W(0, 0, -1).normalized);
            var anim = body.GetComponent<Animator>(); if (anim) { anim.runtimeAnimatorController = ctrl; anim.applyRootMotion = false; anim.cullingMode = AnimatorCullingMode.AlwaysAnimate; }

            var blood = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(blood.GetComponent<Collider>());
            blood.name = "Blood";
            blood.transform.position = Web.W(0.35f, 0.004f, -20.5f); blood.transform.localScale = new Vector3(0.7f, 0.002f, 0.49f);
            blood.GetComponent<Renderer>().sharedMaterial = Lit("Blood", Hex("#2a0505"), 0.85f);

            var hat = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(hat.GetComponent<Collider>());
            hat.name = "Hat";
            hat.transform.position = Web.W(-0.5f, 0.06f, -18.2f); hat.transform.localScale = new Vector3(0.34f, 0.055f, 0.34f);
            hat.transform.rotation = Quaternion.Euler(12, 0, 17);
            hat.GetComponent<Renderer>().sharedMaterial = Lit("Hat", Hex("#28241f"), 0.15f);
        }

        // ---- what only Focus shows -------------------------------------------------------------------
        static Transform[] BuildHiddenDetails(out string[] ids)
        {
            var parent = new GameObject("FocusDetails").transform;
            var list = new List<Transform>(); var idl = new List<string>();

            // narrow heel prints toward the fence
            var heels = new GameObject("HeelPrints").transform; heels.SetParent(parent);
            var heelMat = Unlit("HeelPrint", new Color(1f, 0.82f, 0.48f, 0.9f), Tex("heel", HeelTexture), true, false);
            foreach (var (x, z, r) in new[] { (0.35f, -17.1f, 0.1f), (0.62f, -17.25f, -0.05f), (0.1f, -16.4f, 0.2f), (0.42f, -15.6f, 0.0f), (0.05f, -14.8f, 0.15f) })
            {
                var q = Quad("Heel", heelMat, heels);
                q.transform.position = Web.W(x, 0.012f, z);
                q.transform.rotation = Quaternion.LookRotation(Vector3.down, Web.W(0, 0, -1)) * Quaternion.Euler(0, 0, r * Mathf.Rad2Deg);
                q.transform.localScale = new Vector3(0.1f, 0.22f, 1);
            }
            heels.gameObject.AddComponent<HiddenDetail>().keepOnceFound = true;
            list.Add(heels); idl.Add("heel");

            // a wisp of lavender water under the alley lamp
            var wisp = new GameObject("Scent").transform; wisp.SetParent(parent);
            var wispMat = Unlit("Scent", new Color(0.78f, 0.66f, 1f, 0.5f), Tex("glow", GlowTexture), true, true);
            for (int i = 0; i < 5; i++)
            {
                var q = Quad("Wisp", wispMat, wisp);
                q.transform.position = Web.W(-1.9f + Mathf.Sin(i) * 0.2f, 1.1f + i * 0.22f, -8f);
                q.transform.localScale = Vector3.one * (0.6f + i * 0.12f);
                var bb = q.AddComponent<Billboard>(); bb.bob = true;
            }
            wisp.gameObject.AddComponent<HiddenDetail>().strength = 0.6f;
            list.Add(wisp); idl.Add("scent");

            // the revolver in the weeds past the fence, catching the light
            var gun = new GameObject("Webley").transform; gun.SetParent(parent);
            var steel = Lit("Steel", Hex("#2b2b2e"), 0.75f, 0.9f);
            var barrel = GameObject.CreatePrimitive(PrimitiveType.Cube); Object.DestroyImmediate(barrel.GetComponent<Collider>());
            barrel.transform.SetParent(gun); barrel.transform.localScale = new Vector3(0.22f, 0.03f, 0.03f); barrel.GetComponent<Renderer>().sharedMaterial = steel;
            var grip = GameObject.CreatePrimitive(PrimitiveType.Cube); Object.DestroyImmediate(grip.GetComponent<Collider>());
            grip.transform.SetParent(gun); grip.transform.localPosition = new Vector3(-0.1f, -0.04f, 0); grip.transform.localRotation = Quaternion.Euler(0, 0, -23);
            grip.transform.localScale = new Vector3(0.035f, 0.1f, 0.03f); grip.GetComponent<Renderer>().sharedMaterial = Lit("Grip", Hex("#2a1a10"), 0.3f);
            gun.position = Web.W(0.9f, -0.22f, Chapter1.FenceZ - 1.3f); gun.rotation = Quaternion.Euler(90, 0, 34);
            var glint = new GameObject("Glint").transform; glint.SetParent(parent);
            var gq = Quad("GlintQuad", Unlit("Glint", new Color(1f, 0.95f, 0.8f, 1f), Tex("glow", GlowTexture), true, true), glint);
            gq.transform.localScale = Vector3.one * 0.5f; gq.AddComponent<Billboard>();
            glint.position = Web.W(0.95f, -0.12f, Chapter1.FenceZ - 1.3f);
            var gl = new GameObject("GlintLight").AddComponent<Light>(); gl.transform.SetParent(glint, false);
            gl.type = LightType.Point; gl.color = Hex("#fff0c8"); gl.intensity = 0.8f; gl.range = 1.2f;
            var gh = glint.gameObject.AddComponent<HiddenDetail>(); gh.pulse = true; gh.keepOnceFound = false;
            list.Add(glint); idl.Add("webley");

            // powder scorch on the coat (the close-up)
            var burn = Quad("PowderBurn", Unlit("Burn", new Color(1f, 0.7f, 0.35f, 0.9f), Tex("glow", GlowTexture), true, true), parent);
            burn.transform.position = Web.W(0.6f, 0.39f, -20.38f); burn.transform.rotation = Quaternion.LookRotation(Vector3.down); burn.transform.localScale = Vector3.one * 0.18f;
            burn.AddComponent<HiddenDetail>().keepOnceFound = true;
            list.Add(burn.transform); idl.Add("burns");

            ids = idl.ToArray();
            return list.ToArray();
        }

        static GameObject Quad(string name, Material m, Transform parent)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(q.GetComponent<Collider>());
            q.name = name; q.transform.SetParent(parent, false);
            var r = q.GetComponent<Renderer>(); r.sharedMaterial = m; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            return q;
        }

        // ---- animation -------------------------------------------------------------------------------
        // Prefers motion capture dropped into Art/Animations or Unity's Starter Assets; falls back to
        // the clips exported with the cast.
        static AnimationClip FindClip(params string[] names)
        {
            var all = AssetDatabase.FindAssets("t:AnimationClip", new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath).Distinct()
                .SelectMany(p => AssetDatabase.LoadAllAssetsAtPath(p).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).Select(c => (p, c)))
                .Where(x => x.c.humanMotion)
                .ToList();
            int Rank(string p) => p.Contains("/Art/Animations/") ? 0 : p.Contains("StarterAssets") ? 1 : p.Contains("/holmes") ? 2 : 3;
            foreach (var n in names)
            {
                // Blender names FBX takes "Armature|Walk"
                var hit = all.Where(x => string.Equals(x.c.name.Split('|').Last(), n, System.StringComparison.OrdinalIgnoreCase)).OrderBy(x => Rank(x.p)).FirstOrDefault();
                if (hit.c) return hit.c;
            }
            return null;
        }

        static AnimatorController BuildController(string name, bool lying, out float walkSpeed, out float runSpeed)
        {
            string path = $"{Gen}/{name}.controller";
            AssetDatabase.DeleteAsset(path);
            var ac = AnimatorController.CreateAnimatorControllerAtPath(path);
            ac.AddParameter("Gait", AnimatorControllerParameterType.Float);
            ac.AddParameter("PlayRate", AnimatorControllerParameterType.Float);
            ac.parameters = ac.parameters.Select(p => { if (p.name == "PlayRate") p.defaultFloat = 1; return p; }).ToArray();
            ac.AddParameter("Talk", AnimatorControllerParameterType.Bool);
            ac.AddParameter("Lie", AnimatorControllerParameterType.Bool);
            var layers = ac.layers; layers[0].iKPass = true; ac.layers = layers;
            var sm = ac.layers[0].stateMachine;
            walkSpeed = 1.4f; runSpeed = 3.6f;

            if (lying)
            {
                var lie = FindClip("LieBack");
                var s = sm.AddState("LieBack"); s.motion = lie;
                return ac;
            }

            var idle = FindClip("Idle", "Stand--Idle");
            var walk = FindClip("Walk_N", "Walk", "Locomotion--Walk_N");
            var run = FindClip("Run_N", "Run", "Locomotion--Run_N");
            var talk = FindClip("Talk");
            if (walk && walk.averageSpeed.magnitude > 0.3f) walkSpeed = new Vector2(walk.averageSpeed.x, walk.averageSpeed.z).magnitude;
            if (run && run.averageSpeed.magnitude > walkSpeed) runSpeed = new Vector2(run.averageSpeed.x, run.averageSpeed.z).magnitude;
            Debug.Log($"[Black Bird] {name}: idle={Src(idle)} walk={Src(walk)} ({walkSpeed:0.00} m/s) run={Src(run)} talk={Src(talk)}");

            var tree = new BlendTree { name = "Locomotion", blendParameter = "Gait", useAutomaticThresholds = false };
            AssetDatabase.AddObjectToAsset(tree, ac);
            if (idle) tree.AddChild(idle, 0f);
            if (walk) tree.AddChild(walk, 1f);
            if (run) tree.AddChild(run, 2f);
            var loco = sm.AddState("Locomotion");
            loco.motion = tree; loco.iKOnFeet = true;
            loco.speedParameter = "PlayRate"; loco.speedParameterActive = true;
            sm.defaultState = loco;

            if (talk)
            {
                var ts = sm.AddState("Talk"); ts.motion = talk; ts.iKOnFeet = true;
                var to = loco.AddTransition(ts); to.hasExitTime = false; to.duration = 0.3f;
                to.AddCondition(AnimatorConditionMode.If, 0, "Talk"); to.AddCondition(AnimatorConditionMode.Less, 0.2f, "Gait");
                var back = ts.AddTransition(loco); back.hasExitTime = false; back.duration = 0.4f;
                back.AddCondition(AnimatorConditionMode.IfNot, 0, "Talk");
                var back2 = ts.AddTransition(loco); back2.hasExitTime = false; back2.duration = 0.25f;
                back2.AddCondition(AnimatorConditionMode.Greater, 0.3f, "Gait");
            }
            EditorUtility.SetDirty(ac);
            return ac;
        }
        static string Src(AnimationClip c) => c ? $"{c.name} <{Path.GetFileName(AssetDatabase.GetAssetPath(c))}>" : "none";

        // ---- render pipeline ---------------------------------------------------------------------------
        static UniversalRenderPipelineAsset EnsurePipeline()
        {
            string path = $"{Gen}/BlackBird_URP.asset", rpath = $"{Gen}/BlackBird_Renderer.asset";
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            if (!asset)
            {
                var rd = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(rd, rpath);
                var ppd = AssetDatabase.LoadAssetAtPath<PostProcessData>("Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
                if (ppd) rd.postProcessData = ppd;
                EditorUtility.SetDirty(rd);
                asset = UniversalRenderPipelineAsset.Create(rd);
                AssetDatabase.CreateAsset(asset, path);
            }
            asset.supportsHDR = true;
            asset.msaaSampleCount = 1;          // FXAA on the camera instead: cheaper on phones
            asset.renderScale = 1f;
            asset.supportsCameraDepthTexture = true;   // soft fog particles
            asset.shadowDistance = 30f;
            var so = new SerializedObject(asset);
            void Set(string prop, int v) { var p = so.FindProperty(prop); if (p != null) p.intValue = v; }
            Set("m_MainLightShadowmapResolution", 2048);
            Set("m_AdditionalLightsRenderingMode", 1);    // per pixel
            Set("m_AdditionalLightsPerObjectLimit", 8);
            Set("m_ShadowCascadeCount", 1);
            Set("m_HDRColorBufferPrecision", 0);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            // the fog is transparent and fades against the depth texture: copy depth after opaques,
            // not after transparents, or the fog cuts hard lines where it meets walls and cobbles
            var rdObj = so.FindProperty("m_RendererDataList").GetArrayElementAtIndex(0).objectReferenceValue;
            if (rdObj)
            {
                var rso = new SerializedObject(rdObj);
                var copy = rso.FindProperty("m_CopyDepthMode"); if (copy != null) copy.intValue = 0;
                rso.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(rdObj);
            }

            GraphicsSettings.defaultRenderPipeline = asset;
            int level = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++) { QualitySettings.SetQualityLevel(i, false); QualitySettings.renderPipeline = asset; }
            QualitySettings.SetQualityLevel(level, false);
            PlayerSettings.colorSpace = ColorSpace.Linear;
            AssetDatabase.SaveAssets();
            return asset;
        }

        static VolumeProfile Profile(string name, System.Action<VolumeProfile> fill)
        {
            string path = $"{Gen}/{name}.asset";
            AssetDatabase.DeleteAsset(path);
            var p = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(p, path);
            fill(p);
            foreach (var c in p.components) { c.name = c.GetType().Name; AssetDatabase.AddObjectToAsset(c, p); }
            EditorUtility.SetDirty(p);
            AssetDatabase.SaveAssets();
            return p;
        }

        // ---- materials and textures ----------------------------------------------------------------------
        static Material Save(Material m, string name)
        {
            string path = $"{Gen}/{name}.mat";
            var old = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (old) { old.shader = m.shader; old.CopyPropertiesFromMaterial(m); old.shaderKeywords = m.shaderKeywords; old.renderQueue = m.renderQueue; EditorUtility.SetDirty(old); return old; }
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        static Material Lit(string name, Color c, float smooth, float metal = 0)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", metal);
            return Save(m, name);
        }

        static Material Emissive(string name, Color c, float strength)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            m.SetColor("_BaseColor", c * strength);
            return Save(m, name);
        }

        static void Transparent(Material m, bool additive)
        {
            m.SetFloat("_Surface", 1);
            m.SetFloat("_Blend", additive ? 2 : 0);
            m.SetFloat("_BlendModePreserveSpecular", 0);
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", additive ? (float)BlendMode.Zero : (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)RenderQueue.Transparent;
            m.SetShaderPassEnabled("ShadowCaster", false);
            m.SetShaderPassEnabled("DepthOnly", false);
        }

        static Material Unlit(string name, Color c, Texture2D tex, bool transparent, bool additive)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            m.SetColor("_BaseColor", c);
            if (tex) m.SetTexture("_BaseMap", tex);
            if (transparent) Transparent(m, additive);
            m.SetFloat("_Cull", 0);
            return Save(m, name);
        }

        static Material FogMaterial()
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Particles/Lit"));
            m.SetTexture("_BaseMap", Tex("fogpuff", FogTexture));
            m.SetColor("_BaseColor", Color.white);
            Transparent(m, false);
            m.SetFloat("_Smoothness", 0); m.SetFloat("_Metallic", 0);
            m.SetFloat("_ReceiveShadows", 0); m.EnableKeyword("_RECEIVE_SHADOWS_OFF");
            // fade where the fog meets geometry, and as it reaches the camera
            m.SetFloat("_SoftParticlesEnabled", 1); m.EnableKeyword("_SOFTPARTICLES_ON");
            m.SetFloat("_SoftParticlesNearFadeDistance", 0f); m.SetFloat("_SoftParticlesFarFadeDistance", 1.5f);
            m.SetVector("_SoftParticleFadeParams", new Vector4(0f, 1f / 1.5f, 0, 0));
            m.SetFloat("_CameraFadingEnabled", 1); m.EnableKeyword("_FADING_ON");
            m.SetFloat("_CameraNearFadeDistance", 1.0f); m.SetFloat("_CameraFarFadeDistance", 5f);
            m.SetVector("_CameraFadeParams", new Vector4(1.0f, 1f / 4f, 0, 0));
            m.SetFloat("_ColorMode", 0);
            // a little light of its own, so the fog reads between the lamps and glows amber near them
            m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", new Color(0.10f, 0.12f, 0.15f));
            return Save(m, "Fog");
        }

        static Material ConeMaterial()
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            m.SetTexture("_BaseMap", Texture2D.whiteTexture);
            m.SetColor("_BaseColor", new Color(1f, 0.72f, 0.42f, 0.1f));
            Transparent(m, true);
            m.SetFloat("_Cull", 0);
            m.SetFloat("_SoftParticlesEnabled", 1); m.EnableKeyword("_SOFTPARTICLES_ON");
            m.SetVector("_SoftParticleFadeParams", new Vector4(0f, 1f / 0.8f, 0, 0));
            m.SetFloat("_CameraFadingEnabled", 1); m.EnableKeyword("_FADING_ON");
            m.SetVector("_CameraFadeParams", new Vector4(0.5f, 1f / 2f, 0, 0));
            return Save(m, "LightCone");
        }

        static Texture2D Tex(string name, System.Func<int, Color32[]> draw, int n = 128)
        {
            string path = $"{Gen}/{name}.png";
            if (!File.Exists(path))
            {
                var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
                t.SetPixels32(draw(n)); t.Apply();
                File.WriteAllBytes(path, t.EncodeToPNG());
                Object.DestroyImmediate(t);
                AssetDatabase.ImportAsset(path);
                var ti = (TextureImporter)AssetImporter.GetAtPath(path);
                ti.alphaIsTransparency = true; ti.wrapMode = TextureWrapMode.Clamp; ti.mipmapEnabled = true;
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Color32[] GlowTexture(int n)
        {
            var px = new Color32[n * n]; float c = (n - 1) / 2f;
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                float a = Mathf.Pow(Mathf.Clamp01(1 - d), 2.2f);
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            return px;
        }

        static Color32[] FogTexture(int n)
        {
            var px = new Color32[n * n]; float c = (n - 1) / 2f;
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                float noise = 0;
                for (int o = 0, f = 3; o < 4; o++, f *= 2) noise += (Mathf.PerlinNoise(x * f / (float)n + 13.1f, y * f / (float)n + 7.7f) - 0.5f) / (1 + o);
                float a = Mathf.Clamp01(Mathf.SmoothStep(1, 0, d) * (0.75f + noise * 0.9f));
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            return px;
        }

        static Color32[] HeelTexture(int n)
        {
            // a narrow pointed sole and a small heel, toe at the top
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n * 2 - 1, v = (y + 0.5f) / n;
                float w = v > 0.4f ? 0.55f * Mathf.Sin(Mathf.Clamp01((v - 0.4f) / 0.6f) * Mathf.PI * 0.95f + 0.15f) : (v < 0.22f && v > 0.04f ? 0.38f : 0f);
                float a = Mathf.Clamp01((w - Mathf.Abs(u)) * 12f);
                a *= 0.6f + 0.4f * Mathf.PerlinNoise(x * 0.3f, y * 0.3f);
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            return px;
        }

        static Light PointLight(string name, Vector3 pos, Color c, float intensity, float range)
        {
            var l = new GameObject(name).AddComponent<Light>();
            l.type = LightType.Point; l.transform.position = pos; l.color = c; l.intensity = intensity; l.range = range; l.shadows = LightShadows.None;
            return l;
        }
    }
}
