using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ekqo.Game.Editor
{
    /// <summary>
    /// One-click Quest 1 test: makes the Quest 1 scene and builds it for the web into
    /// ../site/quest1, which the Pages workflow publishes next to the Burmese text test.
    /// The scene is made fresh on every build, so it never loses its references.
    /// The project's own Web settings are put back afterwards.
    /// </summary>
    public static class QuestBuilder
    {
        private const string Folder = "Assets/Ekqo/Game/Quest";
        private const string ScenePath = Folder + "/Quest1.unity";
        private const string PanelPath = "Assets/Ekqo/Game/TextTest/Resources/TextTestPanel.asset";
        private const string FontPath = "Assets/Ekqo/Game/Fonts/NotoSansMyanmar-Regular.ttf";
        private const string BundlePath = "Assets/StreamingAssets/content/my/bundle.json";
        private const string OutputFolder = "../site/quest1";
        private const string Template = "PROJECT:EkqoQuest";
        private const string Title = "Quest 1 test";

        [MenuItem("Ekqo/Build Quest 1 for tablets", priority = 20)]
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
            if (!MakeScene()) return;

            var oldCompression = PlayerSettings.WebGL.compressionFormat;
            var oldFallback = PlayerSettings.WebGL.decompressionFallback;
            var oldTemplate = PlayerSettings.WebGL.template;
            var oldHashes = PlayerSettings.WebGL.nameFilesAsHashes;
            var output = Path.GetFullPath(OutputFolder);
            try
            {
                // GitHub Pages cannot send the headers Brotli needs, so this build uses gzip
                // that the page unpacks itself. Hashed file names mean a tablet never mixes
                // an old cached file with a new one.
                PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
                PlayerSettings.WebGL.decompressionFallback = true;
                PlayerSettings.WebGL.template = Template;
                PlayerSettings.WebGL.nameFilesAsHashes = true;

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
                    // The build time shows in the corner of the game, so you can tell
                    // whether a tablet has the new build or a cached old one.
                    var stamp = $"Quest 1 build {DateTime.Now:yyyy-MM-dd HH:mm}";
                    var streaming = Path.Combine(output, "StreamingAssets");
                    Directory.CreateDirectory(streaming);
                    File.WriteAllText(Path.Combine(streaming, "build.txt"), stamp);

                    var mb = report.summary.totalSize / (1024f * 1024f);
                    Debug.Log($"[Ekqo] {stamp} written to {output} ({mb:0.0} MB).");
                    EditorUtility.DisplayDialog(Title,
                        $"Done ({mb:0.0} MB). {stamp}.\n\nNow commit and push the site/quest1 folder (see game/README.md). The link updates a few minutes later.",
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
                PlayerSettings.WebGL.nameFilesAsHashes = oldHashes;
            }
        }

        [MenuItem("Ekqo/Play Quest 1 here", priority = 21)]
        public static void PlayHere()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!MakeScene()) return;
            EditorSceneManager.OpenScene(ScenePath);
            EditorApplication.EnterPlaymode();
        }

        /// <summary>Makes the Quest 1 scene: a camera and one object with the UI document and the quest player.</summary>
        private static bool MakeScene()
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelPath);
            if (font == null || panel == null)
            {
                EditorUtility.DisplayDialog(Title,
                    $"Could not find {(font == null ? FontPath : PanelPath)}.\nPull the latest code from GitHub and try again.",
                    "OK");
                return false;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var camera = Camera.main;
            if (camera != null)
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.106f, 0.114f, 0.227f);
            }
            var go = new GameObject("Quest 1");
            go.AddComponent<UIDocument>().panelSettings = panel;
            var player = go.AddComponent<QuestPlayer>();
            var so = new SerializedObject(player);
            so.FindProperty("font").objectReferenceValue = font;
            so.ApplyModifiedPropertiesWithoutUndo();

            var saved = EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            return saved;
        }
    }
}
