using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ZombiePile.EditorTools
{
    /// One-click project setup: main scene, build settings and WebGL player settings
    /// tuned for CrazyGames (small + fast loading). Runs automatically the first time.
    [InitializeOnLoad]
    public static class ZombiePileSetup
    {
        const string ScenePath = "Assets/Scenes/Main.unity";

        static ZombiePileSetup()
        {
            EditorApplication.delayCall += () =>
            {
                // First open of the project: apply the WebGL/player settings once.
                if (!File.Exists(ScenePath) || PlayerSettings.productName != "Zombie Pile") Setup();
            };
        }

        [MenuItem("Zombie Pile/Setup Project (scene + WebGL settings)")]
        public static void Setup()
        {
            Directory.CreateDirectory("Assets/Scenes");
            if (!File.Exists(ScenePath))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            PlayerSettings.productName = "Zombie Pile";
            PlayerSettings.colorSpace = ColorSpace.Gamma;
            PlayerSettings.stripEngineCode = true;
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.Medium);
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.nameFilesAsHashes = true;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.None;

            // The game uses the classic Input Manager. Make sure it is enabled.
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (assets != null && assets.Length > 0)
            {
                var so = new SerializedObject(assets[0]);
                var prop = so.FindProperty("activeInputHandler");
                if (prop != null && prop.intValue == 1)
                {
                    prop.intValue = 2; // Both
                    so.ApplyModifiedProperties();
                    Debug.LogWarning("Zombie Pile: switched Active Input Handling to 'Both'. Restart Unity if input does not respond.");
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log("Zombie Pile: project set up. Open Assets/Scenes/Main.unity and press Play.");
        }

        [MenuItem("Zombie Pile/Build WebGL (for CrazyGames)")]
        public static void BuildWebGL()
        {
            Setup();
            var opts = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/WebGL",
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            };
            var report = BuildPipeline.BuildPlayer(opts);
            Debug.Log("Zombie Pile: WebGL build " + report.summary.result + " -> Builds/WebGL (zip the CONTENTS of that folder for CrazyGames).");
            if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded) EditorUtility.RevealInFinder("Builds/WebGL");
        }
    }
}
