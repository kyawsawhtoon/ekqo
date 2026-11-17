using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ekqo.Game.Editor
{
    /// <summary>
    /// One-click week-1 Burmese text test: makes the TextTest scene if it is missing and
    /// builds it for the web into ../site/text-test, which the Pages workflow publishes.
    /// The project's own Web settings (template, compression) are put back afterwards.
    /// </summary>
    public static class TextTestBuilder
    {
        private const string Folder = "Assets/Ekqo/Game/TextTest";
        private const string ScenePath = Folder + "/TextTest.unity";
        private const string PanelPath = Folder + "/TextTestPanel.asset";
        private const string ThemePath = Folder + "/TextTestTheme.tss";
        private const string FontPath = "Assets/Ekqo/Game/Fonts/NotoSansMyanmar-Regular.ttf";
        private const string StylesPath = "Assets/Ekqo/Game/UI/Ekqo.uss";
        private const string BundlePath = "Assets/StreamingAssets/content/my/bundle.json";
        private const string OutputFolder = "../site/text-test";
        private const string Title = "Burmese text test";

        [MenuItem("Ekqo/Build Burmese text test", priority = 1)]
        public static void Build()
        {
            if (!File.Exists(BundlePath))
            {
                EditorUtility.DisplayDialog(Title, $"Missing {BundlePath}.\nPull the latest code from GitHub and try again.", "OK");
                return;
            }
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
            {
                EditorUtility.DisplayDialog(Title,
                    "Web Build Support is not installed.\nUnity Hub → Installs → your Unity 6 version → ⚙ → Add modules → Web Build Support. Then reopen the project.",
                    "OK");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!EnsureScene()) return;

            var oldCompression = PlayerSettings.WebGL.compressionFormat;
            var oldFallback = PlayerSettings.WebGL.decompressionFallback;
            var oldTemplate = PlayerSettings.WebGL.template;
            var output = Path.GetFullPath(OutputFolder);
            try
            {
                // GitHub Pages cannot send the headers Brotli needs, so this build uses gzip
                // that the page unpacks itself.
                PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
                PlayerSettings.WebGL.decompressionFallback = true;
                PlayerSettings.WebGL.template = "PROJECT:Ekqo";

                if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
                    EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);
                if (Directory.Exists(output)) Directory.Delete(output, true);

                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = output,
                    target = BuildTarget.WebGL,
                    targetGroup = BuildTargetGroup.WebGL,
                    options = BuildOptions.None,
                });

                if (report.summary.result == BuildResult.Succeeded)
                {
                    var mb = report.summary.totalSize / (1024f * 1024f);
                    Debug.Log($"[Ekqo] Burmese text test built to {output} ({mb:0.0} MB).");
                    EditorUtility.DisplayDialog(Title,
                        $"Done ({mb:0.0} MB).\n\nNow commit and push the site/text-test folder. The link updates about two minutes later.",
                        "OK");
                }
                else
                {
                    EditorUtility.DisplayDialog(Title,
                        $"The build failed ({report.summary.totalErrors} errors). Copy the red lines from the Console and send them to Claude.",
                        "OK");
                }
            }
            finally
            {
                PlayerSettings.WebGL.compressionFormat = oldCompression;
                PlayerSettings.WebGL.decompressionFallback = oldFallback;
                PlayerSettings.WebGL.template = oldTemplate;
            }
        }

        [MenuItem("Ekqo/Open Burmese text test scene", priority = 2)]
        public static void Open()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (EnsureScene()) EditorSceneManager.OpenScene(ScenePath);
        }

        /// <summary>Creates the panel settings and the scene once. Later runs reuse them.</summary>
        private static bool EnsureScene()
        {
            if (File.Exists(ScenePath)) return true;

            var font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            var styles = AssetDatabase.LoadAssetAtPath<StyleSheet>(StylesPath);
            var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            if (font == null || styles == null || theme == null)
            {
                EditorUtility.DisplayDialog(Title,
                    $"Could not find {(font == null ? FontPath : styles == null ? StylesPath : ThemePath)}.\nPull the latest code from GitHub and try again.",
                    "OK");
                return false;
            }

            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelPath);
            if (panel == null)
            {
                panel = ScriptableObject.CreateInstance<PanelSettings>();
                panel.themeStyleSheet = theme;
                panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                panel.referenceResolution = new Vector2Int(1024, 768);
                panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
                panel.match = 0.5f;
                AssetDatabase.CreateAsset(panel, PanelPath);
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var go = new GameObject("Text Test");
            go.AddComponent<UIDocument>().panelSettings = panel;
            var spike = go.AddComponent<TextSpike>();
            var so = new SerializedObject(spike);
            so.FindProperty("font").objectReferenceValue = font;
            so.FindProperty("styles").objectReferenceValue = styles;
            so.ApplyModifiedPropertiesWithoutUndo();

            var saved = EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            return saved;
        }
    }
}
