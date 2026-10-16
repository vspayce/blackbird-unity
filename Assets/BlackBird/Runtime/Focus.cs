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

}
