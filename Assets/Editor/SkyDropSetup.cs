using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SkyDrop.EditorTools
{
    /// One-click project setup: main scene, build settings and WebGL player settings
    /// tuned for CrazyGames (small + fast loading). Runs automatically the first time.
    [InitializeOnLoad]
    public static class SkyDropSetup
    {
        const string ScenePath = "Assets/Scenes/Main.unity";

        static SkyDropSetup()
        {
            EditorApplication.delayCall += () =>
            {
                // First open of the project: apply the WebGL/player settings once.
                if (!File.Exists(ScenePath) || PlayerSettings.productName != "Sky Drop") Setup();
            };
        }

        [MenuItem("Sky Drop/Setup Project (scene + WebGL settings)")]
        public static void Setup()
        {
            Directory.CreateDirectory("Assets/Scenes");
            if (!File.Exists(ScenePath))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            PlayerSettings.productName = "Sky Drop";
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
                    Debug.LogWarning("Sky Drop: switched Active Input Handling to 'Both'. Restart Unity if input does not respond.");
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log("Sky Drop: project set up. Open Assets/Scenes/Main.unity and press Play.");
        }
    }
}
