// Third-person camera over Holmes's right shoulder, as in the prototype. Pulls in when something
// is between it and him, and eases to fixed shots for close-ups.
using UnityEngine;

namespace BlackBird
{
    public class CameraRig : MonoBehaviour
    {
        public Transform target;
        public float distance = 3.0f, height = 1.62f, shoulder = 0.42f;
        public float minPitch = -35f, maxPitch = 55f;
        public float yaw, pitch = 8f;
        public LayerMask blockers = ~0;

        Camera cam;
        float curDist;
        Vector3 shotPos; Quaternion shotRot; float shotBlend; bool inShot;
        float baseFov;

        void Awake()
        {
            cam = GetComponent<Camera>();
            baseFov = cam ? cam.fieldOfView : 55f;
            curDist = 1f;
        }

        public void SnapBehind()
        {
            if (!target) return;
            yaw = target.eulerAngles.y;
        }

        public void Shot(Vector3 pos, Vector3 look) { shotPos = pos; shotRot = Quaternion.LookRotation(look - pos); inShot = true; }
        public void EndShot() { inShot = false; }
        public bool InShot => inShot || shotBlend > 0.01f;

        public void AddLook(Vector2 delta)
        {
            yaw += delta.x;
            pitch = Mathf.Clamp(pitch - delta.y, minPitch, maxPitch);
        }

        void LateUpdate()
        {
            if (!target) return;
            float dt = Time.unscaledDeltaTime;

            var rot = Quaternion.Euler(pitch, yaw, 0);
            var pivot = target.position + Vector3.up * height;
            var dir = rot * Vector3.right * shoulder - rot * Vector3.forward * distance;
            float len = dir.magnitude;
            float want = 1f;
            if (Physics.SphereCast(pivot, 0.2f, dir / len, out var hit, len, blockers, QueryTriggerInteraction.Ignore))
                want = Mathf.Max(0.2f, (hit.distance - 0.1f) / len);
            curDist = want < curDist ? want : Mathf.MoveTowards(curDist, want, dt * 1.2f);   // curDist: fraction of the full arm
            var followPos = pivot + dir * curDist;

            shotBlend = Mathf.MoveTowards(shotBlend, inShot ? 1f : 0f, dt * 1.6f);
            float k = Mathf.SmoothStep(0, 1, shotBlend);
            transform.SetPositionAndRotation(Vector3.Lerp(followPos, shotPos, k), Quaternion.Slerp(rot, shotRot, k));
            if (cam) cam.fieldOfView = Mathf.Lerp(baseFov, 40f, k);
        }
    }
}
