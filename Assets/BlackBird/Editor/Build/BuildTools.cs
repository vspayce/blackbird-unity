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

        // Each place is its own download (GitHub Pages refuses files over 100 MB, and nobody should wait for the house
        // to see the alley): WebGL/<group>/ is a complete player; the splash page at the root links into them.
        public static readonly (string folder, string[] scenes)[] Groups =
        {
            ("alley", new[] { "BurrittAlley" }),
            ("hopkins", new[] { "HopkinsNight", "HopkinsDay" }),   // together, so Tab turns night to day at once
            ("palace", new[] { "PalaceHotel" }),
            ("stmark", new[] { "StMark" }),
        };

        // The editor must be switched to a platform before building for it (the scripts are compiled per platform). The
        // switch reimports and recompiles, so it is its own step: the first run switches, the next one builds.
        static bool OnTarget(BuildTargetGroup group, BuildTarget target, string what)
        {
            if (EditorUserBuildSettings.activeBuildTarget == target) return true;
            Debug.LogWarning($"[Black Bird] Switching the editor to {target} (this takes a while). Run \"{what}\" again when it finishes.");
            EditorUserBuildSettings.SwitchActiveBuildTarget(group, target);
            return false;
        }

        [MenuItem("Black Bird/Build WebGL", priority = 21)]
        public static void BuildWebGL()
        {
            ConfigurePlayer();
            if (!OnTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL, "Build WebGL")) return;
            var all = Groups.SelectMany(g => g.scenes).Select(n => $"Assets/BlackBird/Scenes/{n}.unity").ToArray();
            var missing = all.Where(p => !File.Exists(Path.Combine(ProjectRoot, p))).ToArray();
            if (missing.Length > 0) { Debug.LogError("[Black Bird] Build these scenes first: " + string.Join(", ", missing)); return; }
            if (Directory.Exists(WebGLDir)) Directory.Delete(WebGLDir, true);
            foreach (var (folder, names) in Groups)
            {
                // each player is built in its own staging folder and then copied into place: building the next one
                // cleans up the previous build's output wherever it was written
                var stage = Path.Combine(ProjectRoot, "Builds", "WebGLStage", folder);   // (Unity refuses to build inside Library)
                if (Directory.Exists(stage)) Directory.Delete(stage, true);
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = names.Select(n => $"Assets/BlackBird/Scenes/{n}.unity").ToArray(),
                    locationPathName = stage, target = BuildTarget.WebGL, options = BuildOptions.None,
                });
                var s = report.summary;
                if (s.result != BuildResult.Succeeded) { Debug.LogError($"[Black Bird] WebGL build of {folder} {s.result}: {s.totalErrors} errors. See the console."); return; }
                var dir = Path.Combine(WebGLDir, folder);
                Directory.CreateDirectory(dir);
                CopyDir(stage, dir);
                var biggest = Directory.GetFiles(Path.Combine(dir, "Build")).Max(f => new FileInfo(f).Length) / (1024f * 1024f);
                Debug.Log($"[Black Bird] WebGL build: {folder} ({s.totalSize / (1024f * 1024f):0.0} MB, largest file {biggest:0.0} MB, {s.totalTime.TotalSeconds:0}s)");
                if (biggest > 95) Debug.LogError($"[Black Bird] {folder}: a file of {biggest:0} MB is over what GitHub Pages will take (100 MB).");
            }
            // the root: the same splash page (its cards lead into the players) and its pictures
            File.Copy(Path.Combine(WebGLDir, Groups[0].folder, "index.html"), Path.Combine(WebGLDir, "index.html"), true);
            var img = Path.Combine(WebGLDir, "img"); Directory.CreateDirectory(img);
            foreach (var f in Directory.GetFiles(Path.Combine(Application.dataPath, "WebGLTemplates", "BlackBird", "img")))
                if (!f.EndsWith(".meta")) File.Copy(f, Path.Combine(img, Path.GetFileName(f)), true);
        }

        // ---- Apple: a Mac app to play at once, and Xcode projects for iPhone/iPad and Apple TV ----
        static readonly string[] AllScenes = { "BurrittAlley", "HopkinsNight", "HopkinsDay", "PalaceHotel", "StMark" };
        static string BuildsDir => Path.Combine(ProjectRoot, "Builds");

        static void Apple(BuildTarget target, BuildTargetGroup group, string path, string what)
        {
            ConfigurePlayer();
            if (!BuildPipeline.IsBuildTargetSupported(group, target)) { Debug.LogError($"[Black Bird] {what}: install the {target} module in Unity Hub first."); return; }
            if (!OnTarget(group, target, what)) return;
            var id = UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(group);
            PlayerSettings.SetApplicationIdentifier(id, "com.vspayce.blackbird");
            PlayerSettings.bundleVersion = "0.1";
            if (target != BuildTarget.StandaloneOSX) PlayerSettings.SetScriptingBackend(id, ScriptingImplementation.IL2CPP);   // (the Mac app stays on Mono: no Xcode needed)
            if (target == BuildTarget.tvOS) { PlayerSettings.tvOS.requireExtendedGameController = true; PlayerSettings.tvOS.targetOSVersionString = "17.0"; }   // played with a controller
            if (target == BuildTarget.iOS) PlayerSettings.iOS.targetOSVersionString = "16.0";
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = AllScenes.Select(n => $"Assets/BlackBird/Scenes/{n}.unity").ToArray(),
                locationPathName = path, target = target, targetGroup = group, options = BuildOptions.None,
            });
            var s = report.summary;
            if (s.result == BuildResult.Succeeded) Debug.Log($"[Black Bird] {what}: {path} ({s.totalTime.TotalSeconds:0}s)");
            else Debug.LogError($"[Black Bird] {what} {s.result}: {s.totalErrors} errors. See the console.");
        }

        [MenuItem("Black Bird/Apple/Build Mac App", priority = 40)]
        public static void BuildMac() => Apple(BuildTarget.StandaloneOSX, BuildTargetGroup.Standalone, Path.Combine(BuildsDir, "macOS", "The Black Bird.app"), "Mac app");

        [MenuItem("Black Bird/Apple/Build Xcode Project for Apple TV", priority = 41)]
        public static void BuildTV() => Apple(BuildTarget.tvOS, BuildTargetGroup.tvOS, Path.Combine(BuildsDir, "tvOS"), "Apple TV Xcode project");

        [MenuItem("Black Bird/Apple/Build Xcode Project for iPhone and iPad", priority = 42)]
        public static void BuildIOS() => Apple(BuildTarget.iOS, BuildTargetGroup.iOS, Path.Combine(BuildsDir, "iOS"), "iPhone/iPad Xcode project");

        [MenuItem("Black Bird/Publish WebGL to GitHub Pages", priority = 22)]
        public static void Publish()
        {
            if (!File.Exists(Path.Combine(WebGLDir, "index.html"))) { Debug.LogError("[Black Bird] No WebGL build yet (Black Bird ▸ Build WebGL)."); return; }
            foreach (var (folder, _) in Groups)
            {
                var b = Path.Combine(WebGLDir, folder, "Build");
                if (!Directory.Exists(b) || !Directory.GetFiles(b, "*.data*").Any()) { Debug.LogError($"[Black Bird] {folder} has no player files; build again before publishing."); return; }
            }
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
            // one commit with no history: each build is ~100 MB and nothing needs the old ones
            Git(pages, "checkout -q --orphan gh-pages-new");
            Git(pages, "add -A");
            Git(pages, $"commit -q -m \"WebGL build {System.DateTime.Now:yyyy-MM-dd HH:mm}\"", allowFail: true);
            Git(pages, "branch -q -D gh-pages", allowFail: true);
            Git(pages, "branch -q -m gh-pages");
            var push = Git(pages, "push -q -f -u origin gh-pages", allowFail: true, capture: true);
            Git(pages, "reflog expire --expire=now --all", allowFail: true);
            Git(pages, "gc -q --prune=now", allowFail: true);
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
