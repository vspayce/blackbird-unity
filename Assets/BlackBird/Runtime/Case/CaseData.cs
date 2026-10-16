// A chapter's story, read from Resources/Cases/<id>.json: the web game's src/cases/<id>.js exported by
// Tools/cases/export_cases.mjs, so the two games tell one story. Positions stay in the web game's world coordinates;
// each world maps them (Web.W for the alley; the Hopkins house also through its enlargement).
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BlackBird
{
    public class Line
    {
        public string who, text, tell, focusTell;
        public PresentDef present;
        public Line(string who, string text) { this.who = who; this.text = text; }
    }

    public class PresentDef { public string id, gives; public string[] evidence; public List<Line> right, wrong; }

    public class ChallengeDef
    {
        public string tell, answer, gives; public bool focusTell;
        public string[] evidence = new string[0];
        public List<Line> right, wrong;
    }

    public class TopicDef
    {
        public string q, gives, @event, after, before;
        public string[] needs = new string[0];
        public List<Line> a;
        public ChallengeDef challenge;
    }

    public class TalkDef { public List<Line> hello; public List<TopicDef> topics; }

    public class FactDef
    {
        public string id, kind, who, title, text;
        public string[] from;                 // deductions: the two facts they come from
        public bool key;
        public bool IsDeduction => from != null;
    }

    public class PersonInfo
    {
        public string id, name, role, note, needs, model, leaves;
        public float[] pos, face;
    }

    public class SpotInfo
    {
        public string id, label, clue, say, after, before, early, worldId, @event, place;
        public string[] clues, needs;
        public float[] pos; public float r = 2f;
        public bool focus, closeup;
        public string[] AllClues() => clues != null && clues.Length > 0 ? clues : clue != null ? new[] { clue } : new string[0];
    }

    // the character portrait (Chapter II): markers on the person, then Holmes's reading completed line by line
    public class PortraitSpot { public float[] local; public string clue, label; }
    public class ProfileLine { public string lead, why; public string[] options; public int answer; }
    public class PortraitDef { public string @event, gives; public List<PortraitSpot> spots; public List<ProfileLine> lines; }

    // fight prediction: three moves in order; a wrong one breaks the plan, with the reason
    public class FightDef
    {
        public string who, title, prompt;
        public List<(string id, string label)> moves;
        public string[] plan;
        public Dictionary<string, string> why;
        public List<Line> success;
    }

    // a story event: cards (lines), or a reconstruction (fragments put back in order), then waking somewhere
    public class EventDef
    {
        public string type, title, prompt, gives;
        public List<string> before = new List<string>(), wrong = new List<string>(), fragments = new List<string>();
        public List<Line> after = new List<Line>(), lines = new List<Line>();
        public float[] wake;
    }

    // tailing (Chapter V): he shadows Holmes, then Holmes follows him along a path without being seen
    public class TailDef
    {
        public string who, after, until;
        public float follow, shadowX, speed, sight, close, lose, loseTime;
        public List<float[]> path = new List<float[]>();
        public Dictionary<int, float> pauses = new Dictionary<int, float>();
        public Dictionary<int, string> waitFor = new Dictionary<int, string>();
        public string[] spotted, lost;
    }

    public class CloseupInfo { public float[] cam, look; public List<SpotInfo> spots; }

    public class AnswerDef { public string a, reply; public bool right; }

    public class ConclusionDef { public string[] needs; public string question; public List<AnswerDef> answers; public List<Line> epilogue; }

    public class CaseData
    {
        public string id, number, title, next, world, opening, summary;
        public int order;
        public bool focusTalk;
        public object start;                                  // [x, z] or a named place ('spawn')
        public string[] startClues = new string[0];
        public List<object> intro = new List<object>();       // strings, or [who, line]
        public Dictionary<string, object> chapter, raw;       // everything, for chapter-specific mechanics
        public readonly Dictionary<string, FactDef> clues = new Dictionary<string, FactDef>();
        public readonly Dictionary<string, FactDef> deductions = new Dictionary<string, FactDef>();
        public readonly Dictionary<string, PersonInfo> people = new Dictionary<string, PersonInfo>();
        public readonly Dictionary<string, PersonInfo> absent = new Dictionary<string, PersonInfo>();
        public readonly Dictionary<string, string[]> reads = new Dictionary<string, string[]>();
        public readonly Dictionary<string, TalkDef> talk = new Dictionary<string, TalkDef>();
        public readonly List<SpotInfo> spots = new List<SpotInfo>();
        public CloseupInfo closeup;
        public ConclusionDef conclusion;
        public TailDef tail;
        public readonly Dictionary<string, PortraitDef> portraits = new Dictionary<string, PortraitDef>();
        public readonly Dictionary<string, FightDef> fights = new Dictionary<string, FightDef>();
        public readonly Dictionary<string, EventDef> events = new Dictionary<string, EventDef>();

        public static readonly string[] Order = { "archer", "cairo", "brigid", "gutman", "gunsel" };

        public static CaseData Load(string id)
        {
            var ta = Resources.Load<TextAsset>("Cases/" + id);
            if (!ta) { Debug.LogError("[Black Bird] No case data for " + id); return null; }
            return new CaseData((Dictionary<string, object>)Json.Parse(ta.text));
        }

        public FactDef Fact(string id) => clues.TryGetValue(id, out var c) ? c : deductions.TryGetValue(id, out var d) ? d : null;

        /// The deduction two facts make together, if any.
        public string Combine(string a, string b)
        {
            foreach (var d in deductions.Values)
                if ((d.from[0] == a && d.from[1] == b) || (d.from[0] == b && d.from[1] == a)) return d.id;
            return null;
        }

        static readonly string[] Wrong =
        {
            "No. Those two facts have nothing to say to one another.",
            "Data, Watson, but not a deduction.",
            "I am theorising without enough to go on. A capital mistake.",
            "Tempting, but it does not hold.",
        };
        public static string WrongLine() => Wrong[Random.Range(0, Wrong.Length)];

        // ---- reading the JSON ----
        CaseData(Dictionary<string, object> d)
        {
            raw = d;
            order = I(d, "order");
            chapter = D(d, "CHAPTER");
            id = S(chapter, "id"); number = S(chapter, "number"); title = S(chapter, "title"); next = S(chapter, "next");
            world = S(chapter, "world"); opening = S(chapter, "opening"); summary = S(chapter, "summary");
            focusTalk = B(chapter, "focusTalk");
            chapter.TryGetValue("start", out start);
            startClues = Strs(chapter, "startClues");
            if (chapter.TryGetValue("intro", out var intro) && intro is List<object> il) this.intro = il;

            foreach (var kv in D(d, "CLUES") ?? new Dictionary<string, object>())
            {
                var c = (Dictionary<string, object>)kv.Value;
                clues[kv.Key] = new FactDef { id = kv.Key, kind = S(c, "kind") ?? "clue", who = S(c, "who"), title = S(c, "title"), text = S(c, "text") };
            }
            foreach (var kv in D(d, "DEDUCTIONS") ?? new Dictionary<string, object>())
            {
                var c = (Dictionary<string, object>)kv.Value;
                deductions[kv.Key] = new FactDef { id = kv.Key, kind = "deduction", title = S(c, "title"), text = S(c, "text"), from = Strs(c, "from"), key = B(c, "key") };
            }
            foreach (var (src, dst) in new[] { ("PEOPLE", people), ("ABSENT", absent) })
                foreach (var kv in D(d, src) ?? new Dictionary<string, object>())
                {
                    var p = (Dictionary<string, object>)kv.Value;
                    var look = D(p, "look");
                    dst[kv.Key] = new PersonInfo { id = kv.Key, name = S(p, "name"), role = S(p, "role"), note = S(p, "note"), needs = S(p, "needs"),
                        model = look != null ? S(look, "model") : kv.Key, pos = Fs(p, "pos"), face = Fs(p, "face"), leaves = S(p, "leaves") };
                }
            foreach (var kv in D(d, "READS") ?? new Dictionary<string, object>()) reads[kv.Key] = ((List<object>)kv.Value).Select(o => (string)o).ToArray();
            if (d.TryGetValue("SPOTS", out var sp) && sp is List<object> spl) foreach (Dictionary<string, object> s in spl) spots.Add(Spot(s));
            var cu = D(d, "CLOSEUP");
            if (cu != null) closeup = new CloseupInfo { cam = Fs(cu, "cam"), look = Fs(cu, "look"), spots = ((List<object>)cu["spots"]).Select(o => Spot((Dictionary<string, object>)o)).ToList() };
            foreach (var kv in D(d, "TALK") ?? new Dictionary<string, object>())
            {
                var t = (Dictionary<string, object>)kv.Value;
                talk[kv.Key] = new TalkDef
                {
                    hello = Lines(t, "hello"),
                    topics = ((List<object>)t["topics"]).Select(o => Topic((Dictionary<string, object>)o)).ToList(),
                };
            }
            foreach (var kv in D(d, "PORTRAITS") ?? new Dictionary<string, object>())
            {
                var p = (Dictionary<string, object>)kv.Value;
                portraits[kv.Key] = new PortraitDef
                {
                    @event = S(p, "event"), gives = S(p, "gives"),
                    spots = ((List<object>)p["spots"]).Select(o => { var x = (Dictionary<string, object>)o; return new PortraitSpot { local = Fs(x, "local"), clue = S(x, "clue"), label = S(x, "label") }; }).ToList(),
                    lines = ((List<object>)p["lines"]).Select(o => { var x = (Dictionary<string, object>)o; return new ProfileLine { lead = S(x, "lead"), why = S(x, "why"), options = Strs(x, "options"), answer = I(x, "answer") }; }).ToList(),
                };
            }
            foreach (var kv in D(d, "FIGHTS") ?? new Dictionary<string, object>())
            {
                var f = (Dictionary<string, object>)kv.Value;
                fights[kv.Key] = new FightDef
                {
                    who = S(f, "who"), title = S(f, "title"), prompt = S(f, "prompt"), plan = Strs(f, "plan"), success = Lines(f, "success"),
                    moves = ((List<object>)f["moves"]).Select(o => { var x = (Dictionary<string, object>)o; return (S(x, "id"), S(x, "label")); }).ToList(),
                    why = (D(f, "why") ?? new Dictionary<string, object>()).ToDictionary(k => k.Key, k => k.Value as string),
                };
            }
            foreach (var kv in D(d, "EVENTS") ?? new Dictionary<string, object>())
            {
                var e = (Dictionary<string, object>)kv.Value;
                List<string> SL(string k) => e.TryGetValue(k, out var v) && v is List<object> l ? l.OfType<string>().ToList() : new List<string>();
                events[kv.Key] = new EventDef
                {
                    type = S(e, "type"), title = S(e, "title"), prompt = S(e, "prompt"), gives = S(e, "gives"), wake = Fs(e, "wake"),
                    before = SL("before"), wrong = SL("wrong"), fragments = SL("fragments"), after = Lines(e, "after"), lines = Lines(e, "lines"),
                };
            }
            var tl = D(d, "TAIL");
            if (tl != null)
            {
                float F(string k) => tl.TryGetValue(k, out var v) && v is double x ? (float)x : 0f;
                tail = new TailDef { who = S(tl, "who"), after = S(tl, "after"), until = S(tl, "until"), follow = F("follow"), shadowX = F("shadowX"),
                    speed = F("speed"), sight = F("sight"), close = F("close"), lose = F("lose"), loseTime = F("loseTime"),
                    spotted = Strs(tl, "spotted"), lost = Strs(tl, "lost") };
                foreach (List<object> pt in (List<object>)tl["path"]) tail.path.Add(pt.Select(o => (float)(double)o).ToArray());
                foreach (var kv in D(tl, "pauses") ?? new Dictionary<string, object>()) tail.pauses[int.Parse(kv.Key)] = (float)(double)kv.Value;
                foreach (var kv in D(tl, "waitFor") ?? new Dictionary<string, object>()) tail.waitFor[int.Parse(kv.Key)] = kv.Value as string;
            }
            var con = D(d, "CONCLUSION");
            if (con != null)
                conclusion = new ConclusionDef
                {
                    needs = Strs(con, "needs"), question = S(con, "question"), epilogue = Lines(con, "epilogue"),
                    answers = ((List<object>)con["answers"]).Select(o => { var a = (Dictionary<string, object>)o; return new AnswerDef { a = S(a, "a"), reply = S(a, "reply"), right = B(a, "right") }; }).ToList(),
                };
        }

        static SpotInfo Spot(Dictionary<string, object> s) => new SpotInfo
        {
            id = S(s, "id"), label = S(s, "label"), clue = S(s, "clue"), say = S(s, "say"), after = S(s, "after"), before = S(s, "before"),
            early = S(s, "early"), worldId = S(s, "worldId"), @event = S(s, "event"), pos = Fs(s, "pos"), place = S(s, "place"), clues = Strs(s, "clues"),
            needs = Strs(s, "needs"),
            r = s.TryGetValue("r", out var r) ? (float)(double)r : 2f, focus = B(s, "focus"), closeup = B(s, "closeup"),
        };

        static TopicDef Topic(Dictionary<string, object> t)
        {
            var c = D(t, "challenge");
            return new TopicDef
            {
                q = S(t, "q"), gives = S(t, "gives"), @event = S(t, "event"), after = S(t, "after"), before = S(t, "before"),
                needs = Strs(t, "needs"), a = Lines(t, "a"),
                challenge = c == null ? null : new ChallengeDef
                {
                    tell = S(c, "tell"), answer = S(c, "answer"), gives = S(c, "gives"), focusTell = B(c, "focusTell"),
                    evidence = Strs(c, "evidence"), right = Lines(c, "right"), wrong = Lines(c, "wrong"),
                },
            };
        }

        public static List<Line> Lines(Dictionary<string, object> d, string key)
        {
            var res = new List<Line>();
            if (d == null || !d.TryGetValue(key, out var v) || !(v is List<object> l)) return res;
            foreach (var o in l) res.Add(ToLine(o));
            return res;
        }

        public static Line ToLine(object o)
        {
            if (o is string s) return new Line(null, s);
            var a = (List<object>)o;
            var line = new Line((string)a[0], (string)a[1]);
            if (a.Count > 2 && a[2] is Dictionary<string, object> opts)
            {
                line.tell = S(opts, "tell");
                var p = D(opts, "present");
                if (p != null) line.present = new PresentDef { id = S(p, "id"), gives = S(p, "gives"), evidence = Strs(p, "evidence"), right = Lines(p, "right"), wrong = Lines(p, "wrong") };
            }
            return line;
        }

        // small readers
        public static Dictionary<string, object> D(Dictionary<string, object> d, string k) => d != null && d.TryGetValue(k, out var v) ? v as Dictionary<string, object> : null;
        public static string S(Dictionary<string, object> d, string k) => d != null && d.TryGetValue(k, out var v) ? v as string : null;
        public static bool B(Dictionary<string, object> d, string k) => d != null && d.TryGetValue(k, out var v) && v is bool b && b;
        public static int I(Dictionary<string, object> d, string k) => d != null && d.TryGetValue(k, out var v) && v is double x ? (int)x : 0;
        public static string[] Strs(Dictionary<string, object> d, string k) =>
            d != null && d.TryGetValue(k, out var v) && v is List<object> l ? l.Select(o => o as string).Where(s => s != null).ToArray() : new string[0];
        public static float[] Fs(Dictionary<string, object> d, string k) =>
            d != null && d.TryGetValue(k, out var v) && v is List<object> l ? l.Select(o => (float)(double)o).ToArray() : null;
    }
}
