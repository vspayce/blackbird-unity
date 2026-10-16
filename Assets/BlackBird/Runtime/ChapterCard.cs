// The card that opens each chapter: its number and name over the dark, where it happens, and a tip along the
// bottom. It fades in, holds a few seconds (or until a press), and fades out on the chapter.
using System;
using UnityEngine;
using UnityEngine.UI;

namespace BlackBird
{
    public class ChapterCard : MonoBehaviour
    {
        static readonly string[] Tips =
        {
            "The casebook keeps your leads: the open one at the top, the ones followed up ticked off below it.",
            "Focus shows what others miss: a mark on a sleeve, a glint in a window, a detail out of place.",
            "In the Mind Palace, choose two facts that speak to one another. A false start costs a little of your rating.",
            "When someone lies, press them, or prove it with something you have found.",
            "Every fact has a colour in the Mind Palace: seen, heard, remembered, worked out, and the ones that answer the case.",
            "Being followed, or following? Doorways, lamp posts and a passing cable car all hide you.",
        };

        CanvasGroup group;
        Action done;
        float t;
        bool leaving;

        /// Show the card for a chapter (by its case id), then call `then`. Returns false if the case isn't a chapter.
        public static bool Show(string caseId, bool resumed, Action then)
        {
            int i = Array.FindIndex(TitleScreen.Chapters, c => c.caseId == caseId);
            if (i < 0) return false;
            var c = TitleScreen.Chapters[i];
            var go = new GameObject("ChapterCard");
            var card = go.AddComponent<ChapterCard>();
            card.done = then;
            card.Build($"Chapter {c.numeral}", c.name, c.where, resumed ? "Resuming where you left off. " + Tips[0] : Tips[i % Tips.Length]);
            return true;
        }

        void Build(string number, string title, string where, string tip)
        {
            var canvas = gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 60;
            var sc = gameObject.AddComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1280, 720); sc.matchWidthOrHeight = 1f;
            gameObject.AddComponent<GraphicRaycaster>();
            group = gameObject.AddComponent<CanvasGroup>(); group.alpha = 0;

            var bg = UIKit.Panel(transform, "Dark", new Color(0.01f, 0.012f, 0.02f, 1)); UIKit.Stretch(bg, 0, 0, 0, 0);
            var lamp = PalaceFX.GlowImage(bg, "Lamp", new Color(1f, 0.7f, 0.38f, 0.16f), new Vector2(1300, 620));
            lamp.rectTransform.anchoredPosition = new Vector2(0, 30);
            var mist = PalaceFX.GlowImage(bg, "Mist", new Color(0.35f, 0.45f, 0.65f, 0.12f), new Vector2(1900, 500));
            mist.rectTransform.anchoredPosition = new Vector2(0, -250);

            var safe = UIKit.Rect("Safe", transform); safe.gameObject.AddComponent<SafeArea>();
            Text L(string n, int size, Color c, float y, float h, Font f = null)
            {
                var t = UIKit.Label(safe, n, size, TextAnchor.MiddleCenter, c, f);
                UIKit.Place(t.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, y), new Vector2(1100, h));
                return t;
            }
            L("Number", 24, new Color(0.78f, 0.67f, 0.43f), 120, 40).text = number.ToUpperInvariant();
            L("Title", 72, new Color(0.95f, 0.89f, 0.75f), 50, 100).text = title;
            var rule = UIKit.Panel(safe, "Rule", new Color(0.78f, 0.67f, 0.43f, 0.6f));
            UIKit.Place(rule, new Vector2(0.5f, 0.5f), new Vector2(0, -12), new Vector2(220, 1.5f));
            L("Where", 26, new Color(1, 1, 1, 0.7f), -50, 40).text = where;
            var tipText = UIKit.Label(safe, "Tip", 19, TextAnchor.MiddleCenter, new Color(1, 1, 1, 0.55f), UIKit.type);
            UIKit.Place(tipText.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 50), new Vector2(980, 60));
            tipText.text = tip;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            t += dt;
            if (!leaving)
            {
                group.alpha = Mathf.Clamp01(t / 0.6f);
                var gi = GameInput.I;
                bool press = t > 0.8f && gi && (gi.Act || gi.Back);
                if (!press && t > 0.8f)
                {
                    var p = UnityEngine.InputSystem.Pointer.current;
                    press = p != null && p.press.wasPressedThisFrame;
                }
                if (t > 4.2f || press) { leaving = true; t = 0; var cb = done; done = null; cb?.Invoke(); }
            }
            else
            {
                group.alpha = 1 - Mathf.Clamp01(t / 0.7f);
                group.blocksRaycasts = false;
                if (t > 0.7f) Destroy(gameObject);
            }
        }
    }
}
