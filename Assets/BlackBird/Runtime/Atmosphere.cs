// The fog and the gaslight. Distance fog is in RenderSettings (set by the builder); this adds the
// drifting, lit fog banks, a soft cone of light under each lamp, and the lamps' flicker.
using System.Collections.Generic;
using UnityEngine;

namespace BlackBird
{
    public class Atmosphere : MonoBehaviour
    {
        public Material fogMaterial;        // URP Particles/Lit, soft, camera-faded, alpha blended
        public Material coneMaterial;       // URP Particles/Unlit, additive, vertex colour
        public Color fogTint = new Color(0.62f, 0.68f, 0.76f, 0.24f);
        public Color coneTint = new Color(1f, 0.72f, 0.42f, 0.11f);
        [Range(0, 1)] public float focusThin = 0.55f;   // how much Focus pulls the fog back

        public List<Light> lamps = new List<Light>();
        readonly List<(Light l, float baseI, float seed)> flicker = new List<(Light, float, float)>();
        readonly List<ParticleSystem> banks = new List<ParticleSystem>();

        void Start()
        {
            // the alley, from Bush Street to the fence, and Bush Street itself (web coordinates)
            Bank("Fog_Alley", Web.W(0, 1.4f, -7), new Vector3(5f, 2.4f, 30f), 16, 4.5f, 8f, 0.25f);
            Bank("Fog_Street", Web.W(0, 1.8f, 15), new Vector3(60f, 3f, 14f), 34, 6f, 11f, 0.45f);
            Bank("Fog_Low", Web.W(0, 0.35f, 0), new Vector3(46f, 0.5f, 40f), 24, 3f, 6f, 0.15f, 1.33f);

            foreach (var l in lamps)
            {
                if (!l) continue;
                flicker.Add((l, l.intensity, Random.value * 100f));
                if (coneMaterial && l.type == LightType.Point && l.transform.position.y > 2f) Cone(l.transform);
            }
        }

        void Bank(string name, Vector3 center, Vector3 size, int count, float minSize, float maxSize, float drift, float alphaScale = 1f)
        {
            if (!fogMaterial) return;
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.position = center;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true; main.prewarm = true; main.playOnAwake = true;
            main.duration = 30f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(26f, 40f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
            main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
            var tint = fogTint; tint.a *= alphaScale;
            main.startColor = tint;
            main.maxParticles = count * 2;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape;

            var em = ps.emission; em.rateOverTime = count / 33f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(Mathf.Abs(size.x), size.y, Mathf.Abs(size.z));

            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(drift * 0.4f, drift);
            vel.y = new ParticleSystem.MinMaxCurve(-0.01f, 0.03f);
            vel.z = new ParticleSystem.MinMaxCurve(-drift * 0.3f, drift * 0.3f);

            var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);

            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                      new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.25f), new GradientAlphaKey(1, 0.75f), new GradientAlphaKey(0, 1) });
            col.color = g;

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = fogMaterial;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.sortMode = ParticleSystemSortMode.Distance;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.maxParticleSize = 3f;
            ps.Play();
            banks.Add(ps);
        }

        // a soft cone of lit fog hanging under a lamp
        void Cone(Transform lamp)
        {
            var go = new GameObject("LightCone");
            go.transform.SetParent(lamp, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.rotation = Quaternion.identity;
            float h = lamp.position.y - 0.05f, r0 = 0.12f, r1 = Mathf.Clamp(h * 0.55f, 1f, 2.2f);
            var mesh = new Mesh { name = "LightCone" };
            const int seg = 24;
            var v = new List<Vector3>(); var c = new List<Color>(); var t = new List<int>();
            // two shells: an inner bright one and an outer faint one, so the edge has no hard line
            for (int shell = 0; shell < 2; shell++)
            {
                float k = shell == 0 ? 0.6f : 1f, a = shell == 0 ? 1f : 0.45f;
                int b = v.Count;
                for (int i = 0; i <= seg; i++)
                {
                    float ang = i * Mathf.PI * 2 / seg;
                    var d = new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang));
                    v.Add(d * r0 * k); c.Add(new Color(1, 1, 1, a));
                    v.Add(d * r1 * k + Vector3.down * h); c.Add(new Color(1, 1, 1, 0));
                }
                for (int i = 0; i < seg; i++)
                {
                    int a0 = b + i * 2;
                    t.AddRange(new[] { a0, a0 + 2, a0 + 1, a0 + 1, a0 + 2, a0 + 3 });
                }
            }
            mesh.SetVertices(v); mesh.SetColors(c); mesh.SetTriangles(t, 0); mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = coneMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var mpb = new MaterialPropertyBlock(); mpb.SetColor("_BaseColor", coneTint); mr.SetPropertyBlock(mpb);
        }

        void Update()
        {
            float t = Time.time;
            foreach (var (l, b, s) in flicker)
                if (l) l.intensity = b * (1f + 0.035f * Mathf.Sin(t * 11f + s) + 0.025f * Mathf.Sin(t * 6.3f + s * 2f) + 0.02f * (Mathf.PerlinNoise(t * 3f, s) - 0.5f));

            float k = Focus.I ? Focus.I.Amount : 0;
            foreach (var ps in banks)
            {
                var r = ps.GetComponent<ParticleSystemRenderer>();
                var mpb = new MaterialPropertyBlock(); r.GetPropertyBlock(mpb);
                // the particles carry fogTint; the material colour multiplies it: cooler and thinner in Focus
                mpb.SetColor("_BaseColor", Color.Lerp(Color.white, new Color(0.75f, 0.85f, 1.05f, 1f - focusThin), k));
                r.SetPropertyBlock(mpb);
            }
        }
    }
}
