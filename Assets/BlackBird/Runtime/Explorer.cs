// Walking Holmes about with Watson at his shoulder: the controls, the camera that swings round behind him,
// Watson's following, and moving the pair somewhere else (a stair door, a tower). Shared by every scene.
using UnityEngine;

namespace BlackBird
{
    public class Explorer : MonoBehaviour
    {
        public Walker holmes, watson;
        public CameraRig cameraRig;
        public float walkSpeed = 1.55f, strollSpeed = 0.75f, hurrySpeed = 2.4f;
        [Tooltip("How quickly the camera swings round behind Holmes when he walks and you aren't steering it (deg/s).")]
        public float followTurn = 120f;
        [Tooltip("Test only: pushes the move stick (x, y) when nothing else does.")]
        public Vector2 autopilot;

        float lastLook = -10f;
        bool ready;

        public void Setup()
        {
            if (ready) return;
            ready = true;
            var ha = holmes.GetComponentInChildren<Animator>(); if (ha) ha.updateMode = AnimatorUpdateMode.UnscaledTime;
            // Watson keeps at Holmes's shoulder: never let him body-block Holmes
            if (watson && holmes.TryGetComponent<CharacterController>(out var hc) && watson.TryGetComponent<CharacterController>(out var wc))
                Physics.IgnoreCollision(hc, wc);
        }

        void Start() => Setup();

        /// Put Holmes (and Watson just behind him) somewhere, facing yaw degrees.
        public void Place(Vector3 pos, float yaw)
        {
            Setup();
            Teleport(holmes, pos, yaw);
            if (watson) Teleport(watson, pos - Quaternion.Euler(0, yaw, 0) * new Vector3(0.95f, 0, 0.55f), yaw);
            cameraRig.yaw = yaw;
        }

        static void Teleport(Walker w, Vector3 pos, float yaw)
        {
            var cc = w.GetComponent<CharacterController>();
            if (cc) cc.enabled = false;
            w.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            if (cc) cc.enabled = true;
        }

        /// Exploring: move relative to the camera; the camera swings round behind Holmes as he walks unless
        /// you're steering it yourself.
        public void Steer()
        {
            var input = GameInput.I;
            input.allowMouseCapture = true;
            cameraRig.AddLook(input.Look);
            if (input.LookedThisFrame) lastLook = Time.unscaledTime;
            var m = input.Move.sqrMagnitude > 0.01f ? input.Move : autopilot;
            var dir = Quaternion.Euler(0, cameraRig.yaw, 0) * new Vector3(m.x, 0, m.y);
            float push = m.magnitude;
            // the brisk pace is the default (the houses are big); Shift (or the stick clicked) slows him to a walk
            float top = input.Hurry ? walkSpeed : hurrySpeed;
            float speed = push < 0.05f ? 0 : (push < 0.7f ? Mathf.Lerp(strollSpeed * 0.6f, strollSpeed, push / 0.7f) : Mathf.Lerp(strollSpeed, top, (push - 0.7f) / 0.3f));
            holmes.Drive(push > 0.05f ? dir.normalized * speed : Vector3.zero, Time.unscaledDeltaTime);   // Focus slows the world, not Holmes
            // (only for a stick pushed mostly forward: steering diagonally while the camera chases walks in circles)
            if (m.y > 0.5f && Mathf.Abs(m.x) < 0.35f && holmes.Speed > 0.3f && Time.unscaledTime - lastLook > 0.8f)
            {
                float off = Mathf.DeltaAngle(cameraRig.yaw, holmes.transform.eulerAngles.y);
                cameraRig.yaw += Mathf.Clamp(off, -1, 1) * Mathf.Min(Mathf.Abs(off), followTurn * Mathf.InverseLerp(0, 40, Mathf.Abs(off)) * Time.unscaledDeltaTime);
            }
        }

        public void Hold(float dt) => holmes.Idle(dt);

        /// Watson keeps to Holmes's left shoulder, and catches up through doors and up stairs.
        public void Follow(float dt, Vector3? watsonLooksAt = null)
        {
            if (!watson) return;
            var h = holmes.transform;
            var slot = h.position - h.right * 0.95f - h.forward * 0.55f;
            var to = slot - watson.transform.position;
            if (Mathf.Abs(to.y) > 1.6f || to.magnitude > 9f)
            {
                // a floor apart or left far behind: he arrives, as Watson always does
                Teleport(watson, h.position - h.forward * 1.4f, h.eulerAngles.y);
                return;
            }
            to.y = 0;
            float d = to.magnitude;
            if (d > 0.35f)
            {
                float s = Mathf.Clamp(d * 1.6f, 0.4f, holmes.Speed + 0.6f);
                if (d > 4f) s = hurrySpeed + 0.4f;
                watson.Drive(to / d * s, dt);
            }
            else
            {
                watson.Idle(dt);
                watson.FaceTowards(watson.transform.position + h.forward, dt);
            }
            watson.lookPoint = watsonLooksAt ?? (h.position + Vector3.up * 1.6f + h.forward * 3f);
        }
    }
}
