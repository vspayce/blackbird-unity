// Chapter I test: walk Burritt Alley with Watson, use Focus, examine the scene and the body.
// (Notebook, interrogation and the Mind Palace stay in the web prototype for now.)
using System;
using System.Collections.Generic;
using UnityEngine;

namespace BlackBird
{
    public class Game : MonoBehaviour
    {
        public Walker holmes, watson;
        public Walker[] people;                 // same order as Chapter1.People
        public CameraRig cameraRig;
        public Light pocketLamp;
        public Transform[] hiddenBySpot;        // optional: the HiddenDetail object for each spot id (heel, scent, webley)
        public string[] hiddenSpotIds;

        public float walkSpeed = 1.55f, strollSpeed = 0.75f, hurrySpeed = 2.4f;
        [Tooltip("How quickly the camera swings round behind Holmes when he walks and you aren't steering it (deg/s).")]
        public float followTurn = 120f;
        [Tooltip("Test only: pushes the move stick (x, y) when nothing else does.")]
        public Vector2 autopilot;

        enum Mode { Explore, Closeup, Talk, Done }
        Mode mode;
        readonly HashSet<string> found = new HashSet<string>();
        readonly Queue<(string who, string line)> lines = new Queue<(string, string)>();
        Action afterLines;
        string contextLabel; Action contextAct;

        float lastLook = -10f;

        void Start()
        {
            var ha = holmes.GetComponentInChildren<Animator>(); if (ha) ha.updateMode = AnimatorUpdateMode.UnscaledTime;
            // Watson keeps at Holmes's shoulder: never let him body-block Holmes
            if (watson && holmes.TryGetComponent<CharacterController>(out var hc) && watson.TryGetComponent<CharacterController>(out var wc))
                Physics.IgnoreCollision(hc, wc);
            holmes.transform.position = Chapter1.Start;
            holmes.transform.rotation = Quaternion.LookRotation(Web.W(0, -1) - Web.W(0, 0));
            cameraRig.SnapBehind();
            for (int i = 0; i < people.Length && i < Chapter1.People.Length; i++)
            {
                var d = Chapter1.People[i];
                people[i].transform.position = d.pos;
                var f = d.face - d.pos; f.y = 0;
                people[i].transform.rotation = Quaternion.LookRotation(f);
            }
            Hud.I.SetObjective(Chapter1.Title, "Examine the scene. Use Focus to see what the police missed.");
            UpdateCounter();
            Narrate("Holmes", Chapter1.Opening);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            var input = GameInput.I;
            Hud.I.SetTouchMode(input.UsingTouch);

            if (input.Focus && mode != Mode.Talk) Focus.I.Toggle();
            if (mode == Mode.Talk)
            {
                GameInput.I.allowMouseCapture = false;
                if (input.Act || input.Back) Hud.I.AdvanceCaption();
                holmes.Idle(dt);
                Follow(dt);
                People(dt);
                Hud.I.Context(null, input.UsingTouch);
                cameraRig.AddLook(input.Look * 0.5f);
                return;
            }

            if (mode == Mode.Closeup)
            {
                holmes.Idle(dt); Follow(dt); People(dt);
                if (input.Back) LeaveCloseup();
                Hud.I.Context(null, input.UsingTouch);
                Reads();
                return;
            }

            // exploring: move relative to the camera; the camera swings round behind Holmes as he walks
            // unless you're steering it yourself
            GameInput.I.allowMouseCapture = true;
            cameraRig.AddLook(input.Look);
            if (input.LookedThisFrame) lastLook = Time.unscaledTime;
            var m = input.Move.sqrMagnitude > 0.01f ? input.Move : autopilot;
            var fwd = Quaternion.Euler(0, cameraRig.yaw, 0);
            var dir = fwd * new Vector3(m.x, 0, m.y);
            float push = m.magnitude;
            float speed = push < 0.05f ? 0 : (push < 0.7f ? Mathf.Lerp(strollSpeed * 0.6f, strollSpeed, push / 0.7f) : Mathf.Lerp(strollSpeed, walkSpeed, (push - 0.7f) / 0.3f));
            if (input.Hurry && push > 0.5f) speed = hurrySpeed;
            holmes.Drive(push > 0.05f ? dir.normalized * speed : Vector3.zero, Time.unscaledDeltaTime);   // Focus slows the world, not Holmes
            // (a stick pushed mostly forward only: steering diagonally while the camera chases would walk in circles)
            if (m.y > 0.5f && Mathf.Abs(m.x) < 0.35f && holmes.Speed > 0.3f && Time.unscaledTime - lastLook > 0.8f)
            {
                // ease toward Holmes's heading, more strongly the further off it is
                float heading = holmes.transform.eulerAngles.y;
                float off = Mathf.DeltaAngle(cameraRig.yaw, heading);
                cameraRig.yaw += Mathf.Clamp(off, -1, 1) * Mathf.Min(Mathf.Abs(off), followTurn * Mathf.InverseLerp(0, 40, Mathf.Abs(off)) * Time.unscaledDeltaTime);
            }
            Follow(dt);
            People(dt);
            FindContext();
            Hud.I.Context(contextLabel, input.UsingTouch);
            if (input.Act && contextAct != null) contextAct();
            Reads();
        }

        // ---- Watson keeps to Holmes's left shoulder ------------------------------------------------
        void Follow(float dt)
        {
            if (!watson) return;
            var h = holmes.transform;
            var slot = h.position - h.right * 0.95f - h.forward * 0.55f;
            var to = slot - watson.transform.position; to.y = 0;
            float d = to.magnitude;
            if (d > 0.35f)
            {
                float s = Mathf.Clamp(d * 1.6f, 0.4f, holmes.Speed + 0.6f);
                if (d > 4f) s = 2.4f;                     // catching up
                watson.Drive(to / d * s, dt);
            }
            else
            {
                watson.Idle(dt);
                watson.FaceTowards(watson.transform.position + h.forward, dt);
            }
            watson.lookPoint = mode == Mode.Closeup ? Chapter1.Closeup.Look : (Vector3?)(h.position + Vector3.up * 1.6f + h.forward * 3f);
        }

        void People(float dt)
        {
            for (int i = 0; i < people.Length; i++)
            {
                var p = people[i];
                p.Idle(dt);
                float d = Vector3.Distance(p.transform.position, holmes.transform.position);
                p.lookTarget = d < 5f ? holmes.transform.Find("LookAt") ?? holmes.transform : null;
                var faceTo = d < 2.6f ? holmes.transform.position : Chapter1.People[i].face;
                p.FaceTowards(faceTo, dt * 0.6f);
            }
            holmes.lookPoint = null;
        }

        // ---- what Holmes could do here ------------------------------------------------------------
        void FindContext()
        {
            contextLabel = null; contextAct = null;
            var hp = holmes.transform.position;
            float best = float.MaxValue;
            bool focusing = Focus.I.Amount > 0.3f;

            foreach (var s in Chapter1.Spots)
            {
                if (s.clue != null && found.Contains(s.clue) && !s.closeup) continue;
                if (s.focus && !focusing) continue;
                var flat = s.pos - hp; flat.y = 0;
                float d = flat.magnitude;
                if (d > s.r || d >= best) continue;
                best = d;
                var spot = s;
                contextLabel = s.label;
                contextAct = () => UseSpot(spot);
            }
            for (int i = 0; i < people.Length; i++)
            {
                float d = Vector3.Distance(people[i].transform.position, hp);
                if (d > 2.2f || d >= best) continue;
                best = d; int k = i;
                contextLabel = "Talk to " + Chapter1.People[i].name;
                contextAct = () => { holmes.lookTarget = null; Talk(Chapter1.Hello[k]); };
            }
            var toW = watson ? watson.transform.position - hp : Vector3.zero; toW.y = 0;
            if (watson && toW.magnitude < 1.8f && contextLabel == null && holmes.Speed < 0.2f && Vector3.Angle(holmes.transform.forward, toW) < 50f)
            {
                contextLabel = "Talk to Dr. Watson";
                contextAct = () => Talk(Chapter1.WatsonHello);
            }
            // Holmes glances at what he could examine
            if (contextLabel != null && best < 3f)
                foreach (var s in Chapter1.Spots) if (s.label == contextLabel) holmes.lookPoint = s.pos;
        }

        void UseSpot(SpotDef s)
        {
            if (s.closeup) { EnterCloseup(); return; }
            if (s.say != null) { Narrate("Holmes", s.say); return; }
            if (s.clue != null) Collect(s.clue, true);
        }

        void Collect(string id, bool speak)
        {
            if (!found.Add(id)) return;
            var c = Chapter1.Find(id);
            Hud.I.ClueSlip(c);
            UpdateCounter();
            if (hiddenSpotIds != null)
                for (int i = 0; i < hiddenSpotIds.Length; i++)
                    if (hiddenSpotIds[i] == id && hiddenBySpot[i] && hiddenBySpot[i].TryGetComponent<HiddenDetail>(out var h)) h.found = true;
            if (found.Count == Chapter1.Clues.Length)
                Talk(new[] { ("Holmes", "That is everything the alley has to tell us, Watson."), ("Watson", "And what does it tell you?"), ("Holmes", "That Miles Archer walked up to someone he trusted, and they shot him. The rest is for the Mind Palace.") },
                    () => Hud.I.SetObjective(Chapter1.Title, "Every clue found. (The Mind Palace comes next.)"));
        }

        void UpdateCounter() => Hud.I.SetCounter($"Clues {found.Count} / {Chapter1.Clues.Length}");

        // ---- the close-up on Archer ---------------------------------------------------------------
        void EnterCloseup()
        {
            mode = Mode.Closeup;
            GameInput.I.allowMouseCapture = false;   // the clues are clicked
            cameraRig.Shot(Chapter1.Closeup.Cam, Chapter1.Closeup.Look);
            Hud.I.Letterbox(true);
            Hud.I.BackButton(true);
            if (pocketLamp) pocketLamp.enabled = true;
            foreach (var m in Chapter1.Closeup.Marks)
            {
                var mark = m;
                Hud.I.AddMark(mark.pos, mark.label, mark.focus, () => found.Contains(mark.clue), () =>
                {
                    if (mark.focus && Focus.I.Amount < 0.3f && !found.Contains(mark.clue)) return;
                    Collect(mark.clue, true);
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

        // ---- captions -----------------------------------------------------------------------------
        // Holmes thinking aloud: a caption that fades on its own and never stops you walking.
        void Narrate(string who, string line) => Hud.I.Narrate(who, line);

        // A conversation: the scene holds until you've read each line (E, Enter, click or tap).
        void Talk(IEnumerable<(string who, string line)> l, Action then = null)
        {
            foreach (var x in l) lines.Enqueue(x);
            afterLines = then;
            if (mode != Mode.Talk) { prev = mode; mode = Mode.Talk; Hud.I.Letterbox(true); NextLine(); }
        }
        Mode prev;
        void NextLine()
        {
            if (lines.Count == 0)
            {
                mode = prev;
                Hud.I.Letterbox(mode == Mode.Closeup);
                var a = afterLines; afterLines = null; a?.Invoke();
                return;
            }
            var (who, line) = lines.Dequeue();
            var speaker = who == "Watson" ? watson : null;
            for (int i = 0; i < Chapter1.People.Length; i++) if (Chapter1.People[i].name.Contains(who)) speaker = people[i];
            foreach (var w in AllWalkers()) w.SetTalking(w == speaker || (who == "Holmes" && w == holmes));
            Hud.I.Say(who, line, () => { foreach (var w in AllWalkers()) w.SetTalking(false); NextLine(); });
        }
        IEnumerable<Walker> AllWalkers() { yield return holmes; if (watson) yield return watson; foreach (var p in people) yield return p; }

        // ---- Focus reads ---------------------------------------------------------------------------
        readonly List<(Vector3, string)> readBuf = new List<(Vector3, string)>();
        void Reads()
        {
            readBuf.Clear();
            float k = Focus.I.Amount;
            if (k > 0.02f)
            {
                for (int i = 0; i < people.Length; i++) AddReads(people[i].transform, Chapter1.People[i].reads);
                if (watson) AddReads(watson.transform, Chapter1.WatsonReads);
            }
            Hud.I.ShowReads(readBuf, k);
        }
        void AddReads(Transform t, string[] r)
        {
            if (Vector3.Distance(t.position, holmes.transform.position) > 9f) return;
            for (int j = 0; j < r.Length; j++) readBuf.Add((t.position + Vector3.up * (1.95f - j * 0.22f) + Camera.main.transform.right * 0.35f, r[j]));
        }
    }
}
