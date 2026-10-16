// Chapter I, Burritt Alley, the whole case: walk the alley with Watson, use Focus, examine the scene and the body,
// question Polhaus and Kelly (and read their lies), keep the Notebook, draw the threads in the Mind Palace and name
// the killer. The story is the web game's (Resources/Cases/archer.json); the alley and its people are this scene's.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BlackBird
{
    public class Game : MonoBehaviour
    {
        public Walker holmes, watson;
        public Walker[] people;                 // same order as Chapter1.People (polhaus, kelly)
        public CameraRig cameraRig;
        public Light pocketLamp;
        public Transform[] hiddenBySpot;        // optional: the HiddenDetail object for each spot id (heel, scent, webley)
        public string[] hiddenSpotIds;
        public string caseId = "archer";

        Explorer ex;
        CaseData data;
        CaseState state;
        CaseUI ui;
        string[] personIds;

        enum Mode { Explore, Closeup, Screen }
        Mode mode;
        string contextLabel; Action contextAct;

        void Start()
        {
            data = CaseData.Load(caseId);
            state = new CaseState(data);
            bool resumed = state.Load();
            ui = gameObject.GetComponent<CaseUI>() ?? gameObject.AddComponent<CaseUI>();
            ui.Bind(state);
            ui.onGive = Give;
            ui.onClosed = () => { mode = Mode.Explore; Refresh(); };
            ui.onSolved = () => { Refresh(); Hud.I.Narrate("Holmes", "Come, Watson. Back to the Palace for an hour's sleep."); };
            ui.onTalking = Talking;
            ui.focusOn = () => Focus.I && Focus.I.On;
            ui.toggleFocus = () => Focus.I?.Toggle();

            ex = GetComponent<Explorer>();
            if (!ex) { ex = gameObject.AddComponent<Explorer>(); ex.holmes = holmes; ex.watson = watson; ex.cameraRig = cameraRig; }
            ex.Place(Chapter1.Start, Quaternion.LookRotation(Web.W(0, -1) - Web.W(0, 0)).eulerAngles.y);
            personIds = Chapter1.People.Select(p => p.id).ToArray();
            for (int i = 0; i < people.Length && i < personIds.Length; i++)
            {
                var d = data.people[personIds[i]];
                people[i].transform.position = Web.W(d.pos[0], d.pos[1]);
                var f = Web.W(d.face[0], d.face[1]) - people[i].transform.position; f.y = 0;
                people[i].transform.rotation = Quaternion.LookRotation(f);
            }
            if (hiddenSpotIds != null)
                for (int i = 0; i < hiddenSpotIds.Length; i++)
                    if (state.Has(hiddenSpotIds[i]) && hiddenBySpot[i] && hiddenBySpot[i].TryGetComponent<HiddenDetail>(out var h)) h.found = true;
            Hud.I.CaseControls(true);
            Refresh();
            Hud.I.Narrate("Holmes", resumed && state.clues.Count > data.startClues.Length ? "Where were we, Watson? Ah yes." : data.opening);
        }

        void Refresh()
        {
            Hud.I.SetObjective(data.title, state.Objective());
            Hud.I.SetCounter($"Clues {state.clues.Count} / {data.clues.Count}");
        }

        void Update()
        {
            float dt = Time.deltaTime;
            var input = GameInput.I;
            Hud.I.SetTouchMode(input.UsingTouch);

            if (ui.Open || mode == Mode.Screen)
            {
                holmes.Idle(dt); Follow(dt); People(dt);
                Hud.I.Context(null, input.UsingTouch);
                Reads();
                return;
            }
            if (input.Focus) Focus.I.Toggle();
            if (input.Book) { ui.OpenBook(); return; }
            if (input.Palace) { ui.OpenPalace(); return; }

            if (mode == Mode.Closeup)
            {
                holmes.Idle(dt); Follow(dt); People(dt);
                if (input.Back) LeaveCloseup();
                Hud.I.Context(null, input.UsingTouch);
                Reads();
                return;
            }

            ex.Steer();
            Follow(dt);
            People(dt);
            FindContext();
            Hud.I.Context(contextLabel, input.UsingTouch);
            if (input.Act && contextAct != null) contextAct();
            Reads();
        }

        void Follow(float dt) => ex.Follow(dt, mode == Mode.Closeup ? Chapter1.Closeup.Look : (Vector3?)null);

        void People(float dt)
        {
            for (int i = 0; i < people.Length; i++)
            {
                var p = people[i];
                p.Idle(dt);
                float d = Vector3.Distance(p.transform.position, holmes.transform.position);
                p.lookTarget = d < 5f ? holmes.transform.Find("LookAt") ?? holmes.transform : null;
                var pd = data.people[personIds[i]];
                var faceTo = d < 2.6f ? holmes.transform.position : Web.W(pd.face[0], pd.face[1]);
                p.FaceTowards(faceTo, dt * 0.6f);
            }
            holmes.lookPoint = null;
        }

        // the speaker gestures while their line is up
        void Talking(string who, bool on)
        {
            foreach (var w in All()) w.SetTalking(false);
            if (!on || who == null) return;
            if (who == "Holmes") { holmes.SetTalking(true); return; }
            if (who == "Watson" && watson) { watson.SetTalking(true); return; }
            for (int i = 0; i < people.Length; i++)
                if (data.people[personIds[i]].name.Contains(who)) people[i].SetTalking(true);
        }
        IEnumerable<Walker> All() { yield return holmes; if (watson) yield return watson; foreach (var p in people) yield return p; }

        // ---- what Holmes could do here ----
        void FindContext()
        {
            contextLabel = null; contextAct = null;
            var hp = holmes.transform.position;
            float best = float.MaxValue;
            bool focusing = Focus.I.Amount > 0.3f;
            foreach (var s in data.spots)
            {
                if (s.clue != null && state.Has(s.clue)) continue;
                if (s.focus && !focusing) continue;
                var flat = Web.W(s.pos[0], s.pos[1], s.pos[2]) - hp; flat.y = 0;
                float d = flat.magnitude;
                if (d > s.r || d >= best) continue;
                best = d; var spot = s;
                contextLabel = s.label; contextAct = () => UseSpot(spot);
                holmes.lookPoint = Web.W(s.pos[0], s.pos[1], s.pos[2]);
            }
            for (int i = 0; i < people.Length; i++)
            {
                float d = Vector3.Distance(people[i].transform.position, hp);
                if (d > 2.2f || d >= best) continue;
                best = d; var id = personIds[i];
                contextLabel = "Talk to " + data.people[id].name;
                contextAct = () => { mode = Mode.Screen; Hud.I.Narrate("", ""); ui.Talk(id); };
            }
            var toW = watson ? watson.transform.position - hp : Vector3.zero; toW.y = 0;
            if (watson && toW.magnitude < 1.8f && contextLabel == null && holmes.Speed < 0.2f && Vector3.Angle(holmes.transform.forward, toW) < 50f)
            {
                contextLabel = "Talk to Dr. Watson";
                contextAct = () => { mode = Mode.Screen; ui.Talk("watson"); };
            }
        }

        void UseSpot(SpotInfo s)
        {
            if (s.closeup) { EnterCloseup(); return; }
            if (s.say != null) { Hud.I.Narrate("Holmes", s.say); return; }
            if (s.clue != null) Give(s.clue);
        }

        // a clue found or a testimony heard
        void Give(string id)
        {
            if (!state.AddClue(id)) return;
            var c = data.clues[id];
            Hud.I.ClueSlip(c.title, c.text, c.kind == "testimony" ? "Testimony" : "New clue");
            if (hiddenSpotIds != null)
                for (int i = 0; i < hiddenSpotIds.Length; i++)
                    if (hiddenSpotIds[i] == id && hiddenBySpot[i] && hiddenBySpot[i].TryGetComponent<HiddenDetail>(out var h)) h.found = true;
            Refresh();
            if (state.clues.Count == data.clues.Count)
                Hud.I.Narrate("Holmes", "That is everything the alley has to tell us, Watson. The rest is for the Mind Palace.");
        }

        // ---- the close-up on Archer ----
        void EnterCloseup()
        {
            mode = Mode.Closeup;
            GameInput.I.allowMouseCapture = false;   // the clues are clicked
            var cu = data.closeup;
            cameraRig.Shot(Web.W(cu.cam[0], cu.cam[1], cu.cam[2]), Web.W(cu.look[0], cu.look[1], cu.look[2]));
            Hud.I.Letterbox(true);
            Hud.I.BackButton(true);
            if (pocketLamp) pocketLamp.enabled = true;
            foreach (var m in cu.spots)
            {
                var mark = m;
                Hud.I.AddMark(Web.W(m.pos[0], m.pos[1], m.pos[2]), mark.label, mark.focus, () => state.Has(mark.clue), () =>
                {
                    if (mark.focus && Focus.I.Amount < 0.3f && !state.Has(mark.clue)) return;
                    Give(mark.clue);
                });
            }
        }

        void LeaveCloseup()
        {
            mode = Mode.Explore;
            cameraRig.EndShot();
            Hud.I.Letterbox(false);
            Hud.I.BackButton(false);
            Hud.I.ClearMarks();
            if (pocketLamp) pocketLamp.enabled = false;
        }

        // ---- Focus reads: noticed ones go into the Notebook ----
        readonly List<(Vector3, string)> readBuf = new List<(Vector3, string)>();
        void Reads()
        {
            readBuf.Clear();
            float k = Focus.I.Amount;
            if (k > 0.02f)
            {
                for (int i = 0; i < people.Length; i++) AddReads(people[i].transform, personIds[i]);
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
