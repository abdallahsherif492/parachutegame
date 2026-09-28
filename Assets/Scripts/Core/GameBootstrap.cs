using UnityEngine;

namespace SkyDrop
{
    /// Creates the whole game at startup, so the scene can stay empty (just a camera).
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (GameManager.I != null) return;

            var camera = Camera.main;
            if (camera == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                camera = go.AddComponent<Camera>();
            }
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.56f, 0.8f, 0.97f);
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 2500f;
            camera.fieldOfView = 60f;
            if (camera.GetComponent<AudioListener>() == null) camera.gameObject.AddComponent<AudioListener>();
            if (camera.GetComponent<CameraRig>() == null) camera.gameObject.AddComponent<CameraRig>();

            // The project shaders do their own lighting; scene lights are not needed.
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None)) l.enabled = false;
            RenderSettings.fog = false;

            new GameObject("Sound").AddComponent<SoundBank>();
            new GameObject("Fx").AddComponent<Fx>();
            new GameObject("Game").AddComponent<GameManager>();
        }
    }
}
