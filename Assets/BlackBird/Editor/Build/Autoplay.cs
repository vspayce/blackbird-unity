// Black Bird ▸ Test ▸ Autoplay Chapter: plays the open chapter through to its end card in Play mode, the way a careful
// player would (every place examined, every question asked, every challenge called right, the portrait, the fight
// and the reconstruction done, the Mind Palace worked to the answer), one action a frame through the real UI.
// It logs each step to the Console and stops where it gets stuck, which is the point: a chapter that can't be finished.
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace BlackBird.EditorTools
{
    public static class Autoplay
    {
        const BindingFlags F = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;
        static bool running; static int lastFrame, steps, idle, fightStep, reconStep;
        static TopicDef lastTopic;
        static readonly List<string> log = new List<string>();

        [MenuItem("Black Bird/Test/Autoplay Chapter (Play mode)", priority = 60)]
        public static void Begin()
        {
            if (!Application.isPlaying) { Debug.LogError("[Black Bird] Autoplay: enter Play mode in a chapter scene first."); return; }
            running = true; lastFrame = -1; steps = 0; idle = 0; fightStep = reconStep = 0; lastTopic = null; log.Clear();
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            Debug.Log("[Black Bird] Autoplay started.");
        }

        [MenuItem("Black Bird/Test/Stop Autoplay", priority = 61)]
        public static void Stop() { running = false; EditorApplication.update -= Tick; }

        public static string Report => string.Join("\n", log);
        public static bool Running => running;

        static void Say(string s) { log.Add(s); if (log.Count > 400) log.RemoveAt(0); }
        static void End(string why) { Say("== " + why); Debug.Log("[Black Bird] Autoplay: " + why + "\n" + string.Join("\n", log.Skip(System.Math.Max(0, log.Count - 40)))); Stop(); }

        static T Get<T>(object o, string f) => (T)o.GetType().GetField(f, F).GetValue(o);
        static object Call(object o, string m, params object[] a) => o.GetType().GetMethod(m, F).Invoke(o, a);

        static IEnumerable<Button> Buttons(Component root) =>
            root.GetComponentsInChildren<Button>(false).Where(b => b && b.interactable && b.gameObject.activeInHierarchy);
        static string Label(Button b) { var t = b.GetComponentInChildren<Text>(); return t ? t.text : ""; }
        static bool Click(Component root, System.Func<string, bool> match, string note = null)
        {
            var b = Buttons(root).FirstOrDefault(x => match(Label(x)));
            if (!b) return false;
            Say("> " + (note ?? Label(b)));
            b.onClick.Invoke();
            return true;
        }

        static void Tick()
        {
            if (!running) return;
            if (!Application.isPlaying) { Stop(); return; }
            if (Time.frameCount == lastFrame) return;      // one action a frame: destroyed buttons go at the frame's end
            lastFrame = Time.frameCount;
            if (++steps > 4000) { End("gave up after 4000 steps"); return; }
            var ch = Object.FindFirstObjectByType<Chapter>();
            if (!ch || ch.State == null) return;
            var ui = ch.GetComponent<CaseUI>();
            try { Step(ch, ui); }
            catch (System.Exception e) { End("exception: " + e); }
        }

        static void Step(Chapter ch, CaseUI ui)
        {
            var data = ch.Data; var st = ch.State;
            string mode = Get<object>(ui, "mode").ToString();
            bool reading = Get<bool>(ui, "reading"), judging = Get<bool>(ui, "judging");

            if (reading)
            {
                var cur = Get<Line>(ui, "current");
                Say("  " + (cur.who ?? "") + ": " + cur.text);
                var p = cur.present;
                if (p != null && !st.calls.ContainsKey("p:" + p.id) && Click(ui, s => s == "Present evidence")) { pendingPresent = p; return; }
                Call(ui, "Advance");
                return;
            }
            if (judging && pendingPresent != null)
            {
                var p = pendingPresent; pendingPresent = null;
                var fact = p.evidence.FirstOrDefault(st.Has);
                if (fact == null || !Click(ui, s => s == data.Fact(fact).title)) Click(ui, s => s.StartsWith("Let her go on"));
                return;
            }

            switch (mode)
            {
                case "End":
                    End($"SOLVED {data.title}: {st.Rating().stars} stars, {st.clues.Count}/{data.clues.Count} clues, {st.deductions.Count}/{data.deductions.Count} deductions, misses {st.misses}");
                    return;
                case "Conclude":
                    var right = data.conclusion.answers.First(a => a.right).a;
                    Click(ui, s => s == right);
                    return;
                case "Palace":
                    foreach (var kv in data.deductions)
                        if (!st.deductions.Contains(kv.Key) && kv.Value.from.All(st.Has))
                        {
                            Say($"Mind Palace: {string.Join(" + ", kv.Value.from)} -> {kv.Key}");
                            foreach (var x in kv.Value.from) Call(ui, "Pick", x);
                            return;
                        }
                    if (st.CanConclude) { Say("Mind Palace: answer"); Click(ui, s => s == data.conclusion.question); return; }
                    Call(ui, "ClosePalace"); palaceTried = st.clues.Count + st.deductions.Count;
                    return;
                case "Book": Call(ui, "CloseBook"); return;
                case "Sheet": Sheet(ch, ui); return;
                case "Talk":
                    if (judging) { Judge(ui, st, data); return; }
                    Menu(ui, st, data);
                    return;
            }
            fightStep = reconStep = 0;

            // ---- exploring ----
            var portrait = Get<PortraitDef>(ch, "portrait");
            if (portrait != null)
            {
                var s = portrait.spots.FirstOrDefault(x => !st.Has(x.clue));
                if (s != null) { Say("portrait: " + s.label); Call(ch, "PortraitFound", s); }
                return;
            }
            if (Get<bool>(ch, "busy")) { if (++idle > 600) End("stuck: busy with nothing on screen"); return; }
            idle = 0;
            // Chapter V's tail: keep well back from the boy, out of his sight but not losing him
            var tail = ch.GetComponent<Tail>();
            if (tail && tail.Phase == "lead" && data.tail != null)
            {
                var who = ch.cast.First(c => c.id == data.tail.who).walker.transform;
                var back = who.position - ch.holmes.transform.position; back.y = 0;
                float gap = (data.tail.sight + data.tail.lose) / 2;
                if (back.magnitude > 0.5f && Mathf.Abs(back.magnitude - gap) > 1.5f)
                    ch.PlaceHolmes(who.position - back.normalized * gap, Quaternion.LookRotation(back).eulerAngles.y);
                if (++tailFrames % 300 == 0) Say($"tailing: {back.magnitude:0} m behind, suspicion {tail.Suspicion:0.00}");
                return;
            }
            foreach (var s in data.spots)
                if ((bool)Call(ch, "SpotOpen", s) && (s.AllClues().Length > 0 || (s.@event != null && (s.needs == null || s.needs.All(st.Has)))))
                {
                    Say("examine: " + (s.label ?? s.id));
                    Call(ch, "UseSpot", s);
                    return;
                }
            foreach (var id in data.talk.Keys)
            {
                if (id != "watson" && !(ch.cast?.Any(c => c.id == id && c.walker && c.walker.gameObject.activeSelf) ?? false)) continue;
                if (Available(ui, st, data, id).Any() || (data.portraits != null && data.portraits.TryGetValue(id, out var pd) && !st.events.Contains(pd.@event)))
                {
                    Say("talk: " + id);
                    Call(ch, "Talk", id);
                    return;
                }
            }
            int have = st.clues.Count + st.deductions.Count;
            if (have != palaceTried || st.CanConclude) { ui.OpenPalace(); return; }
            End($"stuck: nothing left to do. Objective: {st.Objective()}. Clues {string.Join(",", st.clues)}; deductions {string.Join(",", st.deductions)}; events {string.Join(",", st.events)}");
        }
        static PresentDef pendingPresent;
        static int tailFrames;
        static int palaceTried = -1;

        // topics still worth choosing, as CaseUI.Menu shows them
        static IEnumerable<(int i, TopicDef t)> Available(CaseUI ui, CaseState st, CaseData data, string person)
        {
            var asked = Get<HashSet<string>>(ui, "asked");
            var topics = data.talk[person].topics;
            for (int i = 0; i < topics.Count; i++)
            {
                var t = topics[i];
                if (t.needs.Length > 0 && !t.needs.All(st.Has)) continue;
                if (t.@event != null && st.events.Contains(t.@event)) continue;
                if (t.after != null && !st.events.Contains(t.after)) continue;
                if (t.before != null && st.events.Contains(t.before)) continue;
                string key = person + i;
                bool called = t.challenge != null && st.calls.ContainsKey(key);
                bool retry = t.challenge != null && called && t.challenge.gives != null && !st.Has(t.challenge.gives);
                if (!retry && (asked.Contains(key) || (t.gives != null && st.Has(t.gives)) || called)) continue;
                yield return (i, t);
            }
        }

        static void Menu(CaseUI ui, CaseState st, CaseData data)
        {
            var person = Get<string>(ui, "person");
            if (person == null) return;
            var next = Available(ui, st, data, person).FirstOrDefault();
            if (next.t != null && Click(ui, s => s.StartsWith(next.t.q))) { lastTopic = next.t; return; }
            Click(ui, s => s == "That will be all.");
        }

        static void Judge(CaseUI ui, CaseState st, CaseData data)
        {
            var c = lastTopic?.challenge;
            if (c == null) { End("stuck: a challenge with no topic"); return; }
            if (Buttons(ui).Any(b => Label(b) == "On second thought…"))
            {
                var fact = c.evidence.FirstOrDefault(st.Has);
                if (fact == null) { Say("!! no evidence held for this lie; backing out"); Click(ui, s => s == "On second thought…"); return; }
                Click(ui, s => s == data.Fact(fact).title);
                return;
            }
            var label = c.answer == "lie" ? "Prove a lie" : c.answer == "doubt" ? "Press" : "Believe";
            Click(ui, s => s == label);
        }

        static void Sheet(Chapter ch, CaseUI ui)
        {
            if (Click(ui, s => s == "Go on" || s == "Wake up")) return;
            var data = ch.Data;
            var portrait = Get<PortraitDef>(ch, "portrait");
            if (portrait != null)
            {
                foreach (var L in portrait.lines) if (Click(ui, s => s == L.options[L.answer])) return;
                End("stuck: profile sheet with no right answer showing");
                return;
            }
            var labels = Buttons(ui).Select(Label).ToList();
            if (data.fights != null)
                foreach (var f in data.fights.Values)
                    if (f.moves.Any(m => labels.Contains(m.Item2)))
                    {
                        var id = f.plan[fightStep % f.plan.Length]; fightStep++;
                        Click(ui, s => s == f.moves.First(m => m.Item1 == id).Item2);
                        return;
                    }
            foreach (var ev in data.events.Values.Where(e => e.fragments != null && e.fragments.Count > 0))
                if (ev.fragments.Any(labels.Contains))
                {
                    var frag = ev.fragments[System.Math.Min(reconStep, ev.fragments.Count - 1)]; reconStep++;
                    Click(ui, s => s == frag);
                    return;
                }
            End("stuck: a sheet I don't know: " + string.Join(" | ", labels));
        }
    }
}
