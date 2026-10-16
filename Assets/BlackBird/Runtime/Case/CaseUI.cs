// The case screens, built in code like the HUD: conversations (with Believe / Press / Prove a lie, tells seen in
// Focus, and evidence presented mid-speech), the Notebook, the Mind Palace, naming the answer, and the case rating.
// Ports of the web game's src/ui/dialogue.js, palace.js and screens.js. Every button is a uGUI Selectable, so a
// controller (Apple TV) moves through them with the d-pad and presses with A; touch and mouse tap them.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BlackBird
{
    public static class UIKit
    {
        public static readonly Color Ink = new Color(0.94f, 0.9f, 0.82f), Gold = new Color(1f, 0.78f, 0.42f), Blue = new Color(0.55f, 0.75f, 1f),
            Paper = new Color(0.87f, 0.82f, 0.7f), PaperInk = new Color(0.17f, 0.12f, 0.08f), Dark = new Color(0.03f, 0.04f, 0.05f, 0.9f),
            Red = new Color(0.62f, 0.16f, 0.12f);
        public static Font serif, type;

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform; Stretch(r, 0, 0, 0, 0); return r;
        }
        public static RectTransform Panel(Transform parent, string name, Color c)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>(); img.color = c; img.raycastTarget = c.a > 0.01f;
            return (RectTransform)go.transform;
        }
        public static Text Label(Transform parent, string name, int size, TextAnchor anchor, Color c, Font f = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>(); t.font = f ?? serif; t.fontSize = size; t.alignment = anchor; t.color = c;
            t.supportRichText = true; t.raycastTarget = false; t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }
        public static void Place(RectTransform r, Vector2 anchor, Vector2 pos, Vector2 size) { r.anchorMin = r.anchorMax = anchor; r.pivot = anchor; r.anchoredPosition = pos; r.sizeDelta = size; }
        public static void Stretch(RectTransform r, float l, float t, float rt, float b) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = new Vector2(l, b); r.offsetMax = new Vector2(-rt, -t); }

        /// A text button that fills its layout slot.
        public static Button Button(Transform parent, string label, Action onClick, Color bg, Color fg, int size = 22, string sub = null)
        {
            var r = Panel(parent, "Button", bg);
            var le = r.gameObject.AddComponent<LayoutElement>(); le.minHeight = sub == null ? 50 : 66; le.preferredHeight = le.minHeight;
            var b = r.gameObject.AddComponent<Button>();
            var colors = b.colors; colors.normalColor = Color.white; colors.highlightedColor = new Color(1.25f, 1.2f, 1.1f); colors.selectedColor = new Color(1.35f, 1.25f, 1.05f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f); colors.colorMultiplier = 1.4f; b.colors = colors;
            b.onClick.AddListener(() => onClick());
            var t = Label(r, "Label", size, TextAnchor.MiddleLeft, fg); Stretch(t.rectTransform, 18, sub == null ? 4 : 6, 14, sub == null ? 4 : 26);
            t.text = label;
            if (sub != null) { var s = Label(r, "Sub", 15, TextAnchor.LowerLeft, new Color(fg.r, fg.g, fg.b, 0.6f)); Stretch(s.rectTransform, 18, 34, 14, 6); s.text = sub; }
            var ol = r.gameObject.AddComponent<Outline>(); ol.effectColor = new Color(Gold.r, Gold.g, Gold.b, 0.0f);
            r.gameObject.AddComponent<SelectGlow>();
            return b;
        }

        public static VerticalLayoutGroup Column(RectTransform r, float spacing = 8)
        {
            var v = r.gameObject.AddComponent<VerticalLayoutGroup>(); v.spacing = spacing;
            v.childControlHeight = true; v.childControlWidth = true; v.childForceExpandHeight = false; v.childForceExpandWidth = true;
            return v;
        }

        public static void Select(GameObject go) { if (EventSystem.current) EventSystem.current.SetSelectedGameObject(go); }

        /// A vertical scroll area; returns the content to fill (it grows with what's in it).
        public static RectTransform Scroll(RectTransform parent, out ScrollRect sr)
        {
            var view = Panel(parent, "View", new Color(0, 0, 0, 0.001f)); Stretch(view, 0, 0, 0, 0);
            view.gameObject.AddComponent<RectMask2D>();
            sr = view.gameObject.AddComponent<ScrollRect>(); sr.horizontal = false; sr.movementType = ScrollRect.MovementType.Clamped; sr.scrollSensitivity = 30;
            var content = Rect("Content", view);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(0.5f, 1); content.offsetMin = content.offsetMax = Vector2.zero;
            Column(content, 10).padding = new RectOffset(4, 4, 4, 4);
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            sr.viewport = view; sr.content = content;
            return content;
        }
    }

    public partial class CaseUI : MonoBehaviour
    {
        public static CaseUI I { get; private set; }
        public bool Open => mode != Screen.None;

        enum Screen { None, Talk, Book, Palace, Conclude, End, Sheet }
        Screen mode;
        RectTransform root, talkPanel, choices, book, palace, endCard;
        Text who, line, tell, thought;
        CaseState state;
        CaseData data;

        // the hooks the game gives each screen
        public Action<string> onGive;        // a testimony or clue handed over
        public Action<string> onEvent;       // a topic breaks off into a story event
        public Action<string, bool> onTalking; // who is speaking (for the figures' gestures)
        public Action onClosed;              // any screen closed, back to exploring
        public Action onSolved;              // the case is answered; the end card has been dismissed
        public Func<bool> focusOn;
        public Action toggleFocus;

        void Awake()
        {
            I = this;
            UIKit.serif = Hud.I ? Hud.I.serif : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            UIKit.type = Hud.I ? Hud.I.type : UIKit.serif;
            Build();
        }

        public void Bind(CaseState s) { state = s; data = s.data; }

        void Build()
        {
            var canvasGo = new GameObject("CaseUI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 20;
            var scaler = canvasGo.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = 1f;
            root = UIKit.Rect("Safe", canvasGo.transform); root.gameObject.AddComponent<SafeArea>();

            // conversation: the speaker and line along the bottom, choices stacked at the right
            talkPanel = UIKit.Panel(root, "Talk", UIKit.Dark);
            UIKit.Place(talkPanel, new Vector2(0.5f, 0), new Vector2(0, 24), new Vector2(1180, 180));
            talkPanel.gameObject.AddComponent<Button>().onClick.AddListener(Advance);
            who = UIKit.Label(talkPanel, "Who", 22, TextAnchor.UpperLeft, UIKit.Gold); UIKit.Stretch(who.rectTransform, 28, 16, 28, 140);
            line = UIKit.Label(talkPanel, "Line", 28, TextAnchor.UpperLeft, UIKit.Ink); UIKit.Stretch(line.rectTransform, 28, 48, 28, 40);
            tell = UIKit.Label(talkPanel, "Tell", 20, TextAnchor.LowerLeft, UIKit.Blue, UIKit.type); UIKit.Stretch(tell.rectTransform, 28, 120, 28, 12);
            choices = UIKit.Rect("Choices", root);
            choices.anchorMin = new Vector2(1, 0); choices.anchorMax = new Vector2(1, 1); choices.pivot = new Vector2(1, 0);
            choices.offsetMin = new Vector2(-520, 216); choices.offsetMax = new Vector2(-28, -90);
            var cv = UIKit.Column(choices, 8); cv.childAlignment = TextAnchor.LowerRight;
            talkPanel.gameObject.SetActive(false);
        }

        void Update()
        {
            if (mode == Screen.None) return;
            var gi = GameInput.I;
            if (!gi) return;
            gi.allowMouseCapture = false;
            if (mode == Screen.Talk && reading && (gi.Act || gi.Back)) Advance();
            else if (gi.Back && mode == Screen.Book) CloseBook();
            else if (gi.Back && mode == Screen.Palace) ClosePalace();
            // keep a controller's selection alive
            if (EventSystem.current && EventSystem.current.currentSelectedGameObject == null && firstChoice) UIKit.Select(firstChoice);
            if (mode == Screen.Talk && reading && !judging && focusOn != null && current?.tell != null) tell.text = focusOn() ? current.tell : "";
        }

        // ================= conversation =================
        string person;
        readonly Queue<Line> queue = new Queue<Line>();
        Action then;
        Line current;
        bool reading, judging;
        GameObject firstChoice;
        readonly HashSet<string> asked = new HashSet<string>();

        public void Talk(string personId)
        {
            if (!data.talk.ContainsKey(personId)) return;
            person = personId; mode = Screen.Talk;
            talkPanel.gameObject.SetActive(true); choices.gameObject.SetActive(true);
            Hud.I.Letterbox(true);
            bool greeted = state.talked.Contains(personId);
            var hello = data.talk[personId].hello;
            Play(greeted ? new List<Line> { hello[hello.Count - 1] } : hello, Menu);
            if (!greeted) { state.talked.Add(personId); state.Save(); }
        }

        /// Lines with no menu after (a cutscene, an epilogue). then runs when the last is read.
        public void Lines(List<Line> lines, Action done)
        {
            mode = Screen.Talk; person = null;
            talkPanel.gameObject.SetActive(true); choices.gameObject.SetActive(true);
            Hud.I.Letterbox(true);
            Play(lines, () => { CloseTalk(); done?.Invoke(); });
        }

        void Play(List<Line> lines, Action after)
        {
            queue.Clear(); foreach (var l in lines) queue.Enqueue(l);
            then = after;
            ClearChoices(); tell.text = "";
            Next();
        }

        void Advance() { if (reading) Next(); }

        void Next()
        {
            if (queue.Count == 0)
            {
                reading = false;
                onTalking?.Invoke(null, false);
                var t = then; then = null; t?.Invoke();
                return;
            }
            current = queue.Dequeue();
            reading = true;
            who.text = current.who ?? "";
            who.color = current.who == "Holmes" ? UIKit.Gold : UIKit.Ink;
            line.text = current.text;
            onTalking?.Invoke(current.who, true);
            LineTools();
        }

        // while a line is said: its tell (in Focus), a Focus toggle and Present evidence where the chapter allows
        void LineTools()
        {
            ClearChoices();
            bool focus = focusOn?.Invoke() ?? false;
            if (!judging) tell.text = current?.tell != null && focus ? current.tell : "";
            if (!data.focusTalk) return;
            Choice(focus ? "◉ Focus" : "○ Focus", () => { toggleFocus?.Invoke(); LineTools(); }, UIKit.Dark, UIKit.Blue);
            var p = current?.present;
            if (p != null && (!state.calls.ContainsKey("p:" + p.id) || (p.gives != null && !state.Has(p.gives))))
                Choice("Present evidence", () => Present(p), new Color(0.25f, 0.12f, 0.08f, 0.95f), UIKit.Gold);
            firstChoice = null;   // the line itself is advanced with A; these are optional
        }

        void Present(PresentDef p)
        {
            var rest = queue.ToList(); var after = then;
            reading = false; judging = true;
            ClearChoices();
            tell.text = "Which fact gives the lie to what she is saying?";
            void Done(bool right)
            {
                judging = false;
                state.Call("p:" + p.id, right);
                Called(right);
                if (right) Play(p.right, () => { if (p.gives != null) Give(p.gives); after?.Invoke(); });
                else Play(p.wrong.Concat(rest).ToList(), after);
            }
            foreach (var id in state.clues.Concat(state.deductions).ToList())
            {
                var f = data.Fact(id); var fid = id;
                Choice(f.title, () => Done(p.evidence.Contains(fid)), new Color(0.12f, 0.1f, 0.08f, 0.95f), UIKit.Ink);
            }
            Choice("Let her go on…", () => { judging = false; reading = true; LineTools(); }, UIKit.Dark, new Color(1, 1, 1, 0.6f));
            SelectFirst();
        }

        void Menu()
        {
            reading = false; current = null;
            who.text = data.people.TryGetValue(person, out var pi) ? pi.name : person; who.color = UIKit.Ink;
            line.text = ""; tell.text = "";
            ClearChoices();
            var topics = data.talk[person].topics;
            for (int i = 0; i < topics.Count; i++)
            {
                var t = topics[i];
                if (t.needs.Length > 0 && !t.needs.All(state.Has)) continue;
                if (t.@event != null && state.events.Contains(t.@event)) continue;
                if (t.after != null && !state.events.Contains(t.after)) continue;
                if (t.before != null && state.events.Contains(t.before)) continue;
                string key = person + i;
                bool called = t.challenge != null && state.calls.ContainsKey(key);
                bool retry = t.challenge != null && called && t.challenge.gives != null && !state.Has(t.challenge.gives);
                bool done = !retry && (asked.Contains(key) || (t.gives != null && state.Has(t.gives)) || called);
                var bg = t.challenge != null && !(called && !retry) ? new Color(0.22f, 0.1f, 0.06f, 0.95f) : new Color(0.08f, 0.09f, 0.1f, 0.92f);
                var fg = done ? new Color(UIKit.Ink.r, UIKit.Ink.g, UIKit.Ink.b, 0.5f) : UIKit.Ink;
                string label = t.q + (called && !retry ? (state.calls[key] ? "  ✓" : "  ✗") : t.challenge != null ? "  ◆" : "");
                Choice(label, () =>
                {
                    asked.Add(key);
                    var lines = new List<Line> { new Line("Holmes", t.q) }; lines.AddRange(t.a);
                    Play(lines, () =>
                    {
                        if (t.@event != null) { CloseTalk(); onEvent?.Invoke(t.@event); return; }
                        if (t.gives != null) Give(t.gives);
                        if (t.challenge != null && (!state.calls.ContainsKey(key) || retry)) Challenge(key, t.challenge);
                        else Menu();
                    });
                }, bg, fg);
            }
            Choice("That will be all.", CloseTalk, UIKit.Dark, new Color(1, 1, 1, 0.6f));
            SelectFirst();
        }

        static readonly (string id, string label, string sub)[] Calls =
            { ("truth", "Believe", "Take it as the truth"), ("doubt", "Press", "Something is held back"), ("lie", "Prove a lie", "Show the fact that breaks it") };

        void Challenge(string key, ChallengeDef c)
        {
            reading = false; judging = true;
            ClearChoices();
            bool focus = focusOn?.Invoke() ?? false;
            tell.text = c.focusTell && !focus ? "Something in the manner. Focus to read it." : c.tell ?? "";
            if (c.focusTell && data.focusTalk) Choice(focus ? "◉ Focus" : "○ Focus", () => { toggleFocus?.Invoke(); Challenge(key, c); }, UIKit.Dark, UIKit.Blue);
            foreach (var (id, label, sub) in Calls)
            {
                var cid = id;
                Choice(label, () => { if (cid == "lie") Evidence(key, c); else Resolve(key, c, cid == c.answer); },
                    cid == "lie" ? new Color(0.3f, 0.1f, 0.07f, 0.95f) : new Color(0.1f, 0.11f, 0.13f, 0.95f), UIKit.Ink, sub);
            }
            SelectFirst();
        }

        void Evidence(string key, ChallengeDef c)
        {
            ClearChoices();
            tell.text = "Which fact gives the lie to it?";
            foreach (var id in state.clues.Concat(state.deductions).ToList())
            {
                var f = data.Fact(id); var fid = id;
                Choice(f.title, () => Resolve(key, c, c.answer == "lie" && c.evidence.Contains(fid)), new Color(0.12f, 0.1f, 0.08f, 0.95f), UIKit.Ink);
            }
            Choice("On second thought…", () => Challenge(key, c), UIKit.Dark, new Color(1, 1, 1, 0.6f));
            SelectFirst();
        }

        void Resolve(string key, ChallengeDef c, bool right)
        {
            judging = false;
            state.Call(key, right);
            Called(right);
            Play(right ? c.right : c.wrong, () => { if (right && c.gives != null) Give(c.gives); Menu(); });
        }

        void Called(bool right)
        {
            if (right) Sound.Deduce(); else Sound.Wrong();
            Hud.I.Narrate(right ? "You read them right" : "You misjudged them", "");
        }

        void Give(string id) { onGive?.Invoke(id); }

        void CloseTalk()
        {
            talkPanel.gameObject.SetActive(false); ClearChoices();
            reading = judging = false; mode = Screen.None; firstChoice = null;
            onTalking?.Invoke(null, false);
            Hud.I.Letterbox(false);
            onClosed?.Invoke();
        }

        void ClearChoices() { foreach (Transform c in choices) Destroy(c.gameObject); firstChoice = null; }
        Button Choice(string label, Action a, Color bg, Color fg, string sub = null)
        {
            var b = UIKit.Button(choices, label, a, bg, fg, 21, sub);
            return b;
        }
        void SelectFirst()
        {
            // the first real option, once the layout has the buttons
            Button first = null;
            foreach (Transform c in choices) { var b = c.GetComponent<Button>(); if (b && !c.name.StartsWith("x")) { first = b; break; } }
            firstChoice = first ? first.gameObject : null;
            if (firstChoice && GameInput.I && !GameInput.I.UsingTouch) UIKit.Select(firstChoice);
        }

        // ================= the Notebook =================
        string tab = "case";
        RectTransform bookBody; Text bookTitle;
        public void OpenBook()
        {
            if (Open) return;
            mode = Screen.Book;
            if (!book)
            {
                book = UIKit.Panel(root, "Notebook", new Color(0, 0, 0, 0.55f)); UIKit.Stretch(book, 0, 0, 0, 0);
                var page = UIKit.Panel(book, "Page", UIKit.Paper); UIKit.Place(page, new Vector2(0.5f, 0.5f), new Vector2(-40, 0), new Vector2(900, 640));
                bookTitle = UIKit.Label(page, "Title", 34, TextAnchor.UpperLeft, UIKit.PaperInk); UIKit.Stretch(bookTitle.rectTransform, 40, 24, 120, 560);
                var close = UIKit.Button(page, "Close  ✕", CloseBook, new Color(0, 0, 0, 0.12f), UIKit.PaperInk, 18);
                var cr = (RectTransform)close.transform; Destroy(close.GetComponent<LayoutElement>()); UIKit.Place(cr, new Vector2(1, 1), new Vector2(-24, -20), new Vector2(120, 44));
                var bodyArea = UIKit.Rect("Body", page); UIKit.Stretch(bodyArea, 40, 84, 40, 30);
                bookBody = UIKit.Scroll(bodyArea, out _);
                var tabs = UIKit.Rect("Tabs", book); UIKit.Place(tabs, new Vector2(0.5f, 0.5f), new Vector2(510, 0), new Vector2(170, 300));
                UIKit.Column(tabs, 10);
                foreach (var (id, name) in new[] { ("case", "Case"), ("clues", "Clues"), ("people", "People"), ("deductions", "Deductions") })
                { var tid = id; UIKit.Button(tabs, name, () => { tab = tid; RenderBook(); }, new Color(0.35f, 0.25f, 0.15f, 0.95f), UIKit.Ink, 22); }
            }
            book.gameObject.SetActive(true);
            Hud.I.Letterbox(false); Hud.I.Visible(false);
            RenderBook();
            UIKit.Select(book.transform.Find("Tabs").GetChild(0).gameObject);
        }

        void CloseBook() { book.gameObject.SetActive(false); mode = Screen.None; Hud.I.Visible(true); onClosed?.Invoke(); }

        void RenderBook()
        {
            foreach (Transform c in bookBody) Destroy(c.gameObject);
            bookTitle.text = new Dictionary<string, string> { ["case"] = "Case", ["clues"] = "Clues", ["people"] = "People", ["deductions"] = "Deductions" }[tab];
            void Entry(string head, string body, string stamp = null, Color? stampColor = null)
            {
                var t = UIKit.Label(bookBody, "Entry", 20, TextAnchor.UpperLeft, UIKit.PaperInk);
                t.text = (stamp != null ? $"<size=14><color=#{ColorUtility.ToHtmlStringRGB(stampColor ?? UIKit.Red)}>{stamp.ToUpperInvariant()}</color></size>\n" : "") +
                         (head != null ? $"<size=24><b>{head}</b></size>\n" : "") + body;
                t.gameObject.AddComponent<LayoutElement>();
            }
            switch (tab)
            {
                case "case":
                    int keys = data.conclusion.needs.Count(state.deductions.Contains);
                    Entry(data.title, $"<i>Current line of inquiry:</i> {state.Objective()}");
                    Entry(null, data.summary ?? "");
                    Entry(null, $"Clues  {state.clues.Count} / {data.clues.Count}\nDeductions  {state.deductions.Count} / {data.deductions.Count}\nKey deductions  {keys} / {data.conclusion.needs.Length}");
                    break;
                case "clues":
                    Entry(null, $"{state.clues.Count} of {data.clues.Count} found" + (state.clues.Count < data.clues.Count ? ". There is more to see." : ". Nothing has been missed."));
                    foreach (var id in state.clues)
                    {
                        var c = data.clues[id];
                        Entry(c.title, c.text, c.kind == "memory" ? "Remembered" : c.kind == "testimony" ? "Heard" : "Observed");
                    }
                    break;
                case "people":
                    foreach (var p in data.absent.Values.Where(p => p.needs == null || state.Has(p.needs))
                                 .Concat(data.people.Values.Where(p => p.id == "watson" || state.talked.Contains(p.id))))
                    {
                        var seen = data.reads.TryGetValue(p.id, out var rr) ? rr.Where((_, i) => state.reads.Contains(p.id + ":" + i)).ToList() : new List<string>();
                        var said = state.clues.Where(c => data.clues[c].who == p.id).Select(c => data.clues[c].text).ToList();
                        Entry(p.name, $"<i>{p.role}</i>\n{p.note}" +
                            (seen.Count > 0 ? "\n<b>Observed</b>\n· " + string.Join("\n· ", seen) : "") +
                            (said.Count > 0 ? "\n<b>Testimony</b>\n· " + string.Join("\n· ", said) : ""));
                    }
                    break;
                case "deductions":
                    if (state.deductions.Count == 0) Entry(null, "Nothing yet. Facts are combined in the Mind Palace.");
                    foreach (var id in state.deductions)
                    {
                        var d = data.deductions[id];
                        Entry(d.title, d.text + $"\n<size=16><i>{string.Join(" + ", d.from.Select(f => data.Fact(f)?.title))}</i></size>", d.key ? "Key" : null);
                    }
                    break;
            }
        }

        // ================= the Mind Palace =================
        RectTransform board, plane; ScrollRect boardScroll; Text boardCount;
        readonly List<string> sel = new List<string>();
        string fresh;
        const float NW = 210, NH = 62, ROW = 76, COL = 270, PAD = 24;

        public void OpenPalace()
        {
            if (Open) return;
            mode = Screen.Palace;
            sel.Clear();
            if (!palace)
            {
                palace = UIKit.Panel(root, "MindPalace", new Color(0.02f, 0.03f, 0.05f, 0.94f)); UIKit.Stretch(palace, 0, 0, 0, 0);
                var head = UIKit.Label(palace, "Head", 34, TextAnchor.UpperLeft, UIKit.Gold); UIKit.Stretch(head.rectTransform, 40, 22, 200, 640); head.text = "Mind Palace";
                thought = UIKit.Label(palace, "Thought", 22, TextAnchor.UpperLeft, UIKit.Ink); UIKit.Stretch(thought.rectTransform, 40, 70, 200, 560);
                var close = UIKit.Button(palace, "Close  ✕", ClosePalace, new Color(1, 1, 1, 0.08f), UIKit.Ink, 18);
                Destroy(close.GetComponent<LayoutElement>()); UIKit.Place((RectTransform)close.transform, new Vector2(1, 1), new Vector2(-28, -22), new Vector2(130, 44));
                board = UIKit.Panel(palace, "Board", new Color(0, 0, 0, 0.001f)); UIKit.Stretch(board, 30, 150, 30, 60);
                board.gameObject.AddComponent<RectMask2D>();
                boardScroll = board.gameObject.AddComponent<ScrollRect>(); boardScroll.movementType = ScrollRect.MovementType.Clamped; boardScroll.scrollSensitivity = 30;
                plane = UIKit.Rect("Plane", board); plane.anchorMin = plane.anchorMax = new Vector2(0, 1); plane.pivot = new Vector2(0, 1);
                boardScroll.viewport = board; boardScroll.content = plane;
                boardCount = UIKit.Label(palace, "Count", 18, TextAnchor.LowerLeft, new Color(1, 1, 1, 0.6f), UIKit.type); UIKit.Stretch(boardCount.rectTransform, 40, 680, 40, 18);
            }
            palace.gameObject.SetActive(true);
            Hud.I.Letterbox(false); Hud.I.Visible(false);
            RenderPalace("Choose two facts that speak to one another.");
        }

        void ClosePalace() { palace.gameObject.SetActive(false); mode = Screen.None; Hud.I.Visible(true); onClosed?.Invoke(); }

        int Depth(string id) => data.deductions.TryGetValue(id, out var d) ? 1 + d.from.Max(Depth) : 0;

        Dictionary<string, Vector2> Layout()
        {
            var order = data.deductions.Keys.ToList();
            int FirstUse(string id) { int i = order.FindIndex(k => data.deductions[k].from.Contains(id)); return i < 0 ? 99 : i; }
            var pos = new Dictionary<string, Vector2>();
            var cl = state.clues.OrderBy(FirstUse).ToList();
            for (int i = 0; i < cl.Count; i++) pos[cl[i]] = new Vector2(0, i * ROW);
            foreach (var g in state.deductions.GroupBy(Depth).OrderBy(g => g.Key))
            {
                float last = float.NegativeInfinity;
                foreach (var (id, y) in g.Select(id => (id, data.deductions[id].from.Where(pos.ContainsKey).Select(f => pos[f].y).DefaultIfEmpty(0).Average())).OrderBy(t => t.Item2))
                { float yy = Mathf.Max(y, last + ROW); pos[id] = new Vector2(g.Key * COL, yy); last = yy; }
            }
            int maxDepth = Mathf.Max(2, state.deductions.Select(Depth).DefaultIfEmpty(0).Max());
            var made = data.conclusion.needs.Where(pos.ContainsKey).ToList();
            float cy = made.Count > 0 ? made.Average(d => pos[d].y) : (cl.Count - 1) * ROW / 2f;
            pos["$end"] = new Vector2((maxDepth + 1) * COL, Mathf.Max(0, cy));
            return pos;
        }

        void RenderPalace(string msg)
        {
            foreach (Transform c in plane) Destroy(c.gameObject);
            thought.text = msg;
            var pos = Layout();
            float w = pos.Values.Max(p => p.x) + NW + PAD * 2, h = pos.Values.Max(p => p.y) + NH + PAD * 2;
            plane.sizeDelta = new Vector2(w, h);

            void Thread(Vector2 a, Vector2 b, bool key)
            {
                // a straight thread from the right edge of a to the left edge of b
                var p0 = new Vector2(a.x + NW + PAD, -(a.y + NH / 2 + PAD)); var p1 = new Vector2(b.x + PAD, -(b.y + NH / 2 + PAD));
                var r = UIKit.Panel(plane, "Thread", key ? new Color(UIKit.Red.r, UIKit.Red.g, UIKit.Red.b, 0.9f) : new Color(1, 1, 1, 0.35f));
                r.GetComponent<Image>().raycastTarget = false;
                r.anchorMin = r.anchorMax = new Vector2(0, 1); r.pivot = new Vector2(0, 0.5f);
                var d = p1 - p0; r.anchoredPosition = p0; r.sizeDelta = new Vector2(d.magnitude, key ? 3 : 2);
                r.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            }
            foreach (var id in state.deductions) foreach (var f in data.deductions[id].from) if (pos.ContainsKey(f)) Thread(pos[f], pos[id], data.deductions[id].key);
            foreach (var d in data.conclusion.needs) if (pos.ContainsKey(d)) Thread(pos[d], pos["$end"], true);

            GameObject first = null, freshGo = null;
            Button Node(string id, string title, Color bg, Color fg, Action onClick)
            {
                var b = UIKit.Button(plane, title, onClick, bg, fg, 17);
                Destroy(b.GetComponent<LayoutElement>());
                var r = (RectTransform)b.transform; r.anchorMin = r.anchorMax = new Vector2(0, 1); r.pivot = new Vector2(0, 1);
                r.anchoredPosition = new Vector2(pos[id].x + PAD, -(pos[id].y + PAD)); r.sizeDelta = new Vector2(NW, NH);
                b.GetComponentInChildren<Text>().alignment = TextAnchor.MiddleCenter;
                return b;
            }
            foreach (var id in state.clues.Concat(state.deductions))
            {
                var f = data.Fact(id); var fid = id;
                bool ded = f.IsDeduction;
                var feeds = data.deductions.Values.Where(d => d.from.Contains(id)).ToList();
                bool spent = feeds.Count > 0 && feeds.All(d => state.deductions.Contains(d.id));
                Color bg = sel.Contains(id) ? new Color(0.55f, 0.4f, 0.12f, 0.98f)
                    : ded ? (f.key ? new Color(0.36f, 0.08f, 0.06f, 0.95f) : new Color(0.2f, 0.16f, 0.1f, 0.95f))
                    : f.kind == "testimony" ? new Color(0.1f, 0.15f, 0.22f, 0.95f) : f.kind == "memory" ? new Color(0.17f, 0.12f, 0.2f, 0.95f) : new Color(0.12f, 0.13f, 0.14f, 0.95f);
                var fg = spent ? new Color(UIKit.Ink.r, UIKit.Ink.g, UIKit.Ink.b, 0.45f) : UIKit.Ink;
                var b = Node(id, f.title, bg, fg, () => Pick(fid));
                if (first == null) first = b.gameObject;
                if (id == fresh) freshGo = b.gameObject;
            }
            bool ready = state.CanConclude;
            Node("$end", data.conclusion.question, ready ? new Color(0.6f, 0.45f, 0.12f, 1f) : new Color(0.1f, 0.1f, 0.1f, 0.9f), ready ? Color.white : new Color(1, 1, 1, 0.5f), () =>
            {
                if (!state.CanConclude) { RenderPalace("Not yet. I have not enough to name anyone."); return; }
                palace.gameObject.SetActive(false); Hud.I.Visible(true); Conclude();
            });
            int keys = data.conclusion.needs.Count(state.deductions.Contains);
            boardCount.text = $"Threads drawn {state.deductions.Count} / {data.deductions.Count} · Key {keys} / {data.conclusion.needs.Length}" + (ready ? " · Choose the question to answer it" : "");
            firstChoice = freshGo ?? first;
            UIKit.Select(firstChoice);
            fresh = null;
        }

        void Pick(string id)
        {
            var f = data.Fact(id);
            int i = sel.IndexOf(id);
            if (i >= 0) { sel.RemoveAt(i); RenderPalace("Choose two facts that speak to one another."); return; }
            sel.Add(id);
            if (sel.Count < 2) { RenderPalace(f.text + " …and what does it speak to?"); return; }
            string a = sel[0], b = sel[1]; sel.Clear();
            var d = data.Combine(a, b);
            if (d != null && !state.deductions.Contains(d))
            {
                state.AddDeduction(d); Sound.Deduce(); fresh = d;
                RenderPalace($"<color=#ffc66b>{data.deductions[d].title}.</color> {data.deductions[d].text}");
            }
            else if (d != null) RenderPalace("I have already drawn that thread.");
            else { state.misses++; state.Save(); Sound.Wrong(); RenderPalace(CaseData.WrongLine()); }
        }

        // ================= the answer, the epilogue, the rating =================
        void Conclude()
        {
            mode = Screen.Conclude;
            talkPanel.gameObject.SetActive(true); choices.gameObject.SetActive(true);
            Hud.I.Letterbox(true);
            who.text = "Holmes"; who.color = UIKit.Gold; line.text = data.conclusion.question; tell.text = "";
            ClearChoices();
            foreach (var ans in data.conclusion.answers)
            {
                var a = ans;
                Choice(a.a, () =>
                {
                    if (!a.right)
                    {
                        state.wrongAccusations++; state.Save(); Sound.Wrong();
                        mode = Screen.Talk;
                        Play(new List<Line> { new Line("Holmes", a.reply) }, Conclude);
                        return;
                    }
                    Sound.Deduce();
                    mode = Screen.Talk;
                    var lines = new List<Line> { new Line("Holmes", a.reply) }; lines.AddRange(data.conclusion.epilogue);
                    Play(lines, () => { state.solved = true; state.Save(); talkPanel.gameObject.SetActive(false); ClearChoices(); EndCard(); });
                }, new Color(0.12f, 0.1f, 0.08f, 0.95f), UIKit.Ink);
            }
            Choice("Not yet…", () => { talkPanel.gameObject.SetActive(false); ClearChoices(); mode = Screen.None; Hud.I.Letterbox(false); OpenPalace(); }, UIKit.Dark, new Color(1, 1, 1, 0.6f));
            SelectFirst();
        }

        void EndCard()
        {
            mode = Screen.End;
            Hud.I.Letterbox(false); Hud.I.Visible(false);
            if (endCard) Destroy(endCard.gameObject);
            endCard = UIKit.Panel(root, "EndCard", new Color(0.02f, 0.03f, 0.04f, 0.96f)); UIKit.Stretch(endCard, 0, 0, 0, 0);
            var col = UIKit.Rect("Col", endCard); UIKit.Place(col, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(640, 600));
            var v = UIKit.Column(col, 12); v.childAlignment = TextAnchor.MiddleCenter;
            var (stars, rows) = state.Rating();
            void T(string s, int size, Color c) { var t = UIKit.Label(col, "T", size, TextAnchor.MiddleCenter, c); t.text = s; t.gameObject.AddComponent<LayoutElement>(); }
            T(data.title, 26, new Color(1, 1, 1, 0.7f));
            T("Case closed", 46, UIKit.Ink);
            T(new string('★', stars) + new string('☆', 5 - stars), 48, UIKit.Gold);
            T(string.Join("\n", rows.Select(r => $"{r.Item1}   <b>{r.Item2}</b>")), 20, UIKit.Ink);
            if (!string.IsNullOrEmpty(data.next)) T("Next: " + data.next, 22, new Color(1, 1, 1, 0.65f));
            var b = UIKit.Button(col, "Continue", () => { Destroy(endCard.gameObject); mode = Screen.None; Hud.I.Visible(true); onSolved?.Invoke(); }, new Color(0.35f, 0.25f, 0.12f, 1), Color.white, 24);
            UIKit.Select(b.gameObject);
        }
    }
}
