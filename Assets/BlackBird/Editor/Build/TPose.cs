// The cast is rigged at rest with the arms down. Humanoid retargeting measures every animation from a
// T-pose, so without this, mocap arrives with the arms wrong by the difference. This is the scripted
// version of the Avatar editor's "Enforce T-Pose": straighten the arms out sideways in the avatar's
// reference skeleton (the mesh and its own clips are untouched).
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BlackBird.EditorTools
{
    public static class TPose
    {
        const string Marker = "bb-tpose-v1";

        [MenuItem("Black Bird/Setup/Enforce T-Pose on Cast")]
        public static void EnforceOnCastMenu() => EnforceOnCast(force: true);

        public static void EnforceOnCast(bool force = false)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets/BlackBird/Art/Characters" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var mi = AssetImporter.GetAtPath(path) as ModelImporter;
                if (!mi || mi.animationType != ModelImporterAnimationType.Human) continue;
                if (!force && mi.userData == Marker) continue;
                if (Enforce(path, mi)) { mi.userData = Marker; mi.SaveAndReimport(); Debug.Log($"[Black Bird] T-pose set for {path}"); }
            }
        }

        static bool Enforce(string path, ModelImporter mi)
        {
            var hd = mi.humanDescription;
            if (hd.human == null || hd.human.Length == 0 || hd.skeleton == null || hd.skeleton.Length == 0)
            {
                Debug.LogWarning($"[Black Bird] {path} has no humanoid mapping yet; skipping T-pose.");
                return false;
            }
            string Bone(string human) => hd.human.FirstOrDefault(h => h.humanName == human).boneName;

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var inst = Object.Instantiate(model);
            try
            {
                var all = inst.GetComponentsInChildren<Transform>(true);
                Transform T(string human) { var n = Bone(human); return string.IsNullOrEmpty(n) ? null : all.FirstOrDefault(t => t.name == n); }

                // reset to the skeleton the avatar was built from
                foreach (var sb in hd.skeleton)
                {
                    var t = all.FirstOrDefault(x => x.name == sb.name);
                    if (t && t != inst.transform) { t.localPosition = sb.position; t.localRotation = sb.rotation; }
                }

                var hips = T("Hips");
                foreach (var side in new[] { "Left", "Right" })
                {
                    var up = T(side + "UpperArm"); var lo = T(side + "LowerArm"); var hand = T(side + "Hand");
                    if (!up || !lo || !hand) return false;
                    var outward = up.position - (hips ? hips.position : inst.transform.position); outward.y = 0; outward.Normalize();
                    Align(up, lo.position - up.position, outward);
                    Align(lo, hand.position - lo.position, outward);
                    var mid = T(side + "MiddleProximal");
                    if (mid) Align(hand, mid.position - hand.position, outward);
                }
                // legs straight down, feet under the hips
                foreach (var side in new[] { "Left", "Right" })
                {
                    var up = T(side + "UpperLeg"); var lo = T(side + "LowerLeg"); var foot = T(side + "Foot");
                    if (!up || !lo || !foot) continue;
                    Align(up, lo.position - up.position, Vector3.down);
                    Align(lo, foot.position - lo.position, Vector3.down);
                }

                var sk = hd.skeleton;
                for (int i = 0; i < sk.Length; i++)
                {
                    var t = all.FirstOrDefault(x => x.name == sk[i].name);
                    if (t && t != inst.transform) sk[i].rotation = t.localRotation;
                }
                hd.skeleton = sk;
                mi.humanDescription = hd;
                return true;
            }
            finally { Object.DestroyImmediate(inst); }
        }

        static void Align(Transform t, Vector3 current, Vector3 want)
        {
            if (current.sqrMagnitude < 1e-8f) return;
            t.rotation = Quaternion.FromToRotation(current.normalized, want.normalized) * t.rotation;
        }
    }
}
