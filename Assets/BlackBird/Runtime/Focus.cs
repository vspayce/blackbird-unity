// Holmes's Focus: time slows, the picture drains and cools, and details nobody else saw fade in.
// About ten seconds of it, refilling while it's off.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BlackBird
{
    public class Focus : MonoBehaviour
    {
        public static Focus I { get; private set; }
        public Volume volume;                   // the Focus grade, weight driven by Amount
        public float capacity = 10f, refillRate = 0.5f, slowTo = 0.3f;

        public bool On { get; private set; }
        public float Amount { get; private set; }   // 0..1, eased
        public float Meter { get; private set; } = 1f;

        static readonly List<HiddenDetail> hidden = new List<HiddenDetail>();
        public static void Register(HiddenDetail h) { if (!hidden.Contains(h)) hidden.Add(h); }
        public static void Unregister(HiddenDetail h) => hidden.Remove(h);

        void Awake() { I = this; }
        void OnDisable() { Time.timeScale = 1f; }

        public void Toggle() { if (On) On = false; else if (Meter > 0.15f) On = true; }
        public void ForceOff() => On = false;

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (On) { Meter -= dt / capacity; if (Meter <= 0) { Meter = 0; On = false; } }
            else Meter = Mathf.Min(1f, Meter + dt * refillRate / capacity);
            Amount = Mathf.MoveTowards(Amount, On ? 1f : 0f, dt * (On ? 2.2f : 1.6f));
            float k = Mathf.SmoothStep(0, 1, Amount);
            Time.timeScale = Mathf.Lerp(1f, slowTo, k);
            if (volume) volume.weight = k;
            foreach (var h in hidden) h.Show(k);
        }
    }

    // Something only Focus shows. Fades the renderers' colour alpha (and any light) with the Focus amount;
    // once its clue is found it can stay visible.
    public class HiddenDetail : MonoBehaviour
    {
        public float strength = 1f;
        public bool keepOnceFound;
        public bool found;
        public bool pulse;
        Renderer[] rends; Light[] lights; float[] lightBase;
        MaterialPropertyBlock mpb;
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        Color[] baseColors;

        void OnEnable()
        {
            rends = GetComponentsInChildren<Renderer>(true);
            lights = GetComponentsInChildren<Light>(true);
            lightBase = new float[lights.Length];
            for (int i = 0; i < lights.Length; i++) lightBase[i] = lights[i].intensity;
            baseColors = new Color[rends.Length];
            for (int i = 0; i < rends.Length; i++)
                baseColors[i] = rends[i].sharedMaterial && rends[i].sharedMaterial.HasProperty(BaseColor) ? rends[i].sharedMaterial.GetColor(BaseColor) : Color.white;
            mpb = new MaterialPropertyBlock();
            Focus.Register(this);
            Show(0);
        }
        void OnDisable() => Focus.Unregister(this);

        public void Show(float k)
        {
            if (found && keepOnceFound) k = Mathf.Max(k, 0.6f);
            float a = k * strength * (pulse ? 0.7f + 0.3f * Mathf.Sin(Time.unscaledTime * 3f) : 1f);
            for (int i = 0; i < rends.Length; i++)
            {
                rends[i].enabled = a > 0.01f;
                var c = baseColors[i]; c.a *= a;
                rends[i].GetPropertyBlock(mpb);
                mpb.SetColor(BaseColor, c);
                rends[i].SetPropertyBlock(mpb);
            }
            for (int i = 0; i < lights.Length; i++) { lights[i].intensity = lightBase[i] * a; lights[i].enabled = a > 0.01f; }
        }
    }
}
