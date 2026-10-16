// The game's sound, made in code like the web prototype's (no recordings yet): wind off the bay that dulls when
// there's a roof overhead, a foghorn across the water at night, a cable car's bell now and then by day, footsteps
// for everyone who walks, and small cues for Focus and for clues.
//
// It installs itself when the game starts and survives scene changes. Browsers only allow sound after the first
// click or key, so on the web it starts then. (Unity's WebGL player has no audio filters, so the muffled indoor
// wind is a second, pre-filtered loop crossfaded with the open one.)
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BlackBird
{
    public class Sound : MonoBehaviour
    {
        public static Sound I { get; private set; }
        const int Rate = 22050;

        AudioSource windOut, windIn, cues;
        AudioClip horn, bell, clue, deduce, wrong, focusOn, focusOff;
        internal AudioClip[] steps;
        float hornT = 4f, bellT = 6f, indoor, master = 1f;
        bool night = true, lastFocus;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (I) return;
            var go = new GameObject("Sound");
            DontDestroyOnLoad(go);
            I = go.AddComponent<Sound>();
        }

#if (UNITY_IOS || UNITY_TVOS) && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")] static extern void BB_PlaybackAudio();
#endif

        void Awake()
        {
#if (UNITY_IOS || UNITY_TVOS) && !UNITY_EDITOR
            BB_PlaybackAudio();      // heard in silent mode too (Plugins/Apple/BlackBirdAudio.mm)
#endif
            var rng = new System.Random(1895);
            windOut = Loop(Wind(rng, 6f, 0.06f, 1.0f), 0.32f);
            windIn = Loop(Wind(rng, 6f, 0.012f, 0.55f), 0f);
            cues = gameObject.AddComponent<AudioSource>(); cues.playOnAwake = false;
            horn = Horn();
            bell = Make(1.6f, (t, s) => Bell(t, 0) + Bell(t, 0.22f));
            clue = Make(1.0f, (t, s) => Tone(t, 880, 0.6f, 0.10f) + Tone(t - 0.08f, 1320, 0.8f, 0.07f));
            deduce = Make(1.6f, (t, s) => { float v = 0; float[] f = { 523, 659, 784, 1046 }; for (int i = 0; i < 4; i++) v += Tone(t - i * 0.09f, f[i], 1.2f, 0.08f, true); return v; });
            wrong = Make(0.5f, (t, s) => Tone(t, 180, 0.4f, 0.10f, true));
            focusOn = Make(0.8f, (t, s) => Tone(t, 220, 0.5f, 0.08f) + Tone(t - 0.12f, 330, 0.5f, 0.06f));
            focusOff = Make(0.8f, (t, s) => Tone(t, 330, 0.5f, 0.08f) + Tone(t - 0.12f, 220, 0.5f, 0.06f));
            steps = new AudioClip[6];
            for (int i = 0; i < steps.Length; i++) steps[i] = Step(rng, i);
            SceneManager.sceneLoaded += (s, m) => Dress(s);
            Dress(SceneManager.GetActiveScene());
            // An Apple TV often renegotiates its audio output (HDMI, a HomePod) just after an app starts; Unity then
            // resets its audio and every looping source stops. Start them again whenever that happens.
            AudioSettings.OnAudioConfigurationChanged += changed =>
            {
                Debug.Log($"[Black Bird] audio configuration changed (device changed: {changed}): {Describe()}");
                foreach (var a in new[] { windOut, windIn }) if (a && !a.isPlaying) a.Play();
            };
            Debug.Log("[Black Bird] audio: " + Describe());
        }

        static string Describe()
        {
            var c = AudioSettings.GetConfiguration();
            return $"{c.sampleRate} Hz, {c.speakerMode}, buffer {c.dspBufferSize}, driver {AudioSettings.driverCapabilities}, listener {(FindAnyObjectByType<AudioListener>() ? "present" : "MISSING")}, volume {AudioListener.volume}";
        }

        void Dress(Scene s)
        {
            night = !s.name.Contains("Day");
            foreach (var w in FindObjectsByType<Walker>(FindObjectsSortMode.None))
                if (!w.GetComponent<Footsteps>()) w.gameObject.AddComponent<Footsteps>();
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            // a roof overhead: the wind dulls and drops
            var cam = Camera.main;
            bool under = cam && Physics.Raycast(cam.transform.position + Vector3.up * 0.3f, Vector3.up, 45f, ~0, QueryTriggerInteraction.Ignore);
            indoor = Mathf.MoveTowards(indoor, under ? 1 : 0, dt * 0.8f);
            float wind = (night ? 0.55f : 0.4f) * master;
            windOut.volume = wind * (1 - indoor);
            windIn.volume = wind * 0.9f * indoor;
            if (!windOut.isPlaying && Time.frameCount % 60 == 0) { windOut.Play(); windIn.Play(); }

            if (night)
            {
                hornT -= dt;
                if (hornT <= 0) { hornT = 22f + Random.Range(0f, 14f); cues.PlayOneShot(horn, (indoor > 0.5f ? 0.35f : 0.8f) * master); }
            }
            else
            {
                bellT -= dt;
                if (bellT <= 0) { bellT = 35f + Random.Range(0f, 40f); cues.PlayOneShot(bell, (indoor > 0.5f ? 0.15f : 0.4f) * master); }
            }
            var f = Focus.I;
            bool on = f && f.On;
            if (on != lastFocus) { lastFocus = on; cues.PlayOneShot(on ? focusOn : focusOff, 0.7f * master); }
        }

        public static void Bell(float vol = 0.5f) { if (I) I.cues.PlayOneShot(I.bell, vol); }
        public static void Clue() { if (I) I.cues.PlayOneShot(I.clue, 0.8f); }
        public static void Deduce() { if (I) I.cues.PlayOneShot(I.deduce, 0.8f); }
        public static void Wrong() { if (I) I.cues.PlayOneShot(I.wrong, 0.8f); }

        // ---- making the sounds ----
        AudioSource Loop(AudioClip c, float vol)
        {
            var a = gameObject.AddComponent<AudioSource>();
            a.clip = c; a.loop = true; a.volume = vol; a.playOnAwake = false; a.Play();
            return a;
        }

        static AudioClip Make(float seconds, System.Func<float, int, float> f, string name = "cue")
        {
            int n = (int)(seconds * Rate);
            var d = new float[n];
            for (int i = 0; i < n; i++) d[i] = Mathf.Clamp(f(i / (float)Rate, i), -1f, 1f);
            var c = AudioClip.Create(name, n, 1, Rate, false);
            c.SetData(d, 0);
            return c;
        }

        // a soft attack and a long exponential tail, like the Web Audio envelope in the prototype
        static float Env(float t, float dur)
        {
            if (t < 0 || t > dur) return 0;
            float a = Mathf.Min(0.4f, dur * 0.2f);
            return t < a ? t / a : Mathf.Exp(-6.9f * (t - a) / (dur - a));
        }

        static float Tone(float t, float freq, float dur, float vol, bool tri = false)
        {
            if (t < 0 || t > dur) return 0;
            float ph = t * freq;
            float w = tri ? 4f * Mathf.Abs(ph - Mathf.Floor(ph + 0.5f)) - 1f : Mathf.Sin(2 * Mathf.PI * ph);
            return w * vol * Env(t, dur);
        }

        static float Bell(float t, float d)
        {
            t -= d;
            return Tone(t, 1568, 1.1f, 0.08f) + Tone(t, 2350, 0.7f, 0.04f) + Tone(t, 3720, 0.3f, 0.02f);
        }

        // wind: brown noise, gusting slowly; `cut` sets how dark it is (smaller is duller), crossfaded at the seam
        static AudioClip Wind(System.Random rng, float seconds, float cut, float gain)
        {
            int n = (int)(seconds * Rate);
            var d = new float[n];
            float last = 0, lp = 0;
            for (int i = 0; i < n; i++)
            {
                last = last * 0.985f + ((float)rng.NextDouble() * 2 - 1) * 0.015f;
                lp += (last * 6f - lp) * cut * 6f;
                float t = i / (float)Rate;
                float gust = 0.7f + 0.3f * Mathf.Sin(t * 2 * Mathf.PI / seconds) * Mathf.Sin(t * 1.3f);
                d[i] = lp * gain * gust * 1.8f;
            }
            int fade = Rate / 2;          // loop seam
            for (int i = 0; i < fade; i++) { float k = i / (float)fade; d[i] = d[i] * k + d[n - fade + i] * (1 - k); }
            var c = AudioClip.Create("wind", n - fade, 1, Rate, false);
            var trimmed = new float[n - fade]; System.Array.Copy(d, trimmed, n - fade);
            c.SetData(trimmed, 0);
            return c;
        }

        // the foghorn: a two-tone diaphone far across the water, with its echo off the hills
        static AudioClip Horn()
        {
            float Note(float t, float f, float dur)
            {
                if (t < 0 || t > dur) return 0;
                float a = Mathf.Clamp01(t / 0.35f) * Mathf.Clamp01((dur - t) / 0.6f);
                float ph = t * f;
                float v = 4f * Mathf.Abs(ph - Mathf.Floor(ph + 0.5f)) - 1f;                       // triangle
                v += 0.35f * Mathf.Sin(2 * Mathf.PI * ph * 2f) + 0.15f * Mathf.Sin(2 * Mathf.PI * ph * 3f);
                return v * a;
            }
            float Dry(float t) => 0.16f * Note(t, 98, 2.6f) + 0.14f * Note(t - 2.4f, 73, 2.2f);
            float lp = 0;
            return Make(6.5f, (t, i) =>
            {
                float x = Dry(t) + 0.35f * Dry(t - 0.42f) + 0.15f * Dry(t - 0.95f);
                lp += (x - lp) * 0.08f;                                                          // far off: dull
                return lp * 1.6f;
            }, "foghorn");
        }

        // a footstep: a leather heel on stone (a click and a scuff) over a low thud of weight
        static AudioClip Step(System.Random rng, int k)
        {
            float pitch = 0.9f + 0.05f * k, hp = 0, prev = 0, lp = 0;
            return Make(0.22f, (t, i) =>
            {
                float n = (float)rng.NextDouble() * 2 - 1;
                hp = 0.75f * (hp + n - prev); prev = n;                                          // brighter noise
                lp += (n - lp) * 0.25f;
                float click = hp * Mathf.Exp(-t * 180f) * 0.5f;
                float scuff = lp * Mathf.Exp(-t * 28f) * 0.35f;
                float thud = Mathf.Sin(2 * Mathf.PI * 85f * pitch * t) * Mathf.Exp(-t * 40f) * 0.5f;
                return click + scuff + thud;
            }, "step");
        }
    }

}
