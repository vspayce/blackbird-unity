// Muybridge's 1878 panorama of San Francisco, taken from the Hopkins tower, wrapped round the horizon so the
// real city of the time stands beyond the modelled grounds by day. The image covers 360 degrees across and
// about 38 degrees up and down; horizon and heading are tunable so the bay and the hills line up.
using UnityEngine;

namespace BlackBird
{
    [ExecuteAlways]
    public class Panorama : MonoBehaviour
    {
        public Material material;
        public Vector3 centre;
        public float radius = 2400f;
        [Tooltip("Where the horizon falls in the image, from the top (0) to the bottom (1).")]
        [Range(0, 1)] public float horizon = 0.42f;
        [Tooltip("Degrees to turn the image so its bay lies over the real bay.")]
        public float heading = 0f;
        public float verticalDegrees = 38.3f;
        [Tooltip("The image is a strip of joined plates; repeat its first column so the seam closes.")]
        public int segments = 128;

        Mesh mesh;
        float built;

        void OnEnable() => Build();
        void OnValidate() => built = -1;
        void Update() { if (built < 0) Build(); }

        void Build()
        {
            built = 1;
            if (!mesh) { mesh = new Mesh { name = "Panorama" }; mesh.hideFlags = HideFlags.DontSave; }
            float up = verticalDegrees * horizon * Mathf.Deg2Rad, down = verticalDegrees * (1 - horizon) * Mathf.Deg2Rad;
            float top = radius * Mathf.Tan(up), bottom = -radius * Mathf.Tan(down);
            var v = new Vector3[(segments + 1) * 2]; var uv = new Vector2[v.Length]; var t = new int[segments * 6];
            for (int i = 0; i <= segments; i++)
            {
                float a = (heading + 360f * i / segments) * Mathf.Deg2Rad;
                var d = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * radius;
                v[i * 2] = d + Vector3.up * bottom; v[i * 2 + 1] = d + Vector3.up * top;
                uv[i * 2] = new Vector2(i / (float)segments, 0); uv[i * 2 + 1] = new Vector2(i / (float)segments, 1);
                if (i < segments) { int b = i * 2; int k = i * 6; t[k] = b; t[k + 1] = b + 1; t[k + 2] = b + 2; t[k + 3] = b + 1; t[k + 4] = b + 3; t[k + 5] = b + 2; }
            }
            mesh.Clear(); mesh.vertices = v; mesh.uv = uv; mesh.triangles = t; mesh.RecalculateBounds();
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * radius * 3);
            transform.position = centre;
            var mf = GetComponent<MeshFilter>(); if (!mf) mf = gameObject.AddComponent<MeshFilter>();
            var mr = GetComponent<MeshRenderer>(); if (!mr) mr = gameObject.AddComponent<MeshRenderer>();
            mf.sharedMesh = mesh; mr.sharedMaterial = material;
            // clamp across, so the filter doesn't pull the far end's sky into the strip's first column (a bright seam)
            if (material && material.HasProperty("_BaseMap") && material.GetTexture("_BaseMap") is Texture pt && pt.wrapModeU != TextureWrapMode.Clamp)
                pt.wrapModeU = TextureWrapMode.Clamp;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
        }
    }
}
