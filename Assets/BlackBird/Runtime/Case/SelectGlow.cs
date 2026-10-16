using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BlackBird
{
    /// The selected button glows gold (so a controller always shows where it is).
    public class SelectGlow : MonoBehaviour, ISelectHandler, IDeselectHandler
    {
        Outline o;
        void Awake() { o = GetComponent<Outline>(); if (o) o.effectDistance = new Vector2(2, -2); }
        public void OnSelect(BaseEventData e) { if (o) o.effectColor = UIKit.Gold; }
        public void OnDeselect(BaseEventData e) { if (o) o.effectColor = new Color(0, 0, 0, 0); }
    }
}
