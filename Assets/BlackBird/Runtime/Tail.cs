// Tailing, both ways (Chapter V), from the web game's game/tail.js and the case's TAIL:
//   first the boy shadows Holmes up the far sidewalk, keeping `follow` metres behind and studying a shop window when
//   Holmes turns back; after the `after` event (the hatter's doorway) Holmes follows him along `path`.
//   At the pauses he stops and looks back: be in cover (a doorway, a lamp post, a cab, the cable car between you)
//   or out of `sight`. Closer than `close` he sees you anyway; farther than `lose` for `loseTime` and he is gone.
//   Either way it starts again from the last pause he reached, with Holmes a way behind. At the end of the path the
//   `until` event runs.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BlackBird
{
    [Serializable] public class CoverPoint { public Vector3 pos; public string kind; }

    public class Tail : MonoBehaviour
    {
        public CoverPoint[] cover = new CoverPoint[0];

        Chapter ch; TailDef d; Walker who, holmes, watson;
        int i, checkpoint; float pause, pauseLen, lostT, suspicion; bool started;
        public float Suspicion => suspicion;
        public string Phase { get; private set; } = "shadow";

        // the strip at the top of the screen while you follow him
        Canvas canvas; Text label; Image bar, barBack;

        public void Bind(Chapter chapter, TailDef def, Walker target, Walker h, Walker w)
        {
            ch = chapter; d = def; who = target; holmes = h; watson = w;
        }

        Vector3 Pt(int k) => Web.W(d.path[k][0], 0, d.path[k][1]);

        string CurrentPhase(CaseState s) => s.events.Contains(d.until) ? "done" : s.events.Contains(d.after) ? "lead" : "shadow";

        void Reset(int k)
        {
            i = k; checkpoint = k; pause = 0; lostT = 0; suspicion = 0;
            Explorer.Teleport(who, Pt(k), who.transform.eulerAngles.y);
        }

        void PlaceHolmesBehind()
        {
            // walk back along the path from the checkpoint, about 16 m
            int k = checkpoint; var p = Pt(k); float left = 16f;
            while (k > 0 && left > 0)
            {
                var q = Pt(k - 1); float seg = Vector3.Distance(p, q);
                if (seg >= left) { p += (q - p) * (left / seg); left = 0; } else { p = q; left -= seg; k--; }
            }
            if (left > 0) p += (Pt(0) - Pt(Mathf.Min(1, d.path.Count - 1))).normalized * left;   // before the start: further back
            var fwd = Pt(Mathf.Min(checkpoint + 1, d.path.Count - 1)) - p; fwd.y = 0;
            float yaw = fwd.sqrMagnitude > 0.01f ? Quaternion.LookRotation(fwd).eulerAngles.y : 0;
            ch.PlaceHolmes(p, yaw);
        }

        void Fail(string[] lines)
        {
            if (lines != null && lines.Length > 0) Hud.I.Narrate("Holmes", lines[UnityEngine.Random.Range(0, lines.Length)]);
            Sound.Wrong();
            Reset(checkpoint);
            PlaceHolmesBehind();
        }

        bool InCover(Vector3 at, Vector3 watcher)
        {
            foreach (var c in cover)
            {
                var f = c.pos - at; f.y = 0;
                if (f.magnitude < (c.kind == "doorway" ? 1.5f : 1.1f)) return true;
            }
            return CableCar.I && CableCar.I.Between(watcher, at);
        }

        /// Called by the chapter each frame while exploring.
        public void Tick(float dt, CaseState s)
        {
            if (d == null || !who) return;
            Phase = CurrentPhase(s);
            if (Phase == "done") { Show(null, false); return; }
            var o = who.transform; var h = holmes.transform.position;
            var flat = o.position - h; flat.y = 0;
            float dist = flat.magnitude;

            if (Phase == "shadow")
            {
                Show(null, false);
                // keep `follow` metres behind Holmes on the far sidewalk; stop and study a window if he comes back
                var hw = Web.Unmap(h);
                float tz = Mathf.Max(hw.z + d.follow, Mathf.Min(Web.Unmap(o.position).z, hw.z + 30));
                var target = Web.W(d.shadowX, 0, tz);
                var to = target - o.position; to.y = 0;
                if (dist > 11 && to.magnitude > 1.2f) who.Drive(to.normalized * Mathf.Min(1.6f, to.magnitude), dt);
                else
                {
                    who.Idle(dt);
                    if (dist <= 11) { float wx = Web.Unmap(o.position).x; who.FaceTowards(o.position + Web.W(wx >= 0 ? 1 : -1, 0, 0), dt); }   // a shop window on his side
                }
                return;
            }

            // --- Holmes follows him ---
            if (!started) { started = true; Reset(0); }
            bool looking = false;
            if (pause > 0)
            {
                pause -= dt;
                looking = pauseLen - pause > 0.7f;        // he takes a moment to turn: time to step behind something
                who.Idle(dt);
                who.FaceTowards(h, dt * 2f);
                who.lookTarget = holmes.transform;
            }
            else if (i < d.path.Count - 1)
            {
                who.lookTarget = null;
                bool wait = d.waitFor.TryGetValue(i, out var w) && w == "cablecar" && CableCar.I && CableCar.I.Visible && Mathf.Abs(CableCar.I.WebX) < 14;
                if (wait) who.Idle(dt);
                else
                {
                    var next = Pt(i + 1); var to = next - o.position; to.y = 0;
                    if (to.magnitude < 0.35f)
                    {
                        i++;
                        if (d.pauses.TryGetValue(i, out var pl)) { pause = pauseLen = pl; checkpoint = i; }
                        if (i == d.path.Count - 1) { Show(null, false); who.Idle(dt); ch.RunEvent(d.until); return; }
                    }
                    who.Drive(to.normalized * d.speed, dt);
                }
            }
            else who.Idle(dt);

            // being seen
            bool covered = InCover(h, o.position);
            bool seen = dist < d.close || (looking && dist < d.sight && !covered);
            suspicion = seen ? Mathf.Min(1, suspicion + dt * (dist < d.close ? 1.5f : 0.9f)) : Mathf.Max(0, suspicion - dt * 0.25f);
            if (suspicion >= 1) { Fail(d.spotted); return; }
            lostT = dist > d.lose ? lostT + dt : 0;
            if (lostT > d.loseTime) { Fail(d.lost); return; }

            string st = looking ? (covered ? "He looks back. You are hidden" : "He is looking back!") : dist > d.lose * 0.8f ? "Losing him in the fog" : "Following";
            Show(st, looking && !covered || dist > d.lose * 0.8f);
        }

        void Show(string text, bool warn)
        {
            if (text == null) { if (canvas) canvas.enabled = false; return; }
            if (!canvas) Build();
            canvas.enabled = true;
            label.text = text;
            label.color = warn ? new Color(1f, 0.55f, 0.45f) : UIKit.Ink;
            bar.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(suspicion), 1);
            bar.color = Color.Lerp(UIKit.Gold, new Color(0.85f, 0.25f, 0.2f), suspicion);
        }

        void Build()
        {
            if (!UIKit.serif) UIKit.serif = Hud.I && Hud.I.serif ? Hud.I.serif : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var go = new GameObject("TailStrip", typeof(Canvas), typeof(CanvasScaler));
            go.transform.SetParent(transform, false);
            canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 15;
            var sc = go.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1280, 720); sc.matchWidthOrHeight = 1f;
            var safe = UIKit.Rect("Safe", go.transform); safe.gameObject.AddComponent<SafeArea>();
            var panel = UIKit.Panel(safe, "Strip", new Color(0.03f, 0.04f, 0.05f, 0.7f));
            UIKit.Place(panel, new Vector2(0.5f, 1f), new Vector2(0, -62), new Vector2(360, 56));
            label = UIKit.Label(panel, "State", 22, TextAnchor.UpperCenter, UIKit.Ink); UIKit.Stretch(label.rectTransform, 10, 6, 10, 20);
            barBack = UIKit.Panel(panel, "Back", new Color(1, 1, 1, 0.12f)).GetComponent<Image>();
            var br = barBack.rectTransform; br.anchorMin = new Vector2(0, 0); br.anchorMax = new Vector2(1, 0); br.pivot = new Vector2(0.5f, 0);
            br.offsetMin = new Vector2(14, 8); br.offsetMax = new Vector2(-14, 14);
            bar = UIKit.Panel(barBack.transform, "Fill", UIKit.Gold).GetComponent<Image>();
            var fr = bar.rectTransform; fr.anchorMin = Vector2.zero; fr.anchorMax = new Vector2(0, 1); fr.offsetMin = fr.offsetMax = Vector2.zero;
        }
    }
}
