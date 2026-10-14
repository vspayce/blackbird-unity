// Drives a Humanoid character's Animator from how fast it is really moving, so the stride matches
// the ground speed (no skating), turns the body smoothly, plants the feet with IK and turns the
// head toward whatever it is looking at.
//
// Animator parameters (made by the scene builder): Gait (0 idle, 1 walk, 2 run), PlayRate (state
// speed multiplier), Talk (bool), Lie (bool).
using UnityEngine;

namespace BlackBird
{
    public class Walker : MonoBehaviour
    {
        [Tooltip("Ground speed of the walk and run clips (m/s), read from the clips by the builder.")]
        public float walkClipSpeed = 1.4f, runClipSpeed = 3.6f;
        public float turnSpeed = 540f;          // degrees per second at full walk
        public float acceleration = 4f;         // m/s²

        public Transform lookTarget;
        public Vector3? lookPoint;
        public float lookWeight = 0.8f;

        Animator anim;
        CharacterController cc;
        Vector3 velocity;                       // smoothed, horizontal
        float gait, playRate = 1f, lookW;
        static readonly int GaitId = Animator.StringToHash("Gait"), RateId = Animator.StringToHash("PlayRate"),
            TalkId = Animator.StringToHash("Talk"), LieId = Animator.StringToHash("Lie");

        public float Speed => velocity.magnitude;
        public Vector3 Velocity => velocity;

        void Awake()
        {
            anim = GetComponentInChildren<Animator>();
            cc = GetComponent<CharacterController>();
            if (anim) { anim.applyRootMotion = false; anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms; }
        }

        /// Move toward a desired horizontal velocity (world space). Call once per frame.
        public void Drive(Vector3 desired, float dt)
        {
            desired.y = 0;
            velocity = Vector3.MoveTowards(velocity, desired, acceleration * dt);
            var step = velocity * dt;
            if (cc && cc.enabled) { cc.Move(step + Vector3.down * 2f * dt); SafetyNet(); }
            else transform.position += step;
            if (cc && cc.enabled && dt > 0)
            {
                // walking into a wall: let the legs slow down with the body
                var real = Vector3.ProjectOnPlane(cc.velocity, Vector3.up);
                if (real.magnitude < velocity.magnitude * 0.6f) velocity = real;
            }

            // face where we're going; slower turns while barely moving reads as weight
            var flat = desired.sqrMagnitude > 0.01f ? desired : velocity;
            if (flat.sqrMagnitude > 0.01f)
            {
                var want = Quaternion.LookRotation(flat.normalized, Vector3.up);
                float k = Mathf.Clamp01(velocity.magnitude / walkClipSpeed);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, want, turnSpeed * Mathf.Lerp(0.35f, 1f, k) * dt);
            }
            Animate(dt);
        }

        // If the ground ever lets him through (a gap in a set, a walk off the edge of the modelled world), put him
        // back where he last stood rather than letting him fall forever.
        Vector3 lastFooting, lastPos; bool hasFooting;
        public float fallLimit = 2.5f;
        void SafetyNet()
        {
            if ((transform.position - lastPos).sqrMagnitude > 9f) hasFooting = false;   // teleported: a new place
            lastPos = transform.position;
            if (cc.isGrounded) { lastFooting = transform.position; hasFooting = true; return; }
            if (hasFooting && transform.position.y < lastFooting.y - fallLimit)
            {
                cc.enabled = false;
                transform.position = lastFooting - velocity.normalized * 0.6f;   // a step back from the edge
                velocity = Vector3.zero;
                cc.enabled = true;
                lastPos = transform.position;
            }
        }

        public void FaceTowards(Vector3 point, float dt)
        {
            var d = point - transform.position; d.y = 0;
            if (d.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(d), 200f * dt);
        }

        void Animate(float dt)
        {
            if (!anim) return;
            float s = velocity.magnitude;
            float slowest = walkClipSpeed * 0.55f;   // below this, the walk clip slowed any further looks like wading
            float g, rate;
            if (s < 0.05f) { g = 0; rate = 1; }
            else if (s < slowest) { g = s / slowest; rate = Mathf.Lerp(1f, 0.55f, g); }
            else if (s <= walkClipSpeed) { g = 1; rate = s / walkClipSpeed; }
            else if (runClipSpeed <= walkClipSpeed) { g = 1; rate = Mathf.Min(s / walkClipSpeed, 1.8f); }   // no run clip: a brisker walk
            else { g = 1 + Mathf.Clamp01((s - walkClipSpeed) / (runClipSpeed - walkClipSpeed)); rate = s / Mathf.Lerp(walkClipSpeed, runClipSpeed, g - 1); }
            gait = Mathf.MoveTowards(gait, g, dt * 4f);
            playRate = Mathf.MoveTowards(playRate, rate, dt * 3f);
            anim.SetFloat(GaitId, gait);
            anim.SetFloat(RateId, playRate);
        }

        public void Idle(float dt) => Drive(Vector3.zero, dt);
        public void SetTalking(bool on) { if (anim) anim.SetBool(TalkId, on); }
        public void SetLying(bool on) { if (anim) anim.SetBool(LieId, on); }

        void LateUpdate()
        {
            var want = (lookTarget || lookPoint.HasValue) ? lookWeight : 0f;
            lookW = Mathf.MoveTowards(lookW, want, Time.deltaTime * 1.5f);
        }

        void OnAnimatorIK(int layer)
        {
            if (!anim) return;
            Vector3? p = lookTarget ? lookTarget.position : lookPoint;
            if (p.HasValue)
            {
                // don't crane round past the shoulder
                var head = anim.GetBoneTransform(HumanBodyBones.Head);
                var to = p.Value - (head ? head.position : transform.position + Vector3.up * 1.6f);
                float ang = Vector3.Angle(transform.forward, Vector3.ProjectOnPlane(to, Vector3.up));
                float w = lookW * Mathf.InverseLerp(110f, 60f, ang);
                anim.SetLookAtWeight(w, 0.15f, 0.7f, 1f, 0.6f);
                anim.SetLookAtPosition(p.Value);
            }
            else anim.SetLookAtWeight(0);
        }
    }

    // OnAnimatorIK is sent to the Animator's own GameObject; this forwards it to the Walker on the root.
    [RequireComponent(typeof(Animator))]
    public class IKRelay : MonoBehaviour
    {
        public Walker walker;
        void OnAnimatorIK(int layer) { if (walker) walker.SendMessage("OnAnimatorIK", layer, SendMessageOptions.DontRequireReceiver); }
    }
}
