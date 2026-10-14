// Moving between the places in this build: a link (?scene=hopkins, ?scene=hopkins-day, ?scene=alley) opens
// that place, and in the game the number keys do: 1 Burritt Alley, 2 the Hopkins house by night, 3 by day.
// Tab switches the Hopkins house between night and day.
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace BlackBird
{
    public class SceneSwitch : MonoBehaviour
    {
        static bool urlChecked;
        static readonly string[] Scenes = { "BurrittAlley", "HopkinsNight", "HopkinsDay" };

        void Awake()
        {
            if (urlChecked) return;
            urlChecked = true;
            var url = Application.absoluteURL ?? "";
            int q = url.IndexOf("scene=");
            if (q < 0) return;
            var v = url.Substring(q + 6).Split('&', '#')[0].ToLowerInvariant();
            string want = v == "hopkins" || v == "hopkins-night" ? "HopkinsNight" : v == "hopkins-day" || v == "day" ? "HopkinsDay" : v == "alley" ? "BurrittAlley" : null;
            if (want != null && want != SceneManager.GetActiveScene().name) Go(want);
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.digit1Key.wasPressedThisFrame) Go(Scenes[0]);
            if (kb.digit2Key.wasPressedThisFrame) Go(Scenes[1]);
            if (kb.digit3Key.wasPressedThisFrame) Go(Scenes[2]);
            if (kb.tabKey.wasPressedThisFrame)
            {
                var cur = SceneManager.GetActiveScene().name;
                if (cur == "HopkinsNight") Go("HopkinsDay"); else if (cur == "HopkinsDay") Go("HopkinsNight");
            }
        }

        public static void Go(string scene)
        {
            if (Application.CanStreamedLevelBeLoaded(scene)) SceneManager.LoadScene(scene);
            else Debug.LogWarning("[Black Bird] Scene not in the build: " + scene);
        }
    }
}
