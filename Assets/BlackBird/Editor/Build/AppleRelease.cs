// What an Apple build needs before it can go to TestFlight: the icons (Apple TV's layered app icon and Top Shelf
// images, the square icon for iPhone, iPad and Mac), a build number that grows with every upload, the signing team,
// and the Info.plist line that says the game uses no encryption of its own (so App Store Connect doesn't stop each
// build to ask). BuildTools.Apple calls Prepare before it builds; the post-process runs on the Xcode project.
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Callbacks;
using UnityEngine;

namespace BlackBird.EditorTools
{
    public static class AppleRelease
    {
        const string Icons = "Assets/BlackBird/Icons/";

        /// The paid developer team (developer.apple.com ▸ Membership). Tools/apple/team.txt overrides it.
        public static string Team
        {
            get
            {
                var f = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Tools", "apple", "team.txt");
                return File.Exists(f) ? File.ReadAllText(f).Trim() : "";
            }
        }

        /// A build number that only grows: the minute it was built, in UTC (yyMMddHHmm).
        public static string BuildNumber => DateTime.UtcNow.ToString("yyMMddHHmm");

        public static void Prepare(BuildTarget target)
        {
            var team = Team;
            if (!string.IsNullOrEmpty(team))
            {
                PlayerSettings.iOS.appleDeveloperTeamID = team;
                PlayerSettings.iOS.appleEnableAutomaticSigning = true;
            }
            PlayerSettings.companyName = "vspayce";
            PlayerSettings.productName = "The Black Bird";
            if (target == BuildTarget.tvOS) TvIcons();
            else SquareIcons(target == BuildTarget.iOS ? NamedBuildTarget.iOS : NamedBuildTarget.Standalone);
        }

        static Texture2D Tex(string file)
        {
            var path = Icons + file;
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp && (imp.npotScale != TextureImporterNPOTScale.None || imp.mipmapEnabled || imp.textureCompression != TextureImporterCompression.Uncompressed || imp.maxTextureSize < 8192))
            {
                imp.textureType = TextureImporterType.Default;
                imp.npotScale = TextureImporterNPOTScale.None; imp.mipmapEnabled = false;
                imp.textureCompression = TextureImporterCompression.Uncompressed; imp.maxTextureSize = 8192;
                imp.alphaIsTransparency = file.Contains("front");
                imp.SaveAndReimport();
            }
            var t = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (!t) Debug.LogError("[Black Bird] Missing icon art: " + path);
            return t;
        }

        static void TvIcons()
        {
            var nb = NamedBuildTarget.tvOS;
            Texture2D back = Tex("tv_icon_back.png"), front = Tex("tv_icon_front.png"), shelf = Tex("tv_topshelf.png"), wide = Tex("tv_topshelf_wide.png");
            foreach (var kind in PlayerSettings.GetSupportedIconKinds(nb))
            {
                var icons = PlayerSettings.GetPlatformIcons(nb, kind);
                string k = kind.ToString().ToLowerInvariant();
                foreach (var ic in icons)
                {
                    if (k.Contains("shelf")) ic.SetTexture(k.Contains("wide") ? wide : shelf);
                    else
                    {
                        int n = Mathf.Max(2, ic.minLayerCount);
                        var layers = new Texture2D[n];
                        layers[0] = back;
                        for (int i = 1; i < n; i++) layers[i] = front;
                        ic.SetTextures(layers);
                    }
                }
                PlayerSettings.SetPlatformIcons(nb, kind, icons);
                Debug.Log($"[Black Bird] tvOS icons: {kind} × {icons.Length} ({string.Join(", ", icons.Select(i => $"{i.width}x{i.height}"))})");
            }
        }

        static void SquareIcons(NamedBuildTarget nb)
        {
            var sq = Tex("icon_1024.png");
            foreach (var kind in PlayerSettings.GetSupportedIconKinds(nb))
            {
                var icons = PlayerSettings.GetPlatformIcons(nb, kind);
                foreach (var ic in icons) ic.SetTexture(sq);
                PlayerSettings.SetPlatformIcons(nb, kind, icons);
            }
        }

        [PostProcessBuild(100)]
        public static void OnBuilt(BuildTarget target, string path)
        {
            if (target != BuildTarget.tvOS && target != BuildTarget.iOS) return;
            var plist = Path.Combine(path, "Info.plist");
            if (!File.Exists(plist)) return;
            var s = File.ReadAllText(plist);
            s = SetKey(s, "ITSAppUsesNonExemptEncryption", "<false/>");
            s = SetKey(s, "CFBundleVersion", $"<string>{BuildNumber}</string>");
            File.WriteAllText(plist, s);
            Debug.Log($"[Black Bird] Info.plist: build {BuildNumber}, no non-exempt encryption.");
        }

        // replace a key's value in a plain XML plist, or add the key before the closing </dict>
        static string SetKey(string plist, string key, string valueXml)
        {
            var tag = $"<key>{key}</key>";
            int i = plist.IndexOf(tag, StringComparison.Ordinal);
            if (i >= 0)
            {
                int v0 = plist.IndexOf('<', i + tag.Length);
                int close = plist.IndexOf('>', v0);
                int v1 = plist[close - 1] == '/' ? close + 1 : plist.IndexOf('>', plist.IndexOf("</", close, StringComparison.Ordinal)) + 1;
                return plist.Substring(0, v0) + valueXml + plist.Substring(v1);
            }
            int end = plist.LastIndexOf("</dict>", StringComparison.Ordinal);
            return plist.Substring(0, end) + $"\t{tag}\n\t{valueXml}\n" + plist.Substring(end);
        }
    }
}
