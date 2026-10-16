// The Places menu: every place in the game, picked with a controller, the keyboard or a tap.
// A controller's View / Select button (PlayStation: Create) or P opens it; B / Esc closes it.
// It installs itself, like SceneSwitch, so every scene has it whatever the scene holds.
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BlackBird
{
    public class PlacesMenu : MonoBehaviour
    {
        // still 'open' on the frame it closes, so the B that closed it isn't also read by the game
        public static bool IsOpen => open || Time.frameCount == closedFrame;
        static bool open; static int closedFrame = -1;
        static PlacesMenu instance;

        static readonly (string scene, string title, string sub)[] Places =
        {
            ("BurrittAlley", "Burritt Alley", "Chapter I · The Black Bird · night"),
            ("PalaceHotel", "The Palace Hotel", "Chapter II · Cairo"),
            ("StMark", "The St. Mark", "Chapter III · Miss O'Shaughnessy"),
            ("HopkinsNight", "The Hopkins House by night", "Chapter IV · the Fat Man"),
            ("HopkinsDay", "The Hopkins House by day", "Nob Hill, free to wander"),
            ("Kearny", "Kearny Street", "Chapter V · the Gunsel"),
            ("Title", "The title screen", "Chapters, and where you left off"),
        };

        Canvas canvas;
        RectTransform list;
        GameObject first;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (instance) return;
            var go = new GameObject("PlacesMenu");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<PlacesMenu>();
        }

        void Awake()
        {
            if (instance && instance != this) { Destroy(this); return; }
            SceneManager.sceneLoaded += (s, m) => { if (open) Close(); };
        }

        static bool Available(string scene)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return true;   // the others are a page away (SceneSwitch.Go)
#else
            return Application.CanStreamedLevelBeLoaded(scene);
#endif
        }

        Canvas tapCanvas;

        // on a phone or tablet there is no View button: a small "Places" tab at the top of the screen opens the menu
        void TapButton()
        {
            bool touch = Application.isMobilePlatform && Application.platform != RuntimePlatform.tvOS;
            if (!touch) return;
            if (!tapCanvas)
            {
                EnsureEventSystem();
                if (!UIKit.serif) UIKit.serif = Hud.I && Hud.I.serif ? Hud.I.serif : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                var go = new GameObject("PlacesTab", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                go.transform.SetParent(transform, false);
                tapCanvas = go.GetComponent<Canvas>(); tapCanvas.renderMode = RenderMode.ScreenSpaceOverlay; tapCanvas.sortingOrder = 39;
                var sc = go.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                sc.referenceResolution = new Vector2(1280, 720); sc.matchWidthOrHeight = 1f;
                var safe = UIKit.Rect("Safe", go.transform); safe.gameObject.AddComponent<SafeArea>();
                var r = UIKit.Panel(safe, "Tab", new Color(0.03f, 0.04f, 0.05f, 0.75f));
                UIKit.Place(r, new Vector2(0.5f, 1f), new Vector2(0, -10), new Vector2(150, 44));
                r.gameObject.AddComponent<Button>().onClick.AddListener(() => { if (!open) Open(); });
                var t = UIKit.Label(r, "Label", 22, TextAnchor.MiddleCenter, UIKit.Gold); UIKit.Stretch(t.rectTransform, 0, 0, 0, 0);
                t.text = "Places";
            }
            tapCanvas.enabled = !open;
        }

        void Update()
        {
            TapButton();
            var pad = Gamepad.current; var kb = Keyboard.current;
            bool toggle = (pad != null && pad.selectButton.wasPressedThisFrame) || (kb != null && kb.pKey.wasPressedThisFrame);
            if (!open)
            {
                if (toggle) Open();
                return;
            }
            bool back = toggle || (pad != null && pad.buttonEast.wasPressedThisFrame) || (kb != null && kb.escapeKey.wasPressedThisFrame);
            if (back) { Close(); return; }
            var es = EventSystem.current;
            if (es && first && (es.currentSelectedGameObject == null || !es.currentSelectedGameObject.transform.IsChildOf(list))) es.SetSelectedGameObject(first);
        }

        /// The title screen's "Wander".
        public void OpenFromMenu() { if (!open) Open(); }

        void Open()
        {
            EnsureEventSystem();
            if (!canvas) Build();
            Fill();
            canvas.enabled = true;
            open = true;
            Cursor.lockState = CursorLockMode.None;
            if (GameInput.I) GameInput.I.allowMouseCapture = false;
            if (EventSystem.current && first) EventSystem.current.SetSelectedGameObject(first);
        }

        void Close()
        {
            open = false; closedFrame = Time.frameCount;
            if (canvas) canvas.enabled = false;
            if (GameInput.I) GameInput.I.allowMouseCapture = true;
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
        }

        static void EnsureEventSystem()
        {
            if (EventSystem.current) return;
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            DontDestroyOnLoad(es);
        }

        void Build()
        {
            if (!UIKit.serif) { UIKit.serif = Hud.I && Hud.I.serif ? Hud.I.serif : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }
            if (!UIKit.type) UIKit.type = Hud.I && Hud.I.type ? Hud.I.type : UIKit.serif;

            var go = new GameObject("PlacesCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false);
            canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 40;
            var scaler = go.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = 1f;

            var shade = UIKit.Panel(go.transform, "Shade", new Color(0, 0, 0, 0.72f)); UIKit.Stretch(shade, 0, 0, 0, 0);
            var safe = UIKit.Rect("Safe", go.transform); safe.gameObject.AddComponent<SafeArea>();

            var card = UIKit.Panel(safe, "Card", UIKit.Dark);
            UIKit.Place(card, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620, 560));
            var title = UIKit.Label(card, "Title", 40, TextAnchor.UpperCenter, UIKit.Gold); UIKit.Stretch(title.rectTransform, 24, 22, 24, 480);
            title.text = "Places";
            var hint = UIKit.Label(card, "Hint", 17, TextAnchor.LowerCenter, new Color(UIKit.Ink.r, UIKit.Ink.g, UIKit.Ink.b, 0.6f), UIKit.type);
            UIKit.Stretch(hint.rectTransform, 24, 500, 24, 16);
            hint.text = "A choose   ·   B close   ·   View / P opens this anywhere";

            list = UIKit.Rect("List", card); UIKit.Stretch(list, 40, 86, 40, 56);
            UIKit.Column(list, 10);
            canvas.enabled = false;
        }

        void Fill()
        {
            for (int i = list.childCount - 1; i >= 0; i--) { var c = list.GetChild(i); c.SetParent(null, false); Destroy(c.gameObject); }
            first = null;
            var here = SceneManager.GetActiveScene().name;
            Button prev = null; GameObject current = null;
            foreach (var p in Places)
            {
                if (!Available(p.scene)) continue;
                bool isHere = p.scene == here;
                var scene = p.scene;
                var b = UIKit.Button(list, p.title, () => { Close(); if (scene != SceneManager.GetActiveScene().name) SceneSwitch.Go(scene); },
                    new Color(0.12f, 0.1f, 0.08f, 0.95f), isHere ? UIKit.Gold : UIKit.Ink, 24, isHere ? "you are here" : p.sub);
                if (!first) first = b.gameObject;
                if (isHere) current = b.gameObject;
                // explicit up/down so the controller can't wander off the list
                var nav = new Navigation { mode = Navigation.Mode.Explicit };
                if (prev) { nav.selectOnUp = prev; var pn = prev.navigation; pn.selectOnDown = b; prev.navigation = pn; }
                b.navigation = nav;
                prev = b;
            }
            var close = UIKit.Button(list, "Back to the game", Close, new Color(0.08f, 0.08f, 0.08f, 0.95f), UIKit.Ink, 20);
            if (prev) { var cn = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = prev }; close.navigation = cn; var pn = prev.navigation; pn.selectOnDown = close; prev.navigation = pn; }
            if (!first) first = close.gameObject;
            // start on the next place along, so a single press of A moves on as the View button used to
            if (current)
            {
                int idx = current.transform.GetSiblingIndex() + 1;
                first = (idx < list.childCount - 1 ? list.GetChild(idx) : list.GetChild(0)).gameObject;
            }
        }
    }
}
