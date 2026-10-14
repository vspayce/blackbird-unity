// One place for every input: keyboard and mouse, gamepads (Xbox, PlayStation, MFi) and touch.
// Touch follows the web prototype: a fixed stick bottom-left, drag anywhere else to look,
// on-screen buttons for everything else (they are UI and never start a look drag).
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace BlackBird
{
    public class GameInput : MonoBehaviour
    {
        public static GameInput I { get; private set; }

        public Vector2 Move { get; private set; }      // -1..1, magnitude is how hard you push
        public Vector2 Look { get; private set; }      // degrees this frame
        public bool Act { get; private set; }
        public bool Focus { get; private set; }
        public bool Back { get; private set; }
        public bool UsingTouch { get; private set; }
        public float Turn { get; private set; }        // keyboard turning, -1..1 (not counted as "looking")
        public bool Hurry { get; private set; }         // Shift, or the left stick pushed all the way with the stick clicked
        public bool LookedThisFrame { get; private set; }   // any deliberate camera input (for the auto-follow camera)
        public bool MouseCaptured => Cursor.lockState == CursorLockMode.Locked;
        /// Free the mouse (close-ups, menus) or let a click capture it again.
        public bool allowMouseCapture = true;

        // the touch stick, in screen pixels (the HUD draws it)
        public bool StickActive { get; private set; }
        public Vector2 StickCenter { get; private set; }
        public Vector2 StickKnob { get; private set; }
        public float StickRadius => Screen.dpi > 0 ? Screen.dpi * 0.45f : 80f;

        InputAction move, turn, look, act, focus, back, hurry;
        int stickFinger = -1;
        readonly HashSet<int> lookFingers = new HashSet<int>();
        readonly HashSet<int> uiFingers = new HashSet<int>();
        static readonly List<RaycastResult> hits = new List<RaycastResult>();

        public float mouseSensitivity = 0.12f, stickLookSpeed = 140f, touchLookSensitivity = 0.22f, turnSpeed = 110f;

        void Awake()
        {
            I = this;
            move = new InputAction("move", InputActionType.Value);
            // keyboard: W/S (or up/down) walk forward and back; A/D (or left/right) turn, camera and all
            move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s");
            move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow");
            turn = new InputAction("turn", InputActionType.Value);
            turn.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/a").With("Positive", "<Keyboard>/d");
            turn.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/leftArrow").With("Positive", "<Keyboard>/rightArrow");
            hurry = new InputAction("hurry", InputActionType.Button);
            hurry.AddBinding("<Keyboard>/leftShift"); hurry.AddBinding("<Keyboard>/rightShift"); hurry.AddBinding("<Gamepad>/leftStickPress");
            move.AddBinding("<Gamepad>/leftStick");
            look = new InputAction("look", InputActionType.Value, "<Gamepad>/rightStick");
            act = new InputAction("act", InputActionType.Button);
            act.AddBinding("<Keyboard>/e"); act.AddBinding("<Keyboard>/enter"); act.AddBinding("<Gamepad>/buttonSouth");
            focus = new InputAction("focus", InputActionType.Button);
            focus.AddBinding("<Keyboard>/f"); focus.AddBinding("<Gamepad>/buttonWest");
            back = new InputAction("back", InputActionType.Button);
            back.AddBinding("<Keyboard>/escape"); back.AddBinding("<Keyboard>/backspace"); back.AddBinding("<Gamepad>/buttonEast");
            foreach (var a in new[] { move, turn, look, act, focus, back, hurry }) a.Enable();
            EnhancedTouchSupport.Enable();
        }

        void OnDestroy()
        {
            foreach (var a in new[] { move, turn, look, act, focus, back, hurry }) a?.Dispose();
            Cursor.lockState = CursorLockMode.None;
            if (EnhancedTouchSupport.enabled) EnhancedTouchSupport.Disable();
        }

        // buttons on the HUD call these
        bool uiAct, uiFocus, uiBack;
        public void PressAct() => uiAct = true;
        public void PressFocus() => uiFocus = true;
        public void PressBack() => uiBack = true;

        void Update()
        {
            var m = move.ReadValue<Vector2>();
            var l = look.ReadValue<Vector2>() * stickLookSpeed * Time.unscaledDeltaTime;

            Turn = turn.ReadValue<float>();
            l.x += Turn * turnSpeed * Time.unscaledDeltaTime;

            // Mouse: click the game to capture the mouse, then it looks around without dragging (Esc frees
            // it; browsers do that themselves). Dragging still works while it isn't captured.
            var mouse = Mouse.current;
            if (!allowMouseCapture && MouseCaptured) Cursor.lockState = CursorLockMode.None;
            if (mouse != null && Touch.activeTouches.Count == 0)
            {
                if (MouseCaptured) { l += mouse.delta.ReadValue() * mouseSensitivity; UsingTouch = false; }
                else if (mouse.leftButton.isPressed && !OverUI(mouse.position.ReadValue(), -1))
                {
                    if (mouse.leftButton.wasPressedThisFrame)
                    {
                        if (allowMouseCapture) Cursor.lockState = CursorLockMode.Locked;
                    }
                    else l += mouse.delta.ReadValue() * mouseSensitivity;
                    UsingTouch = false;
                }
            }
            if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) UsingTouch = false;

            // touch
            StickActive = false;
            foreach (var t in Touch.activeTouches)
            {
                UsingTouch = true;
                int id = t.touchId;
                if (t.phase == UnityEngine.InputSystem.TouchPhase.Began)
                {
                    if (OverUI(t.screenPosition, id)) uiFingers.Add(id);
                    else if (stickFinger < 0 && t.screenPosition.x < Screen.width * 0.4f && t.screenPosition.y < Screen.height * 0.6f) stickFinger = id;
                    else lookFingers.Add(id);
                }
                if (id == stickFinger)
                {
                    StickActive = true;
                    StickCenter = DefaultStickCenter();
                    var d = t.screenPosition - StickCenter;
                    var v = Vector2.ClampMagnitude(d / StickRadius, 1f);
                    StickKnob = StickCenter + v * StickRadius;
                    m = v.magnitude < 0.12f ? Vector2.zero : v;
                }
                else if (lookFingers.Contains(id) && t.phase == UnityEngine.InputSystem.TouchPhase.Moved)
                {
                    l += t.delta * touchLookSensitivity * (160f / Mathf.Max(100f, Screen.dpi > 0 ? Screen.dpi : 160f));
                }
                if (t.phase == UnityEngine.InputSystem.TouchPhase.Ended || t.phase == UnityEngine.InputSystem.TouchPhase.Canceled)
                {
                    if (id == stickFinger) stickFinger = -1;
                    lookFingers.Remove(id); uiFingers.Remove(id);
                }
            }
            if (!StickActive) { StickCenter = DefaultStickCenter(); StickKnob = StickCenter; }
            if (Gamepad.current != null && (Gamepad.current.leftStick.ReadValue().sqrMagnitude > 0.05f || Gamepad.current.rightStick.ReadValue().sqrMagnitude > 0.05f)) UsingTouch = false;

            Move = Vector2.ClampMagnitude(m, 1f);
            Look = l;
            var manual = l; manual.x -= Turn * turnSpeed * Time.unscaledDeltaTime;
            LookedThisFrame = manual.sqrMagnitude > 0.0004f;
            Hurry = hurry.IsPressed();
            Act = act.WasPressedThisFrame() || uiAct;
            Focus = focus.WasPressedThisFrame() || uiFocus;
            Back = back.WasPressedThisFrame() || uiBack;
            uiAct = uiFocus = uiBack = false;
        }

        public Vector2 DefaultStickCenter()
        {
            var safe = Screen.safeArea;
            return new Vector2(safe.xMin + StickRadius * 1.6f, safe.yMin + StickRadius * 1.6f);
        }

        static bool OverUI(Vector2 pos, int pointerId)
        {
            var es = EventSystem.current;
            if (es == null) return false;
            var data = new PointerEventData(es) { position = pos, pointerId = pointerId };
            hits.Clear();
            es.RaycastAll(data, hits);
            return hits.Count > 0;
        }
    }
}
