// The look of the Mind Palace: a soft round glow (made once, in code) for the facts and the light in the dark,
// and the motes that drift through it.
using UnityEngine;
using UnityEngine.UI;

namespace BlackBird
{
    public class PalaceFX : MonoBehaviour
    {
        static Sprite glow;

        /// A white disc that fades to nothing at its edge (tint it with Image.color).
        public static Sprite Glow
        {
            get
            {
                if (glow) return glow;
                const int N = 64;
                var t = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                var px = new Color32[N * N];
                for (int y = 0; y < N; y++)
                    for (int x = 0; x < N; x++)
                    {
                        float dx = (x + 0.5f) / N * 2 - 1, dy = (y + 0.5f) / N * 2 - 1;
                        float r = Mathf.Sqrt(dx * dx + dy * dy);
                        float a = Mathf.Clamp01(1 - r); a = a * a * (3 - 2 * a);            // smooth falloff
                        float core = Mathf.Clamp01(1 - r * 2.2f);                          // a brighter heart
                        px[y * N + x] = new Color(1, 1, 1, Mathf.Clamp01(a * 0.75f + core * 0.6f));
                    }
                t.SetPixels32(px); t.Apply();
                glow = Sprite.Create(t, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100);
                return glow;
            }
        }

        public static Image GlowImage(Transform parent, string name, Color c, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>(); img.sprite = Glow; img.color = c; img.raycastTarget = false;
            ((RectTransform)go.transform).sizeDelta = size;
            return img;
        }

        // ---- motes: specks of light drifting up through the dark, each twinkling on its own ----
        RectTransform area;
        RectTransform[] motes;
        Image[] images;
        Vector2[] vel;
        float[] phase, speed, baseA;

        public static PalaceFX Motes(RectTransform area, int n = 70)
        {
            var fx = area.gameObject.AddComponent<PalaceFX>();
            fx.area = area;
            fx.motes = new RectTransform[n]; fx.images = new Image[n]; fx.vel = new Vector2[n];
            fx.phase = new float[n]; fx.speed = new float[n]; fx.baseA = new float[n];
            for (int i = 0; i < n; i++)
            {
                float s = Random.Range(4f, 14f);
                var warm = Random.value < 0.25f;
                var img = GlowImage(area, "Mote", warm ? new Color(1f, 0.8f, 0.5f, 0.5f) : new Color(0.6f, 0.8f, 1f, 0.5f), new Vector2(s, s));
                var r = img.rectTransform; r.anchorMin = r.anchorMax = Vector2.zero;
                fx.motes[i] = r; fx.images[i] = img;
                fx.vel[i] = new Vector2(Random.Range(-6f, 6f), Random.Range(4f, 16f));
                fx.phase[i] = Random.value * 10; fx.speed[i] = Random.Range(0.6f, 1.8f); fx.baseA[i] = Random.Range(0.15f, 0.55f);
            }
            fx.scattered = false;
            return fx;
        }

        bool scattered;

        void Update()
        {
            if (motes == null) return;
            var size = area.rect.size;
            if (size.x < 10) return;
            if (!scattered)
            {
                foreach (var m in motes) m.anchoredPosition = new Vector2(Random.value * size.x, Random.value * size.y);
                scattered = true;
            }
            float dt = Time.unscaledDeltaTime, t = Time.unscaledTime;
            for (int i = 0; i < motes.Length; i++)
            {
                var p = motes[i].anchoredPosition + vel[i] * dt;
                p.x += Mathf.Sin(t * 0.3f + phase[i]) * 4 * dt;
                if (p.y > size.y + 10) { p.y = -10; p.x = Random.value * size.x; }
                if (p.x < -10) p.x = size.x + 10; else if (p.x > size.x + 10) p.x = -10;
                motes[i].anchoredPosition = p;
                var c = images[i].color; c.a = baseA[i] * (0.55f + 0.45f * Mathf.Sin(t * speed[i] + phase[i])); images[i].color = c;
            }
        }
    }
}
