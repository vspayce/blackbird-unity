using System.Collections.Generic;
using UnityEngine;

namespace BlackBird
{
    /// A walker's steps, timed by the distance it really covers, in real recordings (Resources/Audio/Steps, Kenney's
    /// CC0 "Impact Sounds") chosen by what is underfoot: stone and cobbles, wooden floors and stairs, rugs and carpet
    /// runners, lawns and gravel. The surface is the material of the set mesh straight below.
    public class Footsteps : MonoBehaviour
    {
        AudioSource src;
        Vector3 last;
        float travelled, surfaceT;
        int lastClip = -1;
        string surface = "concrete";
        static readonly RaycastHit[] hits = new RaycastHit[8];

        static Dictionary<string, AudioClip[]> clips;
        static readonly Dictionary<string, float> Volume = new Dictionary<string, float>
            { ["concrete"] = 0.55f, ["wood"] = 0.6f, ["carpet"] = 0.7f, ["grass"] = 0.55f };

        static void Load()
        {
            if (clips != null) return;
            clips = new Dictionary<string, AudioClip[]>();
            foreach (var s in Volume.Keys)
            {
                var list = new List<AudioClip>();
                for (int i = 0; i < 8; i++) { var c = Resources.Load<AudioClip>($"Audio/Steps/{s}_{i}"); if (c) list.Add(c); }
                if (list.Count > 0) clips[s] = list.ToArray();
            }
        }

        void Start()
        {
            Load();
            src = gameObject.AddComponent<AudioSource>();
            src.spatialBlend = 1f; src.rolloffMode = AudioRolloffMode.Linear; src.minDistance = 1.5f; src.maxDistance = 22f;
            src.playOnAwake = false; src.dopplerLevel = 0;
            last = transform.position;
        }

        // what the floor below is made of, from its material's name
        static string Classify(string m)
        {
            m = m.ToLowerInvariant();
            if (m.Contains("rug") || m.Contains("carpet") || m.Contains("runner") || m.Contains("velvet")) return "carpet";
            if (m.Contains("parquet") || m.Contains("wood") || m.Contains("walnut") || m.Contains("oak") || m.Contains("board") || m.Contains("plank")
                || m.Contains("floor") && !m.Contains("tile") || m.Contains("stair") || m.Contains("plaster") || m.Contains("ebony") || m.Contains("rosewood") || m.Contains("deck")) return "wood";
            if (m.Contains("grass") || m.Contains("lawn") || m.Contains("gravel") || m.Contains("skirt") || m.Contains("hedge") || m.Contains("dirt") || m.Contains("mud")) return "grass";
            return "concrete";
        }

        void Surface()
        {
            int n = Physics.RaycastNonAlloc(transform.position + Vector3.up * 0.4f, Vector3.down, hits, 1.4f, ~(1 << 2), QueryTriggerInteraction.Ignore);
            float best = float.MaxValue; string found = null;
            for (int i = 0; i < n; i++)
            {
                var h = hits[i];
                if (h.collider.transform.IsChildOf(transform) || h.distance >= best) continue;
                var r = h.collider.GetComponent<Renderer>();
                if (!r || !r.sharedMaterial) continue;                 // plain box colliders say nothing about the floor
                best = h.distance; found = r.sharedMaterial.name;
            }
            if (found != null) surface = Classify(found);
        }

        void Update()
        {
            var p = transform.position;
            var d = p - last; last = p;
            if (d.sqrMagnitude > 4f) { travelled = 0; return; }                                  // teleported
            float flat = new Vector2(d.x, d.z).magnitude;
            float speed = flat / Mathf.Max(Time.deltaTime, 1e-4f);
            if (speed < 0.2f) { travelled = Mathf.Min(travelled, 0.3f); return; }
            travelled += flat;
            float stride = Mathf.Lerp(0.62f, 0.95f, Mathf.InverseLerp(0.8f, 2.6f, speed));
            if (travelled < stride) return;
            travelled = 0;
            if ((surfaceT -= stride) <= 0) { surfaceT = 1.2f; Surface(); }

            AudioClip[] set = null;
            if (clips != null && !clips.TryGetValue(surface, out set)) clips.TryGetValue("concrete", out set);
            if (set == null || set.Length == 0) { if (Sound.I == null || Sound.I.steps == null) return; set = Sound.I.steps; }   // the old synthesised steps
            int k; do k = Random.Range(0, set.Length); while (k == lastClip && set.Length > 1);
            lastClip = k;
            src.pitch = Random.Range(0.92f, 1.06f);
            float vol = Volume.TryGetValue(surface, out var v) ? v : 0.55f;
            src.PlayOneShot(set[k], Sound.Effects * vol * Mathf.Lerp(0.85f, 1.1f, Mathf.InverseLerp(0.8f, 2.6f, speed)));   // a brisk step lands harder
        }
    }
}
