// Kearny Street (Chapter V): a Sutter Street cable car crosses the intersection, waits off in the fog, and comes back
// the other way. It rings its bell coming up to Kearny, lights the fog round it, blocks the way while it passes,
// and hides you from anyone on the far side of it (Tail asks InCover).
using UnityEngine;

namespace BlackBird
{
    public class CableCar : MonoBehaviour
    {
        public Transform car;                    // the car's model (moved along Sutter Street)
        public float z, x0, x1, speed = 4.5f, period = 75f, length = 8f, width = 2.5f;   // the web game's coordinates
        public static CableCar I { get; private set; }

        Vector3 origin;                          // where the model sits when it is at x0
        float t;
        bool rang;
        Light glow;
        BoxCollider box;
        Renderer[] renderers;

        public bool Visible { get; private set; }
        public float WebX { get; private set; }

        void Awake() => I = this;

        void Start()
        {
            if (!car) { enabled = false; return; }
            origin = car.position;
            t = period * 0.55f;                  // the first pass comes soon after you reach the first block's end
            renderers = car.GetComponentsInChildren<Renderer>(true);
            var go = new GameObject("CarCollider");
            go.transform.SetParent(car, false);
            box = go.AddComponent<BoxCollider>();
            var rb = go.AddComponent<Rigidbody>(); rb.isKinematic = true;
            glow = new GameObject("CarLamp").AddComponent<Light>();
            glow.type = LightType.Point; glow.color = new Color(1f, 0.82f, 0.54f); glow.range = 9f; glow.intensity = 0;
            glow.lightmapBakeType = LightmapBakeType.Realtime; glow.shadows = LightShadows.None;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            t = (t + dt) % (period * 2);
            float run = (x1 - x0) / speed, leg = t % period;
            int dir = t < period ? 1 : -1;
            float k = Mathf.Min(1, leg / run);
            WebX = dir > 0 ? Mathf.Lerp(x0, x1, k) : Mathf.Lerp(x1, x0, k);
            Visible = leg < run;
            foreach (var r in renderers) r.enabled = Visible;
            car.position = origin + (Web.W(WebX, 0, z) - Web.W(x0, 0, z));
            box.enabled = Visible;
            box.transform.position = Web.W(WebX, 1.5f, z);
            box.transform.rotation = Quaternion.identity;
            box.size = new Vector3(length, 3f, width);
            box.center = Vector3.zero;
            if (Visible)
            {
                glow.transform.position = Web.W(WebX + dir * (length / 2 + 1), 2.2f, z);
                glow.intensity = 6f;
                if (!rang && Mathf.Abs(WebX) < 16) { rang = true; Sound.Bell(0.6f); }
            }
            else { glow.intensity = 0; rang = false; }
        }

        /// Is the car between a watcher at `from` and a point `at` (both world positions)?
        public bool Between(Vector3 from, Vector3 at)
        {
            if (!Visible) return false;
            var b = new Bounds(Web.W(WebX, 1.5f, z), new Vector3(length, 3f, width));
            var dir = at - from; dir.y = 0;
            float d = dir.magnitude; if (d < 0.01f) return false;
            var ray = new Ray(new Vector3(from.x, 1.4f, from.z), dir / d);
            return b.IntersectRay(ray, out float hit) && hit < d;
        }
    }
}
