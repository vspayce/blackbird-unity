using UnityEngine;

namespace BlackBird
{
    // OnAnimatorIK is sent to the Animator's own GameObject; this forwards it to the Walker on the root.
    [RequireComponent(typeof(Animator))]
    public class IKRelay : MonoBehaviour
    {
        public Walker walker;
        void OnAnimatorIK(int layer) { if (walker) walker.SendMessage("OnAnimatorIK", layer, SendMessageOptions.DontRequireReceiver); }
    }
}
