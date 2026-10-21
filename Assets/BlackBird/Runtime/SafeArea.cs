using UnityEngine;

namespace BlackBird
{
    // Keeps its RectTransform inside the screen's safe area (notch, home indicator). On Apple TV the whole screen is
    // reported safe, but televisions crop their edges (overscan), and text along the top was cut off: there it keeps
    // to Apple's title-safe area instead (60 pt top and bottom, 80 pt either side, on a 1920 x 1080 screen).
    public class SafeArea : MonoBehaviour
    {
        Rect last;
        void Update()
        {
            var s = Screen.safeArea;
            if (Application.platform == RuntimePlatform.tvOS)
            {
                float mx = Screen.width * (80f / 1920f), my = Screen.height * (60f / 1080f);
                s = Rect.MinMaxRect(Mathf.Max(s.xMin, mx), Mathf.Max(s.yMin, my), Mathf.Min(s.xMax, Screen.width - mx), Mathf.Min(s.yMax, Screen.height - my));
            }
            if (s == last || Screen.width == 0) return;
            last = s;
            var r = (RectTransform)transform;
            r.anchorMin = new Vector2(s.xMin / Screen.width, s.yMin / Screen.height);
            r.anchorMax = new Vector2(s.xMax / Screen.width, s.yMax / Screen.height);
            r.offsetMin = r.offsetMax = Vector2.zero;
        }
    }
}
