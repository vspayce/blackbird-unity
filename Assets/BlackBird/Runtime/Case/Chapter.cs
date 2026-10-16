// A chapter of the case in any set: Holmes and Watson explore, examine the places the case names, talk to the people
// in it, keep the Notebook and work in the Mind Palace, and answer the chapter's question. Story events from
// conversations (the fight with Cairo, the drugged whisky) and the character portrait are run here too.
// The story is Resources/Cases/<caseId>.json (from the web game); the set's named places come from its JSON.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BlackBird
{
    [Serializable] public class NamedPlace { public string id; public Vector3 pos; }
    [Serializable] public class CastMember { public string id; public Walker walker; }

    public class Chapter : MonoBehaviour
    {
        public string caseId;
        public Walker holmes, watson;
        public CastMember[] cast;
        public CameraRig cameraRig;
        public NamedPlace[] places;
        public Vector3 spawn; public float spawnYaw; public Vector3 watsonOffset = new Vector3(-1.1f, 0, -0.7f);
        public string nextScene;                 // where "Continue" goes after the end card
        public string warp;                      // "hopkins": the case's positions are the prototype's, carried into the doubled house
        public PlaceDef[] uses = new PlaceDef[0];          // the set's own things to use: a stair to the tower, a line about a room
        public Destination[] destinations = new Destination[0];

        Explorer ex;
        CaseData data;
        CaseState state;
        CaseUI ui;
        string contextLabel; Action contextAct;
        bool busy;                               // a cutscene, portrait or fight has the screen

        public CaseState State => state;
        public CaseData Data => data;

        void Start()
        {
            data = CaseData.Load(caseId);
            state = new CaseState(data);
            bool resumed = state.Load();
            ui = GetComponent<CaseUI>() ?? gameObject.AddComponent<CaseUI>();
            ui.Bind(state);
            ui.onGive = Give;
            ui.onClosed = () => { busy = false; Refresh(); };
            ui.onEvent = RunEvent;
            ui.onTalking = Talking;
            ui.focusOn = () => Focus.I && Focus.I.On;
            ui.toggleFocus = () => Focus.I?.Toggle();
            ui.onSolved = () =>
            {
                Refresh();
                if (!string.IsNullOrEmpty(nextScene)) SceneSwitch.Go(nextScene);
            };

            ex = GetComponent<Explorer>();
            if (!ex) { ex = gameObject.AddComponent<Explorer>(); ex.holmes = holmes; ex.watson = watson; ex.cameraRig = cameraRig; }
            ex.Place(spawn, spawnYaw);
            if (watson) watson.transform.position = data.people.TryGetValue("watson", out var wp) && wp.pos != null ? P(wp.pos)   // where the case puts him
                                                  : spawn + Quaternion.Euler(0, spawnYaw, 0) * watsonOffset;
            foreach (var c in cast)
            {
                if (!data.people.TryGetValue(c.id, out var p) || p.pos == null || !c.walker) continue;
                c.walker.transform.position = P(p.pos);
                if (p.face != null) { var f = P(p.face) - c.walker.transform.position; f.y = 0; if (f.sqrMagnitude > 0.01f) c.walker.transform.rotation = Quaternion.LookRotation(f); }
            }
            Presence();
            Hud.I.CaseControls(true);
            Refresh();
            if (!resumed || state.talked.Count == 0)
            {
                busy = true;
                var intro = data.intro.Select(o => o is string s ? new Line(null, s) : CaseData.ToLine(o)).ToList();
                if (intro.Count > 0) ui.Lines(intro, () => { busy = false; Hud.I.Narrate("Holmes", data.opening); });
                else { busy = false; Hud.I.Narrate("Holmes", data.opening); }
            }
            else Hud.I.Narrate("Holmes", "Where were we, Watson?");
        }

        /// A position from the case data, in the web game's coordinates of this set.
        public static Vector3 Pos(float[] p) => p.Length >= 3 ? Web.W(p[0], p[1], p[2]) : Web.W(p[0], p[1]);

        /// The same, through this set's warp if it has one.
        public Vector3 P(float[] p)
        {
            var v = p.Length >= 3 ? new Vector3(p[0], p[1], p[2]) : new Vector3(p[0], 0, p[1]);
            if (warp == "hopkins") v = HopkinsWarp.Map(v);
            return Web.W(v.x, v.y, v.z);
        }

        Vector3? PlacePos(SpotInfo s)
        {
            if (s.pos != null) return P(s.pos);
            var id = s.place ?? s.worldId ?? s.id;
            var np = places?.FirstOrDefault(p => p.id == id);
            return np != null ? np.pos : (Vector3?)null;
        }

        void Refresh()
        {
            Hud.I.SetObjective(data.title, state.Objective());
            Hud.I.SetCounter($"Clues {state.clues.Count} / {data.clues.Count}");
        }

        // who is here: people who leave after an event go
        void Presence()
        {
            foreach (var c in cast)
                if (c.walker && data.people.TryGetValue(c.id, out var p) && p.leaves != null)
                    c.walker.gameObject.SetActive(!state.events.Contains(p.leaves));
        }

        void Update()
        {
            float dt = Time.deltaTime;
            var input = GameInput.I;
            Hud.I.SetTouchMode(input.UsingTouch);
            if (ui.Open || busy || portrait != null)
            {
                holmes.Idle(dt); FollowWatson(dt); People(dt);
                Hud.I.Context(null, input.UsingTouch);
                if (portrait != null) PortraitUpdate();
                Reads();
                return;
            }
            if (input.Focus) Focus.I.Toggle();
            if (input.Book) { ui.OpenBook(); return; }
            if (input.Palace) { ui.OpenPalace(); return; }
            ex.Steer();
            FollowWatson(dt);
            People(dt);
            FindContext();
            Hud.I.Context(contextLabel, input.UsingTouch);
            if (input.Act && contextAct != null) contextAct();
            Reads();
        }

        // Watson keeps at Holmes's shoulder, except where the case has him wait (Chapter IV: below the tower until Holmes
        // has seen the Fat Man alone)
        bool waitSaid;
        void FollowWatson(float dt)
        {
            if (watson && data.chapter != null && data.chapter.TryGetValue("watsonWaits", out var wo) && wo is Dictionary<string, object> ww
                && !state.events.Contains((string)ww["until"]))
            {
                float above = P(new[] { 0f, Convert.ToSingle(ww["above"]), 0f }).y;
                if (holmes.transform.position.y > above)
                {
                    if (!waitSaid) { waitSaid = true; Hud.I.Narrate("Holmes", (string)ww["line"]); }
                    watson.Idle(dt);
                    return;
                }
            }
            ex.Follow(dt);
        }

        void People(float dt)
        {
            foreach (var c in cast)
            {
                if (!c.walker || !c.walker.gameObject.activeSelf) continue;
                var p = c.walker; p.Idle(dt);
                float d = Vector3.Distance(p.transform.position, holmes.transform.position);
                p.lookTarget = d < 5f ? holmes.transform.Find("LookAt") ?? holmes.transform : null;
                if (d < 2.6f) p.FaceTowards(holmes.transform.position, dt * 0.6f);
            }
            holmes.lookPoint = null;
        }

        void Talking(string who, bool on)
        {
            foreach (var w in All()) w.SetTalking(false);
            if (!on || who == null) return;
            if (who == "Holmes") { holmes.SetTalking(true); return; }
            if (who == "Watson" && watson) { watson.SetTalking(true); return; }
            foreach (var c in cast)
                if (c.walker && data.people.TryGetValue(c.id, out var p) && (p.name.Contains(who) || c.id == who.ToLowerInvariant())) c.walker.SetTalking(true);
        }
        IEnumerable<Walker> All() { yield return holmes; if (watson) yield return watson; foreach (var c in cast) if (c.walker) yield return c.walker; }

        bool SpotOpen(SpotInfo s)
        {
            if (s.after != null && !state.events.Contains(s.after)) return false;
            if (s.before != null && state.events.Contains(s.before)) return false;
            var clues = s.AllClues();
            if (clues.Length > 0 && clues.All(state.Has)) return false;
            return true;
        }

        void FindContext()
        {
            contextLabel = null; contextAct = null;
            var hp = holmes.transform.position;
            float best = float.MaxValue;
            bool focusing = Focus.I.Amount > 0.3f;
            foreach (var s in data.spots)
            {
                if (!SpotOpen(s) || (s.focus && !focusing)) continue;
                var at = PlacePos(s); if (at == null) continue;
                var flat = at.Value - hp; flat.y = 0;
                float d = flat.magnitude;
                if (d > s.r || d >= best || Mathf.Abs(at.Value.y - hp.y) > 3.5f) continue;
                best = d; var spot = s;
                contextLabel = s.label ?? "Look closer"; contextAct = () => UseSpot(spot);
                holmes.lookPoint = at;
            }
            foreach (var c in cast)
            {
                if (!c.walker || !c.walker.gameObject.activeSelf || !data.talk.ContainsKey(c.id)) continue;
                float d = Vector3.Distance(c.walker.transform.position, hp);
                if (d > 2.4f || d >= best) continue;
                best = d; var id = c.id;
                contextLabel = "Talk to " + data.people[id].name;
                contextAct = () => Talk(id);
            }
            foreach (var u in uses)
            {
                var d3 = u.pos - hp;
                if (Mathf.Abs(d3.y) > 2.2f) continue;
                d3.y = 0; float d = d3.magnitude;
                if (d > u.r || d >= best || (string.IsNullOrEmpty(u.to) && string.IsNullOrEmpty(u.line))) continue;
                best = d; var use = u;
                contextLabel = u.label; contextAct = () => Use(use);
                holmes.lookPoint = u.pos;
            }
            var toW = watson ? watson.transform.position - hp : Vector3.zero; toW.y = 0;
            if (watson && toW.magnitude < 1.8f && contextLabel == null && holmes.Speed < 0.2f && Vector3.Angle(holmes.transform.forward, toW) < 50f && data.talk.ContainsKey("watson"))
            {
                contextLabel = "Talk to Dr. Watson";
                contextAct = () => Talk("watson");
            }
        }

        void Use(PlaceDef u)
        {
            if (!string.IsNullOrEmpty(u.to))
                foreach (var d in destinations)
                    if (d.id == u.to) { ex.Place(d.pos, d.yaw); break; }
            if (!string.IsNullOrEmpty(u.line)) Hud.I.Narrate("Holmes", u.line);
        }

        void UseSpot(SpotInfo s)
        {
            var clues = s.AllClues();
            if (clues.Length > 0) { foreach (var c in clues) Give(c); return; }
            if (s.say != null) Hud.I.Narrate("Holmes", s.say);
        }

        public void Give(string id)
        {
            if (!state.AddClue(id)) return;
            var c = data.clues[id];
            Hud.I.ClueSlip(c.title, c.text, c.kind == "testimony" ? "Testimony" : "New clue");
            Refresh();
        }

        // ---- conversations, and the character portrait that opens some of them ----
        void Talk(string id)
        {
            if (data.portraits != null && data.portraits.TryGetValue(id, out var pd) && !state.events.Contains(pd.@event)) { EnterPortrait(id, pd); return; }
            Hud.I.Narrate("", "");
            ui.Talk(id);
        }

        PortraitDef portrait; Walker portraitWho;
        void EnterPortrait(string id, PortraitDef def)
        {
            portrait = def; portraitDone = false;
            portraitWho = cast.First(c => c.id == id).walker;
            var p = portraitWho.transform;
            var toH = holmes.transform.position - p.position; toH.y = 0;
            p.rotation = Quaternion.LookRotation(toH);
            var right = Vector3.Cross(Vector3.up, p.forward);
            cameraRig.Shot(p.position + p.forward * 1.9f + right * 0.3f + Vector3.up * 1.5f, p.position + Vector3.up * 1.12f);
            Hud.I.Letterbox(true);
            GameInput.I.allowMouseCapture = false;
            Focus.I.ForceOn();
            Hud.I.Narrate("Holmes", "Hold. Look at him, Watson, before he says a word.");
            foreach (var s in def.spots)
            {
                var spot = s;
                Hud.I.AddMark(p.TransformPoint(new Vector3(s.local[0], s.local[1], s.local[2])), s.label, false, () => state.Has(spot.clue), () => PortraitFound(spot));
            }
        }

        void PortraitUpdate()
        {
            GameInput.I.allowMouseCapture = false;
            if (!portraitDone && portrait.spots.All(x => state.Has(x.clue)) && !ui.Open) { portraitDone = true; PortraitSheet(); }
        }
        bool portraitDone;

        void PortraitFound(PortraitSpot s) => Give(s.clue);   // the sheet opens once every mark is found (PortraitUpdate)

        void PortraitSheet()
        {
            var def = portrait; var id = cast.First(c => c.walker == portraitWho).id;
            Hud.I.ClearMarks();
            ui.ProfileSheet(data.people[id].name, def.lines, () =>
            {
                state.AddEvent(def.@event);
                Give(def.gives);
                portrait = null;
                cameraRig.EndShot(); Hud.I.Letterbox(false); Focus.I.ForceOff();
                ui.Talk(id);
            });
        }

        // ---- story events ----
        void RunEvent(string name)
        {
            if (data.fights != null && data.fights.TryGetValue(name, out var fight)) { RunFight(name, fight); return; }
            if (data.events.TryGetValue(name, out var ev))
            {
                busy = true;
                var before = ev.before.Select(s => new Line(null, s)).ToList();
                Action wake = () =>
                {
                    state.AddEvent(name);
                    Presence();
                    if (ev.wake != null) { ex.Place(P(ev.wake), 0); if (watson) watson.transform.position = P(ev.wake) + new Vector3(0.9f, 0, 0.6f); }
                    ui.Lines(ev.after, () => { busy = false; if (ev.gives != null) Give(ev.gives); Refresh(); });
                };
                if (ev.fragments != null && ev.fragments.Count > 0) ui.Lines(before, () => ui.Reconstruct(ev, wake));
                else ui.Lines(ev.lines.Count > 0 ? ev.lines : before, wake);
                return;
            }
            state.AddEvent(name); Presence(); Refresh();
        }

        void RunFight(string name, FightDef f)
        {
            busy = true;
            var foe = cast.First(c => c.id == f.who).walker.transform;
            var h = holmes.transform;
            foe.rotation = Quaternion.LookRotation(Flat(h.position - foe.position));
            h.rotation = Quaternion.LookRotation(Flat(foe.position - h.position));
            var mid = Vector3.Lerp(h.position, foe.position, 0.5f);
            var dir = Flat(foe.position - h.position).normalized; var perp = Vector3.Cross(Vector3.up, dir);
            cameraRig.Shot(mid + perp * 2.6f - dir * 0.4f + Vector3.up * 1.55f, mid + Vector3.up * 1.3f);
            var foeW = foe.GetComponent<Walker>(); foeW.SetPose("Aim"); holmes.SetPose("HandsUp");
            Focus.I.ForceOn();
            ui.FightPlanner(f, ok =>
            {
                if (!ok) { state.misses++; state.Save(); return; }
                Focus.I.ForceOff();
                foeW.SetPose(null); holmes.SetPose(null);
                ui.Lines(f.success, () =>
                {
                    cameraRig.EndShot();
                    state.AddEvent(name); Presence();
                    busy = false; Refresh();
                    Hud.I.Narrate("Holmes", "Now then. Your pockets, onto the table.");
                });
            });
        }

        static Vector3 Flat(Vector3 v) { v.y = 0; return v; }

        // ---- Focus reads ----
        readonly List<(Vector3, string)> readBuf = new List<(Vector3, string)>();
        void Reads()
        {
            readBuf.Clear();
            float k = Focus.I.Amount;
            if (k > 0.02f && portrait == null)
            {
                foreach (var c in cast) if (c.walker && c.walker.gameObject.activeSelf) AddReads(c.walker.transform, c.id);
                if (watson) AddReads(watson.transform, "watson");
            }
            Hud.I.ShowReads(readBuf, k);
        }
        void AddReads(Transform t, string id)
        {
            if (!data.reads.TryGetValue(id, out var r)) return;
            if (Vector3.Distance(t.position, holmes.transform.position) > 9f) return;
            for (int j = 0; j < r.Length; j++)
            {
                readBuf.Add((t.position + Vector3.up * (1.95f - j * 0.22f) + Camera.main.transform.right * 0.35f, r[j]));
                if (Focus.I.Amount > 0.8f) state.AddRead(id + ":" + j);
            }
        }
    }
}
