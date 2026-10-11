using UnityEngine;

namespace BlackBird
{
    // Turns to face the camera (for glints and wisps).
    public class Billboard : MonoBehaviour
    {
        public bool bob; public float bobHeight = 0.1f, sway = 0.25f;
        Vector3 home; float seed;
        void Start() { home = transform.position; seed = Random.value * 10f; }
        void LateUpdate()
        {
            var cam = Camera.main; if (!cam) return;
            transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
            if (bob)
            {
                float t = Time.time + seed;
                transform.position = home + new Vector3(Mathf.Sin(t * 0.7f) * sway, Mathf.Sin(t * 0.5f) * bobHeight, Mathf.Cos(t * 0.6f) * sway);
            }
        }
    }

    // What the builder measured from the set (for reference; the mapping itself is Web.SX / Web.SZ).
    public class WorldAxes : MonoBehaviour
    {
        public float sx = Web.SX, sz = Web.SZ;
    }
}
