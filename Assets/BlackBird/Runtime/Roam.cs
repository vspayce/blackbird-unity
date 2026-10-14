// Walking freely round a place with Watson: the room you're in shows as the objective, things you can look at
// offer themselves, and some of them take you somewhere (the tower stair). The Hopkins house uses it.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace BlackBird
{
    [Serializable]
    public class RoomDef
    {
        public string id, name;
        public Bounds box;          // Unity space
        public bool upper;          // on the upper floor
    }

    [Serializable]
    public class PlaceDef
    {
        public string id, label, line, to;
        public Vector3 pos;
        public float r;
    }

    [Serializable]
    public class Destination
    {
        public string id;
        public Vector3 pos;
        public float yaw;
    }

    public class Roam : MonoBehaviour
    {
        public string title = "The Mark Hopkins Institute of Art";
        public Explorer ex;
        public RoomDef[] rooms = new RoomDef[0];
        public PlaceDef[] places = new PlaceDef[0];
        public Destination[] destinations = new Destination[0];
        public Destination spawn;
        public Bounds hall;                 // the great hall, rising through both floors
        public float streetZ;               // Unity z of the terrace wall; beyond it (toward the street) is California Street
        public bool streetIsPositive;
        public string opening = "The Hopkins house, Watson. Forty rooms, and a tower that sees the whole city.";

        string room;
        string contextLabel; Action contextAct;

        void Start()
        {
            ex.Setup();
            if (spawn != null) ex.Place(spawn.pos, spawn.yaw);
            Hud.I.Narrate("Holmes", opening);
            UpdateRoom(true);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            var input = GameInput.I;
            Hud.I.SetTouchMode(input.UsingTouch);
            if (input.Focus && Focus.I) Focus.I.Toggle();
            ex.Steer();
            ex.Follow(dt);
            UpdateRoom(false);
            FindContext();
            Hud.I.Context(contextLabel, input.UsingTouch);
            if (input.Act && contextAct != null) contextAct();
        }

        void UpdateRoom(bool force)
        {
            var r = RoomAt(ex.holmes.transform.position);
            if (r == room && !force) return;
            room = r;
            Hud.I.SetObjective(title, r);
        }

        public string RoomAt(Vector3 p)
        {
            if (p.y > 15f) return "The Tower Observatory";
            if (streetIsPositive ? p.z > streetZ : p.z < streetZ) return "California Street";
            bool up = p.y > 4f;
            var flat = new Vector3(p.x, 0, p.z);
            bool inHall = hall.size != Vector3.zero && Contains2D(hall, flat);
            if (up && inHall) return "The Gallery";
            foreach (var rm in rooms)
                if (rm.upper == up && Contains2D(rm.box, flat)) return rm.name;
            return up ? "The Grand Stair" : "The Grounds";
        }

        static bool Contains2D(Bounds b, Vector3 p) => p.x > b.min.x && p.x < b.max.x && p.z > b.min.z && p.z < b.max.z;

        void FindContext()
        {
            contextLabel = null; contextAct = null;
            var hp = ex.holmes.transform.position;
            float best = float.MaxValue;
            foreach (var pl in places)
            {
                var d3 = pl.pos - hp;
                if (Mathf.Abs(d3.y) > 2.2f) continue;
                d3.y = 0;
                float d = d3.magnitude;
                if (d > pl.r || d >= best) continue;
                best = d;
                var place = pl;
                contextLabel = pl.label;
                contextAct = () => Use(place);
            }
            if (contextLabel != null)
                foreach (var pl in places) if (pl.label == contextLabel) ex.holmes.lookPoint = pl.pos;
            if (contextLabel == null) ex.holmes.lookPoint = null;
        }

        void Use(PlaceDef pl)
        {
            if (!string.IsNullOrEmpty(pl.to))
            {
                foreach (var d in destinations)
                    if (d.id == pl.to) { ex.Place(d.pos, d.yaw); if (!string.IsNullOrEmpty(pl.line)) Hud.I.Narrate("Holmes", pl.line); return; }
            }
            if (!string.IsNullOrEmpty(pl.line)) Hud.I.Narrate("Holmes", pl.line);
        }
    }
}
