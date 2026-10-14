// Player settings for the phone-landscape game, the WebGL test build, and publishing that build to
// the repo's gh-pages branch (GitHub Pages) so it can be opened on a phone.
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace BlackBird.EditorTools
{
    public static class BuildTools
    {
        static string ProjectRoot => Path.GetDirectoryName(Application.dataPath);
        static string WebGLDir => Path.Combine(ProjectRoot, "WebGL");

        [MenuItem("Black Bird/Configure Player Settings", priority = 20)]
        public static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "vspayce";
            PlayerSettings.productName = "The Black Bird";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.runInBackground = false;

            // phones, landscape only
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.iOS, "com.vspayce.blackbird");

            // WebGL: GitHub Pages can't send Content-Encoding headers, so gzip with the JS fallback
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.WebGL.nameFilesAsHashes = true;    // a new build never mixes with a cached old one
            PlayerSettings.WebGL.template = "PROJECT:BlackBird";   // full-window, landscape, our loading screen
            // optimised code rather than the quick-to-build default
            EditorUserBuildSettings.SetPlatformSettings("WebGL", "CodeOptimization", "RuntimeSpeed");
            Debug.Log("[Black Bird] Player settings configured.");
        }

        [MenuItem("Black Bird/Build WebGL", priority = 21)]
        public static void BuildWebGL()
        {
            ConfigurePlayer();
            // every place the splash page offers, in the order SceneSwitch numbers them
            var scenes = new[] { "BurrittAlley", "HopkinsNight", "HopkinsDay" }
                .Select(n => $"Assets/BlackBird/Scenes/{n}.unity").ToArray();
            var missing = scenes.Where(p => !File.Exists(Path.Combine(ProjectRoot, p))).ToArray();
            if (missing.Length > 0) { Debug.LogError("[Black Bird] Build these scenes first: " + string.Join(", ", missing)); return; }
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes, locationPathName = WebGLDir, target = BuildTarget.WebGL, options = BuildOptions.None,
            });
            var s = report.summary;
            if (s.result == BuildResult.Succeeded) Debug.Log($"[Black Bird] WebGL build: {WebGLDir} ({s.totalSize / (1024f * 1024f):0.0} MB, {s.totalTime.TotalSeconds:0}s)");
            else Debug.LogError($"[Black Bird] WebGL build {s.result}: {s.totalErrors} errors. See the console.");
        }

        [MenuItem("Black Bird/Publish WebGL to GitHub Pages", priority = 22)]
        public static void Publish()
        {
            if (!File.Exists(Path.Combine(WebGLDir, "index.html"))) { Debug.LogError("[Black Bird] No WebGL build yet (Black Bird ▸ Build WebGL)."); return; }
            var url = Git(ProjectRoot, "remote get-url origin").Trim();
            if (string.IsNullOrEmpty(url)) { Debug.LogError("[Black Bird] This project has no git remote 'origin'."); return; }

            // a separate checkout of gh-pages inside Library (ignored), so the main branch never carries builds
            var pages = Path.Combine(ProjectRoot, "Library", "BlackBirdPages");
            if (!Directory.Exists(Path.Combine(pages, ".git")))
            {
                Directory.CreateDirectory(pages);
                Git(pages, "init -q");
                Git(pages, "checkout -q -b gh-pages");
                Git(pages, $"remote add origin {url}");
                Git(pages, "fetch -q --depth 1 origin gh-pages", allowFail: true);
                Git(pages, "reset -q --soft origin/gh-pages", allowFail: true);
            }
            foreach (var f in Directory.GetFiles(pages)) File.Delete(f);
            foreach (var d in Directory.GetDirectories(pages)) if (Path.GetFileName(d) != ".git") Directory.Delete(d, true);
            CopyDir(WebGLDir, pages);
            File.WriteAllText(Path.Combine(pages, ".nojekyll"), "");
            Git(pages, "add -A");
            Git(pages, $"commit -q -m \"WebGL build {System.DateTime.Now:yyyy-MM-dd HH:mm}\"", allowFail: true);
            var push = Git(pages, "push -q -u origin gh-pages", allowFail: true, capture: true);
            Debug.Log("[Black Bird] Published to gh-pages. " + push +
                      "\nIf this is the first publish: GitHub ▸ Settings ▸ Pages ▸ Source: Deploy from a branch ▸ gh-pages / (root).");
        }

        [MenuItem("Black Bird/Build and Publish WebGL", priority = 23)]
        public static void BuildAndPublish() { BuildWebGL(); Publish(); }

        static void CopyDir(string from, string to)
        {
            foreach (var d in Directory.GetDirectories(from, "*", SearchOption.AllDirectories)) Directory.CreateDirectory(d.Replace(from, to));
            foreach (var f in Directory.GetFiles(from, "*", SearchOption.AllDirectories)) File.Copy(f, f.Replace(from, to), true);
        }

        static string Git(string dir, string args, bool allowFail = false, bool capture = true)
        {
            var psi = new ProcessStartInfo("git", args)
            {
                WorkingDirectory = dir, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true,
            };
            // the editor doesn't inherit the login shell's PATH on macOS
            psi.EnvironmentVariables["PATH"] = "/opt/homebrew/bin:/usr/local/bin:/usr/bin:/bin:" + System.Environment.GetEnvironmentVariable("PATH");
            using var p = Process.Start(psi);
            var output = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
            p.WaitForExit();
            if (p.ExitCode != 0 && !allowFail) Debug.LogError($"[Black Bird] git {args} failed: {output}");
            return output;
        }
    }
}
