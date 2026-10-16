using UnityEngine;

namespace BlackBird
{
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
