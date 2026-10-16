// The title screen: the name over the house at night, Continue (the first chapter not yet solved), the five
// chapters with how far each has got (and a star rating once solved), and Wander for the places outside the story.
// Built in code like the rest of the interface; works with a controller, a mouse or a finger.
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BlackBird
{
    public class TitleScreen : MonoBehaviour
    {
        public Font serif;

        public static readonly (string caseId, string scene, string numeral, string name, string where)[] Chapters =
        {
            ("archer", "BurrittAlley", "I", "The Black Bird", "Burritt Alley, Tuesday night"),
            ("cairo", "PalaceHotel", "II", "The Levantine", "The Palace Hotel"),
            ("brigid", "StMark", "III", "Miss Wonderly", "The St. Mark"),
            ("gutman", "HopkinsNight", "IV", "The Fat Man", "The Hopkins house, Nob Hill"),
            ("gunsel", "Kearny", "V", "The Gunsel", "Kearny Street, in fog"),
        };

        RectTransform main, chapters; Text credit;
        GameObject firstMain, firstChapter;

        void Start()
        {
            Cursor.lockState = CursorLockMode.None;
            UIKit.serif = serif ? serif : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (!UIKit.type) UIKit.type = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (!EventSystem.current)   // (a module added at run time needs its actions given in a player build)
                new GameObject("EventSystem", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule))
                    .GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>().AssignDefaultActions();
            Build();
            Show(main);
        }

        void Update()
        {
            var es = EventSystem.current;
            if (PlacesMenu.IsOpen || !es) return;
            var pad = UnityEngine.InputSystem.Gamepad.current; var kb = UnityEngine.InputSystem.Keyboard.current;
            bool back = (pad != null && pad.buttonEast.wasPressedThisFrame) || (kb != null && kb.escapeKey.wasPressedThisFrame);
            if (back && chapters.gameObject.activeSelf) Show(main);
            var open = chapters.gameObject.activeSelf ? chapters : main;
            if (es.currentSelectedGameObject == null || !es.currentSelectedGameObject.transform.IsChildOf(open))
                es.SetSelectedGameObject(open == chapters ? firstChapter : firstMain);
        }

        void Show(RectTransform panel)
        {
            main.gameObject.SetActive(panel == main);
            chapters.gameObject.SetActive(panel == chapters);
            credit.gameObject.SetActive(panel == main);
            confirm = null;
            if (panel == chapters) FillChapters();
            EventSystem.current?.SetSelectedGameObject(panel == main ? firstMain : firstChapter);
        }

        void Build()
        {
            var go = new GameObject("Title", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false);
            var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 5;
            var sc = go.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1280, 720); sc.matchWidthOrHeight = 1f;

            var bg = new GameObject("Picture", typeof(RectTransform), typeof(RawImage)); bg.transform.SetParent(go.transform, false);
            var raw = bg.GetComponent<RawImage>(); raw.texture = Resources.Load<Texture2D>("UI/title"); raw.color = Color.white;
            var br = (RectTransform)bg.transform; UIKit.Stretch(br, 0, 0, 0, 0);
            var fit = bg.AddComponent<AspectRatioFitter>(); fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent; fit.aspectRatio = 16f / 9f;

            var safe = UIKit.Rect("Safe", go.transform); safe.gameObject.AddComponent<SafeArea>();
            var name = UIKit.Label(safe, "Name", 76, TextAnchor.UpperLeft, new Color(0.93f, 0.87f, 0.73f));
            UIKit.Place(name.rectTransform, new Vector2(0, 1), new Vector2(80, -70), new Vector2(760, 110));
            name.text = "The Black Bird";
            var sub = UIKit.Label(safe, "Sub", 26, TextAnchor.UpperLeft, new Color(0.78f, 0.67f, 0.43f));
            UIKit.Place(sub.rectTransform, new Vector2(0, 1), new Vector2(84, -172), new Vector2(760, 40));
            sub.text = "A Sherlock Holmes mystery · San Francisco, 1895";

            // the main menu
            main = UIKit.Rect("Main", safe);
            UIKit.Place(main, new Vector2(0, 1), new Vector2(80, -250), new Vector2(430, 330));
            UIKit.Column(main, 12);
            var next = NextChapter();
            bool started = Chapters.Any(c => CaseState.HasSave(c.caseId));
            bool allDone = Chapters.All(c => Solved(c.caseId));
            var bg0 = new Color(0.08f, 0.07f, 0.06f, 0.88f);
            var cont = allDone
                ? UIKit.Button(main, "The case is closed", () => Show(chapters), bg0, UIKit.Gold, 30, "Replay any chapter")
                : UIKit.Button(main, started ? "Continue" : "Begin", () => Play(next, false), bg0, UIKit.Gold, 30, $"Chapter {next.numeral} · {next.name}");
            firstMain = cont.gameObject;
            UIKit.Button(main, "Chapters", () => Show(chapters), bg0, UIKit.Ink, 26, "Play or replay any of the five");
            UIKit.Button(main, "Wander", () => PlacesOpen(), bg0, UIKit.Ink, 26, "The places, outside the story");
            credit = UIKit.Label(safe, "Credit", 15, TextAnchor.LowerLeft, new Color(1, 1, 1, 0.45f));
            UIKit.Place(credit.rectTransform, new Vector2(0, 0), new Vector2(84, 30), new Vector2(900, 40));
            credit.text = "After Dashiell Hammett's The Maltese Falcon (1930), with Holmes in Spade's shoes.";

            // the chapters
            chapters = UIKit.Rect("Chapters", safe);
            UIKit.Place(chapters, new Vector2(0, 1), new Vector2(80, -240), new Vector2(560, 470));
            UIKit.Column(chapters, 8);
        }

        void FillChapters()
        {
            for (int k = chapters.childCount - 1; k >= 0; k--) { var c = chapters.GetChild(k); c.SetParent(null, false); Destroy(c.gameObject); }
            firstChapter = null;
            Button prev = null;
            foreach (var c in Chapters)
            {
                var (state, solved, stars) = Progress(c.caseId);
                string sub = solved ? $"{c.where} · solved, {stars} of 5 stars" : state ? $"{c.where} · in progress" : c.where;
                var ch = c;
                Button b = null;
                b = UIKit.Button(chapters, $"{c.numeral}.  {c.name}", () =>
                {
                    // replaying a solved chapter clears its stars: ask first (a second press within a few seconds)
                    if (solved && (confirm != b || Time.unscaledTime - confirmAt > 4f))
                    {
                        confirm = b; confirmAt = Time.unscaledTime;
                        b.GetComponentInChildren<Text>().text = $"{ch.numeral}.  Replay from the start? Press again";
                        return;
                    }
                    Play(ch, solved);
                }, new Color(0.08f, 0.07f, 0.06f, 0.88f), solved ? UIKit.Ink : UIKit.Gold, 24, sub);
                Link(prev, b); prev = b;
                if (!firstChapter) firstChapter = b.gameObject;
            }
            var back = UIKit.Button(chapters, "Back", () => Show(main), new Color(0.05f, 0.05f, 0.05f, 0.88f), UIKit.Ink, 20);
            Link(prev, back);
        }

        static void Link(Button a, Button b)
        {
            var nb = b.navigation; nb.mode = Navigation.Mode.Explicit; nb.selectOnUp = a; b.navigation = nb;
            if (!a) return;
            var na = a.navigation; na.mode = Navigation.Mode.Explicit; na.selectOnDown = b; a.navigation = na;
        }

        Button confirm; float confirmAt;

        public static bool Solved(string caseId) => Progress(caseId).solved;

        static (bool started, bool solved, int stars) Progress(string caseId)
        {
            if (!CaseState.HasSave(caseId)) return (false, false, 0);
            var data = CaseData.Load(caseId); if (data == null) return (false, false, 0);
            var s = new CaseState(data); s.Load();
            return (true, s.solved, s.solved ? s.Rating().stars : 0);
        }

        static (string caseId, string scene, string numeral, string name, string where) NextChapter()
        {
            foreach (var c in Chapters) if (!Progress(c.caseId).solved) return c;
            return Chapters[Chapters.Length - 1];
        }

        static void Play((string caseId, string scene, string numeral, string name, string where) c, bool replay)
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            if (!Application.CanStreamedLevelBeLoaded(c.scene)) { Debug.LogWarning("[Black Bird] Not in this build: " + c.scene); return; }
#endif
            if (replay) { var d = CaseData.Load(c.caseId); if (d != null) new CaseState(d).Erase(); }
            SceneSwitch.Go(c.scene);
        }

        static void PlacesOpen()
        {
            var pm = FindAnyObjectByType<PlacesMenu>();
            if (pm) pm.OpenFromMenu();
        }
    }
}
