// San Francisco at night below Nob Hill, as the prototype had it (src/world/hopkins.js): gas lamps along the
// street grid, lit windows (more toward the waterfront and Chinatown), the far shore and ships at anchor, the
// land as a dark sheet falling to the bay, distant hills, stars and a moon. Everything is generated from a
// fixed seed at start-up, so the scene stays light.
using System.Collections.Generic;
using UnityEngine;

namespace BlackBird
{
    public class NightCity : MonoBehaviour
    {
        public Material lightsMaterial;     // additive particles
        public Material landMaterial, bayMaterial, hillsMaterial, moonMaterial;
        public int seed = 1895;
        uint s;

        float Rnd() { s = s * 1664525u + 1013904223u; return s / 4294967296f; }

        static float Ground(float x, float z)
        {
            float d = Mathf.Sqrt(x * x + z * z);
            float fall = -0.12f * Mathf.Max(0, d - 40) - 0.06f * Mathf.Max(0, -z - 40) - 0.04f * Mathf.Max(0, x - 40);
            return Mathf.Max(-95, fall) - 3;
        }

        static bool Water(float x, float z) => z < -620 + 0.18f * x || x > 900 - 0.3f * z;

        void Start()
        {
            s = (uint)seed;
            var lamps = new List<Vector3>(); var windows = new List<Vector3>();
            const float R = 1300, block = 42;
            for (float x = -R; x <= R; x += block)
            for (float z = -R; z <= R; z += block)
            {
                if (Mathf.Sqrt(x * x + z * z) < 70 || Water(x, z)) continue;
                float y = Ground(x, z);
                lamps.Add(Web.W(x, y + 3.5f, z)); lamps.Add(Web.W(x + block / 2, y + 3.5f, z));
                int busy = (z < -500 || (x > -200 && x < 150 && z < -100 && z > -500)) ? 6 : 2;
                for (int k = 0; k < busy; k++)
                    if (Rnd() < 0.55f) windows.Add(Web.W(x + 4 + Rnd() * (block - 8), y + 3 + Rnd() * 9, z + 4 + Rnd() * (block - 8)));
            }
            var shore = new List<Vector3>();
            for (int i = 0; i < 700; i++)
            {
                float x = -1800 + Rnd() * 4200, z = -2600 - Rnd() * 600;
                shore.Add(Web.W(x, -80 + Rnd() * 40 + Mathf.Max(0, (z + 2600) * -0.05f), z));
            }
            var ships = new List<Vector3>();
            for (int i = 0; i < 40; i++)
            {
                float x, z; int guard = 0;
                do { x = -1500 + Rnd() * 3200; z = -800 - Rnd() * 1700; } while (!Water(x, z) && ++guard < 50);
                ships.Add(Web.W(x, -96, z)); ships.Add(Web.W(x + 3, -90 - Rnd() * 6, z));
            }
            var stars = new List<Vector3>();
            for (int i = 0; i < 900; i++)
            {
                float a = Rnd() * Mathf.PI * 2, e = 0.08f + Rnd() * 1.3f;
                stars.Add(new Vector3(Mathf.Cos(a) * Mathf.Cos(e), Mathf.Sin(e), Mathf.Sin(a) * Mathf.Cos(e)) * 2600);
            }

            Points("Lamps", lamps, new Color(3.0f, 1.9f, 0.9f), 2.2f);
            Points("Windows", windows, new Color(2.2f, 1.5f, 0.8f), 1.6f);
            Points("Shore", shore, new Color(1.8f, 1.3f, 0.8f), 1.4f);
            Points("Ships", ships, new Color(2.4f, 2.0f, 1.6f), 1.8f);
            Points("Stars", stars, new Color(0.75f, 0.82f, 0.95f), 1.5f);
            Land(R);
            Moon();
        }

        // Points of light that stay a pixel or two across at any distance.
        void Points(string name, List<Vector3> pts, Color c, float px)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false; main.playOnAwake = false; main.maxParticles = pts.Count;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = 1e6f; main.startSpeed = 0;
            var em = ps.emission; em.enabled = false;
            var sh = ps.shape; sh.enabled = false;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = lightsMaterial;
            r.minParticleSize = px * 0.0011f;     // a fraction of the screen: a pixel or two on a phone
            r.maxParticleSize = 0.01f;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            var parts = new ParticleSystem.Particle[pts.Count];
            for (int i = 0; i < pts.Count; i++)
            {
                parts[i].position = pts[i];
                parts[i].startSize = 0.6f;
                parts[i].startLifetime = parts[i].remainingLifetime = 1e6f;
                // vary each light a little: gas, oil and the odd electric arc
                float k = 0.75f + 0.5f * Rnd();
                parts[i].startColor = (Color32)new Color(Mathf.Clamp01(c.r * k / 3f), Mathf.Clamp01(c.g * k / 3f), Mathf.Clamp01(c.b * k / 3f), 1f);
            }
            ps.SetParticles(parts, parts.Length);
            ps.Pause();
        }

        void Land(float R)
        {
            int seg = 90; float size = 2 * R + 400;
            var v = new Vector3[(seg + 1) * (seg + 1)];
            var t = new int[seg * seg * 6];
            for (int j = 0, i = 0; j <= seg; j++)
            for (int k = 0; k <= seg; k++, i++)
            {
                float x = -size / 2 + size * k / seg, z = -size / 2 + size * j / seg;
                float y = Mathf.Sqrt(x * x + z * z) < 45 ? -3.5f : Ground(x, z) - 0.5f;
                v[i] = Web.W(x, y, z);
            }
            for (int j = 0, n = 0; j < seg; j++)
            for (int k = 0; k < seg; k++)
            {
                int a = j * (seg + 1) + k, b = a + 1, c = a + seg + 1, d = c + 1;
                t[n++] = a; t[n++] = b; t[n++] = c; t[n++] = b; t[n++] = d; t[n++] = c;
            }
            var mesh = new Mesh { name = "Land", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.vertices = v; mesh.triangles = t;
            // Web.W mirrors z, which turns the winding inside out: flip so the land faces up
            if (Vector3.Cross(v[1] - v[0], v[seg + 1] - v[0]).y < 0) { System.Array.Reverse(t); mesh.triangles = t; }
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            Child("Land", mesh, landMaterial, Vector3.zero, Vector3.one);
            var quad = new Mesh { name = "Bay", vertices = new[] { new Vector3(-1, 0, -1), new Vector3(1, 0, -1), new Vector3(-1, 0, 1), new Vector3(1, 0, 1) }, triangles = new[] { 0, 2, 1, 1, 2, 3 } };
            quad.RecalculateNormals();
            Child("Bay", quad, bayMaterial, new Vector3(0, -97, 0), new Vector3(4500, 1, 4500));
            // the far hills: a dark band round the horizon, lowest over the Golden Gate
            int hs = 96; var hv = new List<Vector3>(); var ht = new List<int>();
            for (int i = 0; i <= hs; i++)
            {
                float a = i * Mathf.PI * 2 / hs;
                float h = 60 + 140 * Mathf.PerlinNoise(i * 0.21f, 3.1f) + 80 * Mathf.Max(0, Mathf.Sin(a + 0.6f));
                var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                hv.Add(dir * 3200 + Vector3.up * -110); hv.Add(dir * 3200 + Vector3.up * h);
                if (i < hs) { int b = i * 2; ht.AddRange(new[] { b, b + 1, b + 2, b + 1, b + 3, b + 2 }); }
            }
            var hm = new Mesh { name = "Hills" }; hm.SetVertices(hv); hm.SetTriangles(ht, 0); hm.RecalculateNormals();
            Child("Hills", hm, hillsMaterial, Vector3.zero, Vector3.one).GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        void Moon()
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(q.GetComponent<Collider>());
            q.name = "Moon"; q.transform.SetParent(transform, false);
            q.transform.position = Web.W(-260, 240, -700).normalized * 2400;
            q.transform.localScale = Vector3.one * 110;
            q.transform.rotation = Quaternion.LookRotation(q.transform.position);
            q.GetComponent<Renderer>().sharedMaterial = moonMaterial;
        }

        GameObject Child(string name, Mesh m, Material mat, Vector3 pos, Vector3 scale)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(transform, false);
            go.transform.localPosition = pos; go.transform.localScale = scale;
            go.GetComponent<MeshFilter>().sharedMesh = m;
            var r = go.GetComponent<MeshRenderer>(); r.sharedMaterial = mat; r.receiveShadows = false;
            return go;
        }
    }
}
