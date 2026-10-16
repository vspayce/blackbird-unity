// The case's set pieces, on the same canvas as the conversations: completing Holmes's reading of someone (the
// character portrait), planning three moves against a pistol (fight prediction), and putting a drugged evening
// back in order (the reconstruction). Ports of profileSheet, fightPlanner and reconstruct in the web game.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace BlackBird
{
    public partial class CaseUI
    {
        RectTransform sheet;

        RectTransform Sheet(string pre, string title, bool seeThrough)
        {
            if (sheet) Destroy(sheet.gameObject);
            mode = Screen.Sheet;
            sheet = UIKit.Panel(root, "Sheet", seeThrough ? new Color(0.02f, 0.03f, 0.05f, 0.55f) : new Color(0.02f, 0.03f, 0.05f, 0.95f));
            UIKit.Stretch(sheet, 0, 0, 0, 0);
            var col = UIKit.Rect("Col", sheet);
            UIKit.Place(col, seeThrough ? new Vector2(0, 0.5f) : new Vector2(0.5f, 0.5f), seeThrough ? new Vector2(40, 0) : Vector2.zero, new Vector2(seeThrough ? 620 : 900, 640));
            var v = UIKit.Column(col, 10); v.childAlignment = TextAnchor.UpperLeft;
            T(col, pre.ToUpperInvariant(), 16, new Color(1, 1, 1, 0.55f));
            T(col, title, 38, UIKit.Gold);
            Hud.I.Visible(false);
            return col;
        }

        static Text T(Transform parent, string s, int size, Color c)
        {
            var t = UIKit.Label(parent, "T", size, TextAnchor.UpperLeft, c); t.text = s; t.gameObject.AddComponent<LayoutElement>(); return t;
        }

        void CloseSheet() { if (sheet) Destroy(sheet.gameObject); sheet = null; mode = Screen.None; Hud.I.Visible(true); }

        // ---- the character portrait: Holmes's reading, completed line by line ----
        public void ProfileSheet(string who, List<ProfileLine> lines, Action done)
        {
            var col = Sheet("Character portrait", who, true);
            var reply = UIKit.Label(sheet, "Reply", 22, TextAnchor.LowerLeft, UIKit.Blue, UIKit.type);
            UIKit.Place(reply.rectTransform, new Vector2(0, 0), new Vector2(40, 40), new Vector2(900, 60));
            int i = 0;
            void Next()
            {
                if (i >= lines.Count)
                {
                    var go = UIKit.Button(col, "Go on", () => { CloseSheet(); done(); }, new Color(0.35f, 0.25f, 0.12f, 1), Color.white, 22);
                    UIKit.Select(go.gameObject); return;
                }
                var L = lines[i];
                var row = UIKit.Rect("Row", col);
                var rowLe = row.gameObject.AddComponent<LayoutElement>(); rowLe.preferredHeight = 56;
                var h = row.gameObject.AddComponent<HorizontalLayoutGroup>(); h.spacing = 8; h.childControlWidth = h.childControlHeight = true; h.childForceExpandWidth = false;
                var lead = UIKit.Label(row, "Lead", 24, TextAnchor.MiddleLeft, UIKit.Ink); lead.text = L.lead; lead.gameObject.AddComponent<LayoutElement>().preferredWidth = 140;
                Button first = null;
                for (int k = 0; k < L.options.Length; k++)
                {
                    int kk = k;
                    Button b = null;
                    b = UIKit.Button(row, L.options[k], () =>
                    {
                        if (kk != L.answer)
                        {
                            b.interactable = false; reply.text = L.why ?? "No. Look again.";
                            state.misses++; state.Save(); Sound.Wrong(); return;
                        }
                        foreach (Transform c in row) if (c != lead.transform) Destroy(c.gameObject);
                        var got = UIKit.Label(row, "Got", 24, TextAnchor.MiddleLeft, UIKit.Gold); got.text = L.options[kk]; got.gameObject.AddComponent<LayoutElement>().preferredWidth = 420;
                        reply.text = ""; Sound.Clue();
                        i++; Next();
                    }, new Color(0.1f, 0.11f, 0.13f, 0.95f), UIKit.Ink, 19);
                    b.GetComponent<LayoutElement>().preferredWidth = 220;
                    if (first == null) first = b;
                }
                UIKit.Select(first.gameObject);
            }
            Next();
        }

        // ---- fight prediction: plan three moves in order ----
        public void FightPlanner(FightDef f, Action<bool> result)
        {
            var col = Sheet("Fight prediction", f.title, true);
            T(col, f.prompt, 22, UIKit.Ink);
            var planText = T(col, "", 22, UIKit.Gold);
            var reply = T(col, "", 20, UIKit.Blue);
            var plan = new List<string>();
            var moves = UIKit.Rect("Moves", col); moves.gameObject.AddComponent<LayoutElement>().preferredHeight = 330;
            UIKit.Column(moves, 6);
            void Show() => planText.text = string.Join("\n", plan.Select((m, k) => $"{k + 1}. {f.moves.First(x => x.id == m).label}")) + (plan.Count < 3 ? $"\n{plan.Count + 1}. …" : "");
            Button first = null;
            foreach (var (id, label) in f.moves)
            {
                var mid = id;
                var b = UIKit.Button(moves, label, () =>
                {
                    if (plan.Count >= 3 || plan.Contains(mid)) return;
                    plan.Add(mid); Show();
                    if (plan.Count < 3) return;
                    int at = Enumerable.Range(0, 3).FirstOrDefault(k => plan[k] != f.plan[k]);
                    bool ok = plan.SequenceEqual(f.plan);
                    if (ok) { Sound.Deduce(); CloseSheet(); result(true); return; }
                    Sound.Wrong(); result(false);
                    reply.text = f.why.TryGetValue(plan[at], out var w) ? w : "No.";
                    plan.Clear(); Show();
                }, new Color(0.1f, 0.11f, 0.13f, 0.95f), UIKit.Ink, 20);
                if (first == null) first = b;
            }
            Show();
            UIKit.Select(first.gameObject);
        }

        // ---- the reconstruction: fragments of memory, put back in the order they happened ----
        public void Reconstruct(EventDef ev, Action wake)
        {
            var col = Sheet("Reconstruction", ev.title, false);
            T(col, ev.prompt, 22, UIKit.Ink);
            var placed = T(col, "", 21, UIKit.Gold);
            var reply = T(col, "", 20, UIKit.Blue);
            var frags = UIKit.Rect("Frags", col); frags.gameObject.AddComponent<LayoutElement>().preferredHeight = 360;
            UIKit.Column(frags, 6);
            var order = Enumerable.Range(0, ev.fragments.Count).OrderBy(_ => UnityEngine.Random.value).ToList();
            if (order.Select((v, k) => v == k).All(x => x)) order.Reverse();
            int next = 0, w = 0;
            var lines = new List<string>();
            void Build()
            {
                foreach (Transform c in frags) Destroy(c.gameObject);
                Button first = null;
                foreach (var i in order)
                {
                    int ii = i;
                    var b = UIKit.Button(frags, ev.fragments[i], () =>
                    {
                        if (ii != next) { reply.text = ev.wrong.Count > 0 ? ev.wrong[w++ % ev.wrong.Count] : "No."; Sound.Wrong(); return; }
                        lines.Add($"{next + 1}. {ev.fragments[ii]}"); placed.text = string.Join("\n", lines);
                        order.Remove(ii); next++; reply.text = ""; Sound.Clue();
                        if (next == ev.fragments.Count)
                        {
                            foreach (Transform c in frags) Destroy(c.gameObject);
                            var go = UIKit.Button(frags, "Wake up", () => { CloseSheet(); wake(); }, new Color(0.35f, 0.25f, 0.12f, 1), Color.white, 22);
                            UIKit.Select(go.gameObject);
                        }
                        else Build();
                    }, new Color(0.1f, 0.11f, 0.13f, 0.95f), UIKit.Ink, 18);
                    if (first == null) first = b;
                }
                if (first) UIKit.Select(first.gameObject);
            }
            Build();
        }
    }
}
