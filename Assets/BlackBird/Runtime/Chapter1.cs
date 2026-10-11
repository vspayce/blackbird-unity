// Chapter I, Burritt Alley: the slice of src/cases/archer.js and src/world/alley.js this test needs.
// Positions are kept in the web game's coordinates (three.js: Y up, the alley along -Z) and mapped
// with W(), so the numbers can be checked against the prototype line for line.
using UnityEngine;

namespace BlackBird
{
    public static class Web
    {
        // The FBX path mirrors X relative to three.js (Unity is left-handed). If the set ever comes in
        // the other way round, flip this, not the data.
        public static float SX = -1f;
        public static float SZ = 1f;
        public static Vector3 W(float x, float y, float z) => new Vector3(SX * x, y, SZ * z);
        public static Vector3 W(float x, float z) => W(x, 0, z);
        public static Bounds Box(float x0, float y0, float z0, float x1, float y1, float z1)
        {
            var a = W(x0, y0, z0); var b = W(x1, y1, z1);
            var b0 = new Bounds(a, Vector3.zero); b0.Encapsulate(b); return b0;
        }
    }

    public enum ClueKind { Clue, Testimony, Memory }

    public class Clue
    {
        public string id, title, text; public ClueKind kind;
        public Clue(string id, string title, string text, ClueKind kind = ClueKind.Clue) { this.id = id; this.title = title; this.text = text; this.kind = kind; }
    }

    public class SpotDef
    {
        public string id, label, clue, say; public Vector3 pos; public float r; public bool focus, closeup;
    }

    public class PersonDef
    {
        public string id, name, model; public Vector3 pos, face; public string[] reads;
    }

    public static class Chapter1
    {
        public const string Title = "Chapter I · Burritt Alley";
        public const string Opening = "Half past two, and Polhaus already here. Let us see what the fog has left us, Watson.";
        public static readonly Vector3 Start = Web.W(0, 14.5f);
        public const float FenceZ = -22f;

        public static readonly Clue[] Clues =
        {
            new Clue("coat", "Coat buttoned", "Archer's overcoat is buttoned to the collar. A man who expects trouble keeps his coat open."),
            new Clue("gun", "Revolver never drawn", "His revolver is still on his hip, under the buttoned coat. All six chambers loaded."),
            new Clue("wound", "One shot, from the front", "A single wound, high in the chest. He was facing the man who shot him. Or the woman."),
            new Clue("burns", "Powder burns", "The cloth around the wound is scorched. The muzzle was not two feet from him."),
            new Clue("card", "A card in his pocket", "Miss Wonderly's card, St. Mark Hotel. On the back, in pencil: \"Burritt St. 2 o'clock\". The hand is not Archer's."),
            new Clue("fence", "Broken fence", "Three boards snapped outward at the dead end. Archer went back against them when he fell. His back was to the fence and the drop."),
            new Clue("heel", "Narrow heel prints", "In the damp between the cobbles: a narrow, pointed heel. A lady's boot, small, and standing an arm's length from where Archer fell."),
            new Clue("scent", "Lavender water", "Under the alley lamp the fog still holds it. Lavender water. Someone stood here and waited."),
            new Clue("webley", "An English revolver", "In the weeds past the fence: a Webley, an English army revolver, one chamber fired. Rare in San Francisco. Thrown, not dropped. It was meant to be found."),
        };

        public static Clue Find(string id) { foreach (var c in Clues) if (c.id == id) return c; return null; }

        public static readonly SpotDef[] Spots =
        {
            new SpotDef { id = "body", label = "Examine Miles Archer", pos = Web.W(0.5f, 0, -19.9f), r = 2.3f, closeup = true },
            new SpotDef { id = "fence", label = "Examine the broken fence", pos = Web.W(0.8f, 1, -21.6f), r = 1.8f, clue = "fence" },
            new SpotDef { id = "heel", label = "Marks in the damp", pos = Web.W(0.3f, 0.05f, -16.3f), r = 2.6f, clue = "heel", focus = true },
            new SpotDef { id = "scent", label = "Something in the air", pos = Web.W(-1.9f, 1.5f, -8), r = 2.8f, clue = "scent", focus = true },
            new SpotDef { id = "webley", label = "A glint past the fence", pos = Web.W(0.95f, -0.12f, -23.3f), r = 2.6f, clue = "webley", focus = true },
            new SpotDef { id = "hat", label = "Archer's hat", pos = Web.W(-0.5f, 0.1f, -18.2f), r = 1.5f, say = "His hat, two yards from the body. Knocked off as he went over backwards. No struggle." },
            new SpotDef { id = "billboard", label = "The billboard", pos = Web.W(2.2f, 1.6f, -19.5f), r = 1.6f, say = "\"Cures every ailment known to science.\" Except, it would seem, a bullet." },
        };

        public static readonly PersonDef[] People =
        {
            new PersonDef { id = "polhaus", name = "Sgt. Polhaus", model = "polhaus", pos = Web.W(-1.5f, -16.4f), face = Web.W(0.5f, -19.5f),
                reads = new[] { "Cigar ash on lapel, gravy on cuff: called from his supper", "Hill clay on boots: lives on Telegraph Hill", "Doesn't believe it was a robbery" } },
            new PersonDef { id = "kelly", name = "Patrolman Kelly", model = "kelly", pos = Web.W(1.6f, 6.2f), face = Web.W(0, 12),
                reads = new[] { "Lantern wick fresh-trimmed", "Wet to the knees: ran here", "Nervous of his sergeant" } },
        };

        public static readonly (string who, string line)[][] Hello =
        {
            new[] { ("Polhaus", "Mr. Holmes. Didn't figure you for an early riser."), ("Holmes", "Nor I you, Sergeant. Tell me what you have."), ("Polhaus", "Kelly found him at ten past two. One shot. Nobody heard it over the foghorns.") },
            new[] { ("Kelly", "Terrible thing, sir. Terrible. I was only round the corner.") },
        };
        public static readonly (string who, string line)[] WatsonHello = { ("Watson", "Poor Archer. Only yesterday afternoon he was boasting of what an easy job Miss Wonderly had handed him.") };

        public static readonly string[] WatsonReads = { "Hasn't slept", "Service revolver, right coat pocket", "Grieving: liked Archer" };

        public static class Closeup
        {
            public static readonly Vector3 Cam = Web.W(1.35f, 1.2f, -19.45f);
            public static readonly Vector3 Look = Web.W(0.45f, 0.25f, -20.2f);
            public static readonly (Vector3 pos, string clue, string label, bool focus)[] Marks =
            {
                (Web.W(0.52f, 0.37f, -20.18f), "coat", "Coat", false),
                (Web.W(0.8f, 0.26f, -19.85f), "gun", "Hip", false),
                (Web.W(0.4f, 0.38f, -20.45f), "wound", "Chest", false),
                (Web.W(0.62f, 0.41f, -20.38f), "burns", "Scorching", true),
                (Web.W(0.27f, 0.3f, -19.8f), "card", "Pocket", false),
            };
        }

        // The walkable volume, from alley.js: the buildings either side of the alley and along Bush
        // Street, the ends of the street, lamp posts, crates, barrels, the fence and the body.
        public static Bounds[] Colliders()
        {
            var list = new System.Collections.Generic.List<Bounds>();
            void Box(float w, float h, float d, float x, float y, float z) => list.Add(Web.Box(x - w / 2, y - h / 2, z - d / 2, x + w / 2, y + h / 2, z + d / 2));
            Box(11.5f, 12, 30, -8.25f, 6, -7);
            Box(11.5f, 9, 30, 8.25f, 4.5f, -7);
            Box(14, 10, 14, -21, 5, 1);
            Box(14, 11, 14, 21, 5.5f, 1);
            foreach (var (x, h) in new[] { (-24f, 9f), (-12f, 13f), (0f, 10f), (12f, 12f), (24f, 8f) }) Box(12, h, 8, x, h / 2, 26);
            list.Add(Web.Box(-34, 0, -8, -28.5f, 10, 30));
            list.Add(Web.Box(28.5f, 0, -8, 34, 10, 30));
            foreach (var (x, z, r) in new[] { (13f, 9.6f, .2f), (-19f, 9.6f, .2f), (7f, 20.6f, .2f), (-13f, 20.6f, .2f), (24f, 20.6f, .2f), (-10f, 9.5f, .15f), (10f, 9.5f, .15f), (-3.6f, 9.6f, .12f) })
                list.Add(Web.Box(x - r, 0, z - r, x + r, 4, z + r));
            Box(0.9f, 0.9f, 0.9f, -1.9f, 0.45f, -3.5f);
            Box(0.8f, 0.8f, 0.8f, 1.95f, 0.4f, -11.5f);
            foreach (var (x, z) in new[] { (2.0f, -6.2f), (1.95f, -7.1f), (-2.0f, -13.0f) }) list.Add(Web.Box(x - .32f, 0, z - .32f, x + .32f, .95f, z + .32f));
            list.Add(Web.Box(-2.5f, 0, FenceZ - 0.3f, 2.5f, 2, FenceZ + 0.25f));
            list.Add(Web.Box(0.15f, 0, -20.9f, 0.85f, 0.5f, -18.9f));
            return list.ToArray();
        }
    }
}
