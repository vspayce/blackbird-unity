// Pictures of a scene from the command line, for checking a fix without opening the editor:
//   Unity -batchmode -projectPath . -executeMethod BlackBird.EditorTools.QAShot.Run
//         -qaScene BurrittAlley -qaShots "name:x,y,z>lx,ly,z;name2:..." (web coordinates) -qaOut Builds/qa
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using BlackBird;

namespace BlackBird.EditorTools
{
    public static class QAShot
    {
        static string Arg(string k, string d)
        {
            var a = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(a, k);
            return i >= 0 && i + 1 < a.Length ? a[i + 1] : d;
        }

        static Vector3 V(string s) { var p = s.Split(','); return Web.W(float.Parse(p[0]), float.Parse(p[1]), float.Parse(p[2])); }

        public static void Run()
        {
            var scene = Arg("-qaScene", "BurrittAlley");
            var outDir = Arg("-qaOut", "Builds/qa");
            Directory.CreateDirectory(outDir);
            EditorSceneManager.OpenScene($"Assets/BlackBird/Scenes/{scene}.unity");
            var go = new GameObject("QACam");
            var cam = go.AddComponent<Camera>();
            var main = Camera.main;
            if (main) { cam.CopyFrom(main); }
            cam.fieldOfView = 60; cam.nearClipPlane = 0.05f; cam.farClipPlane = 600;
            var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32); rt.antiAliasing = 4;
            cam.targetTexture = rt;
            foreach (var shot in Arg("-qaShots", "fence:0,1.6,-19>0,-3,-40").Split(';'))
            {
                if (string.IsNullOrWhiteSpace(shot)) continue;
                var nameRest = shot.Split(':'); var pts = nameRest[1].Split('>');
                go.transform.position = V(pts[0]);
                go.transform.LookAt(V(pts[1]));
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tex.Apply();
                File.WriteAllBytes(Path.Combine(outDir, $"{scene}_{nameRest[0]}.jpg"), tex.EncodeToJPG(80));
                RenderTexture.active = null;
                Debug.Log($"[QAShot] {scene}_{nameRest[0]}");
            }
        }
    }
}
