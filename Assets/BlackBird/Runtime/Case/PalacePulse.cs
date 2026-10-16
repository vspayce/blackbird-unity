using UnityEngine;

namespace BlackBird
{
    /// A fact newly drawn in the Mind Palace swells and settles; a selected one breathes.
    public class PalacePulse : MonoBehaviour
    {
        public float born, amount = 0.6f;
        public bool breathe;
        Vector3 baseScale;
        void Start() { baseScale = transform.localScale; born = Time.unscaledTime; }
        void Update()
        {
            float age = Time.unscaledTime - born;
            float s = breathe ? 1 + 0.12f * Mathf.Sin(age * 4f) : 1 + amount * Mathf.Exp(-age * 3f) * Mathf.Cos(age * 9f);
            transform.localScale = baseScale * s;
            if (!breathe && age > 2.5f) { transform.localScale = baseScale; enabled = false; }
        }
    }
}
