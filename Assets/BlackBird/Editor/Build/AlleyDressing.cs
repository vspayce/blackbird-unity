// The parts of Burritt Alley that the web prototype built in code (src/world/alley.js) rather than in the
// Blender set: the cobbles of Bush Street and the alley, kerbs, the cable car slot and rails, the plank
// fence with Archer's gap, the billboard, the weedy ledge and Stockton Street's lights far below.
// Textures are generated here: wet granite setts (water standing in the joints) and weathered planks.
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using BlackBird;

namespace BlackBird.EditorTools
{
    public static class AlleyDressing
    {
        const string Gen = "Assets/BlackBird/Generated";

        public static void Build(Transform parent, Font font)
        {
            var root = new GameObject("Dressing").transform;
            root.SetParent(parent, false);

            // ---- cobbles: Bush Street and the alley ----
            var setts = Cobbles();
            Plane(root, "BushStreet", setts, Web.W(0, 0, 15), 60, 14, 60 / 2.2f, 14 / 2.2f);
            Plane(root, "AlleyCobbles", setts, Web.W(0, 0.002f, -7), 5, 30, 5 / 2.2f, 30 / 2.2f);

            // ---- kerbs ----
            var kerb = Lit("Kerb", new Color(0.36f, 0.35f, 0.33f), 0.35f);
            Box(root, "Kerb", kerb, -14.25f, 0.075f, 8.8f, 23.5f, 0.15f, 1.6f);
            Box(root, "Kerb", kerb, 14.25f, 0.075f, 8.8f, 23.5f, 0.15f, 1.6f);
            Box(root, "Kerb", kerb, 0, 0.075f, 21.2f, 60, 0.15f, 1.6f);

            // ---- the cable car slot and rails down the middle of Bush Street ----
            var iron = Lit("Rail", new Color(0.29f, 0.29f, 0.3f), 0.75f, 0.85f);
            var slot = Lit("Slot", new Color(0.01f, 0.01f, 0.01f), 0.2f);
            Box(root, "Rail", iron, 0, 0.01f, 14.2f, 60, 0.02f, 0.08f);
            Box(root, "Rail", iron, 0, 0.01f, 15.8f, 60, 0.02f, 0.08f);
            Box(root, "Slot", slot, 0, 0.006f, 15.0f, 60, 0.012f, 0.04f);

            // ---- the fence at the dead end, with the gap where Archer went back against it ----
            var plank = Planks();
            const float z = Chapter1.FenceZ;
            for (float x = -2.4f; x < 2.45f; x += 0.2f)
            {
                if (x > 0.35f && x < 1.15f) continue;
                var b = Box(root, "Board", plank, x, 0.95f, z, 0.18f, 1.9f, 0.04f);
                b.transform.localRotation *= Quaternion.Euler(0, 0, Random.Range(-1.2f, 1.2f));
            }
            Box(root, "Rail", plank, 0, 1.6f, z - 0.05f, 5, 0.1f, 0.08f);
            Box(root, "Rail", plank, 0, 0.4f, z - 0.05f, 5, 0.1f, 0.08f);
            // snapped boards lying outward on the ledge
            foreach (var (x, rz, ry) in new[] { (0.5f, 0.2f, 0.3f), (0.9f, -0.15f, -0.4f), (0.7f, 0.05f, 0.1f) })
            {
                var b = Box(root, "Snapped", plank, x, -0.2f, z - 0.9f, 0.18f, 1.2f, 0.04f);
                b.transform.rotation = Quaternion.Euler(Mathf.Rad2Deg * (-Mathf.PI / 2 + 0.15f), Mathf.Rad2Deg * ry, Mathf.Rad2Deg * rz);
            }

            // ---- the drop beyond: a weedy ledge, then Stockton Street's lights far below in the fog ----
            Plane(root, "Ledge", Lit("Ledge", new Color(0.16f, 0.17f, 0.12f), 0.1f), Web.W(0, -0.25f, z - 1.2f), 6, 2.2f, 1, 1);
            var lights = new GameObject("StocktonLights").transform; lights.SetParent(root, false);
            var glow = Unlit("CityLight", new Color(2.4f, 1.7f, 0.9f));
            var rng = new System.Random(7);
            for (int i = 0; i < 60; i++)
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Object.DestroyImmediate(q.GetComponent<Collider>());
                q.transform.SetParent(lights, false);
                q.transform.position = Web.W((float)(rng.NextDouble() - 0.5) * 70, -14 - (float)rng.NextDouble() * 6, -45 - (float)rng.NextDouble() * 40);
                q.transform.localScale = Vector3.one * 0.35f;
                q.GetComponent<Renderer>().sharedMaterial = glow;
                q.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                q.AddComponent<Billboard>();
            }

            // ---- the billboard on the right wall at the dead end ----
            var bill = new GameObject("Billboard").transform; bill.SetParent(root, false);
            bill.position = Web.W(2.44f, 2.6f, -19.5f);
            bill.rotation = Quaternion.LookRotation(Web.W(-1, 0, 0) - Web.W(0, 0, 0));   // faces into the alley
            var paper = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(paper.GetComponent<Collider>());
            paper.name = "Paper"; paper.transform.SetParent(bill, false);
            paper.transform.localScale = new Vector3(3.4f, 1.7f, 1); paper.transform.localRotation = Quaternion.Euler(0, 180, 0);
            paper.GetComponent<Renderer>().sharedMaterial = Lit("Poster", new Color(0.62f, 0.55f, 0.42f), 0.05f);
            if (font)
            {
                Text(bill, font, "DR. HALLORAN'S", 0.55f, 0.13f, new Color(0.35f, 0.08f, 0.05f));
                Text(bill, font, "CELEBRATED ELIXIR", 0.28f, 0.2f, new Color(0.12f, 0.08f, 0.05f));
                Text(bill, font, "Cures every ailment known to science", -0.15f, 0.085f, new Color(0.15f, 0.1f, 0.07f));
                Text(bill, font, "25 CENTS · ALL DRUGGISTS", -0.52f, 0.1f, new Color(0.35f, 0.08f, 0.05f));
            }
        }

        // ---- pieces ---------------------------------------------------------------------------------
        static GameObject Plane(Transform parent, string name, Material m, Vector3 center, float w, float d, float tu, float tv)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(q.GetComponent<Collider>());
            q.name = name; q.transform.SetParent(parent, false);
            q.transform.position = center;
            q.transform.rotation = Quaternion.Euler(90, 0, 0);
            q.transform.localScale = new Vector3(w, d, 1);
            var r = q.GetComponent<Renderer>();
            // tile by scaling UVs through a per-object material instance kept as an asset
            var inst = new Material(m) { name = m.name + "_" + name };
            inst.SetTextureScale("_BaseMap", new Vector2(tu, tv));
            inst.SetTextureScale("_BumpMap", new Vector2(tu, tv));
            r.sharedMaterial = SaveMat(inst, inst.name);
            r.shadowCastingMode = ShadowCastingMode.Off;
            q.isStatic = true;
            return q;
        }

        static GameObject Box(Transform parent, string name, Material m, float x, float y, float z, float w, float h, float d)
        {
            var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(b.GetComponent<Collider>());
            b.name = name; b.transform.SetParent(parent, false);
            b.transform.position = Web.W(x, y, z);
            b.transform.localScale = new Vector3(w, h, d);
            b.GetComponent<Renderer>().sharedMaterial = m;
            b.isStatic = true;
            return b;
        }

        static void Text(Transform bill, Font font, string s, float y, float size, Color c)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(bill, false);
            go.transform.localPosition = new Vector3(0, y, 0.01f);   // the alley side of the paper
            go.transform.localRotation = Quaternion.Euler(0, 180, 0);
            var tm = go.AddComponent<TextMesh>();
            tm.font = font; tm.text = s; tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center;
            tm.fontSize = 64; tm.characterSize = size / 6.4f; tm.color = c;
            var mr = go.GetComponent<MeshRenderer>();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            mat.SetTexture("_BaseMap", font.material.mainTexture);
            mat.SetColor("_BaseColor", c * 0.6f);
            mat.SetFloat("_AlphaClip", 1); mat.EnableKeyword("_ALPHATEST_ON"); mat.SetFloat("_Cutoff", 0.5f);
            mat.renderQueue = (int)RenderQueue.AlphaTest;
            mr.sharedMaterial = SaveMat(mat, "PosterInk_" + Mathf.Abs(s.GetHashCode()));
        }

        // ---- materials ------------------------------------------------------------------------------
        static Material SaveMat(Material m, string name)
        {
            string path = $"{Gen}/{name}.mat";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        static Material Lit(string name, Color c, float smooth, float metal = 0)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", metal);
            return SaveMat(m, name);
        }

        static Material Unlit(string name, Color c)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            m.SetColor("_BaseColor", c);
            return SaveMat(m, name);
        }

        static Material Cobbles()
        {
            var (albedo, normal) = CobbleTextures(512);
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetTexture("_BaseMap", albedo);
            m.SetColor("_BaseColor", Color.white);
            m.SetTexture("_BumpMap", normal); m.EnableKeyword("_NORMALMAP"); m.SetFloat("_BumpScale", 0.55f);
            // smoothness from the albedo's alpha: dull stone, water standing in the joints
            m.SetFloat("_SmoothnessTextureChannel", 1); m.EnableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
            m.SetFloat("_Smoothness", 1f);
            m.SetFloat("_Metallic", 0);
            return SaveMat(m, "Cobbles");
        }

        static Material Planks()
        {
            var (albedo, normal) = PlankTextures(256);
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetTexture("_BaseMap", albedo);
            m.SetTexture("_BumpMap", normal); m.EnableKeyword("_NORMALMAP");
            m.SetFloat("_Smoothness", 0.15f);
            return SaveMat(m, "FencePlanks");
        }

        // ---- generated textures -----------------------------------------------------------------------
        // Granite setts in courses (each course offset), slightly irregular, domed, with dark wet joints.
        static (Texture2D, Texture2D) CobbleTextures(int n)
        {
            var h = new float[n * n]; var col = new Color[n * n]; var wet = new float[n * n];
            const int courses = 8;
            float courseH = 1f / courses;
            var rng = new System.Random(11);
            var stoneTint = new Dictionary<(int, int), Color>();
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n, v = (y + 0.5f) / n;
                int c = Mathf.FloorToInt(v * courses);
                float offset = (c % 2) * 0.5f / 4f + Hash(c, 99) * 0.05f;
                const int perCourse = 4;
                float su = (u + offset) * perCourse;
                int s = Mathf.FloorToInt(su);
                float fu = su - s, fv = v * courses - c;
                // distance to the sett's edge, with a little wobble so the joints aren't ruled lines
                float wob = (Mathf.PerlinNoise(u * 40, v * 40) - 0.5f) * 0.12f;
                float edge = Mathf.Min(Mathf.Min(fu, 1 - fu) * 1.0f, Mathf.Min(fv, 1 - fv) * 1.0f) + wob * 0.3f;
                float stone = Mathf.SmoothStep(0.03f, 0.12f, edge);
                float dome = Mathf.Sqrt(Mathf.Clamp01(edge * 4f));
                h[y * n + x] = stone * (0.55f + 0.45f * dome) + (Mathf.PerlinNoise(u * 90, v * 90) - 0.5f) * 0.08f;
                var key = (c, ((s % perCourse) + perCourse) % perCourse);
                if (!stoneTint.TryGetValue(key, out var tint))
                {
                    float g = 0.30f + (float)rng.NextDouble() * 0.16f;
                    tint = new Color(g * (0.98f + (float)rng.NextDouble() * 0.06f), g * 0.97f, g * (0.93f + (float)rng.NextDouble() * 0.05f));
                    stoneTint[key] = tint;
                }
                float grain = 0.85f + 0.3f * Mathf.PerlinNoise(u * 160 + c, v * 160);
                var joint = new Color(0.05f, 0.045f, 0.04f);
                col[y * n + x] = Color.Lerp(joint, tint * grain, stone);
                // standing water in the joints, and a few shallow puddles
                float puddle = Mathf.SmoothStep(0.62f, 0.72f, Mathf.PerlinNoise(u * 3.1f + 5, v * 3.1f + 2));
                wet[y * n + x] = Mathf.Lerp(Mathf.Lerp(0.88f, 0.42f, stone), 0.93f, puddle);
            }
            var a = new Texture2D(n, n, TextureFormat.RGBA32, true);
            var px = new Color[n * n];
            for (int i = 0; i < px.Length; i++) { var c0 = col[i]; c0.a = wet[i]; px[i] = c0; }
            a.SetPixels(px); a.Apply();
            var albedo = SaveTex(a, "cobbles", false);
            var normal = SaveTex(NormalFromHeight(h, n, 6f), "cobbles_n", true);
            return (albedo, normal);
        }

        static (Texture2D, Texture2D) PlankTextures(int n)
        {
            var h = new float[n * n]; var px = new Color[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n, v = (y + 0.5f) / n;
                float grain = Mathf.PerlinNoise(u * 6, v * 60) * 0.6f + Mathf.PerlinNoise(u * 30, v * 200) * 0.4f;
                float weather = Mathf.PerlinNoise(u * 3 + 10, v * 3);
                var c = Color.Lerp(new Color(0.30f, 0.25f, 0.19f), new Color(0.42f, 0.40f, 0.36f), weather * 0.8f) * (0.75f + grain * 0.4f);
                c.a = 1; px[y * n + x] = c;
                h[y * n + x] = grain;
            }
            var a = new Texture2D(n, n, TextureFormat.RGBA32, true); a.SetPixels(px); a.Apply();
            return (SaveTex(a, "planks", false), SaveTex(NormalFromHeight(h, n, 2f), "planks_n", true));
        }

        static Texture2D NormalFromHeight(float[] h, int n, float strength)
        {
            var t = new Texture2D(n, n, TextureFormat.RGBA32, true);
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float l = h[y * n + (x - 1 + n) % n], r = h[y * n + (x + 1) % n];
                float d = h[((y - 1 + n) % n) * n + x], u = h[((y + 1) % n) * n + x];
                var nv = new Vector3((l - r) * strength, (d - u) * strength, 1f).normalized;
                px[y * n + x] = new Color(nv.x * 0.5f + 0.5f, nv.y * 0.5f + 0.5f, nv.z * 0.5f + 0.5f, 1);
            }
            t.SetPixels(px); t.Apply();
            return t;
        }

        static Texture2D SaveTex(Texture2D t, string name, bool normal)
        {
            string path = $"{Gen}/{name}.png";
            File.WriteAllBytes(path, t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            ti.sRGBTexture = !normal;
            ti.wrapMode = TextureWrapMode.Repeat;
            ti.mipmapEnabled = true; ti.anisoLevel = 8;
            ti.alphaSource = normal ? TextureImporterAlphaSource.None : TextureImporterAlphaSource.FromInput;
            ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static float Hash(int a, int b) { unchecked { int h = a * 374761393 + b * 668265263; h = (h ^ (h >> 13)) * 1274126177; return (h & 0xffff) / 65535f; } }
    }
}
