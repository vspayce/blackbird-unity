// The on-screen interface, built in code so the scene stays simple: objective, captions, clue slips,
// the context and Focus buttons, the touch stick, Focus reads, close-up markers and letterbox bars.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BlackBird
{
    public class Hud : MonoBehaviour
    {
        public static Hud I { get; private set; }
        public Font serif;        // IM Fell English SC
        public Font type;         // reads (falls back to the built-in font)

        static readonly Color Ink = new Color(0.94f, 0.9f, 0.82f), Gold = new Color(1f, 0.78f, 0.42f), Blue = new Color(0.55f, 0.75f, 1f);

        RectTransform root;
        Text objective, captionWho, captionLine, slipTitle, slipText, actLabel, counter, hint;
        CanvasGroup caption, slip, actGroup, focusGroup, stickGroup, backGroup, rotate;
        Image meterFill, stickBase, stickKnob, focusIcon;
        RectTransform barTop, barBottom;
        readonly List<Text> reads = new List<Text>();
        readonly List<(Button b, Text t, Vector3 world, bool focusOnly, Func<bool> found)> marks = new List<(Button, Text, Vector3, bool, Func<bool>)>();
        float slipTimer, letterbox, letterboxTarget;
        Sprite circle, ring;
        Camera cam;
        Action onCaptionDone;

        public bool CaptionOpen => caption && caption.alpha > 0.5f && captionLine.text.Length > 0 && onCaptionShown;
        bool onCaptionShown;

        void Awake()
        {
            I = this;
            cam = Camera.main;
            circle = MakeCircle(128, false); ring = MakeCircle(128, true);
            if (!type) type = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (!serif) serif = type;
            Build();
        }

        // ---- construction -----------------------------------------------------------------------
        void Build()
        {
            var canvasGo = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 1f;
            root = (RectTransform)canvasGo.transform;
            var safe = Rect("Safe", root); safe.gameObject.AddComponent<SafeArea>();

            barTop = Bar("BarTop", root, true); barBottom = Bar("BarBottom", root, false);

            objective = Label(safe, "Objective", serif, 26, TextAnchor.UpperLeft, Ink);
            Place(objective.rectTransform, new Vector2(0, 1), new Vector2(28, -22), new Vector2(700, 90));
            counter = Label(safe, "Counter", serif, 20, TextAnchor.UpperRight, new Color(Ink.r, Ink.g, Ink.b, 0.7f));
            Place(counter.rectTransform, new Vector2(1, 1), new Vector2(-28, -24), new Vector2(360, 40));

            // caption
            var cap = Panel(safe, "Caption", new Color(0.03f, 0.04f, 0.05f, 0.82f));
            Place(cap, new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(820, 150));
            caption = cap.gameObject.AddComponent<CanvasGroup>(); caption.alpha = 0; caption.blocksRaycasts = false;
            captionWho = Label(cap, "Who", serif, 22, TextAnchor.UpperLeft, Gold);
            Stretch(captionWho.rectTransform, 26, 14, 26, 108);
            captionLine = Label(cap, "Line", serif, 27, TextAnchor.UpperLeft, Ink);
            Stretch(captionLine.rectTransform, 26, 46, 26, 14);
            var capBtn = cap.gameObject.AddComponent<Button>(); capBtn.transition = Selectable.Transition.None;
            capBtn.onClick.AddListener(AdvanceCaption);

            // clue slip
            var sl = Panel(safe, "Slip", new Color(0.86f, 0.8f, 0.66f, 0.96f));
            Place(sl, new Vector2(1, 1), new Vector2(-28, -70), new Vector2(380, 150));
            slip = sl.gameObject.AddComponent<CanvasGroup>(); slip.alpha = 0; slip.blocksRaycasts = false;
            slipTitle = Label(sl, "Title", serif, 24, TextAnchor.UpperLeft, new Color(0.25f, 0.12f, 0.05f));
            Stretch(slipTitle.rectTransform, 18, 12, 18, 104);
            slipText = Label(sl, "Text", serif, 19, TextAnchor.UpperLeft, new Color(0.16f, 0.12f, 0.08f));
            Stretch(slipText.rectTransform, 18, 46, 18, 10);

            // Focus button with its meter, and the context button above it
            var fb = RoundButton(safe, "FocusButton", new Vector2(-70, 80), 108, () => GameInput.I.PressFocus());
            focusGroup = fb.gameObject.AddComponent<CanvasGroup>();
            meterFill = Img(fb, "Meter", ring, Gold); Stretch(meterFill.rectTransform, -6, -6, -6, -6);
            meterFill.type = Image.Type.Filled; meterFill.fillMethod = Image.FillMethod.Radial360; meterFill.fillOrigin = 2; meterFill.raycastTarget = false;
            focusIcon = Img(fb, "Eye", circle, Ink); Place(focusIcon.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30, 30));
            focusIcon.raycastTarget = false;
            var eye = Label(fb, "EyeLabel", serif, 18, TextAnchor.MiddleCenter, Ink); eye.text = "FOCUS"; Place(eye.rectTransform, new Vector2(0.5f, 0), new Vector2(0, -20), new Vector2(120, 26));

            var ab = Panel(safe, "ActButton", new Color(0.05f, 0.06f, 0.07f, 0.78f));
            Place(ab, new Vector2(1, 0), new Vector2(-28, 168), new Vector2(330, 64));
            ab.pivot = new Vector2(1, 0);
            var abtn = ab.gameObject.AddComponent<Button>(); abtn.onClick.AddListener(() => GameInput.I.PressAct());
            actGroup = ab.gameObject.AddComponent<CanvasGroup>(); actGroup.alpha = 0; actGroup.blocksRaycasts = false;
            actLabel = Label(ab, "Label", serif, 24, TextAnchor.MiddleCenter, Ink); Stretch(actLabel.rectTransform, 12, 6, 12, 6);

            var bb = Panel(safe, "BackButton", new Color(0.05f, 0.06f, 0.07f, 0.78f));
            Place(bb, new Vector2(0, 0), new Vector2(28, 28), new Vector2(200, 60)); bb.pivot = Vector2.zero;
            bb.gameObject.AddComponent<Button>().onClick.AddListener(() => GameInput.I.PressBack());
            backGroup = bb.gameObject.AddComponent<CanvasGroup>(); backGroup.alpha = 0; backGroup.blocksRaycasts = false;
            var bl = Label(bb, "Label", serif, 24, TextAnchor.MiddleCenter, Ink); bl.text = "Step back"; Stretch(bl.rectTransform, 6, 6, 6, 6);

            // touch stick
            var st = Rect("Stick", root);
            stickGroup = st.gameObject.AddComponent<CanvasGroup>(); stickGroup.blocksRaycasts = false; stickGroup.interactable = false;
            stickBase = Img(st, "Base", ring, new Color(1, 1, 1, 0.35f)); stickBase.raycastTarget = false;
            stickKnob = Img(st, "Knob", circle, new Color(1, 1, 1, 0.45f)); stickKnob.raycastTarget = false;
            foreach (var i in new[] { stickBase, stickKnob }) { i.rectTransform.anchorMin = i.rectTransform.anchorMax = Vector2.zero; }

            hint = Label(safe, "Hint", type, 16, TextAnchor.LowerLeft, new Color(Ink.r, Ink.g, Ink.b, 0.45f));
            Place(hint.rectTransform, new Vector2(0, 0), new Vector2(28, 12), new Vector2(900, 30)); hint.rectTransform.pivot = Vector2.zero;
            hint.text = "WASD walk · drag to look · E examine · F Focus      Gamepad: Ⓐ act · Ⓧ Focus · Ⓑ back";

            // rotate prompt for phones held upright
            var rp = Panel(root, "Rotate", new Color(0.03f, 0.04f, 0.05f, 1f)); Stretch(rp, 0, 0, 0, 0);
            rotate = rp.gameObject.AddComponent<CanvasGroup>(); rotate.alpha = 0; rotate.blocksRaycasts = false;
            var rt = Label(rp, "Text", serif, 40, TextAnchor.MiddleCenter, Ink); rt.text = "Turn your phone sideways."; Stretch(rt.rectTransform, 40, 40, 40, 40);
        }

        // ---- public API -------------------------------------------------------------------------
        public void SetObjective(string title, string line) { objective.text = $"<size=20>{title.ToUpperInvariant()}</size>\n{line}"; }
        public void SetCounter(string s) => counter.text = s;

        public void Say(string who, string line, Action done = null)
        {
            captionWho.text = who; captionLine.text = line;
            onCaptionDone = done; onCaptionShown = true;
            caption.blocksRaycasts = true;
        }
        public void AdvanceCaption()
        {
            if (!onCaptionShown) return;
            onCaptionShown = false; caption.blocksRaycasts = false;
            var d = onCaptionDone; onCaptionDone = null; d?.Invoke();
        }

        public void ClueSlip(Clue c) { slipTitle.text = "New clue · " + c.title; slipText.text = c.text; slipTimer = 6f; }

        public void Context(string label, bool touch)
        {
            bool show = !string.IsNullOrEmpty(label);
            if (show) actLabel.text = touch ? label : $"{label}  <color=#c9a46a>[E]</color>";
            actGroup.alpha = Mathf.MoveTowards(actGroup.alpha, show ? 1 : 0, Time.unscaledDeltaTime * 6f);
            actGroup.blocksRaycasts = show;
        }

        public void Letterbox(bool on) => letterboxTarget = on ? 1 : 0;
        public void BackButton(bool on) { backGroup.alpha = on ? 1 : 0; backGroup.blocksRaycasts = on; }

        public void ClearMarks() { foreach (var m in marks) Destroy(m.b.gameObject); marks.Clear(); }
        public void AddMark(Vector3 world, string label, bool focusOnly, Func<bool> found, Action onTap)
        {
            var r = Panel(root, "Mark_" + label, new Color(0, 0, 0, 0));
            r.sizeDelta = new Vector2(84, 84); r.anchorMin = r.anchorMax = Vector2.zero;
            var ringImg = Img(r, "Ring", ring, Ink); Stretch(ringImg.rectTransform, 16, 16, 16, 16); ringImg.raycastTarget = false;
            var b = r.gameObject.AddComponent<Button>(); b.onClick.AddListener(() => onTap());
            r.GetComponent<Image>().raycastTarget = true;
            var t = Label(r, "Label", serif, 18, TextAnchor.UpperCenter, Ink); t.text = label;
            Place(t.rectTransform, new Vector2(0.5f, 0), new Vector2(0, -6), new Vector2(160, 26)); t.rectTransform.pivot = new Vector2(0.5f, 1);
            marks.Add((b, t, world, focusOnly, found));
        }

        public void ShowReads(IList<(Vector3 world, string text)> items, float k)
        {
            while (reads.Count < items.Count)
            {
                var t = Label(root, "Read", type, 17, TextAnchor.MiddleLeft, Blue);
                t.rectTransform.anchorMin = t.rectTransform.anchorMax = Vector2.zero; t.rectTransform.sizeDelta = new Vector2(420, 28);
                t.rectTransform.pivot = new Vector2(0, 0.5f);
                var sh = t.gameObject.AddComponent<Shadow>(); sh.effectColor = new Color(0, 0, 0, 0.8f);
                reads.Add(t);
            }
            float scale = root.localScale.x;
            for (int i = 0; i < reads.Count; i++)
            {
                bool on = i < items.Count && k > 0.02f;
                if (!on) { reads[i].enabled = false; continue; }
                var sp = cam.WorldToScreenPoint(items[i].world);
                if (sp.z < 0.5f || sp.z > 14f) { reads[i].enabled = false; continue; }
                reads[i].enabled = true;
                reads[i].rectTransform.anchoredPosition = (Vector2)sp / scale;
                // type the line out as Focus comes up
                int n = Mathf.Clamp(Mathf.RoundToInt(items[i].text.Length * Mathf.Clamp01(k * 1.4f)), 0, items[i].text.Length);
                reads[i].text = "— " + items[i].text.Substring(0, n);
                var c = Blue; c.a = Mathf.Clamp01(k * 1.5f) * Mathf.InverseLerp(14f, 8f, sp.z); reads[i].color = c;
            }
        }

        public void SetTouchMode(bool touch) { hint.enabled = !touch; stickGroup.alpha = touch ? 1 : 0; }

        void LateUpdate()
        {
            if (!cam) cam = Camera.main;
            float dt = Time.unscaledDeltaTime;
            float scale = root.localScale.x;

            caption.alpha = Mathf.MoveTowards(caption.alpha, onCaptionShown ? 1 : 0, dt * 5f);
            slipTimer -= dt; slip.alpha = Mathf.MoveTowards(slip.alpha, slipTimer > 0 ? 1 : 0, dt * 3f);

            letterbox = Mathf.MoveTowards(letterbox, letterboxTarget, dt * 2f);
            float h = Mathf.SmoothStep(0, 1, letterbox) * 80f;
            barTop.sizeDelta = new Vector2(0, h); barBottom.sizeDelta = new Vector2(0, h);

            var f = Focus.I;
            if (f)
            {
                meterFill.fillAmount = f.Meter;
                focusIcon.color = Color.Lerp(Ink, Blue, f.Amount);
            }

            var gi = GameInput.I;
            if (gi)
            {
                float r = gi.StickRadius / scale;
                stickBase.rectTransform.sizeDelta = new Vector2(r * 2, r * 2);
                stickKnob.rectTransform.sizeDelta = new Vector2(r * 0.9f, r * 0.9f);
                stickBase.rectTransform.anchoredPosition = gi.StickCenter / scale;
                stickKnob.rectTransform.anchoredPosition = gi.StickKnob / scale;
                var c = stickBase.color; c.a = gi.StickActive ? 0.45f : 0.18f; stickBase.color = c;
            }

            foreach (var m in marks)
            {
                var sp = cam.WorldToScreenPoint(m.world);
                bool vis = sp.z > 0 && (!m.focusOnly || m.found() || (f && f.Amount > 0.3f));
                m.b.gameObject.SetActive(vis);
                if (!vis) continue;
                ((RectTransform)m.b.transform).anchoredPosition = (Vector2)sp / scale;
                var ringImg = m.b.transform.GetChild(0).GetComponent<Image>();
                ringImg.color = m.found() ? Gold : (m.focusOnly ? Blue : Ink);
            }

            rotate.alpha = Screen.height > Screen.width * 1.05f && Application.isMobilePlatform ? 1 : 0;
            rotate.blocksRaycasts = rotate.alpha > 0.5f;
        }

        // ---- helpers ----------------------------------------------------------------------------
        static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform; Stretch(r, 0, 0, 0, 0); return r;
        }
        static RectTransform Panel(Transform parent, string name, Color c)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>(); img.color = c; img.raycastTarget = c.a > 0.01f;
            return (RectTransform)go.transform;
        }
        static Image Img(Transform parent, string name, Sprite s, Color c)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>(); img.sprite = s; img.color = c; return img;
        }
        static Text Label(Transform parent, string name, Font font, int size, TextAnchor anchor, Color c)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>(); t.font = font; t.fontSize = size; t.alignment = anchor; t.color = c;
            t.supportRichText = true; t.raycastTarget = false; t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }
        static void Place(RectTransform r, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            r.anchorMin = r.anchorMax = anchor; r.pivot = anchor; r.anchoredPosition = pos; r.sizeDelta = size;
        }
        static void Stretch(RectTransform r, float l, float t, float rt, float b)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = new Vector2(l, b); r.offsetMax = new Vector2(-rt, -t);
        }
        RectTransform Bar(string name, Transform parent, bool top)
        {
            var r = Panel(parent, name, Color.black);
            r.GetComponent<Image>().raycastTarget = false;
            r.anchorMin = new Vector2(0, top ? 1 : 0); r.anchorMax = new Vector2(1, top ? 1 : 0); r.pivot = new Vector2(0.5f, top ? 1 : 0);
            r.anchoredPosition = Vector2.zero; r.sizeDelta = Vector2.zero; return r;
        }
        RectTransform RoundButton(Transform parent, string name, Vector2 fromBottomRight, float size, Action onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>(); img.sprite = circle; img.color = new Color(0.05f, 0.06f, 0.07f, 0.72f);
            go.GetComponent<Button>().onClick.AddListener(() => onClick());
            var r = (RectTransform)go.transform; r.anchorMin = r.anchorMax = new Vector2(1, 0); r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = new Vector2(fromBottomRight.x - size / 2 + 20, fromBottomRight.y); r.sizeDelta = new Vector2(size, size);
            return r;
        }
        static Sprite MakeCircle(int n, bool ringOnly)
        {
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            float c = (n - 1) / 2f;
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                float a = Mathf.Clamp01((1f - d) * c);                       // soft edge
                if (ringOnly) a *= Mathf.Clamp01((d - 0.86f) * c);
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(px); tex.Apply();
            return Sprite.Create(tex, new UnityEngine.Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100);
        }
    }

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
