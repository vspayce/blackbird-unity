using UnityEngine;

namespace BlackBird
{
    /// A walker's steps, timed by the distance it really covers.
    public class Footsteps : MonoBehaviour
    {
        AudioSource src;
        Vector3 last;
        float travelled;
        int lastClip = -1;

        void Start()
        {
            src = gameObject.AddComponent<AudioSource>();
            src.spatialBlend = 1f; src.rolloffMode = AudioRolloffMode.Linear; src.minDistance = 1.5f; src.maxDistance = 22f;
            src.playOnAwake = false; src.dopplerLevel = 0;
            last = transform.position;
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
            if (travelled < stride || Sound.I == null || Sound.I.steps == null) return;
            travelled = 0;
            int k; do k = Random.Range(0, Sound.I.steps.Length); while (k == lastClip && Sound.I.steps.Length > 1);
            lastClip = k;
            src.pitch = Random.Range(0.94f, 1.06f);
            src.PlayOneShot(Sound.I.steps[k], Random.Range(0.5f, 0.65f));
        }
    }
}
