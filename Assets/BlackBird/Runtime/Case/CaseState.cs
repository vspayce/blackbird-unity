// Where an investigation stands: what Holmes has found, heard and worked out, how he read people, and the story
// events so far. A port of the web game's src/game/state.js, with the same L.A. Noire style rating. Saved per chapter.
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace BlackBird
{
    public class CaseState
    {
        public readonly CaseData data;
        public List<string> clues = new List<string>(), deductions = new List<string>(), talked = new List<string>(),
            reads = new List<string>(), events = new List<string>();
        public Dictionary<string, bool> calls = new Dictionary<string, bool>();   // a challenge's first call: right or wrong
        public bool solved;
        public int misses, wrongAccusations;

        string Key => "blackbird." + data.id + ".v1";

        public CaseState(CaseData d) { data = d; Reset(); }

        public void Reset()
        {
            clues.Clear(); deductions.Clear(); talked.Clear(); reads.Clear(); events.Clear(); calls.Clear();
            solved = false; misses = wrongAccusations = 0;
            foreach (var c in data.startClues) if (!clues.Contains(c)) clues.Add(c);
        }

        public bool Has(string id) => clues.Contains(id) || deductions.Contains(id);
        public bool AddClue(string id) { if (clues.Contains(id) || !data.clues.ContainsKey(id)) return false; clues.Add(id); Save(); return true; }
        public bool AddDeduction(string id) { if (deductions.Contains(id)) return false; deductions.Add(id); Save(); return true; }
        public void AddEvent(string e) { if (!events.Contains(e)) { events.Add(e); Save(); } }
        public void AddRead(string k) { if (!reads.Contains(k)) { reads.Add(k); Save(); } }
        public void Call(string key, bool right) { if (!calls.ContainsKey(key)) { calls[key] = right; Save(); } }
        public bool CanConclude => data.conclusion != null && data.conclusion.needs.All(d => deductions.Contains(d));

        // ---- the rating ----
        int Challenges()
        {
            int n = 0;
            foreach (var t in data.talk.Values)
                foreach (var tp in t.topics)
                {
                    if (tp.challenge != null) n++;
                    n += tp.a.Count(l => l.present != null);
                }
            return Mathf.Max(1, n);
        }

        public (int stars, List<(string, string)> rows) Rating()
        {
            int nc = data.clues.Count, nd = Mathf.Max(1, data.deductions.Count), ch = Challenges();
            int right = calls.Values.Count(b => b);
            float score = clues.Count / (float)nc * 2 + deductions.Count / (float)nd * 1.5f + right / (float)ch * 1.5f
                          - Mathf.Min(1, misses * 0.15f) - wrongAccusations * 0.75f;
            return (Mathf.Clamp(Mathf.RoundToInt(score), 1, 5), new List<(string, string)>
            {
                ("Clues found", $"{clues.Count} / {nc}"),
                ("Deductions drawn", $"{deductions.Count} / {data.deductions.Count}"),
                ("Interrogations read", $"{right} / {ch}"),
                ("False starts in the Mind Palace", misses.ToString()),
                ("Wrong accusations", wrongAccusations.ToString()),
            });
        }

        // ---- what to do next ----
        public string Objective()
        {
            if (solved) return "Chapter complete";
            var unmet = data.people.Keys.Where(p => p != "watson" && data.talk.ContainsKey(p) && !talked.Contains(p)).ToList();
            if (unmet.Count > 0 && talked.Count == 0) return "Speak with " + data.people[unmet[0]].name;
            // spots that still hold something, and are open now (some only after a story event, some only before)
            bool Live(SpotInfo s) => (s.after == null || events.Contains(s.after)) && (s.before == null || !events.Contains(s.before)) && s.AllClues().Any(c => !Has(c));
            int open = data.spots.Count(Live) + (data.closeup?.spots.Count(Live) ?? 0);
            bool hidden = data.spots.Any(s => s.focus && Live(s));
            var fresh = data.spots.FirstOrDefault(s => s.after != null && Live(s));
            if (fresh != null && !CanConclude) return fresh.label ?? "Look closer";
            if (CanConclude) return "Open the Mind Palace and " + (data.conclusion.question.StartsWith("Who") ? "name the killer" : "answer the question");
            if (hidden && open > 0) return "Search the scene. Use Focus to see what others miss";
            if (open > 0) return "Examine the scene";
            if (unmet.Count > 0) return "Speak with " + data.people[unmet[0]].name;
            if (deductions.Count == 0) return "Combine what you know in the Mind Palace";
            int missing = data.conclusion.needs.Count(d => !deductions.Contains(d));
            return $"Mind Palace: {missing} key deduction{(missing > 1 ? "s" : "")} still to make";
        }

        // ---- saving (a simple line-based format; PlayerPrefs on every platform, IndexedDB on the web) ----
        public void Save()
        {
            var sb = new StringBuilder();
            sb.Append("clues=").Append(string.Join(",", clues)).Append('\n');
            sb.Append("deductions=").Append(string.Join(",", deductions)).Append('\n');
            sb.Append("talked=").Append(string.Join(",", talked)).Append('\n');
            sb.Append("reads=").Append(string.Join(",", reads)).Append('\n');
            sb.Append("events=").Append(string.Join(",", events)).Append('\n');
            sb.Append("calls=").Append(string.Join(",", calls.Select(kv => kv.Key + ":" + (kv.Value ? 1 : 0)))).Append('\n');
            sb.Append("solved=").Append(solved ? 1 : 0).Append('\n');
            sb.Append("misses=").Append(misses).Append('\n');
            sb.Append("wrong=").Append(wrongAccusations).Append('\n');
            PlayerPrefs.SetString(Key, sb.ToString());
            PlayerPrefs.Save();
        }

        public bool Load()
        {
            var s = PlayerPrefs.GetString(Key, "");
            if (string.IsNullOrEmpty(s)) return false;
            Reset();
            foreach (var line in s.Split('\n'))
            {
                int i = line.IndexOf('='); if (i < 0) continue;
                string k = line.Substring(0, i), v = line.Substring(i + 1);
                List<string> L() => v.Split(',').Where(x => x.Length > 0).ToList();
                switch (k)
                {
                    case "clues": clues = L().Where(c => data.clues.ContainsKey(c)).ToList(); break;
                    case "deductions": deductions = L().Where(c => data.deductions.ContainsKey(c)).ToList(); break;
                    case "talked": talked = L(); break;
                    case "reads": reads = L(); break;
                    case "events": events = L(); break;
                    case "calls": foreach (var c in L()) { int j = c.LastIndexOf(':'); if (j > 0) calls[c.Substring(0, j)] = c.EndsWith("1"); } break;
                    case "solved": solved = v == "1"; break;
                    case "misses": int.TryParse(v, out misses); break;
                    case "wrong": int.TryParse(v, out wrongAccusations); break;
                }
            }
            foreach (var c in data.startClues) if (!clues.Contains(c)) clues.Insert(0, c);
            return true;
        }

        public static bool HasSave(string id) => PlayerPrefs.HasKey("blackbird." + id + ".v1");
        public void Erase() { PlayerPrefs.DeleteKey(Key); Reset(); }
    }
}
