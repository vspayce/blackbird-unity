// Black Bird ▸ Build Title Screen: Scenes/Title.unity, the scene the Apple apps open on (TitleScreen builds the menu).
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using BlackBird;

namespace BlackBird.EditorTools
{
    public static class TitleBuilder
    {
        public const string ScenePath = SceneBuilder.Root + "/Scenes/Title.unity";

        [MenuItem("Black Bird/Build Title Screen", priority = 1)]
        public static void Build()
        {
            SceneBuilder.EnsurePipeline();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.02f, 0.025f, 0.035f);
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<UniversalAdditionalCameraData>();
            var t = new GameObject("TitleScreen").AddComponent<TitleScreen>();
            t.serif = AssetDatabase.LoadAssetAtPath<Font>(SceneBuilder.Root + "/Fonts/IMFellEnglishSC-Regular.ttf");
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            RenderSettings.fog = false;
            EditorSceneManager.SaveScene(scene, ScenePath);
            var list = EditorBuildSettings.scenes.ToList();
            list.RemoveAll(s => s.path == ScenePath);
            list.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
            Debug.Log("[Black Bird] Built " + ScenePath);
        }
    }
}
