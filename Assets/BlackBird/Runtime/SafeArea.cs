using UnityEngine;

namespace BlackBird
{
    // Keeps its RectTransform inside the screen's safe area (notch, home indicator).
    public class SafeArea : MonoBehaviour
    {
        Rect last;
        void Update()
        {
            var s = Screen.safeArea;
            if (s == last || Screen.width == 0) return;
            last = s;
            var r = (RectTransform)transform;
            r.anchorMin = new Vector2(s.xMin / Screen.width, s.yMin / Screen.height);
            r.anchorMax = new Vector2(s.xMax / Screen.width, s.yMax / Screen.height);
            r.offsetMin = r.offsetMax = Vector2.zero;
        }
    }
}
