// The Hopkins house in Unity is the prototype's house doubled (Tools/blender/hopkins_warp.py): a piecewise-linear
// stretch, separate in x, y and z, that widens the rooms and slides the walls apart. The case data still speaks in
// the prototype's coordinates, so Chapter IV carries its positions (people, places, where Holmes wakes) through the
// same map. Keep the breaks here in step with hopkins_warp.py.
using UnityEngine;

namespace BlackBird
{
    public static class HopkinsWarp
    {
        static readonly float[] X0 = { 0.0f, 5.8f, 8.2f, 13.8f, 16.2f };                                            // |x|: prototype
        static readonly float[] X1 = { 0.0f, 12.8f, 15.2f, 28.8f, 31.2f };                                          //      Unity set
        static readonly float[] Z0 = { -15.2f, -12.8f, -7.2f, -4.8f, 0.8f, 3.2f, 8.8f, 11.2f, 12.8f, 15.2f };
        static readonly float[] Z1 = { -23.2f, -20.8f, -7.2f, -4.8f, 8.8f, 11.2f, 24.8f, 27.2f, 32.8f, 35.2f };
        const float YScale = 1.6f;

        static float Pw(float[] a, float[] b, float v)
        {
            if (v <= a[0]) return b[0] + (v - a[0]);
            int n = a.Length - 1;
            if (v >= a[n]) return b[n] + (v - a[n]);
            for (int i = 0; i < n; i++)
                if (v <= a[i + 1]) return b[i] + (v - a[i]) / (a[i + 1] - a[i]) * (b[i + 1] - b[i]);
            return v;
        }

        /// A point in the prototype's house (web coordinates) to the same point in the doubled house (web coordinates).
        public static Vector3 Map(Vector3 p) =>
            new Vector3(Mathf.Sign(p.x) * Pw(X0, X1, Mathf.Abs(p.x)), p.y > 0 ? p.y * YScale : p.y, Pw(Z0, Z1, p.z));
    }
}
