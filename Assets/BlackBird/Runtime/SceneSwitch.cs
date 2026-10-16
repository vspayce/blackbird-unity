// Moving between the places in this build: a link (?scene=hopkins, ?scene=hopkins-day, ?scene=alley) opens
// that place, and in the game the number keys do: 1 Burritt Alley, 2 the Hopkins house by night, 3 by day.
// Tab switches the Hopkins house between night and day.
// It installs itself when the game starts, so it works whichever scene opens first and whatever that scene holds.
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace BlackBird
{
    public class SceneSwitch : MonoBehaviour
    {
        static SceneSwitch instance;
        static readonly string[] Scenes = { "BurrittAlley", "HopkinsNight", "HopkinsDay" };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (!instance)
            {
                var go = new GameObject("SceneSwitch");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<SceneSwitch>();
            }
            var want = FromUrl(Application.absoluteURL);
            if (want != null && want != SceneManager.GetActiveScene().name) Go(want);
        }

        static string FromUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return null;
            int q = url.IndexOf("scene=");
            if (q < 0) return null;
            var v = url.Substring(q + 6).Split('&', '#')[0].ToLowerInvariant();
            return v == "hopkins" || v == "hopkins-night" || v == "night" ? "HopkinsNight"
                 : v == "hopkins-day" || v == "day" ? "HopkinsDay"
                 : v == "alley" ? "BurrittAlley" : null;
        }

        void Awake()
        {
            // scenes built earlier carry their own copy; the one installed at start is enough
            if (instance && instance != this) Destroy(this);
        }

        void Update()
        {
            // a controller's View / Select button moves on to the next place (alley, house by night, by day)
            var pad = Gamepad.current;
            if (pad != null && pad.selectButton.wasPressedThisFrame)
            {
                int i = System.Array.IndexOf(Scenes, SceneManager.GetActiveScene().name);
                Go(Scenes[(i + 1) % Scenes.Length]);
                return;
            }
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

        // On the web each place is its own download (alley/, hopkins/): a scene that isn't in this one is a page away.
        static readonly System.Collections.Generic.Dictionary<string, (string key, string folder)> WebPlaces =
            new System.Collections.Generic.Dictionary<string, (string, string)>
            {
                ["BurrittAlley"] = ("alley", "alley"), ["HopkinsNight"] = ("hopkins", "hopkins"), ["HopkinsDay"] = ("hopkins-day", "hopkins"),
            };
#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")] static extern void BB_GoTo(string url);
#endif

        public static void Go(string scene)
        {
            // free the mouse first: the next scene captures it again on a click, which browsers allow
            Cursor.lockState = CursorLockMode.None;
            if (Application.CanStreamedLevelBeLoaded(scene)) { SceneManager.LoadScene(scene); return; }
#if UNITY_WEBGL && !UNITY_EDITOR
            if (WebPlaces.TryGetValue(scene, out var w)) { BB_GoTo($"../{w.folder}/?scene={w.key}"); return; }
#endif
            Debug.LogWarning("[Black Bird] Scene not in the build: " + scene);
        }
    }
}
